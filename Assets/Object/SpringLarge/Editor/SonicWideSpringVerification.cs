using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace SonicFX.Structures.Editor
{
    public static class SonicWideSpringVerification
    {
        static void Check(bool result,string message){if(!result)throw new Exception(message);}
        [MenuItem("Tools/Sonic FX/Structures/Verifier Spring large")]
        public static void Verify()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage()!=null)return;
            Directory.CreateDirectory(SonicWideSpringBuilder.Reports);
            var scene=EditorSceneManager.NewPreviewScene();var active=SceneManager.GetActiveScene();bool dirty=active.isDirty;var selection=Selection.objects;
            string temp=SonicWideSpringBuilder.Folder+"/Editor/Test-"+Guid.NewGuid().ToString("N");
            GameObject go=null,playerGo=null;UnityEditor.Editor editor=null;
            try
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(SonicWideSpringBuilder.PrefabPath);Check(prefab!=null,"Prefab missing");
                go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);var wide=go.GetComponent<SonicWideSpring>();wide.Refresh();
                Check(wide.source!=null && AssetDatabase.Contains(wide.source) && wide.source.isReadable,"Persistent source mesh");
                Check(wide.SlotCount==3 && wide.Slots.Length==3,"Initial three emplacements");
                editor=UnityEditor.Editor.CreateEditor(wide);Check(editor is SonicWideSpringEditor,"Editable inspector not installed");Object.DestroyImmediate(editor);editor=null;
                var mats=wide.visual.GetComponent<MeshRenderer>().sharedMaterials;
                Check(mats.Length==2 && mats[0].mainTexture!=null && mats[1].mainTexture!=null,"Both supplied diffuse textures");
                Check(mats[0].GetTexture("_SpecGlossMap")!=null && mats[1].GetTexture("_SpecGlossMap")!=null && mats[0].GetTexture("_EmissionMap")!=null,"Supplied specular/emission maps");
                Check(mats[1].mainTexture.wrapMode==TextureWrapMode.Repeat,"Original star UVs exceed U=1 and require texture Repeat");
                var audio=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BumperEngineV1/ObjectPrefabs/Spring.prefab").GetComponent<AudioSource>();
                foreach(int count in new[]{1,2,3,8,32})
                {
                    wide.width=count*SonicWideSpring.Pitch+SonicWideSpring.EndMargin;wide.Refresh();Check(wide.SlotCount==count,"Width count "+count);
                    var mesh=wide.visual.sharedMesh;Check(mesh!=wide.source && mesh.subMeshCount==2 && mesh.uv.Length==mesh.vertexCount,"Generated source and UVs "+count);
                    Check(Mathf.Abs(mesh.bounds.size.x-wide.ActualWidth)<.002f,"Full housing width "+count+": "+mesh.bounds.size.x+" expected "+wide.ActualWidth);
                    for(int i=0;i<wide.Slots.Length;i++)
                    {
                        var t=wide.Slots[i];Check(t.gameObject.activeSelf==(i<count) && t.GetComponent<Collider>().enabled==(i<count),"Removed collision / homing target remains active "+i);
                        if(i>=count)continue;
                        Check(t.CompareTag("Spring") && t.GetComponent<Spring_Proprieties>().anim!=null && t.GetComponent<BoxCollider>().isTrigger,"Original Spring receiver "+i);
                        Check(t.GetChild(0).localPosition==Vector3.up*3.8f && t.Find("HomingTarget").CompareTag("HomingTarget"),"Launch / homing points "+i);
                        Check(Mathf.Abs(t.localPosition.x-(i-(count-1)*.5f)*SonicWideSpring.Pitch)<.001f,"Centered slot "+i);
                        Check(t.GetComponent<AudioSource>().clip==audio.clip && t.GetComponent<AudioSource>().outputAudioMixerGroup==audio.outputAudioMixerGroup,"Original sound/mixer "+i);
                        Check(t.GetComponentInChildren<ParticleSystem>().GetComponent<ParticleSystemRenderer>().sharedMaterial.mainTexture!=null,"Supplied burst star texture");
                        bool star=false;var v=mesh.vertices;foreach(int vertex in mesh.GetTriangles(1))if(Mathf.Abs(v[vertex].x-t.localPosition.x)<.5f){star=true;break;}
                        Check(star,"No star at emplacement "+i);
                    }
                }
                var reused=wide.Slots[7];wide.width=3*SonicWideSpring.Pitch+SonicWideSpring.EndMargin;wide.Refresh();
                wide.width=8*SonicWideSpring.Pitch+SonicWideSpring.EndMargin;wide.Refresh();Check(wide.Slots[7]==reused && wide.Slots.Length==32,"Growth does not reuse removed slots");
                wide.width=3*SonicWideSpring.Pitch+SonicWideSpring.EndMargin;go.transform.localScale=new Vector3(2,1,1);wide.Refresh();
                Check(wide.SlotCount==6,"Transform X enlargement does not add slots");
                Check(Mathf.Abs(Vector3.Distance(wide.Slots[0].position,wide.Slots[1].position)-SonicWideSpring.Pitch)<.001f,"X stretches stars instead of adding slots");
                go.transform.localScale=new Vector3(.5f,1,1);wide.Refresh();Check(wide.SlotCount==1,"Transform X reduction does not remove slots");
                go.transform.localScale=Vector3.one;wide.Refresh();wide.visual.sharedMesh=wide.source;
                Check(PrefabUtility.SaveAsPrefabAsset(go,temp+".prefab")!=null,"Save test prefab");
                var loaded=PrefabUtility.LoadPrefabContents(temp+".prefab");
                try{var w=loaded.GetComponent<SonicWideSpring>();w.Refresh();Check(w.SlotCount==3 && w.Slots.Length==32 && w.Slots[0].GetComponent<SonicWideSpringSlot>().owner==w,"Reload count/pool/owner links");}
                finally{PrefabUtility.UnloadPrefabContents(loaded);}
                wide.Refresh();
                Undo.IncrementCurrentGroup();Undo.RecordObject(wide,"Test wide spring width");wide.width=7*SonicWideSpring.Pitch+SonicWideSpring.EndMargin;wide.Refresh();PrefabUtility.RecordPrefabInstancePropertyModifications(wide);Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();wide.Refresh();Check(wide.SlotCount==3,"Undo width");Undo.PerformRedo();wide.Refresh();Check(wide.SlotCount==7,"Redo width");Undo.ClearUndo(wide);
                wide.width=3*SonicWideSpring.Pitch+SonicWideSpring.EndMargin;wide.Refresh();
                playerGo=new GameObject("Sonic Spring verification");SceneManager.MoveGameObjectToScene(playerGo,scene);
                var rb=playerGo.AddComponent<Rigidbody>();rb.useGravity=false;var player=playerGo.AddComponent<PlayerBhysics>();player.enabled=false;player.p_rigidbody=rb;
                var actions=playerGo.AddComponent<ActionManager>();actions.enabled=false;
                actions.Action00=playerGo.AddComponent<Action00_Regular>();actions.Action00.enabled=false;
                actions.Action01=playerGo.AddComponent<Action01_Jump>();actions.Action01.enabled=false;actions.Action01.JumpBall=new GameObject("JumpBall");actions.Action01.JumpBall.transform.SetParent(playerGo.transform,false);
                var input=playerGo.AddComponent<PlayerBinput>();input.enabled=false;
                var actorController=AnimatorController.CreateAnimatorControllerAtPath(temp+".controller");actorController.AddParameter("Action",AnimatorControllerParameterType.Int);actorController.layers[0].stateMachine.AddState("Regular");
                actions.Action00.CharacterAnimator=playerGo.AddComponent<Animator>();actions.Action00.CharacterAnimator.runtimeAnimatorController=actorController;
                var interaction=playerGo.AddComponent<Objects_Interaction>();interaction.enabled=false;interaction.Player=player;interaction.Actions=actions;interaction.Inp=input;
                go.transform.rotation=Quaternion.Euler(0,0,35);wide.springForce=123;wide.isAdditive=false;wide.lockControl=false;wide.Refresh();
                var slot=wide.Slots[0];var prop=slot.GetComponent<Spring_Proprieties>();prop.anim.Rebind();prop.anim.Update(0);
                rb.linearVelocity=new Vector3(3,-4,5);interaction.OnTriggerEnter(slot.GetComponent<Collider>());
                Check((rb.linearVelocity-slot.up*123).magnitude<.001f,"Original non-additive Spring force / rotated direction");
                Check((playerGo.transform.position-slot.GetChild(0).position).magnitude<.001f && actions.Action==0 && !actions.Action01.JumpBall.activeSelf,"Original launch point and jump action");
                Vector3 launched=rb.linearVelocity;interaction.OnTriggerEnter(wide.Slots[1].GetComponent<Collider>());Check(rb.linearVelocity==launched && playerGo.transform.position==slot.GetChild(0).position,"Neighboring slot double-activation");
                int pulses=wide.PulseCount;prop.anim.Update(.05f);Check(wide.PulseCount>pulses,"Hit animation does not pulse the model");
                wide.TickAnimation(.09f);Check(wide.visual.transform.localScale.y<1,"Compression animation");wide.TickAnimation(.5f);Check(Mathf.Abs(wide.visual.transform.localScale.y-1)<.001f,"Animation returns to normal");
                wide.ClearActivationHistory();wide.isAdditive=true;wide.springForce=77;wide.lockControl=true;wide.lockTime=13;wide.Refresh();var before=new Vector3(3,-4,5);rb.linearVelocity=before;
                interaction.OnTriggerEnter(wide.Slots[2].GetComponent<Collider>());
                Check((rb.linearVelocity-(before+go.transform.up*77)).magnitude<.001f,"Original additive Spring mode");
                var locked=typeof(PlayerBinput).GetProperty("LockInput",BindingFlags.NonPublic|BindingFlags.Instance);Check((bool)locked.GetValue(input),"Original control lock");
                Check(wide.Slots[2].GetComponent<Spring_Proprieties>().LockTime==13,"Configurable original lock duration");
                Capture(prefab);
                Check(SceneManager.GetActiveScene()==active && active.isDirty==dirty,"User map changed");
                File.WriteAllText(Path.Combine(SonicWideSpringBuilder.Reports,"tests.txt"),"PASS\n"+DateTime.Now.ToString("s")+"\nSix supplied textures bound; original model and UVs retained; 1/2/3/8/32 slots; shape and star count; X scale adds/removes rather than stretching; disabled surplus contacts/targets; reused slots; saved/reloaded width and references; Undo/Redo; real Objects_Interaction Spring callback; absolute/additive force and rotation; launch point; regular action; neighbor debounce; original sound/mixer; Hit trigger animation and recovery; input lock; preview; user map unchanged.\n");
                Debug.Log("Spring large installe et verifie : textures, emplacements automatiques, propulsion du Spring, animation et son.");
            }
            catch(Exception e){File.WriteAllText(Path.Combine(SonicWideSpringBuilder.Reports,"tests.txt"),"FAIL\n"+e);Debug.LogException(e);}
            finally
            {
                if(editor!=null)Object.DestroyImmediate(editor);
                if(go!=null){Undo.ClearUndo(go.GetComponent<SonicWideSpring>());Object.DestroyImmediate(go);}
                if(playerGo!=null)Object.DestroyImmediate(playerGo);
                foreach(string extension in new[]{".prefab",".controller"})if(AssetDatabase.LoadMainAssetAtPath(temp+extension)!=null)AssetDatabase.DeleteAsset(temp+extension);
                EditorSceneManager.ClosePreviewScene(scene);Selection.objects=selection;
            }
        }
        static void Capture(GameObject prefab)
        {
            var preview=new PreviewRenderUtility();Texture2D image=null;
            try
            {
                int[] counts={1,3,5};float[] centers={-20,-8,14};
                for(int i=0;i<counts.Length;i++)
                {
                    var go=Object.Instantiate(prefab);preview.AddSingleGO(go);go.transform.position=Vector3.right*centers[i];var w=go.GetComponent<SonicWideSpring>();w.width=counts[i]*SonicWideSpring.Pitch+SonicWideSpring.EndMargin;w.Refresh();
                }
                preview.camera.transform.position=new Vector3(22,26,-38);preview.camera.transform.LookAt(new Vector3(2,1.5f,0));preview.camera.fieldOfView=43;preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=150;preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.06f,.09f,.14f);
                preview.ambientColor=Color.gray;preview.lights[0].intensity=1.4f;preview.lights[0].transform.rotation=Quaternion.Euler(45,-30,0);preview.lights[1].intensity=.8f;
                preview.BeginStaticPreview(new Rect(0,0,1600,850));preview.Render();image=preview.EndStaticPreview();File.WriteAllBytes(Path.Combine(SonicWideSpringBuilder.Reports,"spring-large.png"),image.EncodeToPNG());
            }
            finally{if(image!=null)Object.DestroyImmediate(image);preview.Cleanup();}
        }
    }
}
