using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace SonicFX.Structures.Editor
{
    [CustomEditor(typeof(SonicEditableRamp),true)]
    public sealed class SonicEditableRampEditor : UnityEditor.Editor
    {
        readonly HashSet<int> selected=new HashSet<int>();
        bool showPoints=true;
        int insertionAxis;
        float insertionPosition=.5f;
        bool automaticRowAxis;
        void OnEnable(){Undo.undoRedoPerformed+=OnUndo;automaticRowAxis=target is SonicEditableCube || target is SonicRoundPlatform;if(automaticRowAxis)UpdateRowAxis((SonicEditableRamp)target);}
        void OnDisable(){Undo.undoRedoPerformed-=OnUndo;}
        void OnUndo(){if(target!=null){selected.Clear();((SonicEditableRamp)target).Rebuild();Repaint();SceneView.RepaintAll();}}
        static void Changed(SonicEditableRamp r)
        {
            r.Rebuild();EditorUtility.SetDirty(r);
            if(PrefabUtility.IsPartOfPrefabInstance(r))PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            SceneView.RepaintAll();
        }
        Vector3 Center(SonicEditableRamp r){var p=Vector3.zero;foreach(int i in selected)p+=r.Points[i];return selected.Count==0?p:p/selected.Count;}
        void SelectLayer(int axis,int layer)
        {
            var r=(SonicEditableRamp)target;var counts=r.PointCounts;
            selected.Clear();for(int z=0;z<counts.z;z++)for(int y=0;y<counts.y;y++)for(int x=0;x<counts.x;x++)
                if((axis==0?x:axis==1?y:z)==layer)selected.Add(r.PointIndex(x,y,z));
            SceneView.RepaintAll();
        }
        void CleanSelection(SonicEditableRamp r){selected.RemoveWhere(i=>i<0 || r.Points==null || i>=r.Points.Length);}
        void SuggestPosition(SonicEditableRamp r,bool nearSelection=false)
        {
            int first=-1;foreach(int i in selected)if(first<0 || i<first)first=i;
            insertionPosition=r.SuggestedInsertion(insertionAxis,nearSelection?first:-1);
        }
        // A parallel row is inserted across the selected line, not along it.
        // Use grid coordinates: bending or rotating the structure must not change its topology.
        internal static int ParallelRowAxis(SonicEditableRamp r,IEnumerable<int> selection,Vector3 localView)
        {
            var counts=r.PointCounts;var min=counts;var max=new Vector3Int(-1,-1,-1);int valid=0;
            foreach(int i in selection){
                if(i<0 || i>=r.Points.Length)continue;
                var c=new Vector3Int(i%counts.x,(i/counts.x)%counts.y,i/(counts.x*counts.y));
                min=Vector3Int.Min(min,c);max=Vector3Int.Max(max,c);valid++;
            }
            int varying=0,line=-1,face=-1;
            for(int a=0;a<3;a++)if(valid>0 && min[a]!=max[a]){varying++;line=a;}
            if(varying==2){for(int a=0;a<3;a++)if(min[a]==max[a])face=a;}
            if(face<0){
                // A single point/corner uses the face seen in Scene. A selected line
                // constrains that face to one of the two axes perpendicular to it.
                float best=-1;
                for(int a=0;a<3;a++){
                    if(varying==1 && a==line)continue;
                    float facing=Mathf.Abs(localView[a]);
                    if(facing>best){best=facing;face=a;}
                }
            }
            if(varying==1){for(int a=0;a<3;a++)if(a!=line && a!=face)return a;}
            // Horizontal rows on a wall change height; on the top they change Z.
            return face==1?2:1;
        }
        void UpdateRowAxis(SonicEditableRamp r)
        {
            if(!automaticRowAxis || r.SourceMesh==null)return;
            var scene=SceneView.lastActiveSceneView;
            Vector3 view=scene!=null && scene.camera!=null?r.transform.InverseTransformDirection(scene.camera.transform.forward):Vector3.forward;
            insertionAxis=ParallelRowAxis(r,selected,view);
        }
        void SelectUserLayer(int axis,int layer)
        {
            SelectLayer(axis,layer);var r=(SonicEditableRamp)target;
            if(automaticRowAxis){UpdateRowAxis(r);SuggestPosition(r,true);}Repaint();
        }
        bool AddRow(SonicEditableRamp r)
        {
            if(!r.CanInsertPoints(insertionAxis,insertionPosition))return false;
            Undo.RecordObject(r,"Ajouter des points a la structure");
            if(!r.InsertPoints(insertionAxis,insertionPosition,out int layer))return false;
            // Keep the chosen direction after auto-selecting the new plane. Inferring
            // it again here would switch axes on every successive insertion.
            SelectLayer(insertionAxis,layer);showPoints=true;Changed(r);SuggestPosition(r);return true;
        }
        public override void OnInspectorGUI()
        {
            var r=(SonicEditableRamp)target;
            if(r.SourceMesh==null){EditorGUILayout.HelpBox("Utilise Tools > Sonic FX > Structures > Installer et verifier "+(r.StructureName=="Cube"?"Cube":"ramp_C")+" editable.",MessageType.Warning);return;}
            CleanSelection(r);var counts=r.PointCounts;
            EditorGUILayout.LabelField(r.StructureName+" editable",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Clique un point orange puis deplace les fleches X / Y / Z. Maj + clic ou Ctrl + clic selectionne plusieurs points. La grille deforme progressivement la structure, ses materiaux et ses collisions.",MessageType.Info);
            showPoints=EditorGUILayout.Toggle("Afficher les points",showPoints);
            SonicStructurePointHandles.DrawSizeSetting();
            EditorGUILayout.Space();EditorGUILayout.LabelField("Fluidite de la surface",EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            int subdivisions=EditorGUILayout.IntSlider("Subdivisions des polygones",r.meshSubdivisions,0,4);
            if(EditorGUI.EndChangeCheck()){Undo.RecordObject(r,"Lisser les polygones");r.meshSubdivisions=subdivisions;Changed(r);}
            EditorGUILayout.LabelField("Surface et collisions",r.TriangleCount.ToString("N0")+" triangles");
            EditorGUILayout.HelpBox("2 : lissage normal. 3 ou 4 : courbes plus precises pour une pente raide. 0 : polygones d'origine. Les collisions utilisent la meme surface. Les points de controle restent identiques.",MessageType.None);
            EditorGUILayout.Space();EditorGUILayout.LabelField("Ajouter des points",EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Grille : "+counts.x+" x "+counts.y+" x "+counts.z+" ("+r.Points.Length+" points)");
            if(r is SonicEditableCube || r is SonicRoundPlatform){
                EditorGUI.BeginChangeCheck();
                automaticRowAxis=EditorGUILayout.Toggle("Rangee parallele automatique",automaticRowAxis);
                if(EditorGUI.EndChangeCheck()){UpdateRowAxis(r);SuggestPosition(r,true);SceneView.RepaintAll();}
            }
            EditorGUI.BeginChangeCheck();
            insertionAxis=EditorGUILayout.Popup("Axe de la nouvelle rangee",insertionAxis,new[]{"X","Y","Z"});
            if(EditorGUI.EndChangeCheck()){automaticRowAxis=false;SuggestPosition(r);SceneView.RepaintAll();}
            insertionPosition=EditorGUILayout.Slider("Position dans la structure (%)",insertionPosition*100,1,99)/100;
            using(new EditorGUI.DisabledScope(!r.CanInsertPoints(insertionAxis,insertionPosition))){
                if(GUILayout.Button("Ajouter une rangee de points")){
                    AddRow(r);counts=r.PointCounts;
                }
            }
            if(GUILayout.Button("Placer l'ajout pres de la selection"))SuggestPosition(r,true);
            if(counts[insertionAxis]>=SonicEditableRamp.MaxPointsPerAxis)
                EditorGUILayout.HelpBox("Limite de 16 rangees atteinte sur cet axe. Tu peux encore ajouter sur les autres axes.",MessageType.None);
            else if(!r.CanInsertPoints(insertionAxis,insertionPosition))
                EditorGUILayout.HelpBox("Une rangee existe deja a cette position. Choisis une position voisine ou utilise le bouton de placement automatique.",MessageType.None);
            EditorGUILayout.HelpBox("Apres chaque ajout, la position suivante est proposee dans la zone la moins dense pour repartir les points. Tu peux choisir le pourcentage ou placer l'ajout pres de la selection. La forme actuelle est conservee. X / Y / Z sont les axes locaux.",MessageType.None);
            if(r is SonicEditableCube || r is SonicRoundPlatform)EditorGUILayout.HelpBox("Automatique : selectionne une ligne avec Maj/Ctrl + clic pour ajouter une rangee parallele. Avec un seul point, la face vue dans Scene determine la direction (Y sur un mur, Z sur le dessus). L'axe reste identique pour les ajouts suivants. Choisir X / Y / Z desactive l'automatisme.",MessageType.None);
            EditorGUILayout.Space();
            EditorGUI.BeginChangeCheck();
            float verticalScale=r.transform.TransformVector(Vector3.up).magnitude;
            float depth=Mathf.Max(0,EditorGUILayout.FloatField("Profondeur du mur (metres)",r.wallDepth*verticalScale));
            if(EditorGUI.EndChangeCheck() && verticalScale>.00001f){Undo.RecordObject(r,"Profondeur du mur");r.wallDepth=depth/verticalScale;Changed(r);}
            EditorGUILayout.HelpBox("La profondeur prolonge le dessous jusqu'a une base plate, sans changer le dessus. La poignee bleue permet de tirer le mur vers le bas. Zero restaure le dessous d'origine.",MessageType.None);
            EditorGUI.BeginChangeCheck();
            var scale=r.transform.lossyScale;scale=new Vector3(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z));
            var size=Vector3.Scale(r.ControlBounds.size,scale);
            size=EditorGUILayout.Vector3Field("Dimensions locales (metres)",size);
            if(EditorGUI.EndChangeCheck() && Mathf.Min(scale.x,Mathf.Min(scale.y,scale.z))>.00001f){
                Undo.RecordObject(r,"Redimensionner la structure");r.Resize(new Vector3(size.x/scale.x,size.y/scale.y,size.z/scale.z));Changed(r);
            }
            EditorGUILayout.LabelField("Selection rapide",EditorStyles.boldLabel);
            for(int axis=0;axis<3;axis++){
                EditorGUILayout.BeginHorizontal();string a=axis==0?"X":axis==1?"Y":"Z";
                for(int layer=0;layer<3;layer++)if(GUILayout.Button(a+(layer==0?" -":layer==1?" milieu":" +")))SelectUserLayer(axis,layer==0?0:layer==1?counts[axis]/2:counts[axis]-1);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.BeginHorizontal();
            if(GUILayout.Button("Tout selectionner")){selected.Clear();for(int i=0;i<r.Points.Length;i++)selected.Add(i);SceneView.RepaintAll();}
            if(GUILayout.Button("Deselectionner")){selected.Clear();SceneView.RepaintAll();}
            EditorGUILayout.EndHorizontal();
            if(selected.Count>0){
                EditorGUILayout.LabelField(selected.Count+" point(s) selectionne(s)",EditorStyles.boldLabel);
                Vector3 center=Center(r);EditorGUI.BeginChangeCheck();
                Vector3 moved=EditorGUILayout.Vector3Field("Centre local du groupe",center);
                if(EditorGUI.EndChangeCheck()){Undo.RecordObject(r,"Deplacer les points");r.MovePoints(selected,moved-center);Changed(r);}
            }
            if(GUILayout.Button("Retrouver la forme originale")){Undo.RecordObject(r,"Reinitialiser la structure");r.ResetShape();selected.Clear();insertionPosition=.5f;Changed(r);}
            if(!string.IsNullOrEmpty(r.LastError))EditorGUILayout.HelpBox(r.LastError,MessageType.Warning);
            EditorGUILayout.HelpBox("Y + selectionne le haut. Les tranches X / Z permettent d'etirer une extremite ou de courber les cotes. Ctrl + Z annule. Chaque exemplaire garde sa propre forme.",MessageType.None);
        }
        void OnSceneGUI()
        {
            var r=(SonicEditableRamp)target;if(!showPoints || r.Points==null)return;
            CleanSelection(r);var counts=r.PointCounts;
            Transform t=r.transform;Handles.color=new Color(0,1,1,.35f);
            var bounds=r.ControlBounds;
            Vector3 baseLocal=new Vector3(bounds.center.x,r.OriginalBottomY-r.wallDepth,bounds.center.z);
            Vector3 baseWorld=t.TransformPoint(baseLocal);
            Handles.color=new Color(.2f,.55f,1);Handles.Label(baseWorld,"Profondeur du mur");
            EditorGUI.BeginChangeCheck();
            Vector3 dragged=Handles.Slider(baseWorld,-t.up,HandleUtility.GetHandleSize(baseWorld)*.18f,Handles.CubeHandleCap,0);
            if(EditorGUI.EndChangeCheck()){
                Undo.RecordObject(r,"Etirer le mur");r.wallDepth=Mathf.Max(0,r.OriginalBottomY-t.InverseTransformPoint(dragged).y);Changed(r);
            }
            Handles.color=new Color(0,1,1,.35f);
            for(int z=0;z<counts.z;z++)for(int y=0;y<counts.y;y++)for(int x=0;x<counts.x;x++){
                Vector3 p=t.TransformPoint(r.Points[r.PointIndex(x,y,z)]);
                if(x<counts.x-1)Handles.DrawLine(p,t.TransformPoint(r.Points[r.PointIndex(x+1,y,z)]));
                if(y<counts.y-1)Handles.DrawLine(p,t.TransformPoint(r.Points[r.PointIndex(x,y+1,z)]));
                if(z<counts.z-1)Handles.DrawLine(p,t.TransformPoint(r.Points[r.PointIndex(x,y,z+1)]));
            }
            for(int i=0;i<r.Points.Length;i++){
                var p=t.TransformPoint(r.Points[i]);float s=HandleUtility.GetHandleSize(p)*.06f*SonicStructurePointHandles.SizeMultiplier;
                Handles.color=selected.Contains(i)?Color.yellow:new Color(1,.5f,.1f);
                bool toggle=Event.current.shift || Event.current.control || Event.current.command;
                if(Handles.Button(p,Quaternion.identity,s,s*1.3f,Handles.SphereHandleCap)){
                    if(!toggle)selected.Clear();if(!selected.Add(i))selected.Remove(i);UpdateRowAxis(r);SuggestPosition(r,automaticRowAxis);Repaint();
                }
            }
            if(selected.Count==0)return;
            Vector3 center=Center(r),world=t.TransformPoint(center);
            if(r is SonicEditableCube || r is SonicRoundPlatform){
                Handles.color=new Color(.3f,1,.4f);
                Vector3 direction=insertionAxis==0?t.right:insertionAxis==1?t.up:t.forward;
                float length=HandleUtility.GetHandleSize(world)*.55f;
                Handles.ArrowHandleCap(0,world,Quaternion.LookRotation(direction),length,EventType.Repaint);
                Handles.Label(world+direction*length,"Nouvelle rangee : "+(insertionAxis==0?"X":insertionAxis==1?"Y":"Z"));
            }
            EditorGUI.BeginChangeCheck();Vector3 moved=Handles.PositionHandle(world,Tools.pivotRotation==PivotRotation.Local?t.rotation:Quaternion.identity);
            if(EditorGUI.EndChangeCheck()){
                Undo.RecordObject(r,"Deplacer les points");r.MovePoints(selected,t.InverseTransformPoint(moved)-center);Changed(r);
            }
        }
    }
}
