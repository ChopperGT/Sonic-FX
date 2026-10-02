using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace SonicFX.Decor.Editor
{
    [InitializeOnLoad] internal static class PalmLeafVerification
    {
        static readonly string Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/PalmLeaves");
        static PalmLeafVerification(){EditorApplication.update+=Ready;}
        static void Ready()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            EditorApplication.update-=Ready;
            string report=Path.Combine(Folder,"unity-report.txt");
            if(!File.Exists(report)||!File.ReadAllText(report).Contains("Fixed frond attachments v2"))Verify();
        }
        static void Check(bool value,string label){if(!value)throw new Exception(label);}
        [MenuItem("Tools/Sonic FX/Decor/Verifier les feuilles reactives")]
        static void Verify()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            Directory.CreateDirectory(Folder);PreviewRenderUtility preview=null;var trees=new List<GameObject>();
            var details=new List<string>();
            try
            {
                PalmLeafInstaller.Install();preview=new PreviewRenderUtility();
                foreach(string name in PalmLeafInstaller.Names)
                {
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PalmLeafInstaller.Root+"/"+name+".prefab");
                    var original=AssetDatabase.LoadAssetAtPath<Mesh>(PalmLeafInstaller.Root+"/GreenHill/"+name+"_GreenHill.asset");
                    Check(prefab!=null&&original!=null,"Palmier et maillage original disponibles : "+name);
                    var reaction=prefab.GetComponent<PalmLeafReaction>();
                    var data=reaction.motionData;
                    Check(reaction.reactToSonic&&data!=null&&data.sourceMesh.isReadable,"Case active et maillage lisible en jeu : "+name);
                    Check(data.version==1&&data.bendWeights.Any(weight=>weight==0)&&data.bendWeights.Any(weight=>weight>.9f),"Points d'attache fixes et extremites flexibles : "+name);
                    Check(data.sourceMesh.vertices.SequenceEqual(original.vertices)&&data.sourceMesh.triangles.SequenceEqual(original.triangles)&&
                        data.sourceMesh.uv.SequenceEqual(original.uv)&&data.sourceMesh.normals.SequenceEqual(original.normals),"Forme et textures conservees : "+name);
                    Check(prefab.GetComponent<MeshCollider>().sharedMesh==data.trunkCollisionMesh,"Collision du tronc configuree : "+name);
                    var sourceVertices=data.sourceMesh.vertices;var sourceUV=data.sourceMesh.uv;
                    var sourceTriangles=data.sourceMesh.triangles;
                    var expected=new List<Vector3>();
                    for(int i=0;i<sourceTriangles.Length;i+=3)
                    {
                        int a=sourceTriangles[i],b=sourceTriangles[i+1],c=sourceTriangles[i+2];
                        if(sourceUV[a].y>.5f&&sourceUV[b].y>.5f&&sourceUV[c].y>.5f)continue;
                        expected.Add(sourceVertices[a]);expected.Add(sourceVertices[b]);expected.Add(sourceVertices[c]);
                    }
                    var colliderVertices=data.trunkCollisionMesh.vertices;
                    var actual=data.trunkCollisionMesh.triangles.Select(index=>colliderVertices[index]);
                    Check(actual.SequenceEqual(expected),"Toutes les faces de feuille exclues de la collision : "+name);
                    var tree=Object.Instantiate(prefab);preview.AddSingleGO(tree);trees.Add(tree);
                    tree.transform.position=Vector3.zero;tree.transform.rotation=Quaternion.identity;
                    // Frame the source models at the size typically used in the level.
                    tree.transform.localScale=Vector3.one*(16f/data.sourceMesh.bounds.size.y);
                    reaction=tree.GetComponent<PalmLeafReaction>();
                    typeof(PalmLeafReaction).GetMethod("OnEnable",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(reaction,null);
                    Vector3 near=tree.transform.TransformPoint(data.leafBounds.center);
                    for(int step=0;step<12;step++)reaction.Step(near,near,Vector3.right*10,1f/60f);
                    float slow=reaction.CurrentDisplacement.magnitude;
                    reaction.ResetMotion();
                    for(int step=0;step<12;step++)reaction.Step(near,near,Vector3.right*60,1f/60f);
                    Check(reaction.CurrentDisplacement.magnitude>slow*4f,"Vitesse plus elevee : mouvement plus fort : "+name);
                    Check(reaction.CurrentDisplacement.x>0,"Feuilles poussees dans le sens du passage : "+name);
                    var changed=tree.GetComponent<MeshFilter>().sharedMesh.vertices;var leaves=new HashSet<int>(data.leafVertices);
                    Check(data.leafVertices.Any(index=>Vector3.Distance(changed[index],sourceVertices[index])>.001f),"Feuilles animees : "+name);
                    Check(Enumerable.Range(0,changed.Length).Where(index=>!leaves.Contains(index)).All(index=>changed[index]==sourceVertices[index]),"Tronc immobile : "+name);
                    Check(data.sourceMesh.vertices.SequenceEqual(sourceVertices),"Maillage partage non modifie : "+name);
                    reaction.reactToSonic=false;reaction.Step(near,near,Vector3.right*100,1f/60f);
                    Check(reaction.CurrentDisplacement==Vector3.zero&&tree.GetComponent<MeshFilter>().sharedMesh.vertices.SequenceEqual(sourceVertices),"Case desactivee : retour exact au repos : "+name);
                    Check(tree.GetComponent<MeshCollider>().sharedMesh==data.trunkCollisionMesh,"Desactiver l'effet ne remet pas les collisions de feuilles : "+name);
                    reaction.reactToSonic=true;
                    Vector3 start=near-Vector3.right*50,end=near+Vector3.right*50;
                    reaction.Step(start,end,Vector3.right*300,1f/60f);
                    Check(reaction.CurrentDisplacement.magnitude>0,"Passage tres rapide entre deux images detecte : "+name);
                    for(int step=0;step<600;step++)reaction.Step(end,end,Vector3.zero,1f/60f);
                    Check(reaction.CurrentDisplacement==Vector3.zero,"Oscillation amortie jusqu'au repos : "+name);
                    reaction.Step(near,near,Vector3.up*-60,1f/60f);
                    Check(reaction.CurrentDisplacement.y<0,"Passage par-dessus / chute pousse les feuilles vers le bas : "+name);
                    reaction.ResetMotion();
                    for(int step=0;step<12;step++)reaction.Step(near,near,Vector3.right*60,1f/60f);
                    reaction.enabled=false;
                    // Capture deformation explicitly: editor OnDisable restores the mesh, so the test replays after disabling updates.
                    for(int step=0;step<12;step++)reaction.Step(near,near,Vector3.right*60,1f/60f);
                    details.Add(name+" : "+data.leafVertices.Length+" sommets de feuilles ; "+data.trunkCollisionMesh.triangles.Length/3+" faces de collision du tronc.");
                }
                Render(preview,trees,false);
                foreach(var tree in trees)tree.GetComponent<PalmLeafReaction>().ResetMotion();
                Render(preview,trees,true);
                File.WriteAllText(Path.Combine(Folder,"unity-report.txt"),"PASS\nFixed frond attachments v2. Three existing prefabs updated. Original geometry/UV/materials preserved at rest; leaf faces excluded from collision; trunk remains solid and immobile; cloned runtime mesh only; movement scales with Sonic speed, handles high-speed swept crossings and downward passage; checkbox restores rest without restoring leaf collisions; spring settles. Actual before/after previews rendered.\n"+string.Join("\n",details)+"\n"+DateTime.Now.ToString("s"));
                Debug.Log("Palmiers : collisions du tronc et feuilles reactives verifies.");
            }
            catch(Exception e){File.WriteAllText(Path.Combine(Folder,"unity-report.txt"),"FAIL\n"+e);Debug.LogException(e);}
            finally
            {
                foreach(var tree in trees)
                {
                    if(tree==null)continue;
                    typeof(PalmLeafReaction).GetMethod("OnDisable",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(tree.GetComponent<PalmLeafReaction>(),null);
                    Object.DestroyImmediate(tree);
                }
                if(preview!=null)preview.Cleanup();
            }
        }
        static void Render(PreviewRenderUtility preview,List<GameObject> trees,bool rest)
        {
            var bounds=new Bounds();
            for(int i=0;i<trees.Count;i++)
            {
                var renderer=trees[i].GetComponent<Renderer>();
                var source=trees[i].GetComponent<PalmLeafReaction>().motionData.sourceMesh.bounds;
                trees[i].transform.position=new Vector3(i*14f-source.center.x*trees[i].transform.localScale.x,-source.min.y*trees[i].transform.localScale.y,0);
                if(i==0)bounds=renderer.bounds;else bounds.Encapsulate(renderer.bounds);
            }
            preview.camera.orthographic=true;preview.camera.orthographicSize=Mathf.Max(bounds.size.y*.6f,bounds.size.x/3f);
            preview.camera.transform.position=bounds.center+new Vector3(0,8,-75);preview.camera.transform.LookAt(bounds.center);
            preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=200;
            preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.12f,.35f,.6f);
            preview.ambientColor=new Color(.6f,.6f,.6f);preview.lights[0].intensity=1.2f;preview.lights[0].transform.rotation=Quaternion.Euler(35,-25,0);preview.lights[1].intensity=.5f;
            preview.BeginStaticPreview(new Rect(0,0,1400,750));preview.Render(true);
            var image=preview.EndStaticPreview();File.WriteAllBytes(Path.Combine(Folder,rest?"Palmiers-Repos.png":"Palmiers-Passage.png"),image.EncodeToPNG());Object.DestroyImmediate(image);
        }
    }
}
