// 역할: 방향 안내용 삼각형 UI 메시를 그리는 Graphic.
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class DirectionArrow : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var r = rectTransform.rect;
        vh.AddVert(new Vector3(r.xMin, r.yMin), color, Vector2.zero);
        vh.AddVert(new Vector3(r.xMin, r.yMax), color, Vector2.up);
        vh.AddVert(new Vector3(r.xMax, r.center.y), color, Vector2.right);
        vh.AddTriangle(0, 1, 2);
    }
}
