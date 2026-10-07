using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using Object=UnityEngine.Object;
namespace SonicFX.Structures.Editor
{
    [InitializeOnLoad]
    public static class FloatingPlatformBuilder
    {
        public const string Folder="Assets/Structure/PlateformeFlottante",Prefab=Folder+"/Plateforme_Flottante.prefab";
        static FloatingPlatformBuilder(){EditorApplication.update+=Ready;}
        static void Ready(){if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)return;EditorApplication.update-=Ready;if(!File.Exists(Prefab))Build();UpgradeFlames();}
        public static void UpgradeFlames()
        {
            string path=Folder+"/Textures/Flammes_16.png";
            var shader=Shader.Find("Sonic FX/Flammes animees");var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Materials/Flamme.mat");var atlas=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var embers=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Materials/Braises.mat");
            if(shader==null || material==null || atlas==null || (material.shader==shader && material.mainTexture==atlas && embers!=null))return;
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Default;importer.alphaIsTransparency=true;
            importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.ToNearest;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;importer.SaveAndReimport();
            atlas=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            material.shader=shader;material.mainTexture=atlas;material.SetFloat("_FramesPerSecond",16);material.SetFloat("_Brightness",1.25f);material.shaderKeywords=new string[0];material.renderQueue=3000;EditorUtility.SetDirty(material);
            if(embers==null){embers=new Material(Shader.Find("Sonic FX/Braises")){name="Braises"};AssetDatabase.CreateAsset(embers,Folder+"/Materials/Braises.mat");}
            var root=PrefabUtility.LoadPrefabContents(Prefab);
            try
            {
                var platform=root.GetComponent<SonicFloatingPlatform>();platform.flames.GetComponent<ParticleSystemRenderer>().sharedMaterial=material;
                platform.ConfigureFlames();if(platform.fireRenderer!=null)platform.fireRenderer.enabled=false;
                platform.embers.GetComponent<ParticleSystemRenderer>().sharedMaterial=embers;
                if(Mathf.Approximately(platform.flameWidth,.7f))platform.flameWidth=1.1f;
                if(Mathf.Approximately(platform.flameHeight,1.5f))platform.flameHeight=1.8f;
                // Cube generates a transient mesh on enable; retain its persistent source when saving.
                var source=platform.deck.SourceMesh;platform.deck.GetComponent<MeshFilter>().sharedMesh=source;platform.deckCollider.sharedMesh=source;
                if(platform.fireMesh!=null)platform.fireMesh.sharedMesh=source;
                PrefabUtility.SaveAsPrefabAsset(root,Prefab);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();Debug.Log("Plateforme flottante : animation de feu 16 images et braises installees.");
        }
        [MenuItem("Sonic FX/Structures/Creer la plateforme flottante")]
        public static void Build()
        {
            if(File.Exists(Prefab))return;
            var root=new GameObject("Plateforme_Flottante");
            try
            {
                var rb=root.AddComponent<Rigidbody>();rb.isKinematic=true;rb.useGravity=false;rb.interpolation=RigidbodyInterpolation.None;
                var brain=root.AddComponent<SonicFloatingPlatform>();root.AddComponent<SonicFloatingPlatformEffects>().platform=brain;
                var source=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Meshes/Plateforme_Source.asset");
                var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.name="Plateforme_Editable";cube.transform.SetParent(root.transform,false);Object.DestroyImmediate(cube.GetComponent<BoxCollider>());
                if(source==null){source=Object.Instantiate(cube.GetComponent<MeshFilter>().sharedMesh);source.name="Plateforme Source";var vv=source.vertices;for(int i=0;i<vv.Length;i++)vv[i]=Vector3.Scale(vv[i],new Vector3(8,.8f,6));source.vertices=vv;source.RecalculateBounds();AssetDatabase.CreateAsset(source,Folder+"/Meshes/Plateforme_Source.asset");}
                var texture=new Texture2D(128,128,TextureFormat.RGBA32,true){name="Bois Planches",wrapMode=TextureWrapMode.Repeat};var pixels=new Color[128*128];
                for(int y=0;y<128;y++)for(int x=0;x<128;x++){float grain=Mathf.PerlinNoise(x*.08f,y*.7f)*.13f;float line=y%16<1?.38f:1;pixels[y*128+x]=new Color((.68f+grain)*line,(.42f+grain*.8f)*line,(.16f+grain*.35f)*line,1);}texture.SetPixels(pixels);texture.Apply();AssetDatabase.CreateAsset(texture,Folder+"/Materials/Bois.asset");
                var wood=new Material(Shader.Find("Standard")){name="Bois flottant",mainTexture=texture};wood.SetFloat("_Glossiness",.2f);AssetDatabase.CreateAsset(wood,Folder+"/Materials/Bois.mat");cube.GetComponent<Renderer>().sharedMaterial=wood;
                cube.AddComponent<MeshCollider>();brain.deck=cube.AddComponent<SonicEditableCube>();brain.deck.meshSubdivisions=2;brain.deck.Initialize(source);brain.deck.Rebuild();brain.deckCollider=cube.GetComponent<MeshCollider>();brain.deckCollider.convex=false;
                var fire=new GameObject("Feu_Surface");fire.transform.SetParent(cube.transform,false);brain.fireMesh=fire.AddComponent<MeshFilter>();brain.fireMesh.sharedMesh=source;brain.fireRenderer=fire.AddComponent<MeshRenderer>();
                var burning=new Material(Shader.Find("Sonic FX/Plateforme feu progressif"));AssetDatabase.CreateAsset(burning,Folder+"/Materials/Feu.mat");brain.fireRenderer.sharedMaterial=burning;brain.fireRenderer.enabled=false;
                var particles=new GameObject("Flammes");particles.transform.SetParent(root.transform,false);brain.flames=particles.AddComponent<ParticleSystem>();var main=brain.flames.main;main.playOnAwake=false;main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=500;main.startColor=new Color(1,.48f,.05f,1);main.startLifetime=.6f;main.startSize=.4f;
                var emission=brain.flames.emission;emission.enabled=false;var shape=brain.flames.shape;shape.enabled=false;
                var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(new Color(1,.95f,.4f),0),new GradientColorKey(new Color(1,.2f,.01f),.6f),new GradientColorKey(new Color(.4f,.02f,.005f),1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.15f),new GradientAlphaKey(0,1)});var col=brain.flames.colorOverLifetime;col.enabled=true;col.color=gradient;
                var size=brain.flames.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.7f),new Keyframe(.4f,1),new Keyframe(1,.05f)));
                var flameMat=new Material(Shader.Find("Sonic FX/Flammes animees"));flameMat.renderQueue=3000;AssetDatabase.CreateAsset(flameMat,Folder+"/Materials/Flamme.mat");particles.GetComponent<ParticleSystemRenderer>().sharedMaterial=flameMat;brain.ConfigureFlames();
                brain.flames.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                cube.GetComponent<MeshFilter>().sharedMesh=source;brain.deckCollider.sharedMesh=source;
                PrefabUtility.SaveAsPrefabAsset(root,Prefab);AssetDatabase.SaveAssets();Debug.Log("Plateforme flottante prete : "+Prefab);
            }
            finally{Object.DestroyImmediate(root);}
        }
    }
    [CustomEditor(typeof(SonicFloatingPlatform))]
    public class FloatingPlatformEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();var p=(SonicFloatingPlatform)target;
            EditorGUILayout.HelpBox("Selectionne l'enfant Plateforme_Editable pour retrouver les points du Cube, les rangees et les subdivisions. Le prefab flotte sur l'eau/lave situee sous toute sa surface ; hors liquide il n'est pas utilisable en jeu.",MessageType.Info);
            if(!EditorUtility.IsPersistent(p) && !p.PlacementValid)EditorGUILayout.HelpBox(p.Status??"Place la plateforme sur l'eau ou la lave.",MessageType.Warning);
            if(GUILayout.Button("Editer les points du bloc") && p.deck!=null)Selection.activeGameObject=p.deck.gameObject;
            if(GUILayout.Button("Aligner sur l'eau / la lave")){Undo.RecordObject(p.transform,"Aligner plateforme");p.RefreshPlacement(true);PrefabUtility.RecordPrefabInstancePropertyModifications(p.transform);}
            if(Application.isPlaying){EditorGUILayout.LabelField("Liquide",p.CurrentLiquid.ToString());EditorGUILayout.LabelField("Feu",p.FireState+" / "+Mathf.RoundToInt(p.FireCoverage*100)+" %");}
        }
    }
}

