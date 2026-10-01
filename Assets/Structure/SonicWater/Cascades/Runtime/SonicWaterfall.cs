using System.Collections.Generic;
using UnityEngine;

namespace SonicFX.Water
{
    [ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(100)]
    public class SonicWaterfall : MonoBehaviour
    {
        [Header("Dimensions : placer le pivot au sommet")]
        [InspectorName("Largeur"), Min(.2f)] public float width = 8;
        [InspectorName("Hauteur manuelle"), Min(.5f)] public float height = 15;
        [InspectorName("Courbure vers l'avant"), Min(0)] public float bulge = .6f;
        [InspectorName("Epaisseur de traversee"), Min(.1f)] public float thickness = .7f;
        [InspectorName("Decalage du point de chute (X / Z)")]
        public Vector2 landingOffset;
        [Header("Bassin de reception")]
        [InspectorName("Eau a l'arrivee")] public SonicWaterVolume receivingWater;
        [InspectorName("Ajuster la hauteur a l'eau")] public bool matchWaterHeight = true;
        [InspectorName("Reprendre la couleur de l'eau")] public bool matchWaterColor = true;
        [Header("Aspect")]
        [InspectorName("Couleur de la cascade")] public Color waterColor = new Color(.05f,.65f,.85f,.86f);
        [InspectorName("Couleur de l'ecume")] public Color foamColor = new Color(.8f,.98f,1,1);
        [InspectorName("Vitesse d'ecoulement"), Min(0)] public float flowSpeed = 7;
        [InspectorName("Intensite de l'ecume"), Range(0,2)] public float foamIntensity = 1;
        [InspectorName("Etendue de l'ecume"), Min(.5f)] public float foamSpread = 4;
        [Header("Passage de Sonic")]
        [InspectorName("Eclaboussures au passage")] public bool crossingSplashes = true;
        [InspectorName("Pousser Sonic vers le bas")] public bool pushDown = true;
        [InspectorName("Force du courant"), Min(0)] public float currentAcceleration = 45;
        [InspectorName("Vitesse de chute du courant"), Min(1)] public float currentFallSpeed = 28;
        [Header("References du prefab (deja remplies)")]
        public Transform curtain;
        public Transform foam;
        public ParticleSystem spray;
        public Material splashMaterial;

        public float EffectiveHeight { get; private set; }
        public bool HasWaterLanding { get; private set; }
        public Vector3 LandingLocalPosition => new Vector3(landingOffset.x,-EffectiveHeight,landingOffset.y);
        public Vector3 LandingPosition => transform.TransformPoint(LandingLocalPosition);
        Mesh ownedMesh,originalMesh;
        Vector4 meshShape;
        Vector2 meshOffset;
        public Vector3 CurveCenter(float t) => new Vector3(landingOffset.x*t,-EffectiveHeight*t,landingOffset.y*t+bulge*Mathf.Sin(Mathf.PI*t));
        MaterialPropertyBlock block;
        Renderer curtainRenderer, foamRenderer;
        float nextSearch;
        sealed class Track { public PlayerBhysics player; public Vector3 previous; public float nextSplash; }
        readonly List<Track> tracks = new List<Track>();

        void OnEnable() { nextSearch=0; Refresh(); }
        void OnValidate() { width=Mathf.Max(.2f,width);height=Mathf.Max(.5f,height);thickness=Mathf.Max(.1f,thickness);bulge=Mathf.Max(0,bulge); }
        void Update() { Refresh(); }
        public void Refresh()
        {
            EffectiveHeight=height;
            bool horizontal=Vector3.Dot(transform.up,Vector3.up)>.999f;
            bool waterReady=receivingWater!=null && receivingWater.isActiveAndEnabled && Vector3.Dot(receivingWater.transform.up,Vector3.up)>.999f;
            if(waterReady && horizontal && matchWaterHeight)
            {
                Vector3 projected=transform.TransformPoint(new Vector3(landingOffset.x,0,landingOffset.y));projected.y=receivingWater.transform.position.y;
                float fall=(transform.position.y-projected.y)/Mathf.Max(.001f,transform.lossyScale.y);
                if(fall>=.5f && InWaterFootprint(projected))EffectiveHeight=fall;
            }
            HasWaterLanding=waterReady && horizontal && InWaterFootprint(LandingPosition) && Mathf.Abs(LandingPosition.y-receivingWater.transform.position.y)<.2f;
            if(curtain==null)return;
            curtain.localPosition=Vector3.zero;curtain.localRotation=Quaternion.identity;curtain.localScale=Vector3.one;
            RefreshMesh();
            if(curtainRenderer==null)curtainRenderer=curtain.GetComponent<Renderer>();
            Color tint=waterColor;
            if(matchWaterColor && receivingWater!=null && receivingWater.surface!=null)
            {
                var r=receivingWater.surface.GetComponent<Renderer>();
                if(r!=null && r.sharedMaterial!=null && r.sharedMaterial.HasProperty("_Color")){tint=r.sharedMaterial.GetColor("_Color");tint.a=Mathf.Max(.8f,tint.a);}
            }
            if(block==null)block=new MaterialPropertyBlock();
            block.Clear();block.SetColor("_Color",tint);block.SetColor("_FoamColor",foamColor);
            block.SetFloat("_FlowSpeed",flowSpeed);block.SetVector("_Dimensions",new Vector4(width*Mathf.Abs(transform.lossyScale.x),EffectiveHeight*Mathf.Abs(transform.lossyScale.y),0,0));
            if(curtainRenderer!=null)curtainRenderer.SetPropertyBlock(block);
            if(foam!=null)
            {
                foam.gameObject.SetActive(HasWaterLanding && foamIntensity>0);
                foam.localPosition=LandingLocalPosition+Vector3.up*.04f;foam.localScale=new Vector3(width+1.5f,1,foamSpread);
                if(foamRenderer==null)foamRenderer=foam.GetComponent<Renderer>();
                block.SetFloat("_Intensity",foamIntensity);
                if(HasWaterLanding){block.SetMatrix("_WaterInverse",receivingWater.transform.worldToLocalMatrix);block.SetVector("_WaterSize",new Vector4(receivingWater.width,receivingWater.length,0,0));}
                if(foamRenderer!=null)foamRenderer.SetPropertyBlock(block);
            }
            if(spray!=null)
            {
                bool active=HasWaterLanding && foamIntensity>0;
                spray.gameObject.SetActive(active);spray.transform.localPosition=LandingLocalPosition+Vector3.up*.08f;
                var shape=spray.shape;shape.scale=new Vector3(width,.12f,.5f);
                var emission=spray.emission;emission.rateOverTime=Mathf.Min(180,12*width)*foamIntensity;
                if(Application.isPlaying && active && !spray.isPlaying)spray.Play();
            }
        }
        void RefreshMesh()
        {
            var filter=curtain.GetComponent<MeshFilter>();if(filter==null)return;
            var shape=new Vector4(width,EffectiveHeight,bulge,0);
            if(ownedMesh!=null && meshShape==shape && meshOffset==landingOffset && filter.sharedMesh==ownedMesh)return;
            if(ownedMesh==null){originalMesh=filter.sharedMesh;ownedMesh=new Mesh{name="Cascade - forme editable",hideFlags=HideFlags.DontSave};}
            const int columns=16,rows=64;var vertices=new Vector3[(columns+1)*(rows+1)];var uv=new Vector2[vertices.Length];var triangles=new int[columns*rows*6];int k=0;
            for(int y=0;y<=rows;y++)for(int x=0;x<=columns;x++)
            {
                int i=y*(columns+1)+x;float u=(float)x/columns,t=(float)y/rows;
                vertices[i]=CurveCenter(t)+Vector3.right*((u-.5f)*width);uv[i]=new Vector2(u,t);
                if(x<columns && y<rows){triangles[k++]=i;triangles[k++]=i+columns+1;triangles[k++]=i+1;triangles[k++]=i+1;triangles[k++]=i+columns+1;triangles[k++]=i+columns+2;}
            }
            ownedMesh.Clear();ownedMesh.vertices=vertices;ownedMesh.uv=uv;ownedMesh.triangles=triangles;ownedMesh.RecalculateNormals();ownedMesh.RecalculateBounds();
            var bounds=ownedMesh.bounds;bounds.Expand(.15f);ownedMesh.bounds=bounds;
            filter.sharedMesh=ownedMesh;meshShape=shape;meshOffset=landingOffset;
        }
        void ReleaseMesh()
        {
            if(ownedMesh==null)return;
            if(curtain!=null){var filter=curtain.GetComponent<MeshFilter>();if(filter!=null && filter.sharedMesh==ownedMesh)filter.sharedMesh=originalMesh;}
            if(Application.isPlaying)Destroy(ownedMesh);else DestroyImmediate(ownedMesh);ownedMesh=null;
        }
        bool InWaterFootprint(Vector3 world)
        {
            if(receivingWater==null)return false;
            Vector3 p=receivingWater.transform.InverseTransformPoint(world);
            return Mathf.Abs(p.x)<=receivingWater.width*.5f && Mathf.Abs(p.z)<=receivingWater.length*.5f;
        }
        // A swept segment catches a fast Sonic crossing between two physics frames.
        public bool IntersectsCurtain(Vector3 from,Vector3 to,out Vector3 hit)
        {
            Vector3 a=transform.InverseTransformPoint(from),b=transform.InverseTransformPoint(to);
            hit=to;if(ContainsCurtain(from) || EffectiveHeight<=0)return false;
            float earliest=2;const int slices=64;
            // Each slice follows the same linear segment as the rendered mesh.
            // Shearing the segment into that slice's frame gives an exact box test.
            for(int i=0;i<slices;i++)
            {
                Vector3 p=CurveCenter((float)i/slices),q=CurveCenter((float)(i+1)/slices);
                Vector3 aa=Shear(a,p,q),bb=Shear(b,p,q),delta=bb-aa;float length=delta.magnitude;
                if(length<.000001f)continue;
                var bounds=new Bounds(new Vector3(0,(p.y+q.y)*.5f,0),new Vector3(width,p.y-q.y,thickness));
                if(bounds.Contains(aa))earliest=0;
                else if(bounds.IntersectRay(new Ray(aa,delta/length),out float distance) && distance<=length)earliest=Mathf.Min(earliest,distance/length);
            }
            if(earliest>1)return false;hit=transform.TransformPoint(Vector3.Lerp(a,b,earliest));return true;
        }
        static Vector3 Shear(Vector3 value,Vector3 p,Vector3 q)
        {
            float t=(value.y-p.y)/(q.y-p.y);return new Vector3(value.x-Mathf.LerpUnclamped(p.x,q.x,t),value.y,value.z-Mathf.LerpUnclamped(p.z,q.z,t));
        }
        public bool ContainsCurtain(Vector3 world)
        {
            Vector3 p=transform.InverseTransformPoint(world);
            if(p.y>0 || p.y < -EffectiveHeight)return false;
            Vector3 center=CurveCenter(-p.y/Mathf.Max(.001f,EffectiveHeight));
            return Mathf.Abs(p.x-center.x)<=width*.5f && Mathf.Abs(p.z-center.z)<=thickness*.5f;
        }
        public Vector3 CurrentVelocity(Vector3 velocity,float deltaTime)
        {
            if(pushDown && velocity.y > -currentFallSpeed)velocity.y=Mathf.Max(-currentFallSpeed,velocity.y-currentAcceleration*Mathf.Max(0,deltaTime));
            return velocity;
        }
        void FixedUpdate()
        {
            if(!Application.isPlaying)return;
            if(Time.time>=nextSearch)
            {
                nextSearch=Time.time+1;
                tracks.RemoveAll(t=>t.player==null);
                foreach(var player in Object.FindObjectsByType<PlayerBhysics>())
                    if(!tracks.Exists(t=>t.player==player))tracks.Add(new Track{player=player,previous=player.transform.position});
            }
            foreach(var t in tracks)
            {
                if(t.player==null)continue;Vector3 current=t.player.transform.position;
                bool crossed=IntersectsCurtain(t.previous+Vector3.up*.4f,current+Vector3.up*.4f,out var hit);
                var hurt=t.player.GetComponent<HurtControl>();
                bool alive=t.player.isActiveAndEnabled && (hurt==null || !hurt.isDead);
                if(alive && pushDown && (crossed || ContainsCurtain(current+Vector3.up*.4f)))
                {
                    var body=t.player.p_rigidbody;
                    if(body!=null && !body.isKinematic)body.linearVelocity=CurrentVelocity(body.linearVelocity,Time.fixedDeltaTime);
                }
                if(crossingSplashes && alive && Time.time>=t.nextSplash && crossed)
                {
                        float speed=(current-t.previous).magnitude/Mathf.Max(.001f,Time.fixedDeltaTime);
                        var material=receivingWater!=null && receivingWater.splashMaterial!=null?receivingWater.splashMaterial:splashMaterial;
                        SonicWaterSplash.Spawn(hit,Vector3.up,Mathf.Clamp(.45f+speed*.018f,.45f,1.8f),material);t.nextSplash=Time.time+.75f;
                }
                t.previous=current;
            }
        }
        void OnDisable(){tracks.Clear();ReleaseMesh();if(spray!=null && Application.isPlaying)spray.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
        void OnDestroy(){ReleaseMesh();}
        void OnDrawGizmosSelected()
        {
            Gizmos.matrix=transform.localToWorldMatrix;Gizmos.color=Color.cyan;
            for(int i=0;i<32;i++)
            {
                Vector3 a=CurveCenter((float)i/32),b=CurveCenter((float)(i+1)/32),side=Vector3.right*width*.5f;
                Gizmos.DrawLine(a-side,b-side);Gizmos.DrawLine(a+side,b+side);
            }
            Gizmos.DrawLine(Vector3.left*width*.5f,Vector3.right*width*.5f);
            Gizmos.DrawLine(LandingLocalPosition-Vector3.right*width*.5f,LandingLocalPosition+Vector3.right*width*.5f);
        }
    }
}
