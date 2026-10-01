using UnityEngine;
using UnityEngine.UI;
namespace SonicFX.HUD
{
    [RequireComponent(typeof(CanvasRenderer))]
    public class SonicHudPlate : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();float w=rectTransform.rect.width,h=rectTransform.rect.height;
            Color fill=new Color(.04f,.15f,.22f,.48f),edge=new Color(.36f,.72f,.79f,.65f);
            Polygon(vh,new[]{new Vector2(0,h-4),new Vector2(38,h-4),new Vector2(65,8),new Vector2(27,8)},fill,edge);
            Polygon(vh,new[]{new Vector2(47,h-4),new Vector2(55,h-4),new Vector2(84,8),new Vector2(76,8)},fill,edge);
            Polygon(vh,new[]{new Vector2(66,h-4),new Vector2(75,h-4),new Vector2(104,8),new Vector2(95,8)},fill,edge);
            Vector2[] p={new Vector2(86,h),new Vector2(149,h),new Vector2(173,h*.5f),new Vector2(w-24,h*.5f),new Vector2(w,2),new Vector2(131,2)};
            int start=vh.currentVertCount;foreach(var v in p)vh.AddVert(new Vector3(v.x,v.y),fill,Vector2.zero);
            // Triangle indices follow the concave stepped outline.
            vh.AddTriangle(start,start+1,start+2);vh.AddTriangle(start,start+2,start+5);vh.AddTriangle(start+2,start+4,start+5);vh.AddTriangle(start+2,start+3,start+4);
            for(int i=0;i<p.Length;i++)Line(vh,p[i],p[(i+1)%p.Length],edge,1.7f);
            // Respect the RectTransform pivot, including the HUD's top-left pivot.
            var vertex=UIVertex.simpleVert;Vector2 origin=rectTransform.rect.min;
            for(int i=0;i<vh.currentVertCount;i++){vh.PopulateUIVertex(ref vertex,i);vertex.position+=new Vector3(origin.x,origin.y);vh.SetUIVertex(vertex,i);}
        }
        static void Polygon(VertexHelper vh,Vector2[] points,Color fill,Color edge)
        {
            int start=vh.currentVertCount;foreach(var v in points)vh.AddVert(new Vector3(v.x,v.y),fill,Vector2.zero);
            for(int i=1;i<points.Length-1;i++)vh.AddTriangle(start,start+i,start+i+1);
            for(int i=0;i<points.Length;i++)Line(vh,points[i],points[(i+1)%points.Length],edge,1.4f);
        }
        static void Line(VertexHelper vh,Vector2 a,Vector2 b,Color color,float width)
        {
            Vector2 d=(b-a).normalized,n=new Vector2(-d.y,d.x)*width*.5f;int start=vh.currentVertCount;
            foreach(var v in new[]{a-n,a+n,b+n,b-n})vh.AddVert(new Vector3(v.x,v.y),color,Vector2.zero);
            vh.AddTriangle(start,start+1,start+2);vh.AddTriangle(start,start+2,start+3);
        }
    }
}
