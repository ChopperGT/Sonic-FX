using System;
using System.IO;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace SonicFX.Water.Editor
{
    [InitializeOnLoad]
    public static class SonicWaterBuilder
    {
        public const string Folder = "Assets/Structure/SonicWater";
        const string PrefabPath = Folder + "/Eau_GreenHill.prefab";
        static SonicWaterBuilder() { EditorApplication.delayCall += AutoBuild; }
        static void AutoBuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                string report=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/SonicWater/unity-tests.txt");
                if(File.Exists(report) && File.ReadAllText(report).StartsWith("FAIL")) SonicWaterVerification.Run();
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + "/Sonic 2 Music Drowning.wav") == null) return;
            try { Build(); } catch (Exception e) { Debug.LogException(e); }
        }
        [MenuItem("Tools/Sonic FX/Eau/Creer le prefab d'eau")]
        public static void Build()
        {
            var shader = Shader.Find("Sonic FX/Eau Green Hill");
            if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Le shader d'eau ne compile pas.");
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + "/Sonic 2 Music Drowning.wav");
            if (clip == null) throw new InvalidOperationException("Musique de noyade manquante.");
            string matPath = Folder + "/Eau_GreenHill.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, matPath); }
            string maskPath = Folder + "/Goutte.png";
            if (!File.Exists(maskPath))
            {
                var texture = new Texture2D(64,64,TextureFormat.RGBA32,false);
                for (int y=0;y<64;y++) for (int x=0;x<64;x++)
                {
                    float radius = new Vector2((x-31.5f)/31.5f,(y-31.5f)/31.5f).magnitude;
                    texture.SetPixel(x,y,new Color(1,1,1,1-Mathf.SmoothStep(0.35f,1,radius)));
                }
                texture.Apply(); File.WriteAllBytes(maskPath,texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(maskPath);
                var importer=(TextureImporter)AssetImporter.GetAtPath(maskPath);
                importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
            }
            string splashPath=Folder+"/Eclaboussure.mat";
            var splash=AssetDatabase.LoadAssetAtPath<Material>(splashPath);
            if (splash==null)
            {
                splash=new Material(Shader.Find("Particles/Standard Unlit"));
                splash.SetFloat("_Mode",2); splash.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                splash.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                splash.SetFloat("_ZWrite",0);splash.EnableKeyword("_ALPHABLEND_ON");splash.renderQueue=3000;
                splash.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);AssetDatabase.CreateAsset(splash,splashPath);
            }
            string meshPath=Folder+"/Surface.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if(mesh==null)
            {
                mesh=new Mesh {name="Surface eau"};
                mesh.vertices=new[]{new Vector3(-.5f,0,-.5f),new Vector3(-.5f,0,.5f),new Vector3(.5f,0,.5f),new Vector3(.5f,0,-.5f)};
                mesh.uv=new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right};mesh.triangles=new[]{0,1,2,0,2,3};
                mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,meshPath);
            }
            var go=new GameObject("Eau_GreenHill");
            try
            {
                go.layer=2;
                var surface=new GameObject("Surface");surface.layer=2;surface.transform.SetParent(go.transform,false);
                surface.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=surface.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                var volume=go.AddComponent<SonicWaterVolume>();volume.surface=surface.transform;
                volume.drowningMusic=clip;volume.splashMaterial=splash;surface.transform.localScale=new Vector3(volume.width,1,volume.length);
                PrefabUtility.SaveAsPrefabAsset(go,PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
            AssetDatabase.SaveAssets();
            string report=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/SonicWater/unity-build.txt");
            File.WriteAllText(report,"OK\nPrefab: "+PrefabPath+"\nShader compiled.\nAudio seconds: "+clip.length);
            EditorApplication.delayCall += SonicWaterVerification.Run;
            Debug.Log("Eau prete : glisser Assets/Structure/SonicWater/Eau_GreenHill.prefab dans la scene.");
        }
        [MenuItem("GameObject/Sonic FX/Zone d'eau",false,10)]
        static void AddWater()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if(prefab==null){Build();prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);}
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance,"Ajouter eau");
            if(SceneView.lastActiveSceneView!=null)instance.transform.position=SceneView.lastActiveSceneView.pivot;
            Selection.activeGameObject=instance;
        }
    }
    [CustomEditor(typeof(SonicWaterVolume))]
    public class SonicWaterVolumeEditor:UnityEditor.Editor
    {
        readonly BoxBoundsHandle box=new BoxBoundsHandle();
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Position Y = hauteur de la surface. Width/Length = dimensions du bassin. Depth = profondeur sous la surface. Le fond et les berges restent ceux de ton niveau.",MessageType.Info);
            var water=(SonicWaterVolume)target;
            if(Vector3.Dot(water.transform.up,Vector3.up)<.999f)
                EditorGUILayout.HelpBox("Garde la surface horizontale : rotation X et Z a zero. La rotation Y est possible.",MessageType.Warning);
        }
        void OnSceneGUI()
        {
            var water=(SonicWaterVolume)target;
            using(new Handles.DrawingScope(new Color(.1f,.8f,1),water.transform.localToWorldMatrix))
            {
                box.center=new Vector3(0,-water.depth*.5f,0);box.size=new Vector3(water.width,water.depth,water.length);
                EditorGUI.BeginChangeCheck();box.DrawHandle();
                if(EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(water,"Redimensionner eau");Undo.RecordObject(water.transform,"Deplacer eau");
                    Vector3 newTop=box.center+Vector3.up*box.size.y*.5f;
                    water.transform.position=water.transform.TransformPoint(newTop);
                    water.width=Mathf.Max(1,box.size.x);water.length=Mathf.Max(1,box.size.z);water.depth=Mathf.Max(.5f,box.size.y);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(water);PrefabUtility.RecordPrefabInstancePropertyModifications(water.transform);
                }
            }
        }
    }
}
