using System;
using System.Collections.Generic;
using UnityEngine;

namespace SonicFX.Structures
{
    // The contour is stored independently of the generated mesh, so every instance stays editable.
    [ExecuteAlways, DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public sealed class SonicTurnBlock : MonoBehaviour
    {
        [Serializable]
        public struct Corner
        {
            public Vector2 position;
            [Min(0)] public float rounding;
            // Relative offset keeps existing prefabs unchanged and follows translations of the corner.
            public Vector2 curveOffset;
            public Corner(float x, float z, float radius = 0) { position = new Vector2(x, z); rounding = radius; curveOffset = Vector2.zero; }
        }

        [SerializeField] public List<Corner> corners = RightTurn();
        [InspectorName("Arrondir le contour")] public bool roundCorners = true;
        [InspectorName("Epaisseur du bloc"), Min(.1f)] public float depth = 6;
        [InspectorName("Finesse des arrondis"), Range(4, 24)] public int cornerSegments = 16;
        [InspectorName("Taille de texture"), Min(.1f)] public float uvSize = 4;
        [SerializeField, HideInInspector] Mesh bakedMesh;
        Mesh generatedMesh;
        bool dirty = true;
        public string LastError { get; private set; }

        public static List<Corner> RightTurn()
        {
            // Openings remain straight. Only the inner and outer bends are rounded.
            return new List<Corner> {
                new Corner(-12, 0), new Corner(0, 0), new Corner(0, 12, 8),
                new Corner(12, 12), new Corner(12, 24), new Corner(-12, 24, 20)
            };
        }

        void OnEnable() { dirty = true; Rebuild(); }
        void OnValidate() { dirty = true; }
        void Update() { if (dirty || generatedMesh == null && string.IsNullOrEmpty(LastError)) Rebuild(); }
        void OnDisable() { Release(); }
        void OnDestroy() { Release(); }

        void Release()
        {
            if (generatedMesh == null) return;
            if (GetComponent<MeshFilter>().sharedMesh == generatedMesh) GetComponent<MeshFilter>().sharedMesh = bakedMesh;
            if (GetComponent<MeshCollider>().sharedMesh == generatedMesh) GetComponent<MeshCollider>().sharedMesh = bakedMesh;
            if (Application.isPlaying) Destroy(generatedMesh); else DestroyImmediate(generatedMesh);
            generatedMesh = null;
        }

        public bool Rebuild()
        {
            dirty = false;
            if (!isActiveAndEnabled) return false;
            if (!TryCreateMesh(out var mesh, out var error)) { LastError = error; return false; }
            Release();
            LastError = null;
            generatedMesh = mesh;
            mesh.hideFlags = HideFlags.DontSave;
            GetComponent<MeshFilter>().sharedMesh = mesh;
            var collider = GetComponent<MeshCollider>();
            collider.sharedMesh = null;
            collider.convex = false;
            collider.isTrigger = false;
            collider.sharedMesh = mesh;
            return true;
        }

        public void SetBakedMesh(Mesh mesh)
        {
            bakedMesh = mesh;
            GetComponent<MeshFilter>().sharedMesh = mesh;
            GetComponent<MeshCollider>().sharedMesh = mesh;
        }

        public Bounds ContourBounds
        {
            get {
                var result = new Bounds(Vector3.zero, Vector3.zero);
                if (corners == null || corners.Count == 0) return result;
                result = new Bounds(To3(corners[0].position), Vector3.zero);
                foreach (var c in corners) result.Encapsulate(To3(c.position));
                return result;
            }
        }

        public void Resize(float width, float length)
        {
            var b = ContourBounds;
            if (b.size.x < .001f || b.size.z < .001f) return;
            float sx = Mathf.Clamp(width, 1, 1000) / b.size.x, sz = Mathf.Clamp(length, 1, 1000) / b.size.z;
            for (int i = 0; i < corners.Count; i++) {
                var c = corners[i];
                c.position = new Vector2(b.center.x + (c.position.x - b.center.x) * sx, b.center.z + (c.position.y - b.center.z) * sz);
                c.rounding *= Mathf.Min(sx, sz);
                c.curveOffset = new Vector2(c.curveOffset.x * sx, c.curveOffset.y * sz);
                corners[i] = c;
            }
        }

        public void MoveCorners(IEnumerable<int> indices, Vector2 delta)
        {
            var moved = new HashSet<int>();
            foreach (int i in indices) {
                if (i < 0 || i >= corners.Count || !moved.Add(i)) continue;
                var c = corners[i]; c.position += delta; corners[i] = c;
            }
        }

        public bool GetCornerCurve(int index, out Vector2 start, out Vector2 control, out Vector2 end)
        {
            start = control = end = Vector2.zero;
            if (!roundCorners || corners == null || corners.Count < 3 || index < 0 || index >= corners.Count) return false;
            var corner = corners[index]; Vector2 p = corner.position;
            Vector2 prev = corners[(index + corners.Count - 1) % corners.Count].position, next = corners[(index + 1) % corners.Count].position;
            float cut = Mathf.Min(Mathf.Max(0, corner.rounding), .49f * Mathf.Min(Vector2.Distance(p, prev), Vector2.Distance(p, next)));
            start = p + (prev - p).normalized * cut; end = p + (next - p).normalized * cut;
            control = p + corner.curveOffset;
            return cut >= .001f;
        }

        public bool SetCurveMidpoint(int index, Vector2 midpoint)
        {
            if (!GetCornerCurve(index, out var a, out _, out var b)) return false;
            var c = corners[index];
            c.curveOffset = 2 * (midpoint - .25f * (a + b)) - c.position;
            corners[index] = c;
            return true;
        }

        public bool TryOutline(out List<Vector2> outline, out string error)
        {
            outline = new List<Vector2>(); error = null;
            if (corners == null || corners.Count < 3 || corners.Count > 32) { error = "Le contour doit avoir entre 3 et 32 points."; return false; }
            var raw = new List<Vector2>();
            foreach (var c in corners) {
                if (!Finite(c.position.x) || !Finite(c.position.y) || !Finite(c.rounding) || !Finite(c.curveOffset.x) || !Finite(c.curveOffset.y) || c.position.sqrMagnitude > 100000000f || c.curveOffset.sqrMagnitude > 100000000f) {
                    error = "Un point est invalide ou trop eloigne."; return false;
                }
                raw.Add(c.position);
            }
            if (!SimplePolygon(raw)) { error = "Les cotes se croisent ou deux points se touchent. Ecarte les points : le dernier bloc valide est conserve."; return false; }
            for (int i = 0; i < corners.Count; i++) {
                if (!GetCornerCurve(i, out var a, out var control, out var b)) { outline.Add(raw[i]); continue; }
                int segments = Mathf.Clamp(cornerSegments, 4, 24);
                for (int j = 0; j <= segments; j++) {
                    float t = (float)j / segments;
                    outline.Add((1 - t) * (1 - t) * a + 2 * t * (1 - t) * control + t * t * b);
                }
            }
            RemoveCollinear(outline);
            if (!SimplePolygon(outline)) { error = "L'arrondi croise un autre bord. Reduis l'arrondi du point concerne."; return false; }
            if (SignedArea(outline) < 0) outline.Reverse();
            return true;
        }

        public bool TryCreateMesh(out Mesh mesh, out string error)
        {
            mesh = null;
            if (!TryOutline(out var outline, out error)) return false;
            if (!Finite(depth) || !Finite(uvSize)) { error = "Epaisseur ou taille de texture invalide."; return false; }
            float thickness = Mathf.Clamp(depth, .1f, 1000), tile = Mathf.Max(.1f, uvSize);
            if (!Triangulate(outline, out var cap)) { error = "Contour trop complexe : simplifie les points."; return false; }
            var vertices = new List<Vector3>(); var normals = new List<Vector3>();
            var uv = new List<Vector2>(); var triangles = new List<int>();
            int n = outline.Count;
            for (int face = 0; face < 2; face++) foreach (var p in outline) {
                vertices.Add(To3(p) + Vector3.down * (face * thickness));
                normals.Add(face == 0 ? Vector3.up : Vector3.down); uv.Add(p / tile);
            }
            for (int i = 0; i < cap.Count; i += 3) {
                // A counter-clockwise X/Z contour points down in Unity coordinates.
                int a = cap[i], b = cap[i + 1], c = cap[i + 2];
                triangles.AddRange(new[] { a, c, b, n + a, n + b, n + c });
            }
            float distance = 0;
            for (int i = 0; i < n; i++) {
                Vector3 a = To3(outline[i]), b = To3(outline[(i + 1) % n]);
                Vector3 normal = Vector3.Cross(Vector3.up, b - a).normalized;
                int start = vertices.Count; float length = Vector3.Distance(a, b);
                vertices.AddRange(new[] { a, b, b + Vector3.down * thickness, a + Vector3.down * thickness });
                for (int j = 0; j < 4; j++) normals.Add(normal);
                uv.AddRange(new[] { new Vector2(distance / tile, thickness / tile), new Vector2((distance + length) / tile, thickness / tile), new Vector2((distance + length) / tile, 0), new Vector2(distance / tile, 0) });
                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                distance += length;
            }
            mesh = new Mesh { name = "Bloc virage plein" };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            return true;
        }

        static Vector3 To3(Vector2 p) { return new Vector3(p.x, 0, p.y); }
        static bool Finite(float f) { return !float.IsNaN(f) && !float.IsInfinity(f); }
        static float Cross(Vector2 a, Vector2 b) { return a.x * b.y - a.y * b.x; }
        static float SignedArea(List<Vector2> p) { float area = 0; for (int i = 0; i < p.Count; i++) area += Cross(p[i], p[(i + 1) % p.Count]); return area * .5f; }
        static void RemoveCollinear(List<Vector2> p)
        {
            bool changed = true;
            while (changed && p.Count > 3) {
                changed = false;
                for (int i = 0; i < p.Count; i++) {
                    Vector2 a = p[(i + p.Count - 1) % p.Count], b = p[i], c = p[(i + 1) % p.Count];
                    if (Mathf.Abs(Cross(b - a, c - b)) < .000001f && Vector2.Dot(b - a, c - b) >= 0) { p.RemoveAt(i); changed = true; break; }
                }
            }
        }
        static bool SimplePolygon(List<Vector2> p)
        {
            if (p.Count < 3 || Mathf.Abs(SignedArea(p)) < .01f) return false;
            for (int i = 0; i < p.Count; i++) {
                Vector2 a = p[i], b = p[(i + 1) % p.Count];
                if ((a - b).sqrMagnitude < .000001f) return false;
                for (int j = i + 1; j < p.Count; j++) {
                    if (j == i + 1 || i == 0 && j == p.Count - 1) continue;
                    Vector2 c = p[j], d = p[(j + 1) % p.Count];
                    if (SegmentsMeet(a, b, c, d)) return false;
                }
            }
            return true;
        }
        static bool SegmentsMeet(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            const float e = .000001f;
            if (Mathf.Max(a.x, b.x) + e < Mathf.Min(c.x, d.x) || Mathf.Max(c.x, d.x) + e < Mathf.Min(a.x, b.x) || Mathf.Max(a.y, b.y) + e < Mathf.Min(c.y, d.y) || Mathf.Max(c.y, d.y) + e < Mathf.Min(a.y, b.y)) return false;
            float abC = Cross(b - a, c - a), abD = Cross(b - a, d - a), cdA = Cross(d - c, a - c), cdB = Cross(d - c, b - c);
            return ((abC <= e && abD >= -e) || (abD <= e && abC >= -e)) && ((cdA <= e && cdB >= -e) || (cdB <= e && cdA >= -e));
        }
        static bool Triangulate(List<Vector2> p, out List<int> result)
        {
            result = new List<int>(); var remaining = new List<int>();
            for (int i = 0; i < p.Count; i++) remaining.Add(i);
            while (remaining.Count > 3) {
                bool found = false;
                for (int i = 0; i < remaining.Count; i++) {
                    int a = remaining[(i + remaining.Count - 1) % remaining.Count], b = remaining[i], c = remaining[(i + 1) % remaining.Count];
                    if (Cross(p[b] - p[a], p[c] - p[b]) <= .0000001f) continue;
                    bool occupied = false;
                    foreach (int test in remaining) {
                        if (test == a || test == b || test == c) continue;
                        Vector2 q = p[test];
                        if (Cross(p[b] - p[a], q - p[a]) >= -.0000001f && Cross(p[c] - p[b], q - p[b]) >= -.0000001f && Cross(p[a] - p[c], q - p[c]) >= -.0000001f) { occupied = true; break; }
                    }
                    if (occupied) continue;
                    result.AddRange(new[] { a, b, c }); remaining.RemoveAt(i); found = true; break;
                }
                if (!found) return false;
            }
            result.AddRange(remaining); return true;
        }
    }
}
