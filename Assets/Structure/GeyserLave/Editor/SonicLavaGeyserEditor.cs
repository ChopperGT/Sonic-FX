using UnityEditor;
using UnityEngine;
namespace SonicFX.Lava.Editor
{
    [CustomEditor(typeof(SonicLavaGeyser))]
    public sealed class SonicLavaGeyserEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();var g=(SonicLavaGeyser)target;
            if(g.LastRefusal!=null)EditorGUILayout.HelpBox(g.LastRefusal,MessageType.Warning);
            EditorGUILayout.HelpBox("Declenchement : redimensionner l'enfant Zone_Declenchement (Box Collider). Chutes : deplacer les Zones_Chute avec les poignees orange. Les rochers restent apres impact si leur duree vaut 0.",MessageType.Info);
            if(!g.gameObject.scene.IsValid()){EditorGUILayout.HelpBox("Glisser ce prefab dans la scene pour regler ses zones.",MessageType.Info);return;}
            SonicGeyserRockPreview.DrawToggle();
            DrawRockSizes(g);
            if(GUILayout.Button("Ajouter une zone de chute"))
            {
                Undo.RecordObject(g,"Ajouter une cible de geyser");var go=new GameObject("Zone_Chute_"+(g.landingZones.Length+1));Undo.RegisterCreatedObjectUndo(go,"Ajouter une cible de geyser");go.transform.SetParent(g.transform,false);go.transform.localPosition=new Vector3(8+g.landingZones.Length*3,0,0);
                var z=go.AddComponent<SonicGeyserLandingZone>();var list=new System.Collections.Generic.List<SonicGeyserLandingZone>(g.landingZones);list.Add(z);g.landingZones=list.ToArray();EditorUtility.SetDirty(g);PrefabUtility.RecordPrefabInstancePropertyModifications(g);Selection.activeGameObject=go;
            }
            if(GUILayout.Button("Aligner le geyser sur la lave"))
            {
                if(g.FindLava()){Undo.RecordObject(g.transform,"Aligner le geyser sur la lave");Vector3 p=g.lava.transform.InverseTransformPoint(g.transform.position);p.y=0;g.transform.position=g.lava.transform.TransformPoint(p);EditorUtility.SetDirty(g);PrefabUtility.RecordPrefabInstancePropertyModifications(g);PrefabUtility.RecordPrefabInstancePropertyModifications(g.transform);}
                else EditorUtility.DisplayDialog("Geyser","Aucune lave proche. Placer le geyser a moins de la distance de surface reglee dans l'Inspector.","OK");
            }
            using(new EditorGUI.DisabledScope(!Application.isPlaying))if(GUILayout.Button("Tester l'eruption"))if(!g.TryErupt())Debug.LogWarning(g.LastRefusal,g);
        }
        static void DrawRockSizes(SonicLavaGeyser geyser)
        {
            if(geyser.landingZones==null || geyser.landingZones.Length==0)return;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Taille des rochers par zone",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("0,5 = moitie de la taille ; 1 = taille du modele ; 2 = double. Deux valeurs identiques donnent une taille fixe. Sinon, chaque rocher prend une taille aleatoire entre les deux.",MessageType.Info);
            foreach(var zone in geyser.landingZones)
            {
                if(zone==null)continue;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(zone.name,EditorStyles.boldLabel);
                SonicGeyserRockPreview.DrawModelSelector(zone,geyser);
                var settings=new SerializedObject(zone);settings.Update();
                var minimum=settings.FindProperty("minimumScale");
                var maximum=settings.FindProperty("maximumScale");
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(minimum,new GUIContent("Taille minimale"));
                EditorGUILayout.PropertyField(maximum,new GUIContent("Taille maximale"));
                bool changed=EditorGUI.EndChangeCheck();
                if(GUILayout.Button("Taille fixe (utiliser la minimale)")){maximum.floatValue=minimum.floatValue;changed=true;}
                if(changed)
                {
                    minimum.floatValue=Mathf.Max(.1f,minimum.floatValue);
                    maximum.floatValue=Mathf.Max(minimum.floatValue,maximum.floatValue);
                    settings.ApplyModifiedProperties();
                    SceneView.RepaintAll();
                }
                EditorGUILayout.EndVertical();
            }
        }
        void OnSceneGUI()
        {
            var g=(SonicLavaGeyser)target;if(g.landingZones==null)return;
            foreach(var z in g.landingZones)if(z!=null){Handles.color=new Color(1,.35f,.02f);Handles.Label(z.transform.position+Vector3.up,z.name);EditorGUI.BeginChangeCheck();var p=Handles.PositionHandle(z.transform.position,Quaternion.identity);if(EditorGUI.EndChangeCheck()){Undo.RecordObject(z.transform,"Deplacer une chute de geyser");z.transform.position=p;PrefabUtility.RecordPrefabInstancePropertyModifications(z.transform);}Handles.DrawDottedLine(g.transform.position,z.transform.position,5);}
        }
    }
    [CustomEditor(typeof(SonicGeyserLandingZone))]
    public sealed class SonicGeyserLandingZoneEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI(){DrawDefaultInspector();SonicGeyserRockPreview.DrawToggle();var zone=(SonicGeyserLandingZone)target;SonicGeyserRockPreview.DrawModelSelector(zone,SonicGeyserRockPreview.FindGeyser(zone));EditorGUILayout.HelpBox("Rayon = dispersion des rochers dans cette zone. Taille minimale/maximale = taille des rochers. Un rayon de 0 vise exactement le centre. Decocher la projection pour imposer manuellement la hauteur d'impact.",MessageType.Info);if(GUI.changed)SceneView.RepaintAll();}
        void OnSceneGUI()
        {
            var z=(SonicGeyserLandingZone)target;Handles.color=new Color(1,.3f,.03f);Handles.DrawWireDisc(z.transform.position,z.transform.up,z.radius*Mathf.Max(z.transform.lossyScale.x,z.transform.lossyScale.z));
            EditorGUI.BeginChangeCheck();var matrix=Handles.matrix;Handles.matrix=z.transform.localToWorldMatrix;float r=Handles.RadiusHandle(Quaternion.identity,Vector3.zero,z.radius);Handles.matrix=matrix;
            if(EditorGUI.EndChangeCheck()){Undo.RecordObject(z,"Taille zone de chute");z.radius=Mathf.Max(0,r);EditorUtility.SetDirty(z);PrefabUtility.RecordPrefabInstancePropertyModifications(z);}
        }
    }
    // Scene-only previews: no spawned objects, colliders or changes to the level.
    static class SonicGeyserRockPreview
    {
        const string Preference="SonicFX.Geyser.ShowRockSizePreview";
        static readonly System.Collections.Generic.Dictionary<SonicGeyserLandingZone,int> models=new System.Collections.Generic.Dictionary<SonicGeyserLandingZone,int>();
        static readonly Color SmallColor=new Color(.15f,.85f,1,.8f);
        static readonly Color LargeColor=new Color(1,.55f,.1f,.9f);
        public static void DrawToggle()
        {
            bool current=EditorPrefs.GetBool(Preference,true);
            bool next=EditorGUILayout.Toggle("Afficher l'apercu des rochers",current);
            if(next!=current){EditorPrefs.SetBool(Preference,next);SceneView.RepaintAll();}
            if(next)EditorGUILayout.HelpBox("Dans la vue Scene : cyan = taille minimale, orange = taille maximale. L'apercu suit le sol au centre de chaque zone. Il est masque pendant le jeu.",MessageType.Info);
        }
        public static SonicLavaGeyser FindGeyser(SonicGeyserLandingZone zone)
        {
            var parent=zone.GetComponentInParent<SonicLavaGeyser>();if(parent!=null)return parent;
            foreach(var geyser in Object.FindObjectsByType<SonicLavaGeyser>())
                if(geyser.gameObject.scene==zone.gameObject.scene && geyser.landingZones!=null && System.Array.IndexOf(geyser.landingZones,zone)>=0)return geyser;
            return null;
        }
        static GameObject[] Choices(SonicLavaGeyser geyser)
        {
            var choices=new System.Collections.Generic.List<GameObject>();
            if(geyser!=null && geyser.rockPrefabs!=null)foreach(var prefab in geyser.rockPrefabs)if(prefab!=null)choices.Add(prefab);
            return choices.ToArray();
        }
        public static void DrawModelSelector(SonicGeyserLandingZone zone,SonicLavaGeyser geyser)
        {
            if(!EditorPrefs.GetBool(Preference,true))return;
            if(zone.rockPrefab!=null){EditorGUILayout.LabelField("Modele de l'apercu",zone.rockPrefab.name);return;}
            var choices=Choices(geyser);if(choices.Length==0)return;
            var names=new string[choices.Length];for(int i=0;i<names.Length;i++)names[i]=choices[i].name;
            models.TryGetValue(zone,out int selected);selected=Mathf.Clamp(selected,0,names.Length-1);
            int next=EditorGUILayout.Popup(new GUIContent("Modele de l'apercu","Change seulement l'apercu. Les modeles du jeu restent aleatoires."),selected,names);
            if(next!=selected){models[zone]=next;SceneView.RepaintAll();}
        }
        [DrawGizmo(GizmoType.Selected)]
        static void DrawGeyser(SonicLavaGeyser geyser,GizmoType gizmoType)
        {
            if(!Enabled() || geyser.landingZones==null)return;Physics.SyncTransforms();
            foreach(var zone in geyser.landingZones)if(zone!=null && zone.isActiveAndEnabled)Draw(zone,geyser);
        }
        [DrawGizmo(GizmoType.Selected)]
        static void DrawZone(SonicGeyserLandingZone zone,GizmoType gizmoType)
        {
            if(!Enabled() || !zone.isActiveAndEnabled)return;Physics.SyncTransforms();Draw(zone,FindGeyser(zone));
        }
        static bool Enabled(){return !Application.isPlaying && EditorPrefs.GetBool(Preference,true);}
        static void Draw(SonicGeyserLandingZone zone,SonicLavaGeyser geyser)
        {
            var prefab=zone.rockPrefab;
            if(prefab==null)
            {
                var choices=Choices(geyser);if(choices.Length==0)return;
                models.TryGetValue(zone,out int selected);prefab=choices[Mathf.Clamp(selected,0,choices.Length-1)];
            }
            var point=zone.transform.position;bool hasGround=zone.ResolveGround(point,out var ground,out var normal);
            if(!hasGround){ground=point;normal=Vector3.up;}
            var rotation=Quaternion.FromToRotation(Vector3.up,normal);
            float minimum=Mathf.Max(.1f,zone.minimumScale),maximum=Mathf.Max(minimum,zone.maximumScale);
            var matrix=Gizmos.matrix;var color=Gizmos.color;
            try
            {
                foreach(var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    if(filter.sharedMesh==null || !filter.gameObject.activeSelf)continue;
                    var relative=prefab.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                    Gizmos.matrix=Matrix4x4.TRS(ground,rotation,prefab.transform.localScale*maximum)*relative;
                    Gizmos.color=new Color(LargeColor.r,LargeColor.g,LargeColor.b,.12f);Gizmos.DrawMesh(filter.sharedMesh);
                    Gizmos.color=LargeColor;Gizmos.DrawWireMesh(filter.sharedMesh);
                    if(maximum-minimum>.001f)
                    {
                        Gizmos.matrix=Matrix4x4.TRS(ground,rotation,prefab.transform.localScale*minimum)*relative;
                        Gizmos.color=SmallColor;Gizmos.DrawWireMesh(filter.sharedMesh);
                    }
                }
            }
            finally{Gizmos.matrix=matrix;Gizmos.color=color;}
            string size=maximum-minimum>.001f?"min x"+minimum.ToString("0.##")+" / max x"+maximum.ToString("0.##"):"taille x"+maximum.ToString("0.##");
            Handles.Label(ground+Vector3.up*HandleUtility.GetHandleSize(ground)*.3f,zone.name+" : "+size+(hasGround?"":"\nSol non detecte : apercu a la hauteur de la zone"));
        }
    }
}
