using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
namespace SonicFX.Lava.Editor
{
    [InitializeOnLoad]
    static class SonicLavaVerification
    {
        static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/SonicLava");
        static SonicLavaVerification() { EditorApplication.update += Ready; }
        static void Ready()
        {
            string request = Path.Combine(Folder, "request.txt");
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(request)) return;
            File.Delete(request); Verify();
        }
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        static GameObject New(Scene scene, string name) { var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene); return go; }
        static void Verify()
        {
            var active = SceneManager.GetActiveScene(); bool dirty = active.isDirty; var selection = Selection.objects;
            int rings = Objects_Interaction.RingAmount; bool shield = Monitors_Interactions.HasShield;
            Scene scene = default; var report = new StringBuilder();
            try
            {
                SonicLavaBuilder.Build(); scene = EditorSceneManager.NewPreviewScene();
                Check(scene.GetPhysicsScene() != Physics.defaultPhysicsScene, "Isolated physics");
                var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SonicLavaBuilder.PrefabPath)); SceneManager.MoveGameObjectToScene(root, scene);
                var lava = root.GetComponent<SonicLavaVolume>(); root.transform.position = new Vector3(0,20,0);
                lava.width = 6; lava.length = 8; lava.depth = 4; lava.Refresh();
                Check(lava.lethalVolume.isTrigger && lava.lethalVolume.size == new Vector3(6,4,8) && lava.lethalVolume.center == new Vector3(0,-2,0), "Dimensions of lethal volume");
                Check(lava.surface.localScale == new Vector3(6,1,8), "Surface follows width and length");
                Check(lava.Contains(new Vector3(0,18,0)) && !lava.Contains(new Vector3(0,15,0)) && !lava.Contains(new Vector3(4,18,0)), "Depth and sides");
                var go = New(scene,"Sonic lava verification"); var player = go.AddComponent<PlayerBhysics>(); player.enabled = false;
                var hurt = go.AddComponent<HurtControl>(); hurt.enabled = false; hurt.IsInvencible = true;
                var physical = go.AddComponent<SphereCollider>(); physical.radius = .3f;
                var interaction = go.AddComponent<CapsuleCollider>(); interaction.isTrigger = true; interaction.radius = 2; interaction.height = 5;
                Action<Vector3> place = p => { go.transform.position = p; Physics.SyncTransforms(); };
                place(new Vector3(0,20.35f,0)); Check(!lava.CheckPlayer(player), "Large interaction trigger must not kill above surface");
                Objects_Interaction.RingAmount = 123; Monitors_Interactions.HasShield = true;
                place(new Vector3(0,20.25f,0)); Check(lava.CheckPlayer(player) && hurt.isDead, "Physical contact kills despite rings, shield and invincibility");
                Check(Objects_Interaction.RingAmount == 123 && Monitors_Interactions.HasShield, "Bypasses damage and ring loss path");
                Check(!lava.CheckPlayer(player) && lava.DeathsTriggered == 1, "Death latched once");
                report.AppendLine("PASS dimensions, surface, depth, physical contact, interaction trigger excluded, shield/rings/invincibility bypass, one death latch.");
                // Clear history while dead, then respawn outside: no sweep across the map on respawn.
                place(new Vector3(10,21,0)); lava.CheckPlayer(player); hurt.isDead = false;
                Check(!lava.CheckPlayer(player), "Respawn outside is safe");
                place(new Vector3(-10,21,0)); Check(!lava.CheckPlayer(player), "Fast crossing above the surface is safe");
                lava.enabled = false; lava.enabled = true; place(new Vector3(-10,18,0)); Check(!lava.CheckPlayer(player), "Before fast crossing");
                place(new Vector3(10,18,0)); Check(lava.CheckPlayer(player) && hurt.isDead, "Complete high-speed crossing between physics frames kills");
                lava.CheckPlayer(player); hurt.isDead = false; lava.enabled = false; lava.enabled = true;
                place(new Vector3(0,25,0)); Check(!lava.CheckPlayer(player), "Before fast fall"); place(new Vector3(0,13,0)); Check(lava.CheckPlayer(player), "Complete fall through lava kills");
                lava.CheckPlayer(player); hurt.isDead = false; lava.enabled = false; lava.enabled = true;
                physical.enabled = false; interaction.isTrigger = false; interaction.radius = .25f; interaction.height = 2;
                place(new Vector3(0,21.1f,0)); Check(!lava.CheckPlayer(player), "Capsule above lava"); place(new Vector3(0,20.9f,0)); Check(lava.CheckPlayer(player), "Capsule feet contact kills");
                report.AppendLine("PASS safe respawn, safe overhead crossing, swept high-speed crossing, swept vertical fall, capsule foot contact.");
                lava.CheckPlayer(player); hurt.isDead = false; lava.enabled = false; lava.enabled = true; interaction.enabled = false; physical.enabled = true;
                root.transform.rotation = Quaternion.Euler(0,37,0); root.transform.localScale = new Vector3(2,1.5f,.5f); lava.Refresh();
                place(root.transform.TransformPoint(new Vector3(2.5f,-1,3))); Check(lava.CheckPlayer(player), "Rotated and scaled lava contact");
                var otherRoot = Object.Instantiate(root); SceneManager.MoveGameObjectToScene(otherRoot,scene); var other = otherRoot.GetComponent<SonicLavaVolume>();
                Check(!other.CheckPlayer(player) && other.DeathsTriggered == 0, "Overlapping lava cannot count another death");
                other.enabled = false; hurt.isDead = false; Check(!other.CheckPlayer(player), "Disabled lava safe");
                Check(!ShaderUtil.ShaderHasError(Shader.Find("Sonic FX/Lave")), "Lava shader compiled");
                report.AppendLine("PASS rotated/scaled dimensions, overlapping zones count no second death, disabled zone, shader compilation.");
                Capture(lava);
                Check(SceneManager.GetActiveScene() == active && active.isDirty == dirty, "User map unchanged");
                Check(Selection.objects.Length == selection.Length, "Selection unchanged");
                File.WriteAllText(Path.Combine(Folder,"tests.txt"),"PASS\n" + report + DateTime.Now.ToString("s"));
                Debug.Log("Lave : verification complete, aucun niveau modifie.");
            }
            catch (Exception e) { File.WriteAllText(Path.Combine(Folder,"tests.txt"),"FAIL\n" + report + e); Debug.LogException(e); }
            finally { if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene); Objects_Interaction.RingAmount = rings; Monitors_Interactions.HasShield = shield; }
        }
        static void Capture(SonicLavaVolume source)
        {
            var preview = new PreviewRenderUtility(); Texture2D texture = null;
            try
            {
                var go = Object.Instantiate(source.gameObject); foreach (var part in go.GetComponentsInChildren<Transform>()) part.gameObject.layer = 0;
                preview.AddSingleGO(go); go.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity); go.transform.localScale = Vector3.one;
                var lava = go.GetComponent<SonicLavaVolume>(); lava.width = 12; lava.length = 12; lava.Refresh();
                preview.camera.transform.position = new Vector3(10,12,14); preview.camera.transform.LookAt(Vector3.zero); preview.camera.fieldOfView = 40;
                preview.camera.nearClipPlane = .1f; preview.camera.farClipPlane = 100;
                preview.camera.clearFlags = CameraClearFlags.SolidColor; preview.camera.backgroundColor = new Color(.025f,.025f,.035f);
                preview.lights[0].intensity = 1; preview.lights[0].transform.rotation = Quaternion.Euler(60,-20,0); preview.ambientColor = Color.gray;
                preview.BeginStaticPreview(new Rect(0,0,1000,800)); preview.Render(); texture = preview.EndStaticPreview(); File.WriteAllBytes(Path.Combine(Folder,"lava.png"),texture.EncodeToPNG());
            }
            finally { if(texture!=null)Object.DestroyImmediate(texture); preview.Cleanup(); }
        }
    }
}
