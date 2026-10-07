using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SonicFX.Structures.Editor
{
    [InitializeOnLoad]
    public static class SonicRoundPlatformBuilder
    {
        public const string Folder = "Assets/Structure/PlateformeArrondie";
        public const string PrefabPath = Folder + "/Plateforme_Arrondie.prefab";
        public const string SourcePath = Folder + "/Plateforme_Source.asset";
        public static string Reports => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/RoundPlatform");

        static SonicRoundPlatformBuilder() { EditorApplication.update += Ready; }
        static void Ready()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null) return;
            EditorApplication.update -= Ready;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) Install();
            else
            {
                var report = Path.Combine(Reports, "tests.txt");
                if (!File.Exists(report) || !File.ReadAllText(report).StartsWith("PASS")) SonicRoundPlatformVerification.Verify();
            }
        }

        [MenuItem("Tools/Sonic FX/Structures/Installer et verifier Plateforme arrondie")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null) return;
            Directory.CreateDirectory(Reports);
            GameObject root = null;
            try
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
                {
                    var cube = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Structure/Cube.prefab");
                    if (cube == null || cube.GetComponent<MeshRenderer>() == null) throw new Exception("Materiau du Cube introuvable.");
                    var source = AssetDatabase.LoadAssetAtPath<Mesh>(SourcePath);
                    if (source == null) { source = CreateSource(); AssetDatabase.CreateAsset(source, SourcePath); }
                    root = new GameObject("Plateforme_Arrondie");
                    root.layer = cube.layer;
                    var platform = root.AddComponent<SonicRoundPlatform>();
                    platform.meshSubdivisions = 1;
                    platform.Initialize(source);
                    root.GetComponent<MeshRenderer>().sharedMaterial = cube.GetComponent<MeshRenderer>().sharedMaterial;
                    root.GetComponent<MeshCollider>().convex = false;
                    if (!platform.Rebuild()) throw new Exception(platform.LastError);
                    // Persistent source as fallback; instances regenerate their independent mesh.
                    root.GetComponent<MeshFilter>().sharedMesh = source;
                    root.GetComponent<MeshCollider>().sharedMesh = source;
                    if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null) throw new Exception("Enregistrement du prefab impossible.");
                    AssetDatabase.SaveAssetIfDirty(source);
                }
            }
            catch (Exception e) { File.WriteAllText(Path.Combine(Reports, "tests.txt"), "FAIL installation\n" + e); Debug.LogException(e); return; }
            finally { if (root != null) Object.DestroyImmediate(root); }
            SonicRoundPlatformVerification.Verify();
        }

        public static Mesh CreateSource()
        {
            const int sides = 64, radialSteps = 8;
            const float radius = 8, depth = 2, bevel = .15f;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            int Vertex(Vector3 p, Vector3 n)
            {
                int i = vertices.Count; vertices.Add(p); normals.Add(n);
                uv.Add(new Vector2(p.x, p.z) * .125f); return i;
            }
            void Triangle(int a, int b, int c, Vector3 facing)
            {
                if (Vector3.Dot(Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]), facing) < 0) { int swap = b; b = c; c = swap; }
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
            }
            Vector3 Direction(int i) { float a = i * Mathf.PI * 2 / sides; return new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); }
            void Disk(float y, float r, Vector3 normal)
            {
                int center = Vertex(new Vector3(0, y, 0), normal);
                int first = vertices.Count;
                for (int ring = 1; ring <= radialSteps; ring++)
                    for (int i = 0; i < sides; i++) Vertex(Direction(i) * (r * ring / radialSteps) + Vector3.up * y, normal);
                for (int i = 0; i < sides; i++) Triangle(center, first + i, first + (i + 1) % sides, normal);
                for (int ring = 1; ring < radialSteps; ring++)
                    for (int i = 0; i < sides; i++)
                    {
                        int n = (i + 1) % sides;
                        int a = first + (ring - 1) * sides + i, b = first + (ring - 1) * sides + n;
                        int c = first + ring * sides + n, d = first + ring * sides + i;
                        Triangle(a, b, c, normal); Triangle(a, c, d, normal);
                    }
            }
            Disk(0, radius - bevel, Vector3.up);
            Disk(-depth, radius, Vector3.down);
            int rim = vertices.Count;
            // Small rounded lip, with a flat unobstructed running surface.
            for (int ring = 0; ring <= 4; ring++)
            {
                float a = Mathf.Min(ring, 3) * Mathf.PI / 6;
                float r = radius - bevel + Mathf.Sin(a) * bevel;
                float y = ring == 4 ? -depth : (Mathf.Cos(a) - 1) * bevel;
                for (int i = 0; i < sides; i++)
                {
                    var d = Direction(i);
                    Vertex(d * r + Vector3.up * y, ring == 4 ? d : d * Mathf.Sin(a) + Vector3.up * Mathf.Cos(a));
                }
            }
            for (int ring = 0; ring < 4; ring++)
                for (int i = 0; i < sides; i++)
                {
                    int n = (i + 1) % sides;
                    int a = rim + ring * sides + i, b = rim + ring * sides + n;
                    int c = rim + (ring + 1) * sides + n, d = rim + (ring + 1) * sides + i;
                    var facing = Direction(i) + (ring < 3 ? Vector3.up : Vector3.zero);
                    Triangle(a, b, c, facing); Triangle(a, c, d, facing);
                }
            var mesh = new Mesh { name = "Plateforme arrondie source" };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds(); mesh.RecalculateTangents(); return mesh;
        }
    }
}
