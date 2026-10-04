using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using GuideSpline=UnityEngine.Splines.Spline;

namespace SonicFX.RedRings
{
    public sealed class RedRingStarGuide : MonoBehaviour
    {
        readonly List<Transform> stars=new List<Transform>();Mesh mesh;Material material;float size;
        public void Show(Vector3 start,RedStarRing target,float spacing,float starSize,Color color)
        {
            Clear();size=Mathf.Max(.05f,starSize);
            if(mesh==null)mesh=MakeStar();
            if(material==null)material=new Material(Shader.Find("Sprites/Default"));material.color=color;
            foreach(var position in BuildPositions(start,target,spacing))
            {
                var go=new GameObject("Etoile du parcours",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(transform,false);
                go.transform.position=position;
                go.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
                stars.Add(go.transform);
            }
        }
        // The Scene preview and the game use the same positions, including manually
        // edited stars. Manual positions are exact and have no additional offset.
        public static Vector3[] BuildPositions(Vector3 start,RedStarRing target,float spacing)
        {
            var positions=new List<Vector3>();if(target==null)return positions.ToArray();
            if(target.curveStars)return SampleCurve(BuildCurvePolyline(start,target),spacing);
            if(target.manualStars)
            {
                if(target.editableStarPositions!=null)foreach(var point in target.editableStarPositions)positions.Add(target.GuideToWorld(point));
                return positions.ToArray();
            }
            var points=new List<Vector3>{start};
            if(target.guidePoints!=null)foreach(var point in target.guidePoints)if(point!=null)points.Add(point.position);
            points.Add(target.WorldCenter);
            int maximum=200;
            for(int p=1;p<points.Count&&positions.Count<maximum;p++)
            {
                Vector3 a=points[p-1],b=points[p];float distance=Vector3.Distance(a,b);
                int count=Mathf.Clamp(Mathf.CeilToInt(distance/Mathf.Max(.25f,spacing)),1,maximum-positions.Count);
                for(int i=1;i<=count;i++)
                {
                    positions.Add(Vector3.Lerp(a,b,(float)i/count)+Vector3.up*.25f);
                }
            }
            return positions.ToArray();
        }
        // Use the same AutoSmooth Bezier splines as Tube_Test. Endpoints follow the
        // two ring centers; only interior control points are authored and stored.
        public static Vector3[] BuildCurvePolyline(Vector3 start,RedStarRing target)
        {
            var knots=new List<float3>{(float3)(start+Vector3.up*.25f)};
            if(target.starCurvePoints!=null)foreach(var point in target.starCurvePoints)knots.Add((float3)target.GuideToWorld(point));
            knots.Add((float3)(target.WorldCenter+Vector3.up*.25f));
            bool coincident=true;for(int i=1;i<knots.Count;i++)if(math.distancesq(knots[0],knots[i])>.000001f){coincident=false;break;}
            if(coincident)return new[]{(Vector3)knots[0],(Vector3)knots[knots.Count-1]};
            var spline=new GuideSpline(knots,TangentMode.AutoSmooth);
            int count=Mathf.Clamp((knots.Count-1)*64,128,8192);var line=new Vector3[count+1];
            for(int i=0;i<=count;i++)line[i]=(Vector3)SplineUtility.EvaluatePosition(spline,(float)i/count);
            return line;
        }
        public static Vector3[] SampleCurve(Vector3[] line,float spacing)
        {
            var result=new List<Vector3>();if(line==null||line.Length<2)return result.ToArray();
            float step=Mathf.Max(.25f,spacing),remaining=step;
            for(int i=1;i<line.Length;i++)
            {
                Vector3 a=line[i-1],b=line[i];float distance=Vector3.Distance(a,b);
                while(distance>=remaining&&result.Count<2000)
                {
                    a=Vector3.Lerp(a,b,remaining/distance);result.Add(a);
                    distance=Vector3.Distance(a,b);remaining=step;
                }
                remaining-=distance;if(result.Count>=2000)break;
            }
            var end=line[line.Length-1];
            // Avoid a nearly coincident last star, but still show the destination.
            if(result.Count>0&&Vector3.Distance(result[result.Count-1],end)<step*.35f)result[result.Count-1]=end;
            else result.Add(end);
            return result.ToArray();
        }
        public static Mesh MakeStar()
        {
            var vertices=new Vector3[11];var triangles=new int[30];var colors=new Color[11];
            colors[0]=Color.white;
            for(int i=0;i<10;i++)
            {
                float angle=(90+i*36)*Mathf.Deg2Rad,radius=i%2==0?1:.42f;
                vertices[i+1]=new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius,0);colors[i+1]=Color.white;
                triangles[i*3]=0;triangles[i*3+1]=i+1;triangles[i*3+2]=(i+1)%10+1;
            }
            var result=new Mesh{name="Etoile de guidage"};result.vertices=vertices;result.triangles=triangles;result.colors=colors;result.RecalculateNormals();result.RecalculateBounds();return result;
        }
        void LateUpdate()
        {
            var camera=Camera.main;float time=Time.time;
            for(int i=0;i<stars.Count;i++)if(stars[i]!=null)
            {
                if(camera!=null)stars[i].rotation=camera.transform.rotation*Quaternion.Euler(0,0,time*40+i*15);
                stars[i].localScale=Vector3.one*size*(1+.15f*Mathf.Sin(time*5-i*.6f));
            }
        }
        public void Clear()
        {
            foreach(var star in stars)if(star!=null){star.gameObject.SetActive(false);Release(star.gameObject);}stars.Clear();
        }
        static void Release(Object item){if(Application.isPlaying)Destroy(item);else DestroyImmediate(item);}
        void OnDestroy(){Clear();if(mesh!=null)Release(mesh);if(material!=null)Release(material);}
    }
}
