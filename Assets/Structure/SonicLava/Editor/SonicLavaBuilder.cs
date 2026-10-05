using System;
using System.IO;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Object = UnityEngine.Object;
namespace SonicFX.Lava.Editor
{
    [InitializeOnLoad]
    public static class SonicLavaBuilder
    {
        public const string Folder = "Assets/Structure/SonicLava";
        public const string PrefabPath = Folder + "/Lave.prefab";
        static SonicLavaBuilder() { EditorApplication.update += Ready; }
        static void Ready()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (Shader.Find("Sonic FX/Lave") == null) return;
            EditorApplication.update -= Ready;
            if (!File.Exists(PrefabPath)) Build();
        }
        [MenuItem("Sonic FX/Lave/Creer ou verifier le prefab")]
        public static void Build()
        {
            var shader = Shader.Find("Sonic FX/Lave");
            if (shader == null) throw new InvalidOperationException("Le shader de lave manque.");
            if (ShaderUtil.ShaderHasError(shader))
            {
                string details = "";
                foreach (var message in ShaderUtil.GetShaderMessages(shader)) details += "\n" + message.line + ": " + message.message;
                throw new InvalidOperationException("Le shader de lave ne compile pas." + details);
            }
            if (File.Exists(PrefabPath)) return;
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Lave.mat");
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, Folder + "/Lave.mat"); }
            var mesh = new Mesh { name = "Surface lave" };
            mesh.vertices = new[] {new Vector3(-.5f,0,-.5f),new Vector3(-.5f,0,.5f),new Vector3(.5f,0,.5f),new Vector3(.5f,0,-.5f)};
            mesh.uv = new[] {Vector2.zero, Vector2.up, Vector2.one, Vector2.right}; mesh.triangles = new[] {0,1,2,0,2,3};
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/Surface.asset");
            if (existing == null) AssetDatabase.CreateAsset(mesh, Folder + "/Surface.asset"); else {Object.DestroyImmediate(mesh);mesh=existing;}
            var go = new GameObject("Lave");
            try
            {
                go.layer = 2; var surface = new GameObject("Surface"); surface.layer = 2; surface.transform.SetParent(go.transform, false);
                surface.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = surface.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
                var volume = go.AddComponent<SonicLavaVolume>(); volume.surface = surface.transform;
                volume.lethalVolume = go.AddComponent<BoxCollider>(); volume.Refresh();
                PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            }
            finally { Object.DestroyImmediate(go); }
            AssetDatabase.SaveAssets(); Debug.Log("Lave prete : " + PrefabPath);
        }
        [MenuItem("GameObject/Sonic FX/Zone de lave", false, 10)]
        static void Add()
        {
            Build(); var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            Undo.RegisterCreatedObjectUndo(instance, "Ajouter lave");
            if (SceneView.lastActiveSceneView != null) instance.transform.position = SceneView.lastActiveSceneView.pivot;
            Selection.activeGameObject = instance;
        }
    }
    [CustomEditor(typeof(SonicLavaVolume))]
    class SonicLavaEditor : UnityEditor.Editor
    {
        readonly BoxBoundsHandle box = new BoxBoundsHandle();
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector(); EditorGUILayout.HelpBox("Position Y = hauteur de la surface. Les poignees orange reglent chaque cote et la profondeur. Le contact tue immediatement, meme avec des rings, un bouclier ou de l'invincibilite.", MessageType.Info);
        }
        void OnSceneGUI()
        {
            var lava = (SonicLavaVolume)target;
            using (new Handles.DrawingScope(new Color(1,.4f,.05f), lava.transform.localToWorldMatrix))
            {
                box.center = new Vector3(0,-lava.depth*.5f,0); box.size = new Vector3(lava.width,lava.depth,lava.length);
                EditorGUI.BeginChangeCheck(); box.DrawHandle();
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(lava, "Redimensionner lave"); Undo.RecordObject(lava.transform, "Deplacer lave");
                    if (lava.surface != null) Undo.RecordObject(lava.surface, "Redimensionner surface lave");
                    if (lava.lethalVolume != null) Undo.RecordObject(lava.lethalVolume, "Redimensionner zone mortelle");
                    lava.transform.position = lava.transform.TransformPoint(box.center + Vector3.up * box.size.y * .5f);
                    lava.width = Mathf.Max(.1f,box.size.x); lava.length = Mathf.Max(.1f,box.size.z); lava.depth = Mathf.Max(.1f,box.size.y); lava.Refresh();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(lava); PrefabUtility.RecordPrefabInstancePropertyModifications(lava.transform);
                    if (lava.surface != null) PrefabUtility.RecordPrefabInstancePropertyModifications(lava.surface);
                    if (lava.lethalVolume != null) PrefabUtility.RecordPrefabInstancePropertyModifications(lava.lethalVolume);
                    EditorUtility.SetDirty(lava);
                }
            }
        }
    }
}
