using System.Collections.Generic;
using UnityEngine;

// Animation blending can lower a sole between the fitted poses. Correct only
// the visible rig, after animation, while the regular character stands on ground.
[DefaultExecutionOrder(20000)]
public sealed class CharacterGroundContact : MonoBehaviour
{
    public Transform ModelRoot;
    public SkinnedMeshRenderer[] Shoes;
    PlayerBhysics player;
    ActionManager actions;
    Vector3 restPosition;
    Mesh baked;
    readonly List<Vector3> vertices=new List<Vector3>();
    void Awake(){Initialize();}
    void Initialize(){if(player)return;player=GetComponent<PlayerBhysics>();actions=GetComponent<ActionManager>();if(ModelRoot)restPosition=ModelRoot.localPosition;baked=new Mesh{name="Character sole contact",hideFlags=HideFlags.HideAndDontSave};}
    void LateUpdate(){Apply();}
    public void Apply()
    {
        Initialize();if(!ModelRoot||!player)return;ModelRoot.localPosition=restPosition;
        if(!player.Grounded||!actions||actions.Action!=0||player.isRolling||Shoes==null||Shoes.Length==0)return;
        var capsule=player.CollisionCapsule as CapsuleCollider;if(!capsule)return;
        Vector3 up=capsule.transform.up;
        Vector3 floor=capsule.transform.TransformPoint(capsule.center-Vector3.up*capsule.height*.5f);
        float minimum=float.PositiveInfinity;
        foreach(var shoe in Shoes)
        {
            if(!shoe||!shoe.enabled||!shoe.gameObject.activeInHierarchy)continue;
            shoe.BakeMesh(baked);baked.GetVertices(vertices);
            foreach(var vertex in vertices)minimum=Mathf.Min(minimum,Vector3.Dot(shoe.transform.TransformPoint(vertex)-floor,up));
        }
        if(minimum<.015f)ModelRoot.position+=up*(.015f-minimum);
    }
    void OnDestroy(){if(!baked)return;if(Application.isPlaying)Destroy(baked);else DestroyImmediate(baked);}
}
