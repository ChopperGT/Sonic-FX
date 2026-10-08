using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// Fit the visible soles to the existing capsule; gameplay colliders stay unchanged.
public static class CharacterGroundFit
{
    static readonly HashSet<string> GroundClips=new HashSet<string>{"Idle","IdleTap","IdleArmCross","Walk","Jog","Run","Run_L","Run_R","MachRun","Skid"};
    public static void Apply(PlayerBhysics player,Animator animator,Transform model,string hipsName)
    {
        var shoes=model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.name.StartsWith("Blaze.008_Blaze.004")||r.name.StartsWith("SonicClassic.001")).ToArray();
        if(shoes.Length==0)throw new Exception("Semelles absentes du modele "+model.name);
        var hips=model.GetComponentsInChildren<Transform>(true).First(t=>t.name==hipsName);
        var poses=model.GetComponentsInChildren<Transform>(true).Select(t=>new Pose(t)).ToArray();
        var capsule=player.CollisionCapsule as CapsuleCollider;
        if(!capsule||capsule.direction!=1)throw new Exception("Capsule verticale requise pour adapter les semelles");
        float floor=capsule.transform.TransformPoint(capsule.center-Vector3.up*capsule.height*.5f).y+.025f;
        var clips=animator.runtimeAnimatorController.animationClips.Distinct().Where(c=>GroundClips.Contains(c.name)).ToArray();
        var idle=clips.First(c=>c.name=="Idle");idle.SampleAnimation(animator.gameObject,0);
        float offset=floor-shoes.Min(s=>CharacterFitDiagnostic.Bounds(s).min.y);
        foreach(var pose in poses)pose.Restore();model.position+=Vector3.up*offset;
        poses=model.GetComponentsInChildren<Transform>(true).Select(t=>new Pose(t)).ToArray();
        foreach(var clip in clips)
        {
            var curves=new[]{new AnimationCurve(),new AnimationCurve(),new AnimationCurve()};
            int frames=Mathf.Max(1,Mathf.CeilToInt(clip.length*120));
            for(int frame=0;frame<=frames;frame++)
            {
                foreach(var pose in poses)pose.Restore();float time=clip.length*frame/frames;clip.SampleAnimation(animator.gameObject,time);
                float lift=Mathf.Max(0,floor-shoes.Min(s=>CharacterFitDiagnostic.Bounds(s).min.y));
                Vector3 position=hips.localPosition+hips.parent.InverseTransformVector(Vector3.up*lift);
                curves[0].AddKey(time,position.x);curves[1].AddKey(time,position.y);curves[2].AddKey(time,position.z);
            }
            string path=AnimationUtility.CalculateTransformPath(hips,animator.transform);
            for(int axis=0;axis<3;axis++){for(int k=0;k<curves[axis].length;k++){AnimationUtility.SetKeyLeftTangentMode(curves[axis],k,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curves[axis],k,AnimationUtility.TangentMode.Linear);}AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),"m_LocalPosition."+"xyz"[axis]),curves[axis]);}
            EditorUtility.SetDirty(clip);
            float minimum=float.PositiveInfinity;for(int f=0;f<=frames;f++){foreach(var pose in poses)pose.Restore();clip.SampleAnimation(animator.gameObject,clip.length*f/frames);minimum=Mathf.Min(minimum,shoes.Min(s=>CharacterFitDiagnostic.Bounds(s).min.y));}
            System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"SonicFXCharacterFit","fit-build.txt"),model.name+" "+clip.name+" margin="+(minimum-floor)+"\n");
        }
        foreach(var pose in poses)pose.Restore();
    }
    sealed class Pose{readonly Transform t;readonly Vector3 p,s;readonly Quaternion q;public Pose(Transform t){this.t=t;p=t.localPosition;s=t.localScale;q=t.localRotation;}public void Restore(){t.localPosition=p;t.localScale=s;t.localRotation=q;}}
}
