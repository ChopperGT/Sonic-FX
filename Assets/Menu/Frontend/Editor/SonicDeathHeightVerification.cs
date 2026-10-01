using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad] public static class SonicDeathHeightVerification
{
    static readonly string Report=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/SonicDeathHeight10/unity-report.txt");
    static SonicDeathHeightVerification(){EditorApplication.update+=Ready;}
    static void Ready()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)return;
        EditorApplication.update-=Ready;if(File.Exists(Report))return;
        GameObject probe=null;
        try
        {
            probe=new GameObject("Fall death verification"){hideFlags=HideFlags.HideAndDontSave};probe.SetActive(false);
            var hurt=probe.AddComponent<HurtControl>();var check=typeof(HurtControl).GetMethod("CheckFallDeath",BindingFlags.Instance|BindingFlags.NonPublic);
            if(check==null)throw new Exception("Fall check missing");
            foreach(float height in new[]{10.001f,10f,9.999f,0f,-100f})
            {
                hurt.isDead=false;hurt.IsInvencible=true;probe.transform.position=new Vector3(50,height,60);check.Invoke(hurt,null);
                if(hurt.isDead!=(height<=10))throw new Exception("Unexpected death at height "+height);
            }
            hurt.isDead=true;probe.transform.position=Vector3.up*11;check.Invoke(hurt,null);if(!hurt.isDead)throw new Exception("Existing death was reset");
            File.WriteAllText(Report,"PASS: actual HurtControl fall check survives Y=10.001, kills at Y=10/9.999/0/-100 despite invincibility, and preserves existing death state.");
        }
        catch(Exception e){File.WriteAllText(Report,"FAIL\n"+e);Debug.LogException(e);}
        finally{if(probe!=null)UnityEngine.Object.DestroyImmediate(probe);}
    }
}
