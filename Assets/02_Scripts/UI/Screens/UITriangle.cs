using UnityEngine;
using UnityEngine.UI;

namespace QL.UI.Screens
{
    /// <summary>
    /// U20 — ▶ · ↑ · ↓ 같은 작은 삼각형. Bagel Fat One 에 화살표 글리프가 없어 글자 대신 메시로 그린다 (사각형 안에 꽉 차게, 꼭짓점 = <see cref="direction"/>).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UITriangle : MaskableGraphic
    {
        public enum Direction { Right, Up, Down, Left }

        [SerializeField] private Direction direction = Direction.Right;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            Vector2 a, b, c;
            switch (direction)
            {
                case Direction.Up:
                    a = new Vector2(r.xMin, r.yMin); b = new Vector2(r.center.x, r.yMax); c = new Vector2(r.xMax, r.yMin); break;
                case Direction.Down:
                    a = new Vector2(r.xMin, r.yMax); b = new Vector2(r.xMax, r.yMax); c = new Vector2(r.center.x, r.yMin); break;
                case Direction.Left:
                    a = new Vector2(r.xMax, r.yMin); b = new Vector2(r.xMin, r.center.y); c = new Vector2(r.xMax, r.yMax); break;
                default:
                    a = new Vector2(r.xMin, r.yMin); b = new Vector2(r.xMin, r.yMax); c = new Vector2(r.xMax, r.center.y); break;
            }
            Color32 col = color;
            vh.AddVert(a, col, Vector2.zero);
            vh.AddVert(b, col, Vector2.zero);
            vh.AddVert(c, col, Vector2.zero);
            vh.AddTriangle(0, 1, 2);
        }
    }
}
