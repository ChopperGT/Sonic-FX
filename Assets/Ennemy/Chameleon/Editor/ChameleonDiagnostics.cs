using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
namespace SonicFX.Chameleon.Editor
{
    [InitializeOnLoad] public static class ChameleonDiagnostics
    {
        static double next;
        static readonly double Until=EditorApplication.timeSinceStartup+120;
        static ChameleonDiagnostics() { EditorApplication.update+=Poll; }
        static void Poll() {
            if(EditorApplication.timeSinceStartup>Until){EditorApplication.update-=Poll;return;}
            if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup<next) return;
            next=EditorApplication.timeSinceStartup+5;
            var all=UnityEngine.Object.FindObjectsByType<ChameleonController>(); if(all.Length==0)return;
            var text=new StringBuilder();text.AppendLine(DateTime.Now+" Play="+EditorApplication.isPlaying);
            foreach(var c in all) {
                text.AppendLine(c.name+" Position="+c.transform.position+" Scale="+c.transform.lossyScale+" onWall="+c.onWall+" startCamo="+c.startCamouflaged+" attached="+c.AttachedToWall+" camo="+c.IsCamouflaged+" state="+c.Status);
                text.AppendLine("Player="+(c.player==null?"null":c.player.transform.position.ToString())+" jump="+c.jumpDistance+" Eye="+c.Eye);
                if(c.onWall && c.wallCollider==null) foreach(var d in new[]{Vector3.forward,Vector3.back,Vector3.left,Vector3.right}) foreach(var h in Physics.RaycastAll(c.transform.position+Vector3.up*.15f,d,c.wallSearchDistance,c.environmentLayers,QueryTriggerInteraction.Ignore)) {
                    text.AppendLine("Candidate="+h.collider.name+" type="+h.collider.GetType().Name+" distance="+h.distance);
                    var terrain=h.collider.GetComponent<Terrain>();if(terrain!=null) {var m=terrain.materialTemplate;text.AppendLine("Terrain mat="+m+" shader="+(m!=null?m.shader.name:"")+" layers="+terrain.terrainData.terrainLayers.Length);if(m!=null&&m.HasProperty("_Side"))text.AppendLine("Side="+m.GetTexture("_Side"));}
                }
                if(c.wallCollider!=null) {
                    text.AppendLine("Wall="+c.wallCollider.name+" Type="+c.wallCollider.GetType().Name);
                    for(var t=c.wallCollider.transform;t!=null;t=t.parent) {
                        var r=t.GetComponent<Renderer>(); if(r!=null)foreach(var m in r.sharedMaterials)if(m!=null)text.AppendLine("Renderer="+t.name+" Material="+m.name+" shader="+m.shader.name+" Texture="+(m.HasProperty("_Side")?m.GetTexture("_Side"):m.mainTexture));
                        var terrain=t.GetComponent<Terrain>();if(terrain!=null)text.AppendLine("Terrain material="+terrain.materialTemplate+" layers="+terrain.terrainData.terrainLayers.Length);
                    }
                }
                if(c.camouflage!=null) {text.AppendLine("Camo template="+c.camouflage.camouflageTemplate+" surfaces="+c.camouflage.surfaces.Length);if(c.camouflage.surfaces.Length>0) {var m=c.camouflage.surfaces[0].sharedMaterial;text.AppendLine("Actual material="+m+" texture="+m.mainTexture+" shader errors="+ShaderUtil.ShaderHasError(m.shader));}}
            }
            string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/ChameleonFix");Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,"scene.txt"),text.ToString());
        }
    }
}
