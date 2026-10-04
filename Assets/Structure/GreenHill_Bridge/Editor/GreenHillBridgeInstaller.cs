using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SonicFX.GreenHillBridge.Editor
{
    [InitializeOnLoad]
    public static class GreenHillBridgeInstaller
    {
        const string Folder = "Assets/Structure/GreenHill_Bridge";
        const string PrefabPath = "Assets/Structure/bridge_02.prefab";
        const string SourceGuid = "8659c38aa37038d4c8e5df514fda2eea";
        const string MeshPath = Folder + "/Bridge_GreenHill.asset";
        const string MaterialPath = Folder + "/Bridge_GreenHill.mat";
        const string TexturePath = Folder + "/Bridge_GreenHill_Atlas.png";

        static GreenHillBridgeInstaller()
        {
            EditorApplication.delayCall += AutoInstall;
        }

        static void AutoInstall()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            if (AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath) != null) return;
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath) == null) return;
            try { Install(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        [MenuItem("Tools/Sonic FX/Appliquer les textures du pont Green Hill")]
        public static void Install()
        {
            Mesh source = null;
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(SourceGuid)))
                if (asset is Mesh m && m.name == "bridge_02") { source = m; break; }
            if (source == null) throw new InvalidOperationException("Maillage bridge_02 introuvable dans all_elements_floor.fbx.");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (texture == null) throw new InvalidOperationException("Texture du pont introuvable : " + TexturePath);
            var shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("Shader Standard introuvable.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
                throw new InvalidOperationException("Prefab du pont introuvable.");

            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();

            Mesh copy = UnityEngine.Object.Instantiate(source);
            copy.name = "Bridge_GreenHill";
            Remap(copy);
            copy.RecalculateTangents();
            Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (saved == null) { AssetDatabase.CreateAsset(copy, MeshPath); saved = copy; }
            else { EditorUtility.CopySerialized(copy, saved); UnityEngine.Object.DestroyImmediate(copy); }
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                material.name = "Bridge_GreenHill";
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.shader = shader;
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            material.color = Color.white;
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Glossiness", 0.23f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var filter = root.GetComponent<MeshFilter>();
                var renderer = root.GetComponent<MeshRenderer>();
                if (filter == null || renderer == null) throw new InvalidOperationException("MeshFilter ou MeshRenderer absent du pont.");
                filter.sharedMesh = saved;
                renderer.sharedMaterial = material;
                // Preserve existing collision geometry and every placement/scale setting.
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            string report = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/GreenHill_Bridge/unity-install.txt");
            File.WriteAllText(report, "OK\nPrefab: " + PrefabPath + "\nVertices: " + saved.vertexCount +
                "\nSource vertices: " + source.vertexCount + "\nTriangles: " + saved.triangles.Length / 3);
            Debug.Log("Pont Green Hill texture : bridge_02.prefab. Geometrie et placements conserves.");
        }

        static int Root(int[] parent, int index)
        {
            while (parent[index] != index) { parent[index] = parent[parent[index]]; index = parent[index]; }
            return index;
        }

        static bool IsStrip(Vector2 uv) => uv.x < 0.03f && uv.y > 0.975f;

        static void Remap(Mesh mesh)
        {
            Vector3[] positions = mesh.vertices;
            Vector2[] original = mesh.uv;
            if (original.Length != positions.Length) throw new InvalidOperationException("UV manquants sur le pont.");
            int[] parent = new int[positions.Length];
            var sharedPosition = new Dictionary<Vector3, int>();
            for (int i = 0; i < parent.Length; i++)
            {
                parent[i] = i;
                if (sharedPosition.TryGetValue(positions[i], out int same)) parent[i] = same;
                else sharedPosition.Add(positions[i], i);
            }
            int[] triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
                for (int j = 1; j < 3; j++) parent[Root(parent, triangles[i + j])] = Root(parent, triangles[i]);
            var bounds = new Dictionary<int, Bounds>();
            for (int i = 0; i < parent.Length; i++)
            {
                int group = Root(parent, i);
                if (!bounds.TryGetValue(group, out Bounds b)) b = new Bounds(positions[i], Vector3.zero);
                b.Encapsulate(positions[i]); bounds[group] = b;
            }
            Vector2 stripMin = Vector2.one, stripMax = Vector2.zero, capMin = Vector2.one, capMax = Vector2.zero;
            foreach (Vector2 uv in original)
            {
                if (IsStrip(uv)) { stripMin = Vector2.Min(stripMin, uv); stripMax = Vector2.Max(stripMax, uv); }
                else { capMin = Vector2.Min(capMin, uv); capMax = Vector2.Max(capMax, uv); }
            }
            if (stripMax.x <= stripMin.x || capMax.x <= capMin.x)
                throw new InvalidOperationException("Disposition UV du pont inattendue.");
            var uvOut = new Vector2[original.Length];
            for (int i = 0; i < original.Length; i++)
            {
                Vector2 uv = original[i];
                Vector3 size = bounds[Root(parent, i)].size;
                float[] axes = { size.x, size.y, size.z };
                Array.Sort(axes);
                bool rope = axes[0] < axes[2] * 0.2f && axes[1] > axes[2] * 0.4f;
                bool post = axes[0] > axes[2] * 0.4f;
                if (!IsStrip(uv))
                {
                    float start = rope ? 0.03f : 0.53f;
                    uvOut[i] = new Vector2(Mathf.Lerp(start, start + 0.44f, Mathf.InverseLerp(capMin.x, capMax.x, uv.x)),
                        Mathf.Lerp(start, start + 0.44f, Mathf.InverseLerp(capMin.y, capMax.y, uv.y)));
                    continue;
                }
                float u = Mathf.InverseLerp(stripMin.y, stripMax.y, uv.y);
                float v = Mathf.InverseLerp(stripMin.x, stripMax.x, uv.x);
                uvOut[i] = new Vector2(Mathf.Lerp(0.02f, 0.48f, u) + (post ? 0.5f : 0f),
                    Mathf.Lerp(0.02f, 0.48f, v) + (rope || post ? 0f : 0.5f));
            }
            mesh.uv = uvOut;
        }
    }
}
