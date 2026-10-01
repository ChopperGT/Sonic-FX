using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SonicFX.Structures.Editor
{
    [CustomEditor(typeof(SonicTurnBlock))]
    public sealed class SonicTurnBlockEditor : UnityEditor.Editor
    {
        readonly HashSet<int> selected = new HashSet<int>();
        bool showAdvanced;
        int knownCount;
        void OnEnable() { knownCount = ((SonicTurnBlock)target).corners.Count; Undo.undoRedoPerformed += OnUndo; }
        void OnDisable() { Undo.undoRedoPerformed -= OnUndo; }
        void OnUndo()
        {
            if (target == null) return;
            var block = (SonicTurnBlock)target;
            if (knownCount != block.corners.Count) selected.Clear();
            knownCount = block.corners.Count;
            block.Rebuild(); Repaint(); SceneView.RepaintAll();
        }
        void CleanSelection(SonicTurnBlock block) { selected.RemoveWhere(i => i < 0 || i >= block.corners.Count); knownCount = block.corners.Count; }
        Vector2 SelectionCenter(SonicTurnBlock block)
        {
            Vector2 center = Vector2.zero; foreach (int i in selected) center += block.corners[i].position;
            return selected.Count == 0 ? center : center / selected.Count;
        }
        void SelectPoint(int index, bool toggle)
        {
            if (!toggle) selected.Clear();
            if (!selected.Add(index)) selected.Remove(index);
            Repaint(); SceneView.RepaintAll();
        }
        public override void OnInspectorGUI()
        {
            var block = (SonicTurnBlock)target;
            CleanSelection(block);
            EditorGUILayout.HelpBox("Maj + clic (ou Ctrl + clic) ajoute/retire des points de la selection. Deplace ensuite les fleches jaunes pour bouger tout le groupe. Les poignees roses sur les arrondis modifient directement leur forme.", MessageType.Info);
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("roundCorners"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("depth"));
            if (EditorGUI.EndChangeCheck()) { serializedObject.ApplyModifiedProperties(); Changed(block); }
            else serializedObject.ApplyModifiedProperties();

            var bounds = block.ContourBounds;
            EditorGUI.BeginChangeCheck();
            float width = EditorGUILayout.FloatField("Largeur X", bounds.size.x);
            float length = EditorGUILayout.FloatField("Longueur Z", bounds.size.z);
            if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(block, "Redimensionner le bloc"); block.Resize(width, length); Changed(block); }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Tout selectionner")) { selected.Clear(); for (int i = 0; i < block.corners.Count; i++) selected.Add(i); SceneView.RepaintAll(); }
            if (GUILayout.Button("Deselectionner")) { selected.Clear(); SceneView.RepaintAll(); }
            EditorGUILayout.EndHorizontal();
            if (selected.Count > 0) {
                int first = int.MaxValue; foreach (int i in selected) first = Mathf.Min(first, i);
                EditorGUILayout.Space(); EditorGUILayout.LabelField(selected.Count == 1 ? "Point " + (first + 1) : selected.Count + " points selectionnes", EditorStyles.boldLabel);
                Vector2 center = SelectionCenter(block);
                EditorGUI.BeginChangeCheck();
                Vector2 moved = EditorGUILayout.Vector2Field(selected.Count == 1 ? "Position X / Z" : "Centre du groupe X / Z", center);
                if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(block, "Deplacer les points"); block.MoveCorners(selected, moved - center); Changed(block); }
                float radius = block.corners[first].rounding; bool mixed = false;
                foreach (int i in selected) if (!Mathf.Approximately(block.corners[i].rounding, radius)) mixed = true;
                EditorGUI.showMixedValue = mixed; EditorGUI.BeginChangeCheck();
                radius = Mathf.Max(0, EditorGUILayout.FloatField("Arrondi des points", radius));
                if (EditorGUI.EndChangeCheck()) {
                    Undo.RecordObject(block, "Arrondir les points");
                    foreach (int i in selected) { var c = block.corners[i]; c.rounding = radius; block.corners[i] = c; }
                    Changed(block);
                }
                EditorGUI.showMixedValue = false;
                if (selected.Count == 1) {
                    var c = block.corners[first]; EditorGUI.BeginChangeCheck();
                    c.curveOffset = EditorGUILayout.Vector2Field("Deformation courbe X / Z", c.curveOffset);
                    if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(block, "Deformer l'arrondi"); block.corners[first] = c; Changed(block); }
                }
                if (GUILayout.Button("Reinitialiser la forme des arrondis selectionnes")) {
                    Undo.RecordObject(block, "Reinitialiser les courbes");
                    foreach (int i in selected) { var c = block.corners[i]; c.curveOffset = Vector2.zero; block.corners[i] = c; }
                    Changed(block);
                }
                using (new EditorGUI.DisabledScope(block.corners.Count - selected.Count < 3))
                    if (GUILayout.Button("Supprimer les points selectionnes")) {
                        Undo.RecordObject(block, "Supprimer les points"); var indices = new List<int>(selected); indices.Sort();
                        for (int i = indices.Count - 1; i >= 0; i--) block.corners.RemoveAt(indices[i]);
                        selected.Clear(); Changed(block);
                    }
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Inverser le virage gauche / droite")) {
                Undo.RecordObject(block, "Inverser le virage");
                for (int i = 0; i < block.corners.Count; i++) { var c = block.corners[i]; c.position.x = -c.position.x; c.curveOffset.x = -c.curveOffset.x; block.corners[i] = c; }
                Changed(block);
            }
            if (GUILayout.Button("Repartir du bloc de virage par defaut")) {
                Undo.RecordObject(block, "Reinitialiser le contour"); block.corners = SonicTurnBlock.RightTurn(); selected.Clear(); Changed(block);
            }
            showAdvanced = EditorGUILayout.Foldout(showAdvanced, "Finesse et texture", true);
            if (showAdvanced) {
                serializedObject.Update(); EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(serializedObject.FindProperty("cornerSegments"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("uvSize"));
                if (EditorGUI.EndChangeCheck()) { serializedObject.ApplyModifiedProperties(); Changed(block); } else serializedObject.ApplyModifiedProperties();
            }
            if (!string.IsNullOrEmpty(block.LastError)) EditorGUILayout.HelpBox(block.LastError, MessageType.Warning);
            EditorGUILayout.HelpBox("Cyan : taille X / Z. Bleu sous le bloc : epaisseur. Ctrl + Z annule. Le Mesh Collider suit la forme. Ce bloc a un sol plat ; utilise le Virage Releve pour ajouter un mur incliné.", MessageType.None);
        }

        void OnSceneGUI()
        {
            var block = (SonicTurnBlock)target;
            if (block.corners == null || block.corners.Count < 3) return;
            CleanSelection(block);
            var transform = block.transform;
            using (new Handles.DrawingScope(transform.localToWorldMatrix)) {
                Handles.color = new Color(1, .6f, .15f);
                if (block.TryOutline(out var outline, out _)) {
                    var line = new Vector3[outline.Count + 1];
                    for (int i = 0; i < outline.Count; i++) line[i] = Point(outline[i]);
                    line[line.Length - 1] = line[0]; Handles.DrawAAPolyLine(3, line);
                }
                for (int i = 0; i < block.corners.Count; i++) {
                    Vector3 p = Point(block.corners[i].position);
                    float size = HandleUtility.GetHandleSize(p) * .075f;
                    Handles.color = selected.Contains(i) ? Color.yellow : new Color(1, .5f, .1f);
                    bool toggle = Event.current.shift || Event.current.control || Event.current.command;
                    if (Handles.Button(p, Quaternion.identity, size, size * 1.4f, Handles.SphereHandleCap)) SelectPoint(i, toggle);
                    Handles.Label(p + Vector3.up * size * 2, "Point " + (i + 1));
                    Vector3 next = Point(block.corners[(i + 1) % block.corners.Count].position), mid = (p + next) * .5f;
                    if (block.corners.Count < 32) {
                        Handles.color = new Color(.4f, 1, .6f);
                        if (Handles.Button(mid, Quaternion.identity, size * .65f, size, Handles.DotHandleCap)) {
                            Undo.RecordObject(block, "Ajouter un point");
                            block.corners.Insert(i + 1, new SonicTurnBlock.Corner(mid.x, mid.z)); selected.Clear(); selected.Add(i + 1); Changed(block); break;
                        }
                        Handles.Label(mid + Vector3.up * size, "+");
                    }
                }
                // Midpoint handles lie on the actual curved boundary, not on its construction corner.
                for (int i = 0; i < block.corners.Count; i++) {
                    if (!block.GetCornerCurve(i, out var a, out var control, out var end)) continue;
                    Vector3 mid = Point(.25f * a + .5f * control + .25f * end);
                    Handles.color = new Color(1, .3f, .75f);
                    float size = HandleUtility.GetHandleSize(mid) * .09f;
                    Handles.Label(mid + Vector3.up * size * 2, "Courbe " + (i + 1));
                    EditorGUI.BeginChangeCheck();
                    Vector3 dragged = Handles.Slider2D(mid, Vector3.up, Vector3.right, Vector3.forward, size, Handles.RectangleHandleCap, Vector2.zero);
                    if (EditorGUI.EndChangeCheck()) {
                        Undo.RecordObject(block, "Deformer le contour arrondi");
                        block.SetCurveMidpoint(i, new Vector2(dragged.x, dragged.z)); Changed(block);
                    }
                }
                if (selected.Count > 0) {
                    Vector2 center = SelectionCenter(block);
                    Handles.color = Color.yellow;
                    foreach (int i in selected) Handles.DrawDottedLine(Point(center), Point(block.corners[i].position), 4);
                    EditorGUI.BeginChangeCheck();
                    Vector3 p = Handles.PositionHandle(Point(center), Quaternion.identity);
                    if (EditorGUI.EndChangeCheck()) {
                        Undo.RecordObject(block, "Deplacer les points du bloc"); block.MoveCorners(selected, new Vector2(p.x, p.z) - center); Changed(block);
                    }
                }
                var b = block.ContourBounds;
                Handles.color = Color.cyan;
                ScaleHandle(block, new Vector3(b.max.x, 0, b.center.z), Vector3.right, true);
                b = block.ContourBounds;
                ScaleHandle(block, new Vector3(b.center.x, 0, b.max.z), Vector3.forward, false);
                Handles.color = new Color(.2f, .5f, 1);
                Vector3 bottom = new Vector3(b.center.x, -block.depth, b.center.z);
                Handles.Label(bottom, "Epaisseur");
                EditorGUI.BeginChangeCheck(); var down = Handles.Slider(bottom, Vector3.down);
                if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(block, "Epaisseur du bloc"); block.depth = Mathf.Clamp(-down.y, .1f, 1000); Changed(block); }
            }
        }

        static void ScaleHandle(SonicTurnBlock block, Vector3 origin, Vector3 axis, bool x)
        {
            Handles.Label(origin, x ? "Largeur X" : "Longueur Z");
            EditorGUI.BeginChangeCheck(); Vector3 p = Handles.Slider(origin, axis);
            if (!EditorGUI.EndChangeCheck()) return;
            var b = block.ContourBounds; float old = x ? b.size.x : b.size.z;
            float size = Mathf.Clamp(old + Vector3.Dot(p - origin, axis), 1, 1000);
            Undo.RecordObject(block, "Etirer le bloc");
            block.Resize(x ? size : b.size.x, x ? b.size.z : size);
            // Keep the opposite edge still while stretching the visible handle.
            for (int i = 0; i < block.corners.Count; i++) {
                var c = block.corners[i]; if (x) c.position.x += (size - old) * .5f; else c.position.y += (size - old) * .5f; block.corners[i] = c;
            }
            Changed(block);
        }
        static Vector3 Point(Vector2 p) { return new Vector3(p.x, 0, p.y); }
        static void Changed(SonicTurnBlock block)
        {
            block.Rebuild(); EditorUtility.SetDirty(block);
            if (PrefabUtility.IsPartOfPrefabInstance(block)) PrefabUtility.RecordPrefabInstancePropertyModifications(block);
            SceneView.RepaintAll();
        }
    }

    [InitializeOnLoad]
    public static class SonicTurnBlockBuilder
    {
        public const string Folder = "Assets/Structure/BlocVirage";
        public const string PrefabPath = Folder + "/Bloc_Virage_GreenHill.prefab";
        static string ReportFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/SonicTurnBlock");
        static SonicTurnBlockBuilder() { EditorApplication.update += Ready; }
        static void Ready()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= Ready;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null || !File.Exists(Folder + "/Editor/EditingV2Verified.txt")) Build();
        }
        [MenuItem("Tools/Sonic FX/Structures/Creer et verifier le bloc virage")]
        public static void Build()
        {
            Directory.CreateDirectory(ReportFolder); GameObject go = null;
            try {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) {
                    go = new GameObject("Bloc_Virage_GreenHill"); var block = go.AddComponent<SonicTurnBlock>();
                    var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/BumperEngineV1/Models/Materials/GreenHillTile.mat");
                    if (material == null) throw new Exception("Materiau GreenHillTile introuvable.");
                    go.GetComponent<MeshRenderer>().sharedMaterial = material;
                    if (!block.TryCreateMesh(out var mesh, out var error)) throw new Exception(error);
                    string meshPath = Folder + "/Bloc_Defaut.asset";
                    var oldMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if (oldMesh == null) AssetDatabase.CreateAsset(mesh, meshPath);
                    else { EditorUtility.CopySerialized(mesh, oldMesh); UnityEngine.Object.DestroyImmediate(mesh); mesh = oldMesh; }
                    block.SetBakedMesh(mesh);
                    PrefabUtility.SaveAsPrefabAsset(go, PrefabPath); AssetDatabase.SaveAssets();
                }
                Verify(); Preview();
                File.WriteAllText(Folder + "/Editor/EditingV2Verified.txt", "Selection multiple et deformation des courbes verifiees.");
                AssetDatabase.ImportAsset(Folder + "/Editor/EditingV2Verified.txt");
                File.WriteAllText(Path.Combine(ReportFolder, "unity-tests.txt"), "PASS V2\nMulti-point translation including duplicate/invalid indices; deformation follows curve midpoint; unchanged straight openings; offset mirrors/scales/serializes; undo restores grouped edits; round/sharp and mirrored geometry; roof area, closed collider, physical raycasts, invalid edit retention, lifecycle and preview.\n" + DateTime.Now.ToString("s"));
                Debug.Log("Bloc virage pret : " + PrefabPath);
            }
            catch (Exception e) { File.WriteAllText(Path.Combine(ReportFolder, "unity-tests.txt"), "FAIL\n" + e); Debug.LogException(e); }
            finally { if (go != null) UnityEngine.Object.DestroyImmediate(go); }
        }

        [MenuItem("GameObject/Sonic FX/Bloc virage editable", false, 13)]
        static void Add()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) { Build(); prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath); }
            if (prefab == null) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(go, "Ajouter un bloc virage");
            if (SceneView.lastActiveSceneView != null) go.transform.position = SceneView.lastActiveSceneView.pivot;
            Selection.activeGameObject = go;
        }

        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        static void Verify()
        {
            GameObject go = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            try {
                go.hideFlags = HideFlags.HideAndDontSave; go.transform.position = new Vector3(10000, 1000, 10000);
                var block = go.GetComponent<SonicTurnBlock>(); var collider = go.GetComponent<MeshCollider>();
                Check(go.GetComponent<MeshRenderer>().sharedMaterial != null, "Material assigned");
                foreach (bool rounded in new[] { false, true }) foreach (float sign in new[] { 1f, -1f }) {
                    block.corners = SonicTurnBlock.RightTurn();
                    for (int i = 0; i < block.corners.Count; i++) { var c = block.corners[i]; c.position.x *= sign; block.corners[i] = c; }
                    block.roundCorners = rounded; Check(block.Rebuild(), "Rebuild: " + block.LastError); Physics.SyncTransforms();
                    Check(collider.sharedMesh == go.GetComponent<MeshFilter>().sharedMesh && !collider.convex && !collider.isTrigger, "Solid matching collider");
                    ValidateMesh(collider.sharedMesh);
                    Check(block.TryOutline(out var outline, out _), "Outline available");
                    float area = 0; for (int i = 0; i < outline.Count; i++) { var a = outline[i]; var b = outline[(i + 1) % outline.Count]; area += a.x * b.y - a.y * b.x; }
                    var vertices = collider.sharedMesh.vertices; var triangles = collider.sharedMesh.triangles; float roofArea = 0;
                    for (int i = 0; i < triangles.Length; i += 3) {
                        var a = vertices[triangles[i]]; var b = vertices[triangles[i + 1]]; var c = vertices[triangles[i + 2]];
                        if (a.y == 0 && b.y == 0 && c.y == 0) roofArea += Vector3.Cross(b - a, c - a).magnitude * .5f;
                    }
                    Check(Mathf.Abs(roofArea - area * .5f) < .02f, "Triangulation covers exactly the contour area");
                    foreach (var local in new[] { new Vector3(-6 * sign, 0, 2), new Vector3(-6 * sign, 0, 12), new Vector3(8 * sign, 0, 18) }) {
                        Vector3 p = go.transform.TransformPoint(local);
                        Check(collider.Raycast(new Ray(p + Vector3.up * 3, Vector3.down), out var hit, 4), "Walkable top exists");
                        Check(Mathf.Abs(hit.point.y - p.y) < .001f && hit.normal.y > .9999f, "Top has no steps or ridges");
                    }
                    Vector3 outside = go.transform.TransformPoint(new Vector3(8 * sign, 0, 4));
                    Check(!collider.Raycast(new Ray(outside + Vector3.up * 3, Vector3.down), out _, 12), "No phantom collision outside outline");
                }
                block.Resize(40, 32); block.depth = 9; Check(block.Rebuild(), "Resize valid");
                Check(Mathf.Abs(collider.sharedMesh.bounds.min.y + 9) < .001f && collider.sharedMesh.bounds.max.y == 0, "Thickness never moves the floor");
                Check(Mathf.Abs(block.ContourBounds.size.x - 40) < .001f && Mathf.Abs(block.ContourBounds.size.z - 32) < .001f, "Editable dimensions");
                var validCorners = new List<SonicTurnBlock.Corner>(block.corners); var previous = collider.sharedMesh;
                block.corners[1] = block.corners[0]; Check(!block.Rebuild() && collider.sharedMesh == previous, "Invalid edit preserves previous collision");
                block.corners = validCorners; Check(block.Rebuild(), "Recovery after invalid edit");
                block.enabled = false; block.enabled = true; Check(collider.sharedMesh != null, "Regenerated after enable");
                ValidateMesh(collider.sharedMesh);
                VerifyEditing(block);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        static void VerifyEditing(SonicTurnBlock block)
        {
            block.corners = SonicTurnBlock.RightTurn(); block.roundCorners = true;
            var original = new List<SonicTurnBlock.Corner>(block.corners);
            Vector2 delta = new Vector2(-2, 1);
            block.MoveCorners(new[] { 0, 5, 5, -1, 100 }, delta);
            for (int i = 0; i < block.corners.Count; i++)
                Check(Vector2.Distance(block.corners[i].position, original[i].position + (i == 0 || i == 5 ? delta : Vector2.zero)) < .00001f, "Only selected points move, once each");
            Check(block.Rebuild(), "Multi-point edit remains valid"); ValidateMesh(block.GetComponent<MeshCollider>().sharedMesh);
            block.corners = SonicTurnBlock.RightTurn();
            Check(block.GetCornerCurve(5, out var a, out var control, out var b), "Outer round curve exists");
            Vector2 target = .25f * a + .5f * control + .25f * b + new Vector2(-2, 1.5f);
            Check(block.SetCurveMidpoint(5, target), "Outer curve handle supported");
            block.GetCornerCurve(5, out var a2, out var c2, out var b2);
            Check(Vector2.Distance(.25f * a2 + .5f * c2 + .25f * b2, target) < .00001f, "Rendered midpoint follows handle exactly");
            Check(a == a2 && b == b2 && block.corners[5].position == original[5].position, "Curve edit preserves anchors and straight openings");
            Check(block.Rebuild(), "Deformed curve rebuilds"); ValidateMesh(block.GetComponent<MeshCollider>().sharedMesh);
            var collider = block.GetComponent<MeshCollider>(); Physics.SyncTransforms();
            Vector3 inside = block.transform.TransformPoint(new Vector3(target.x + .3f, 0, target.y - .3f));
            Check(collider.Raycast(new Ray(inside + Vector3.up * 2, Vector3.down), out var hit, 3) && hit.normal.y > .9999f, "Collision follows expanded outer curve");
            string serialized = JsonUtility.ToJson(block); Vector2 savedOffset = block.corners[5].curveOffset;
            block.corners = SonicTurnBlock.RightTurn(); JsonUtility.FromJsonOverwrite(serialized, block);
            Check(block.corners[5].curveOffset == savedOffset, "Curve offsets survive serialization");
            var bounds = block.ContourBounds; block.Resize(bounds.size.x * 2, bounds.size.z * 3);
            Check(Vector2.Distance(block.corners[5].curveOffset, new Vector2(savedOffset.x * 2, savedOffset.y * 3)) < .00001f, "Curve deformation follows resized block");
            Check(block.Rebuild(), "Resized custom curve valid");
            int undoGroup;
            Undo.IncrementCurrentGroup(); undoGroup = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Test selection multiple bloc");
            Vector2 before0 = block.corners[0].position, before5 = block.corners[5].position;
            Undo.RecordObject(block, "Test deplacement groupe"); block.MoveCorners(new[] { 0, 5 }, Vector2.one);
            Undo.FlushUndoRecordObjects(); Undo.CollapseUndoOperations(undoGroup); Undo.PerformUndo();
            Check(block.corners[0].position == before0 && block.corners[5].position == before5, "Undo restores the whole selection");
            Undo.ClearUndo(block); block.Rebuild();
        }

        static void ValidateMesh(Mesh mesh)
        {
            var v = mesh.vertices; var normals = mesh.normals; var t = mesh.triangles;
            Check(mesh.uv.Length == v.Length && normals.Length == v.Length, "UVs and normals complete");
            var edges = new Dictionary<string, int>();
            for (int i = 0; i < t.Length; i += 3) {
                int a = t[i], b = t[i + 1], c = t[i + 2]; var cross = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
                Check(cross.sqrMagnitude > 1e-12f && Vector3.Dot(cross, normals[a] + normals[b] + normals[c]) > 0, "Non-degenerate triangles with outward normals");
                Edge(edges, v[a], v[b]); Edge(edges, v[b], v[c]); Edge(edges, v[c], v[a]);
            }
            foreach (int count in edges.Values) Check(count == 2, "Mesh is watertight");
        }
        static string Key(Vector3 p) { return p.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," + p.y.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," + p.z.ToString("R", System.Globalization.CultureInfo.InvariantCulture); }
        static void Edge(Dictionary<string, int> edges, Vector3 a, Vector3 b)
        {
            string ka = Key(a), kb = Key(b); string key = string.CompareOrdinal(ka, kb) < 0 ? ka + "/" + kb : kb + "/" + ka;
            edges.TryGetValue(key, out int count); edges[key] = count + 1;
        }
        static void Preview()
        {
            var preview = new PreviewRenderUtility(); GameObject go = null; Texture2D image = null;
            try {
                go = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
                var block = go.GetComponent<SonicTurnBlock>();
                block.GetCornerCurve(5, out var a, out var c, out var b); block.SetCurveMidpoint(5, .25f * a + .5f * c + .25f * b + new Vector2(-2, 1.5f)); block.Rebuild();
                preview.AddSingleGO(go);
                preview.camera.fieldOfView = 42; preview.camera.transform.position = new Vector3(38, 37, -35); preview.camera.transform.LookAt(new Vector3(0, -1, 12));
                preview.camera.nearClipPlane = .1f; preview.camera.farClipPlane = 300;
                preview.camera.clearFlags = CameraClearFlags.SolidColor; preview.camera.backgroundColor = new Color(.12f, .19f, .25f);
                preview.lights[0].intensity = 1.3f; preview.lights[0].transform.rotation = Quaternion.Euler(48, -30, 0);
                preview.lights[1].intensity = .8f; preview.ambientColor = new Color(.65f, .65f, .65f);
                preview.BeginStaticPreview(new Rect(0, 0, 1100, 800)); preview.Render(); image = preview.EndStaticPreview();
                File.WriteAllBytes(Path.Combine(ReportFolder, "Bloc_Virage_Courbe_Modifiable.png"), image.EncodeToPNG());
            }
            finally { if (image != null) UnityEngine.Object.DestroyImmediate(image); preview.Cleanup(); if (go != null) UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
