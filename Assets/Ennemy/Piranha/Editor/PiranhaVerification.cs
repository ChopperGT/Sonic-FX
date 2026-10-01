using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using SonicFX.Water;

namespace SonicFX.Piranha.Editor
{
    public static class PiranhaVerification
    {
        [InitializeOnLoadMethod]
        static void VerifyBehaviorUpdate() { EditorApplication.update+=RunWhenReady; }
        static void RunWhenReady()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)return;
            EditorApplication.update-=RunWhenReady;
            string report=Path.Combine(PiranhaBuilder.ReportFolder,"unity-tests.txt");
            if(!File.Exists(report) || !File.ReadAllText(report).Contains("Incremental jump planner regression passed")) Run();
        }
        static void Check(bool value,string label) { if(!value)throw new InvalidOperationException(label); }
        static object Call(object obj,string name,params object[] args) => obj.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(obj,args);
        static void Set(object obj,string name,object value) => obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(obj,value);
        static string Mode(PiranhaController obj) => obj.GetType().GetField("mode",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(obj).ToString();
        [MenuItem("Tools/Sonic FX/Piranha/Verifier le poisson")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            var active=SonicWaterVolume.Active.ToArray();var objects=new List<GameObject>();
            var checks=new List<string>();
            try
            {
                SonicWaterVolume.Active.Clear();Vector3 origin=new Vector3(10000,100,10000);
                var basin=new GameObject("Piranha test water"){hideFlags=HideFlags.HideAndDontSave};objects.Add(basin);basin.transform.position=origin;
                var zone=basin.AddComponent<SonicWaterVolume>();zone.width=40;zone.length=40;zone.depth=30;
                Check(PiranhaController.Fits(zone,origin+Vector3.down*5,1.4f),"Fish inside water");
                Check(!PiranhaController.Fits(zone,origin+Vector3.down*.5f,1.4f),"Whole body clearance at surface");
                Check(!PiranhaController.Fits(zone,origin+new Vector3(19,-5,0),1.4f),"Whole body clearance at banks");
                checks.Add("Volume bounds include fish size.");
                var fish=new GameObject("Piranha test"){hideFlags=HideFlags.HideAndDontSave};objects.Add(fish);fish.transform.position=origin+new Vector3(-4,-1.55f,0);
                var brain=fish.AddComponent<PiranhaController>();brain.bodyRadius=1.4f;brain.water=zone;Call(brain,"Awake");
                Vector3 start=fish.transform.position,end=origin+new Vector3(4,-1.55f,0);
                Check(PiranhaController.SolveArc(start,end,origin.y+5,20,out var velocity,out var duration),"Solve jump");
                Check(Vector3.Distance(PiranhaController.Arc(start,velocity,20,duration),end)<.002f,"Exact landing");
                Vector3 target=PiranhaController.Arc(start,velocity,20,duration*.5f);
                Check(Mathf.Abs(target.y-(origin.y+5))<.002f,"Adjustable apex");
                Check(brain.ValidateArc(start,end,velocity,duration,zone,target),"Clear local jump accepted");
                Check(!brain.ValidateArc(start,origin+Vector3.right*10000,velocity,duration,zone,target),"Distant water rejected");
                checks.Add("Ballistic apex/landing correct; local jump accepted; distant landing rejected.");
                Vector3 oldPosition=zone.transform.position;zone.transform.position=oldPosition+Vector3.down*1000;
                Vector3 deepEnd=end+Vector3.down*1000;
                PiranhaController.SolveArc(start,deepEnd,origin.y+5,20,out var deepVelocity,out var deepDuration);
                Check(!brain.ValidateArc(start,deepEnd,deepVelocity,deepDuration,zone,target),"Distant water below fish rejected");
                zone.transform.position=oldPosition;checks.Add("Vertical landing distance is bounded too.");
                var obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);objects.Add(obstacle);obstacle.hideFlags=HideFlags.HideAndDontSave;obstacle.transform.position=target;obstacle.transform.localScale=new Vector3(1,4,4);Physics.SyncTransforms();
                Check(!brain.ValidateArc(start,end,velocity,duration,zone,target),"Blocked jump rejected");
                obstacle.SetActive(false);Physics.SyncTransforms();
                zone.enabled=false;Check(!brain.ValidateArc(start,end,velocity,duration,zone,target),"Disabled water rejected");zone.enabled=true;
                checks.Add("Obstacles and disabled landing volumes reject jumps.");
                // Two small pools with a gap, both endpoints wet but the gap is too large.
                zone.width=6;zone.transform.position=origin+Vector3.left*4;
                var basin2=new GameObject("Piranha adjacent water"){hideFlags=HideFlags.HideAndDontSave};objects.Add(basin2);basin2.transform.position=origin+Vector3.right*4;
                var zone2=basin2.AddComponent<SonicWaterVolume>();zone2.width=6;zone2.length=40;zone2.depth=30;
                brain.maximumDryGap=1;
                Check(!brain.ValidateArc(start,end,velocity,duration,zone2,target),"Dry gap rejected even with wet endpoints");
                brain.maximumDryGap=6;
                Check(brain.ValidateArc(start,end,velocity,duration,zone2,target),"Adjacent pool accepted within gap limit");
                brain.chargeSpeed=500; // Swimming must not tunnel from one pool to the other.
                Check(!(bool)Call(brain,"Swim",end,500f),"Swimming cannot cross dry gap");
                basin2.SetActive(false);zone.width=40;zone.transform.position=origin;brain.maximumDryGap=3;brain.chargeSpeed=10;
                checks.Add("Adjacent pools respect maximum dry gap; swimming cannot skip across dry land.");
                var playerObj=new GameObject("Piranha test Sonic"){hideFlags=HideFlags.HideAndDontSave};objects.Add(playerObj);playerObj.transform.position=origin+new Vector3(0,-5,0);
                var player=playerObj.AddComponent<PlayerBhysics>();brain.player=player;Set(brain,"nextAttack",-1f);Call(brain,"FixedUpdate");
                Check(Mode(brain)=="Chase","Distant underwater Sonic triggers pursuit, not a premature charge");
                playerObj.transform.position=fish.transform.position+Vector3.right*2-Vector3.up*.65f;
                Call(brain,"FixedUpdate");Check(Mode(brain)=="PrepareCharge","Nearby Sonic starts bite preparation");
                playerObj.transform.position=fish.transform.position+Vector3.right*8-Vector3.up*.65f;
                Call(brain,"FixedUpdate");Check(Mode(brain)=="Chase","Escaping Sonic cancels preparation");
                playerObj.transform.position=fish.transform.position+Vector3.right*2-Vector3.up*.65f;
                Call(brain,"FixedUpdate");Set(brain,"modeAge",1f);Call(brain,"FixedUpdate");
                Check(Mode(brain)=="Charge","Charge follows preparation only within bite range");
                checks.Add("Underwater target triggers preparation then charge.");
                playerObj.transform.position=origin+new Vector3(0,4.35f,0);
                Check((bool)Call(brain,"PlanLeap"),"Sonic above water produces a valid local attack");
                playerObj.transform.position=origin+new Vector3(0,30,0);
                Check(!(bool)Call(brain,"PlanLeap"),"Sonic beyond configured jump height cannot trigger leap");
                checks.Add("Above-water attack is planned; unreachable height rejected.");
                obstacle.SetActive(true);obstacle.transform.position=origin;obstacle.transform.localScale=new Vector3(4,2,4);
                playerObj.transform.position=origin+Vector3.up;
                Physics.SyncTransforms();
                Check(!(bool)Call(brain,"Sees",playerObj.transform.position+Vector3.up*.65f),"Platform blocks direct sight in the regression fixture");
                Check((bool)Call(brain,"FindLaunchPoint"),"Launch planner finds a safe jump over the platform despite blocked direct sight");
                Vector3 stage=(Vector3)typeof(PiranhaController).GetField("stagingPoint",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(brain);
                Check(PiranhaController.Fits(zone,stage,brain.bodyRadius),"Staging point remains in water");
                // Dimensions reported by the user's real level; the original twelve-unit limit cannot span it safely.
                obstacle.transform.position=origin+new Vector3(1.74f,-1.2f,-3.52f);
                obstacle.transform.localScale=new Vector3(8.75f,5.39f,11.76f);
                fish.GetComponent<Rigidbody>().position=origin+new Vector3(-5.73f,-6.74f,7.88f);
                playerObj.transform.position=origin+Vector3.up*1.91f;
                brain.maximumJumpDistance=22;Physics.SyncTransforms();
                var watch=System.Diagnostics.Stopwatch.StartNew();
                Check((bool)Call(brain,"FindLaunchPoint"),"Real level platform has a valid reachable launch and wet landing");
                watch.Stop();checks.Add("Actual level platform regression passed; planning ms="+watch.ElapsedMilliseconds);
                obstacle.transform.localScale=new Vector3(30,30,30);Physics.SyncTransforms();
                Check(!(bool)Call(brain,"FindLaunchPoint"),"Impossible platform does not permit clipping or unsafe leap");
                obstacle.SetActive(false);Physics.SyncTransforms();
                // A high, wide bridge: the old five-unit apex cannot reach Sonic.
                zone.width=100;zone.length=200;brain.maximumJumpDistance=40;brain.launchSearchRadius=24;
                obstacle.SetActive(true);obstacle.transform.position=origin+new Vector3(6.95f,13.115f,-12.16f);
                obstacle.transform.localScale=new Vector3(22.75f,3.25f,142.75f);
                fish.GetComponent<Rigidbody>().position=origin+new Vector3(2.72f,-2.62f,3.75f);
                playerObj.transform.position=origin+Vector3.up*14.74f;
                brain.jumpHeight=5;Physics.SyncTransforms();
                Check(!(bool)Call(brain,"FindLaunchPoint"),"High bridge respects low configured height");
                brain.jumpHeight=20;watch.Restart();
                Check((bool)Call(brain,"FindLaunchPoint"),"High bridge has a reachable launch and a nearby wet landing at Y height 20");
                watch.Stop();
                var route=(List<Vector3>)typeof(PiranhaController).GetField("approachRoute",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(brain);
                Check(route.Count>0,"Launch approach route exists");
                Vector3 cursor=fish.GetComponent<Rigidbody>().position;
                foreach(var point in route){Check((bool)Call(brain,"WetPath",cursor,point),"Each approach segment is wet and clear");cursor=point;}
                fish.GetComponent<Rigidbody>().position=cursor;Physics.SyncTransforms();
                Check((bool)Call(brain,"PlanLeap"),"Fish launches after following planned route");
                checks.Add("High bridge and routed launch regression passed; planning ms="+watch.ElapsedMilliseconds);
                float flight=(float)typeof(PiranhaController).GetField("flightDuration",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(brain);
                Vector3 flightEnd=(Vector3)Call(brain,"FlightPosition",flight);
                Check(PiranhaController.Fits(zone,flightEnd,brain.bodyRadius),"Return jump ends fully submerged");
                fish.GetComponent<Rigidbody>().position=origin+new Vector3(2.72f,-2.62f,3.75f);Physics.SyncTransforms();
                var search=((IEnumerable<bool>)Call(brain,"SearchLaunchPoints")).GetEnumerator();Set(brain,"launchSearch",search);
                int slices=0;
                do{Set(brain,"lastPlanFrame",-1);Call(brain,"AdvanceLaunchSearch");slices++;}
                while(typeof(PiranhaController).GetField("launchSearch",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(brain)!=null && slices<256);
                Check(slices<256 && (bool)typeof(PiranhaController).GetField("hasStagingPoint",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(brain),"Incremental planning completes with a valid launch");
                checks.Add("Incremental jump planner regression passed; slices="+slices);
                // A submerged pier blocks the direct segment and requires multiple waypoints.
                obstacle.transform.position=origin+Vector3.down*3;obstacle.transform.localScale=new Vector3(6,14,6);
                fish.GetComponent<Rigidbody>().position=origin+new Vector3(-7,-4,0);Physics.SyncTransforms();
                Vector3 routeGoal=origin+new Vector3(7,-1.55f,0);var detour=new List<Vector3>();
                Check(!(bool)Call(brain,"WetPath",fish.GetComponent<Rigidbody>().position,routeGoal),"Pier blocks direct swim");
                Check((bool)Call(brain,"FindSwimRoute",routeGoal,detour),"Fish routes around the submerged pier");
                Check(detour.Count>1,"Detour has intermediate waypoints");cursor=fish.GetComponent<Rigidbody>().position;
                foreach(var point in detour){Check((bool)Call(brain,"WetPath",cursor,point),"Pier detour never leaves water or clips the pier");cursor=point;}
                checks.Add("Submerged pier detour regression passed.");
                obstacle.SetActive(false);Physics.SyncTransforms();
                var randomState=UnityEngine.Random.state;UnityEngine.Random.InitState(239);
                var patrolField=typeof(PiranhaController).GetField("patrolTarget",BindingFlags.Instance|BindingFlags.NonPublic);
                float minX=float.MaxValue,maxX=float.MinValue,minZ=float.MaxValue,maxZ=float.MinValue;
                try {
                    Set(brain,"home",origin+Vector3.down*5);fish.GetComponent<Rigidbody>().position=origin+Vector3.down*5;
                    for(int i=0;i<24;i++) {
                        Check((bool)Call(brain,"ChoosePatrolTarget"),"Free patrol selects a wet destination");
                        Vector3 point=(Vector3)patrolField.GetValue(brain);
                        Check(PiranhaController.Fits(zone,point,brain.bodyRadius),"Patrol target remains inside water");
                        minX=Mathf.Min(minX,point.x);maxX=Mathf.Max(maxX,point.x);minZ=Mathf.Min(minZ,point.z);maxZ=Mathf.Max(maxZ,point.z);
                    }
                } finally {UnityEngine.Random.state=randomState;}
                Check(maxX-minX>3 && maxZ-minZ>3,"Patrol varies on both horizontal axes");
                checks.Add("Pursuit, platform leap and free patrol regressions passed. Symmetric platform jump regression passed.");
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Ennemy/Piranha/Piranha_Pret/Piranha_Ennemi.prefab");
                Check(prefab!=null,"Prefab created");Check(prefab.GetComponent<EnemyHealth>().Explosion!=null,"Death effect assigned");
                var hit=prefab.transform.Find("ContactSonic");Check(hit.CompareTag("Enemy")&&hit.GetComponent<SphereCollider>().isTrigger,"Existing Sonic combat integration");
                Check(prefab.GetComponent<Animator>().runtimeAnimatorController.animationClips.Length==3,"Three animations");
                foreach(var material in prefab.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterials)Check(material!=null && !ShaderUtil.ShaderHasError(material.shader),"Material shader valid");
                checks.Add("Prefab references, enemy contact, animations and material shaders valid.");
                File.WriteAllText(Path.Combine(PiranhaBuilder.ReportFolder,"unity-tests.txt"),"PASS\n"+string.Join("\n",checks)+"\nScene Play test remains necessary for actual Sonic contacts.");
                Debug.Log("Piranha : verifications reussies.");
            }
            catch(Exception e){File.WriteAllText(Path.Combine(PiranhaBuilder.ReportFolder,"unity-tests.txt"),"FAIL\n"+string.Join("\n",checks)+"\n"+e);Debug.LogException(e);}
            finally
            {
                for(int i=objects.Count-1;i>=0;i--)if(objects[i]!=null)UnityEngine.Object.DestroyImmediate(objects[i]);
                SonicWaterVolume.Active.Clear();SonicWaterVolume.Active.AddRange(active);
            }
        }
    }
}
