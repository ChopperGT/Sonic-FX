using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SonicFX.Score.Editor
{
    [CustomEditor(typeof(SonicLevelMedals))]
    public sealed class SonicLevelMedalsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using(new EditorGUI.DisabledScope(true))EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("RainbowSeconds"),new GUIContent("Arc-en-ciel (secondes)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("DiamondSeconds"),new GUIContent("Diamant (secondes)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("GoldSeconds"),new GUIContent("Or (secondes)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("SilverSeconds"),new GUIContent("Argent (secondes)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("BronzeSeconds"),new GUIContent("Bronze (secondes)"));
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("Ces temps s'appliquent a cette map, pour tous les personnages. Le temps du joueur doit etre inferieur ou egal au seuil. Au-dela du temps Bronze, aucune medaille. 60 secondes = 1 minute. Enregistre la scene apres modification.",MessageType.Info);
            var times=((SonicLevelMedals)target).GetTimes();
            var names=new[]{"Arc-en-ciel","Diamant","Or","Argent","Bronze"};
            for(int i=0;i<times.Length;i++)EditorGUILayout.LabelField(names[i],Math.Floor((double)times[i]/60).ToString("0")+":"+Math.Floor(times[i]%60).ToString("00"));
        }
        [MenuItem("Tools/Sonic FX/Niveau/Ajouter les reglages des medailles")]
        static void AddToCurrentLevel()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage()!=null){Debug.LogWarning("Quitte Play et le mode Prefab pour ajouter les reglages a la map.");return;}
            var scene=SceneManager.GetActiveScene();if(!scene.IsValid() || !scene.isLoaded)return;
            Selection.activeGameObject=CreateForScene(scene,true).gameObject;
        }
        internal static SonicLevelMedals CreateForScene(Scene scene,bool recordUndo)
        {
            // Include inactive settings to avoid making a duplicate when a map's
            // configuration object has temporarily been disabled in the editor.
            LevelProgressControl progress=null;
            foreach(var root in scene.GetRootGameObjects()){
                var existing=root.GetComponentInChildren<SonicLevelMedals>(true);if(existing!=null)return existing;
                foreach(var candidate in root.GetComponentsInChildren<LevelProgressControl>())if(candidate.isActiveAndEnabled && progress==null)progress=candidate;
            }
            var go=new GameObject("Reglages des medailles");SceneManager.MoveGameObjectToScene(go,scene);
            if(recordUndo)Undo.RegisterCreatedObjectUndo(go,"Ajouter les reglages des medailles");
            var settings=recordUndo?Undo.AddComponent<SonicLevelMedals>(go):go.AddComponent<SonicLevelMedals>();
            if(progress!=null)settings.SetTimes(SonicLevelScore.MedalTimes(progress.MedalTimesSeconds,progress.IdealTimeSeconds));
            if(recordUndo)EditorUtility.SetDirty(settings);
            return settings;
        }
    }
}
