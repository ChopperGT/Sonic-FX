using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SonicFX.Audio;
using SonicFX.RedRings;

[InitializeOnLoad] internal static class RedRingBuilder
{
    internal const string Source="Assets/Object/Rings Rouge.prefab";
    internal const string Route="Assets/Object/RedRings/Defi_Rings_Rouges.prefab";
    static RedRingBuilder(){EditorApplication.update+=Ready;}
    static void Ready()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        EditorApplication.update-=Ready;
        if(AssetDatabase.LoadAssetAtPath<GameObject>(Route)==null)Install();
    }
    [MenuItem("Tools/Sonic FX/Rings rouges/Installer le parcours")]
    static void Install()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        GameObject contents=null,route=null;
        try
        {
            contents=PrefabUtility.LoadPrefabContents(Source);
            if(contents==null)throw new Exception("Prefab Rings Rouge introuvable.");
            contents.name="Rings Rouge";contents.tag="Untagged";
            foreach(var rotation in contents.GetComponentsInChildren<RotateRing>(true))UnityEngine.Object.DestroyImmediate(rotation);
            foreach(var collider in contents.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(collider);
            var ring=contents.GetComponent<RedStarRing>();if(ring==null)ring=contents.AddComponent<RedStarRing>();
            var pickup=contents.AddComponent<SphereCollider>();pickup.isTrigger=true;
            var visuals=contents.GetComponentsInChildren<Renderer>(true);
            if(visuals.Length>0)
            {
                Bounds bounds=visuals[0].bounds;foreach(var visual in visuals)bounds.Encapsulate(visual.bounds);
                pickup.center=contents.transform.InverseTransformPoint(bounds.center);
                ring.pickupRadius=Mathf.Max(.8f,bounds.extents.magnitude*.8f);
                pickup.radius=ring.pickupRadius/Mathf.Max(.001f,Mathf.Abs(contents.transform.lossyScale.x));
            }
            PrefabUtility.SaveAsPrefabAsset(contents,Source);PrefabUtility.UnloadPrefabContents(contents);contents=null;
            route=new GameObject("Defi_Rings_Rouges");var challenge=route.AddComponent<RedRingChallenge>();challenge.rings=new RedStarRing[5];
            var config=Resources.Load<SonicExtraLifeSoundSettings>("SonicExtraLifeSound");if(config!=null)challenge.successMixer=config.mixerGroup;
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            for(int i=0;i<5;i++)
            {
                var go=(GameObject)PrefabUtility.InstantiatePrefab(asset,route.transform);
                go.name="Ring Rouge "+(i+1)+(i==0?" - DEPART":"");go.transform.localPosition=new Vector3(i*8,1.5f,0);
                go.transform.localRotation=Quaternion.identity;var item=go.GetComponent<RedStarRing>();item.order=i+1;challenge.rings[i]=item;
            }
            PrefabUtility.SaveAsPrefabAsset(route,Route);AssetDatabase.SaveAssets();
            Debug.Log("Rings rouges installes : Assets/Object/RedRings/Defi_Rings_Rouges. Temps et son sur le parent; positions sur les enfants.");
        }
        catch(Exception e){Debug.LogException(e);}
        finally{if(contents!=null)PrefabUtility.UnloadPrefabContents(contents);if(route!=null)UnityEngine.Object.DestroyImmediate(route);}
    }
}

