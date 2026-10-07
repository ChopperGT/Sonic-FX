using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace SonicFX.Structures
{
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("Sonic FX/Structures/Spring large editable")]
    public sealed class SonicWideSpring : MonoBehaviour
    {
        public const int MaximumSlots = 32;
        [Min(1)] public float width = 11.672838f;
        [Min(1)] public float springForce = 100;
        public bool isAdditive;
        public bool lockControl;
        [Min(0)] public float lockTime = 60;
        [Min(.01f)] public float activationCooldown = .1f;
        [Min(.05f)] public float animationDuration = .45f;
        [Range(0,.4f)] public float compression = .2f;
        [HideInInspector] public Mesh source, leftPiece, middlePiece, rightPiece, singlePiece;
        [HideInInspector] public GameObject slotPrefab;
        [HideInInspector] public MeshFilter visual;
        [HideInInspector] public Transform slotContainer;
        [SerializeField, HideInInspector] Transform[] slots;
        public const float Pitch = 3.5296575f;
        public const float EndMargin = 1.0838655f;
        Mesh generated;
        int builtCount;
        bool dirty = true;
        float pulseTime = -1;
        readonly Dictionary<Objects_Interaction, float> lastActivation = new Dictionary<Objects_Interaction, float>();
        public int SlotCount => Mathf.Clamp(Mathf.RoundToInt((width * AxisScale - EndMargin) / Pitch),1,MaximumSlots);
        public float ActualWidth => SlotCount * Pitch + EndMargin;
        public float AxisScale => Mathf.Max(.0001f,transform.TransformVector(Vector3.right).magnitude);
        public int PulseCount { get; private set; }
        public Transform[] Slots => slots;

        void OnEnable(){dirty=true;Refresh();}
        void OnValidate(){width=Mathf.Max(1,width);springForce=Mathf.Max(1,springForce);lockTime=Mathf.Max(0,lockTime);activationCooldown=Mathf.Max(.01f,activationCooldown);animationDuration=Mathf.Max(.05f,animationDuration);dirty=true;}
        void OnDisable(){Release();lastActivation.Clear();}
        void OnDestroy(){Release();}
        void Update()
        {
            if(!gameObject.scene.IsValid() || source==null || visual==null || slotContainer==null || slotPrefab==null)return;
            float inverse=1/AxisScale;
            if(dirty || builtCount!=SlotCount || Mathf.Abs(visual.transform.localScale.x-inverse)>.0001f)Refresh();
            if(Application.IsPlaying(gameObject))TickAnimation(Time.deltaTime);
        }

        public void Refresh(bool recordUndo=false)
        {
            if(!gameObject.scene.IsValid() || source==null || visual==null || slotContainer==null || slotPrefab==null)return;
            dirty=false;int count=SlotCount;
            if(generated==null || builtCount!=count)
            {
                var next=SonicWideSpringGeometry.Assemble(leftPiece,middlePiece,rightPiece,singlePiece,count);
                next.name="Spring large "+count+" emplacements";next.hideFlags=HideFlags.DontSave;
                Release();generated=next;visual.sharedMesh=generated;builtCount=count;
            }
            var pool=new List<Transform>(slots??new Transform[0]);pool.RemoveAll(t=>t==null);
            while(pool.Count<count)
            {
                var go=Instantiate(slotPrefab,slotContainer,false);go.name="Emplacement_"+(pool.Count+1);
#if UNITY_EDITOR
                if(recordUndo && !Application.IsPlaying(gameObject))UnityEditor.Undo.RegisterCreatedObjectUndo(go,"Ajouter un emplacement de Spring");
#endif
                pool.Add(go.transform);
            }
            slots=pool.ToArray();float inverse=1/AxisScale;
            visual.transform.localScale=new Vector3(inverse,1,1);slotContainer.localScale=new Vector3(inverse,1,1);
            for(int i=0;i<slots.Length;i++)
            {
                var slot=slots[i];bool active=i<count;
                slot.gameObject.SetActive(active);var collider=slot.GetComponent<Collider>();collider.enabled=active;
                if(!active)continue;
                slot.localPosition=new Vector3((i-(count-1)*.5f)*Pitch,0,0);slot.localRotation=Quaternion.identity;slot.localScale=Vector3.one;
                var prop=slot.GetComponent<Spring_Proprieties>();prop.SpringForce=springForce;prop.IsAdditive=isAdditive;prop.LockControl=lockControl;prop.LockTime=lockTime;prop.anim=slot.GetComponent<Animator>();
                slot.GetComponent<SonicWideSpringSlot>().owner=this;
            }
#if UNITY_EDITOR
            if(!Application.IsPlaying(gameObject))
            {
                UnityEditor.EditorUtility.SetDirty(this);
                if(UnityEditor.PrefabUtility.IsPartOfPrefabInstance(this))
                {
                    UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
                    UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(visual.transform);
                    UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(slotContainer);
                    foreach(var slot in slots)
                    {
                        UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(slot);
                        UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(slot.gameObject);
                        UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(slot.GetComponent<Collider>());
                        UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(slot.GetComponent<Spring_Proprieties>());
                    }
                }
            }
#endif
        }
        // The player still executes its original Spring interaction, once per contact.
        public bool TryActivate(Objects_Interaction player)
        {
            float now=Time.time;
            if(lastActivation.TryGetValue(player,out float last) && now-last<activationCooldown)return false;
            lastActivation[player]=now;return true;
        }
        public void ClearActivationHistory(){lastActivation.Clear();}
        public void Pulse(){pulseTime=0;PulseCount++;TickAnimation(0);}
        public void TickAnimation(float delta)
        {
            if(visual==null)return;
            float stretch=1;
            if(pulseTime>=0)
            {
                pulseTime+=Mathf.Max(0,delta);float t=pulseTime/animationDuration;
                if(t<.2f)stretch=Mathf.Lerp(1,1-compression,t/.2f);
                else if(t<.5f)stretch=Mathf.Lerp(1-compression,1+compression*.7f,(t-.2f)/.3f);
                else if(t<1)stretch=Mathf.Lerp(1+compression*.7f,1,(t-.5f)/.5f);
                else pulseTime=-1;
            }
            visual.transform.localScale=new Vector3(1/AxisScale,stretch,1);
        }
        void Release()
        {
            if(generated==null)return;
            if(visual!=null && visual.sharedMesh==generated)visual.sharedMesh=source;
            if(Application.isPlaying)Destroy(generated);else DestroyImmediate(generated);generated=null;
        }
    }
}
