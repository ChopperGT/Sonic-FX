using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using SonicFX.RedRings;
using Object=UnityEngine.Object;

internal static class RedRingRouteEditing
{
    internal static void Changed(Object item)
    {
        EditorUtility.SetDirty(item);PrefabUtility.RecordPrefabInstancePropertyModifications(item);SceneView.RepaintAll();
    }
    internal static void SetRingCount(RedRingChallenge route,int count)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorUtility.IsPersistent(route))return;
        count=Mathf.Clamp(count,1,1000);
        Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Nombre de rings rouges");
        Undo.RegisterCompleteObjectUndo(route,"Nombre de rings rouges");
        var old=route.rings??Array.Empty<RedStarRing>();var items=new List<RedStarRing>();
        var owned=route.GetComponentsInChildren<RedStarRing>(true).Where(r=>r.GetComponentInParent<RedRingChallenge>()==route).ToArray();
        for(int i=0;i<count;i++)
        {
            var ring=i<old.Length?old[i]:null;
            if(ring==null||items.Contains(ring)||ring.gameObject.scene!=route.gameObject.scene)
                ring=owned.FirstOrDefault(r=>!old.Contains(r)&&!items.Contains(r));
            if(ring==null||items.Contains(ring)||ring.gameObject.scene!=route.gameObject.scene)
            {
                var last=items.Count>0?items[items.Count-1]:null;
                string assetPath=last!=null?PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(last.gameObject):RedRingBuilder.Source;
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if(asset==null||asset.GetComponent<RedStarRing>()==null)asset=AssetDatabase.LoadAssetAtPath<GameObject>(RedRingBuilder.Source);
                if(asset==null)throw new InvalidOperationException("Prefab Rings Rouge introuvable.");
                var go=(GameObject)PrefabUtility.InstantiatePrefab(asset,route.transform);Undo.RegisterCreatedObjectUndo(go,"Ajouter un ring rouge");Undo.RegisterFullObjectHierarchyUndo(go,"Configurer le nouveau ring rouge");
                ring=go.GetComponent<RedStarRing>();
                if(last!=null)
                {
                    EditorUtility.CopySerialized(last,ring);go.transform.localScale=last.transform.localScale;go.transform.rotation=last.transform.rotation;
                    go.transform.position=last.transform.position+route.transform.TransformVector(route.newRingOffset);
                }
                else{go.transform.localPosition=new Vector3(0,1.5f,0);go.transform.localRotation=Quaternion.identity;}
                ring.guidePoints=Array.Empty<Transform>();ring.manualStars=false;ring.editableStarPositions=Array.Empty<Vector3>();ring.lockedStarFollow=Array.Empty<bool>();ring.curveStars=false;ring.starCurvePoints=Array.Empty<Vector3>();ring.starCurveLocks=Array.Empty<bool>();
                go.name="Ring Rouge "+(i+1)+(i==0?" - DEPART":"");Changed(ring);Changed(go.transform);
            }
            items.Add(ring);
        }
        // Never delete externally assigned objects. Only remove rings owned by this route.
        foreach(var ring in old.Concat(owned).Where(r=>r!=null).Distinct())
            if(!items.Contains(ring)&&ring.transform.IsChildOf(route.transform))Undo.DestroyObjectImmediate(ring.gameObject);
        route.rings=items.ToArray();route.automaticSceneRoute=false;SyncOrder(route);Changed(route);Undo.CollapseUndoOperations(group);
    }
    internal static void SyncOrder(RedRingChallenge route)
    {
        for(int i=0;i<route.rings.Length;i++)if(route.rings[i]!=null)
        {Undo.RecordObject(route.rings[i],"Ordre des rings rouges");route.rings[i].order=i+1;Changed(route.rings[i]);}
    }
    internal static void MakeCurveEditable(Vector3 start,RedStarRing target,float spacing)
    {
        if(target.curveStars)return;
        var line=new List<Vector3>{start+Vector3.up*.25f};
        var mandatory=new HashSet<int>{0};
        var old=RedRingStarGuide.BuildPositions(start,target,spacing);
        for(int i=0;i<old.Length;i++)
        {
            if((old[i]-line[line.Count-1]).sqrMagnitude<.00001f)continue;
            line.Add(old[i]);
            if(target.manualStars&&target.lockedStarFollow!=null&&i<target.lockedStarFollow.Length&&target.lockedStarFollow[i])mandatory.Add(line.Count-1);
        }
        Vector3 end=target.WorldCenter+Vector3.up*.25f;
        if((end-line[line.Count-1]).sqrMagnitude>.00001f)line.Add(end);
        if(line.Count==1)line.Add(end);
        mandatory.Add(line.Count-1);
        // Simplify dense legacy stars while retaining bends and individually pinned positions.
        var keep=new SortedSet<int>(mandatory);var anchors=mandatory.OrderBy(i=>i).ToArray();
        for(int i=1;i<anchors.Length;i++)Simplify(line,anchors[i-1],anchors[i],Mathf.Max(.1f,spacing*.1f),keep);
        var points=keep.Where(i=>i>0&&i<line.Count-1).Select(i=>target.WorldToGuide(line[i])).ToList();
        var locks=keep.Where(i=>i>0&&i<line.Count-1).Select(i=>mandatory.Contains(i)).ToList();
        if(points.Count==0)
        {
            points.Add(target.WorldToGuide(Vector3.Lerp(line[0],end,1f/3)));points.Add(target.WorldToGuide(Vector3.Lerp(line[0],end,2f/3)));
            locks.Add(false);locks.Add(false);
        }
        Undo.RecordObject(target,"Creer le chemin courbe");target.starCurvePoints=points.ToArray();target.starCurveLocks=locks.ToArray();target.curveStars=true;Changed(target);
    }
    static void Simplify(List<Vector3> line,int first,int last,float tolerance,SortedSet<int> keep)
    {
        if(last-first<2)return;
        Vector3 a=line[first],direction=line[last]-a;float length=direction.sqrMagnitude,max=tolerance*tolerance;int selected=-1;
        for(int i=first+1;i<last;i++)
        {
            float t=length>.000001f?Mathf.Clamp01(Vector3.Dot(line[i]-a,direction)/length):0;
            float d=(line[i]-(a+direction*t)).sqrMagnitude;if(d>max){max=d;selected=i;}
        }
        if(selected<0)return;keep.Add(selected);Simplify(line,first,selected,tolerance,keep);Simplify(line,selected,last,tolerance,keep);
    }
    internal static bool IsCurvePointLocked(RedStarRing target,int index)=>target.starCurveLocks!=null&&index>=0&&index<target.starCurveLocks.Length&&target.starCurveLocks[index];
    internal static void SetCurvePointLocked(RedStarRing target,int index,bool value)
    {
        if(index<0||index>=target.starCurvePoints.Length)return;
        Undo.RecordObject(target,"Verrouiller le point du chemin");var locks=new bool[target.starCurvePoints.Length];
        if(target.starCurveLocks!=null)Array.Copy(target.starCurveLocks,locks,Math.Min(locks.Length,target.starCurveLocks.Length));
        locks[index]=value;target.starCurveLocks=locks;Changed(target);
    }
    internal static void MoveCurvePoint(RedStarRing target,int index,Vector3 world)
    {
        if(index<0||index>=target.starCurvePoints.Length||IsCurvePointLocked(target,index))return;
        Undo.RecordObject(target,"Courber le chemin d'etoiles");target.starCurvePoints[index]=target.WorldToGuide(world);Changed(target);
    }
    internal static void InsertCurvePoint(Vector3 start,RedStarRing target,int after)
    {
        var points=target.starCurvePoints.ToList();var locks=Enumerable.Range(0,points.Count).Select(i=>IsCurvePointLocked(target,i)).ToList();
        int index=Mathf.Clamp(after+1,0,points.Count);
        Vector3 a=index>0?target.GuideToWorld(points[index-1]):start+Vector3.up*.25f;
        Vector3 b=index<points.Count?target.GuideToWorld(points[index]):target.WorldCenter+Vector3.up*.25f;
        Undo.RecordObject(target,"Ajouter un point au chemin");points.Insert(index,target.WorldToGuide(Vector3.Lerp(a,b,.5f)));locks.Insert(index,false);
        target.starCurvePoints=points.ToArray();target.starCurveLocks=locks.ToArray();Changed(target);
    }
    internal static void RemoveCurvePoint(RedStarRing target,int index)
    {
        if(index<0||index>=target.starCurvePoints.Length||IsCurvePointLocked(target,index))return;
        var points=target.starCurvePoints.ToList();var locks=Enumerable.Range(0,points.Count).Select(i=>IsCurvePointLocked(target,i)).ToList();
        Undo.RecordObject(target,"Retirer un point du chemin");points.RemoveAt(index);locks.RemoveAt(index);
        target.starCurvePoints=points.ToArray();target.starCurveLocks=locks.ToArray();Changed(target);
    }
}

internal static class RedRingStarEditingUI
{
    internal static void Inspector(Vector3 start,RedStarRing target,float spacing,ref int selected)
    {
        using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode||EditorUtility.IsPersistent(target)))
        {
            EditorGUILayout.LabelField("Chemin courbe des etoiles",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Comme le tube : deplace les points bleus du chemin dans la Scene. La courbe se deforme autour du point et toutes les etoiles se repartissent dessus. Les rings restent les extremites du chemin.",MessageType.Info);
            if(!target.curveStars)
            {
                if(GUILayout.Button("Modifier le chemin comme le tube")){RedRingRouteEditing.MakeCurveEditable(start,target,spacing);selected=0;}
                return;
            }
            int count=target.starCurvePoints?.Length??0;
            EditorGUILayout.LabelField("Nombre de points de controle",count.ToString());
            EditorGUILayout.LabelField("Nombre d'etoiles (automatique)",RedRingStarGuide.BuildPositions(start,target,spacing).Length.ToString());
            if(count>0)
            {
                selected=Mathf.Clamp(EditorGUILayout.IntSlider("Point selectionne",selected+1,1,count)-1,0,count-1);
                EditorGUI.BeginChangeCheck();bool locked=EditorGUILayout.Toggle("Verrouiller ce point",RedRingRouteEditing.IsCurvePointLocked(target,selected));
                if(EditorGUI.EndChangeCheck())RedRingRouteEditing.SetCurvePointLocked(target,selected,locked);
                using(new EditorGUI.DisabledScope(locked))
                {
                    EditorGUI.BeginChangeCheck();Vector3 world=EditorGUILayout.Vector3Field("Position dans le monde",target.GuideToWorld(target.starCurvePoints[selected]));
                    if(EditorGUI.EndChangeCheck())RedRingRouteEditing.MoveCurvePoint(target,selected,world);
                }
            }
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("Ajouter un point")){RedRingRouteEditing.InsertCurvePoint(start,target,count>0?selected:-1);selected=Mathf.Min(selected+1,count);}
                using(new EditorGUI.DisabledScope(count==0||RedRingRouteEditing.IsCurvePointLocked(target,selected)))
                    if(GUILayout.Button("Supprimer le point")){RedRingRouteEditing.RemoveCurvePoint(target,selected);selected=Mathf.Max(0,selected-1);}
            }
            if(GUILayout.Button("Revenir au trace automatique"))
            {Undo.RecordObject(target,"Trace automatique");target.curveStars=false;target.manualStars=false;RedRingRouteEditing.Changed(target);}
        }
    }
    internal static void Scene(Vector3 start,RedStarRing target,float spacing,float size,Color color,ref int selected)
    {
        var stars=RedRingStarGuide.BuildPositions(start,target,spacing);
        Handles.color=new Color(color.r,color.g,color.b,.65f);
        var line=target.curveStars?RedRingStarGuide.BuildCurvePolyline(start,target):new[]{start}.Concat(stars).Concat(new[]{target.WorldCenter}).ToArray();
        Handles.DrawAAPolyLine(2,line);
        Quaternion facing=SceneView.currentDrawingSceneView!=null?SceneView.currentDrawingSceneView.camera.transform.rotation:Quaternion.identity;
        foreach(var point in stars)
        {
            var vertices=new Vector3[10];Handles.color=color;
            for(int v=0;v<10;v++){float angle=(90+v*36)*Mathf.Deg2Rad;vertices[v]=point+facing*new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0)*size*(v%2==0?1:.42f);}
            for(int v=0;v<10;v++)Handles.DrawAAConvexPolygon(point,vertices[v],vertices[(v+1)%10]);
        }
        if(!target.curveStars)return;
        int count=target.starCurvePoints?.Length??0;
        for(int i=0;i<count;i++)
        {
            Vector3 p=target.GuideToWorld(target.starCurvePoints[i]);bool locked=RedRingRouteEditing.IsCurvePointLocked(target,i);
            Handles.color=locked?new Color(1,.45f,.1f):new Color(.15f,.65f,1);
            float pick=HandleUtility.GetHandleSize(p)*.085f;
            if(Handles.Button(p,facing,pick,pick,Handles.RectangleHandleCap)){selected=i;GUI.changed=true;SceneView.RepaintAll();}
        }
        if(count==0)return;selected=Mathf.Clamp(selected,0,count-1);
        var current=target.GuideToWorld(target.starCurvePoints[selected]);bool isLocked=RedRingRouteEditing.IsCurvePointLocked(target,selected);
        Handles.Label(current+Vector3.up*.4f,"Point "+(selected+1)+(isLocked?" - verrouille":""));
        if(isLocked)return;
        EditorGUI.BeginChangeCheck();var moved=Handles.PositionHandle(current,Quaternion.identity);
        if(EditorGUI.EndChangeCheck())RedRingRouteEditing.MoveCurvePoint(target,selected,moved);
    }
}
[CustomEditor(typeof(RedRingChallenge))] internal sealed class RedRingChallengeEditor : Editor
{
    ReorderableList ringList;int segment,selectedStar;
    void OnEnable()
    {
        ringList=new ReorderableList(serializedObject,serializedObject.FindProperty("rings"),true,true,true,true);
        ringList.drawHeaderCallback=rect=>EditorGUI.LabelField(rect,"Ordre de ramassage");
        ringList.drawElementCallback=(rect,index,active,focused)=>EditorGUI.PropertyField(rect,ringList.serializedProperty.GetArrayElementAtIndex(index),new GUIContent("Ring "+(index+1)));
        ringList.onAddCallback=list=>Resize(list.serializedProperty.arraySize+1);
        ringList.onRemoveCallback=list=>Resize(Mathf.Max(1,list.serializedProperty.arraySize-1));
        ringList.onReorderCallback=list=>{serializedObject.ApplyModifiedProperties();RedRingRouteEditing.SyncOrder((RedRingChallenge)target);};
        EditorApplication.delayCall+=()=>
        {
            if(this==null||target==null||EditorUtility.IsPersistent(target)||EditorApplication.isPlayingOrWillChangePlaymode)return;
            var route=(RedRingChallenge)target;
            if(route.rings!=null&&route.rings.Length>0&&(route.rings.Any(r=>r==null)||route.rings.Distinct().Count()!=route.rings.Length))Resize(route.rings.Length);
        };
    }
    void Resize(int count)
    {
        serializedObject.ApplyModifiedProperties();RedRingRouteEditing.SetRingCount((RedRingChallenge)target,count);serializedObject.Update();
    }
    public override void OnInspectorGUI()
    {
        var route=(RedRingChallenge)target;serializedObject.Update();
        bool immutable=EditorUtility.IsPersistent(route)||EditorApplication.isPlayingOrWillChangePlaymode;
        if(EditorUtility.IsPersistent(route))EditorGUILayout.HelpBox("Pour creer des rings, ouvrir le prefab avec Open ou le glisser dans la Scene.",MessageType.Info);
        using(new EditorGUI.DisabledScope(immutable))
        {
            EditorGUI.BeginChangeCheck();int count=EditorGUILayout.IntField("Nombre de rings rouges",route.rings?.Length??0);
            if(EditorGUI.EndChangeCheck())Resize(count);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("newRingOffset"),new GUIContent("Decalage du nouveau ring"));
            ringList.DoLayoutList();
            if(GUILayout.Button("Reparer / synchroniser les rings"))Resize(route.rings?.Length??1);
            if(GUILayout.Button("Utiliser les rings enfants dans l'ordre de la Hierarchy"))
            {
                serializedObject.ApplyModifiedProperties();Undo.RecordObject(route,"Rings enfants");
                route.rings=route.GetComponentsInChildren<RedStarRing>(true).Where(r=>r.GetComponentInParent<RedRingChallenge>()==route).ToArray();route.automaticSceneRoute=false;
                RedRingRouteEditing.SyncOrder(route);RedRingRouteEditing.Changed(route);serializedObject.Update();
            }
        }
        DrawPropertiesExcluding(serializedObject,"m_Script","rings","newRingOffset");serializedObject.ApplyModifiedProperties();
        EditorGUILayout.Space();
        if(route.rings!=null&&route.rings.Length>1)
        {
            var names=Enumerable.Range(0,route.rings.Length-1).Select(i=>"Ring "+(i+1)+" vers Ring "+(i+2)).ToArray();
            int next=EditorGUILayout.Popup("Chemin d'etoiles a editer",Mathf.Clamp(segment,0,names.Length-1),names);
            if(next!=segment){segment=next;selectedStar=0;SceneView.RepaintAll();}
            segment=Mathf.Clamp(segment,0,names.Length-1);
            if(route.rings[segment]!=null&&route.rings[segment+1]!=null)
                RedRingStarEditingUI.Inspector(route.rings[segment].WorldCenter,route.rings[segment+1],route.starSpacing,ref selectedStar);
        }
    }
    void OnSceneGUI()
    {
        var route=(RedRingChallenge)target;if(route.rings==null||EditorApplication.isPlayingOrWillChangePlaymode)return;
        foreach(var ring in route.rings.Where(r=>r!=null))
        {
            Handles.color=new Color(1,.2f,.15f);Handles.Label(ring.WorldCenter+Vector3.up,"Ring "+(Array.IndexOf(route.rings,ring)+1));
            EditorGUI.BeginChangeCheck();var next=Handles.PositionHandle(ring.transform.position,Quaternion.identity);
            if(EditorGUI.EndChangeCheck()){Undo.RecordObject(ring.transform,"Deplacer le ring rouge");ring.transform.position=next;RedRingRouteEditing.Changed(ring.transform);}
        }
        segment=Mathf.Clamp(segment,0,Mathf.Max(0,route.rings.Length-2));
        if(route.rings.Length>1&&route.rings[segment]!=null&&route.rings[segment+1]!=null)
        {RedRingStarEditingUI.Scene(route.rings[segment].WorldCenter,route.rings[segment+1],route.starSpacing,route.starSize,route.starColor,ref selectedStar);if(GUI.changed)Repaint();}
    }
}

[CustomEditor(typeof(RedStarRing))] internal sealed class RedStarRingEditor : Editor
{
    int selectedStar;
    RedStarRing Previous(out RedRingChallenge route)
    {
        var ring=(RedStarRing)target;route=ring.GetComponentInParent<RedRingChallenge>();
        var rings=route!=null?route.rings:RedRingChallenge.OrderedSceneRings(ring.gameObject.scene).Where(r=>r.gameObject.activeInHierarchy).ToArray();
        int index=rings!=null?Array.IndexOf(rings,ring):-1;return index>0?rings[index-1]:null;
    }
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();var previous=Previous(out var route);var ring=(RedStarRing)target;
        if(previous!=null)RedRingStarEditingUI.Inspector(previous.WorldCenter,ring,route!=null?route.starSpacing:2,ref selectedStar);
        else EditorGUILayout.HelpBox("Le premier ring lance le defi. Pour editer le chemin vers le suivant, selectionner le deuxieme ring ou le parent du parcours.",MessageType.Info);
    }
    void OnSceneGUI()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var previous=Previous(out var route);if(previous==null)return;
        RedRingStarEditingUI.Scene(previous.WorldCenter,(RedStarRing)target,route!=null?route.starSpacing:2,route!=null?route.starSize:.35f,
            route!=null?route.starColor:new Color(1,.8f,.1f),ref selectedStar);if(GUI.changed)Repaint();
    }
}
