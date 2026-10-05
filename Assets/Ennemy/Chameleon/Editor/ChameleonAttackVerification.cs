using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SonicFX.Chameleon.Editor
{
    // Explicit request only: tests stay in an isolated preview scene.
    [InitializeOnLoad]
    static class ChameleonAttackVerification
    {
        static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/ChameleonAttacks");
        static ChameleonAttackVerification() { EditorApplication.update += Ready; }
        static void Ready()
        {
            string request = Path.Combine(Folder, "request.txt");
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(request)) return;
            File.Delete(request); Verify();
        }
        static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        static bool Hit(ChameleonController c) => (bool)typeof(ChameleonController).GetField("tongueHit", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(c);
        static GameObject Cube(Scene scene, string name, Vector3 pos, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            SceneManager.MoveGameObjectToScene(go, scene); go.transform.position = pos; go.transform.localScale = size; return go;
        }
        static ChameleonController Spawn(Scene scene, string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path); Check(prefab != null, "Missing prefab: " + path);
            var go = Object.Instantiate(prefab); SceneManager.MoveGameObjectToScene(go, scene);
            var c = go.GetComponent<ChameleonController>(); Check(c != null, "Missing controller: " + path);
            c.enabled = false; c.visual.enabled = false; return c;
        }
        static void Step(ChameleonController c, PhysicsScene physics, float duration)
        {
            while (duration > .00001f) { float dt = Mathf.Min(.02f, duration); c.Tick(dt); physics.Simulate(dt); duration -= dt; }
        }
        static void Place(ChameleonController c, Vector3 position, Quaternion rotation)
        {
            var rb = c.GetComponent<Rigidbody>(); rb.interpolation = RigidbodyInterpolation.None;
            c.transform.SetPositionAndRotation(position, rotation); rb.position = position; rb.rotation = rotation;
        }
        static void Target(PlayerBhysics player, Vector3 aim) { player.transform.position = aim - Vector3.up * .65f; Physics.SyncTransforms(); }
        static void ResetWall(ChameleonController c, GameObject wall, PlayerBhysics player, Vector3 normal)
        {
            Place(c, wall.transform.position + normal * 1.2f, Quaternion.identity);
            c.onWall = true; c.startCamouflaged = true; c.wallCollider = wall.GetComponent<Collider>(); c.player = player;
            c.jumpDistance = 15; c.jumpTriggerDistance = 5; c.reactionTime = .35f;
            Physics.SyncTransforms(); c.Initialize(); Physics.SyncTransforms(); Check(c.AttachedToWall && c.IsCamouflaged, "Wall placement/camouflage");
        }
        [MenuItem("Sonic FX/Ennemis/Verifier tirs et langue du cameleon")]
        static void Verify()
        {
            var active = SceneManager.GetActiveScene(); bool dirty = active.isDirty; var selection = Selection.objects;
            var targets = HomingAttackControl.Targets; var targetObject = HomingAttackControl.TargetObject;
            Scene scene = default; var report = new StringBuilder();
            Directory.CreateDirectory(Folder);
            try
            {
                // Read current map settings without changing its objects or prefab overrides.
                foreach (var root in active.GetRootGameObjects()) foreach (var c in root.GetComponentsInChildren<ChameleonController>(true))
                    report.AppendLine(c.name + ": wall=" + c.onWall + " vision=" + c.viewDistance + " leapReach=" + c.jumpDistance + " leapProximity=" + c.jumpTriggerDistance + " projectile=" + (c.projectilePrefab != null) + " mouth=" + (c.mouth != null) + " tongue=" + (c.visual != null && c.visual.tongue != null));
                scene = EditorSceneManager.NewPreviewScene(); var physics = scene.GetPhysicsScene();
                Check(physics.IsValid() && physics != Physics.defaultPhysicsScene, "Test physics must be isolated");
                var dummy = new GameObject("Sonic attack test"); SceneManager.MoveGameObjectToScene(dummy, scene);
                var player = dummy.AddComponent<PlayerBhysics>(); player.enabled = false;
                var playerCollider = dummy.AddComponent<SphereCollider>(); playerCollider.center = Vector3.up * .65f; playerCollider.radius = .45f;
                var wall = Cube(scene, "Test wall", new Vector3(0, 5, 0), new Vector3(30, 20, 1));
                wall.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/BumperEngineV1/Models/Materials/GreenHillTile.mat");
                foreach (string path in new[] { "Assets/Ennemy/Chameleon/Cameleon_Mur.prefab", "Assets/Ennemy/Cameleon.prefab" })
                {
                    var c = Spawn(scene, path);
                    foreach (float angle in new[] { 0f, 90f, 180f, 270f })
                    {
                        var rotation = Quaternion.Euler(0, angle, 0); var normal = rotation * Vector3.forward;
                        wall.transform.rotation = rotation; ResetWall(c, wall, player, normal);
                        // Regression: radius 15 used to select leap instead of shooting at distance 12.
                        Target(player, c.Eye + normal * 12); int before = c.ProjectilesFired;
                        Step(c, physics, .32f); Check(c.ProjectilesFired == before && !c.IsLeaping, "Reaction delay respected");
                        Step(c, physics, .45f); Check(c.ProjectilesFired == before + 1 && c.AttachedToWall && c.IsCamouflaged, "One wall shot instead of distant leap: " + path + "/" + angle);
                        Check(c.FirstHit(c.mouth.position, c.projectilePrefab.radius, normal, .01f, out _) == null, "Projectile exit overlaps terrain");
                        Step(c, physics, .5f); Check(c.ProjectilesFired == before + 1, "No burst during attack");
                        Step(c, physics, c.attackCooldown + c.windup + .8f); Check(c.ProjectilesFired > before + 1, "Shots resume after cooldown");
                        Check(Vector3.Dot(c.transform.up, normal) > .99f, "Wall orientation preserved");
                    }
                    wall.transform.rotation = Quaternion.identity; ResetWall(c, wall, player, Vector3.forward);
                    Target(player, c.Eye + Vector3.forward * 12); int count = c.ProjectilesFired;
                    Step(c, physics, .4f); Target(player, c.Eye + Vector3.forward * 2);
                    Step(c, physics, .36f); Check(c.ProjectilesFired == count + 1 && !c.IsLeaping, "Moving close must not cancel windup");
                    Step(c, physics, .5f); Check(c.IsLeaping && !c.IsCamouflaged && !c.AttachedToWall && c.homingTarget.activeSelf, "Close leap still works after shot");
                    ResetWall(c, wall, player, Vector3.forward); Target(player, c.Eye + Vector3.forward * 2 + Vector3.down * 3);
                    Step(c, physics, .4f); Check(c.IsLeaping, "Close Sonic below wall can still be reached");
                    ResetWall(c, wall, player, Vector3.forward); Target(player, c.Eye + Vector3.forward * 12);
                    var obstacle = Cube(scene, "Vision blocker", c.Eye + Vector3.forward * 6, Vector3.one * 2); Physics.SyncTransforms();
                    count = c.ProjectilesFired; Step(c, physics, 1.2f); Check(c.ProjectilesFired == count && !c.CanSee(player.transform.position + Vector3.up * .65f), "Terrain blocks vision");
                    Object.DestroyImmediate(obstacle); ResetWall(c, wall, player, Vector3.forward); Target(player, c.Eye - Vector3.forward * 12);
                    count = c.ProjectilesFired; Step(c, physics, 1.2f); Check(c.ProjectilesFired == count, "No shooting behind wall");
                    report.AppendLine("PASS wall: " + path + "; four orientations, reaction, shot, cooldown, camouflage, interrupted-windup regression, close/below leap, obstacle and back-facing rejection.");
                    Object.DestroyImmediate(c.gameObject);
                }
                Object.DestroyImmediate(wall);
                var floor = Cube(scene, "Ground", new Vector3(0, -.5f, 0), new Vector3(40, 1, 40));
                foreach (string path in new[] { "Assets/Ennemy/Chameleon/Cameleon_Sol.prefab", "Assets/Ennemy/Cameleon.prefab" })
                {
                    var c = Spawn(scene, path); c.onWall = false; c.player = player;
                    Place(c, Vector3.up * .04f, Quaternion.identity); Physics.SyncTransforms(); c.Initialize();
                    Target(player, c.Eye + Vector3.forward * 4); Step(c, physics, .4f);
                    // Re-aim when Sonic moves during preparation.
                    Target(player, c.Eye + Vector3.forward * 4 + Vector3.right * .7f);
                    Step(c, physics, c.windup + c.tongueDuration * .5f - .05f);
                    Check(c.visual.tongue.enabled && c.visual.tongue.positionCount == 3, "Ground tongue extended: " + path);
                    Check(Hit(c), "Tongue detects Sonic collider"); Check(c.ProjectilesFired == 0 && !c.IsLeaping, "Ground never shoots projectile or leaps");
                    Check(Vector3.Distance(c.visual.tongue.GetPosition(2), c.visual.tongueTip.position) < .001f, "Tongue tip follows extension");
                    CaptureTongue(c, path.Contains("Cameleon_Sol") ? "tongue.png" : "tongue-map-prefab.png");
                    Step(c, physics, c.tongueDuration); Check(!c.visual.tongue.enabled && !c.visual.tongueTip.gameObject.activeSelf, "Tongue retracts after attack");
                    Place(c, Vector3.up * .04f, Quaternion.identity); c.Initialize(); Target(player, c.Eye + Vector3.forward * 4); Step(c, physics, .4f);
                    var obstacle = Cube(scene, "Tongue blocker", c.mouth.position + Vector3.forward * 1.5f, new Vector3(1, 3, .4f)); Physics.SyncTransforms();
                    Step(c, physics, c.windup + c.tongueDuration * .5f - .05f);
                    Check(c.visual.tongue.enabled && !Hit(c), "Tongue cannot hurt through terrain");
                    Check(Vector3.Distance(c.visual.tongue.GetPosition(0), c.visual.tongue.GetPosition(2)) < 2, "Tongue stops at obstacle");
                    Object.DestroyImmediate(obstacle); report.AppendLine("PASS ground: " + path + "; moving target, visible tongue and tip, Sonic contact, no projectile, retraction, terrain blocks tongue.");
                    Object.DestroyImmediate(c.gameObject);
                }
                Check(SceneManager.GetActiveScene() == active && active.isDirty == dirty, "Open map changed");
                Check(Selection.objects.Length == selection.Length, "Selection changed");
                for (int i = 0; i < selection.Length; i++) Check(Selection.objects[i] == selection[i], "Selection changed");
                File.WriteAllText(Path.Combine(Folder, "tests.txt"), "PASS\n" + report + DateTime.Now.ToString("s"));
            }
            catch (Exception e) { File.WriteAllText(Path.Combine(Folder, "tests.txt"), "FAIL\n" + report + e); Debug.LogException(e); }
            finally
            {
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                HomingAttackControl.Targets = targets; HomingAttackControl.TargetObject = targetObject;
            }
        }
        static void CaptureTongue(ChameleonController source, string filename)
        {
            var preview = new PreviewRenderUtility(); Texture2D image = null;
            try
            {
                var go = Object.Instantiate(source.gameObject); preview.AddSingleGO(go);
                var c = go.GetComponent<ChameleonController>(); c.enabled = false; c.visual.enabled = false;
                c.visual.ShowTongue(source.visual.tongue.GetPosition(0), source.visual.tongue.GetPosition(2), source.tongueRadius * 2);
                preview.camera.transform.position = new Vector3(7, 4.5f, 8); preview.camera.transform.LookAt(new Vector3(0, 1, 1.2f)); preview.camera.fieldOfView = 38;
                preview.camera.clearFlags = CameraClearFlags.SolidColor; preview.camera.backgroundColor = new Color(.04f, .09f, .15f);
                preview.lights[0].intensity = 1.3f; preview.lights[0].transform.rotation = Quaternion.Euler(35, -30, 0); preview.lights[1].intensity = .8f; preview.ambientColor = Color.gray;
                preview.BeginStaticPreview(new Rect(0, 0, 1000, 750)); preview.Render(); image = preview.EndStaticPreview();
                File.WriteAllBytes(Path.Combine(Folder, filename), image.EncodeToPNG());
            }
            finally { if (image != null) Object.DestroyImmediate(image); preview.Cleanup(); }
        }
    }
}
