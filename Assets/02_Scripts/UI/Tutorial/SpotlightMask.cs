using UnityEngine;
using UnityEngine.UI;

namespace QL.UI.Tutorial
{
    /// <summary>
    /// U21 3D 배경 어둠 — 자기 Canvas (overrideSorting, UIRoot 보다 낮은 순서) 에 깔려 3D 위 · UI 아래에 그려진다 → UI 는 전부 밝게, 3D 만 어둡게 (사람 수정 2026-11-13).
    /// 둥근 사각 구멍 최대 <see cref="MaxHoles"/> 개 (0 = 배치 발판 15칸 · 1 = 발판 밖 월드 대상 — 게이트 · 격리선). 겹치는 구멍은 하나로 합친다.
    /// 메시 = 구멍 y 경계로 나눈 가로 띠마다 구멍 밖 x 구간 사각형 + 구멍마다 네 모서리 부채 (모서리 꼭짓점 → 호). 구멍이 바뀔 때만 다시 만든다 (멈춘 프레임 재빌드 0).
    /// 클릭은 받지 않는다 (raycastTarget 끔 — UI 아래라 어차피 UI 버튼을 가리지 않고, 드래그 · Space 는 UI 레이캐스트를 쓰지 않음).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SpotlightMask : MaskableGraphic
    {
        public const int MaxHoles = 2;
        private const float Epsilon = 0.25f;

        private readonly Rect[] _holes = new Rect[MaxHoles];
        private readonly bool[] _has = new bool[MaxHoles];
        private readonly Rect[] _work = new Rect[MaxHoles];
        private readonly float[] _ys = new float[MaxHoles * 2 + 2];
        private float _radius;
        private int _segments = 6;

        public bool HasHole => _has[0] || _has[1];
        public bool HasHoleAt(int i) => _has[i];
        /// <summary>구멍 i (이 RectTransform 로컬 좌표).</summary>
        public Rect HoleAt(int i) => _holes[i];
        /// <summary>구멍 0 (발판).</summary>
        public Rect Hole => _holes[0];
        public float Radius => _radius;
        /// <summary>메시를 다시 만든 횟수 (테스트 — 멈춘 프레임엔 늘지 않음).</summary>
        public int RebuildCount { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        public void SetSegments(int segments)
        {
            segments = Mathf.Clamp(segments, 1, 32);
            if (segments == _segments) return;
            _segments = segments;
            SetVerticesDirty();
        }

        public void SetRadius(float radius)
        {
            if (Mathf.Abs(radius - _radius) <= Epsilon) return;
            _radius = radius;
            SetVerticesDirty();
        }

        /// <summary>구멍 i 를 둔다 (로컬 좌표). 거의 같으면 아무것도 하지 않는다.</summary>
        public void SetHole(int i, Rect hole)
        {
            if (_has[i] && Near(hole, _holes[i])) return;
            _has[i] = true;
            _holes[i] = hole;
            SetVerticesDirty();
        }

        public void ClearHole(int i)
        {
            if (!_has[i]) return;
            _has[i] = false;
            SetVerticesDirty();
        }

        public void ClearHoles()
        {
            ClearHole(0);
            ClearHole(1);
        }

        private static bool Near(Rect a, Rect b)
        {
            return Mathf.Abs(a.xMin - b.xMin) <= Epsilon && Mathf.Abs(a.yMin - b.yMin) <= Epsilon
                && Mathf.Abs(a.xMax - b.xMax) <= Epsilon && Mathf.Abs(a.yMax - b.yMax) <= Epsilon;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            RebuildCount++;
            Rect o = rectTransform.rect;
            Color32 c = color;

            // 화면 안으로 자르고 겹치면 합친다 (할당 없음 — 고정 배열)
            int n = 0;
            for (int i = 0; i < MaxHoles; i++)
            {
                if (!_has[i]) continue;
                Rect h = Rect.MinMaxRect(
                    Mathf.Clamp(_holes[i].xMin, o.xMin, o.xMax), Mathf.Clamp(_holes[i].yMin, o.yMin, o.yMax),
                    Mathf.Clamp(_holes[i].xMax, o.xMin, o.xMax), Mathf.Clamp(_holes[i].yMax, o.yMin, o.yMax));
                if (h.width <= 0f || h.height <= 0f) continue;
                bool merged = false;
                for (int k = 0; k < n; k++)
                {
                    if (!_work[k].Overlaps(h)) continue;
                    _work[k] = Rect.MinMaxRect(Mathf.Min(_work[k].xMin, h.xMin), Mathf.Min(_work[k].yMin, h.yMin),
                        Mathf.Max(_work[k].xMax, h.xMax), Mathf.Max(_work[k].yMax, h.yMax));
                    merged = true;
                }
                if (!merged) _work[n++] = h;
            }

            if (n == 0)
            {
                Quad(vh, o.xMin, o.yMin, o.xMax, o.yMax, c);
                return;
            }

            // 가로 띠 — y 경계 정렬 (삽입 정렬, 최대 6개)
            int ny = 0;
            _ys[ny++] = o.yMin;
            _ys[ny++] = o.yMax;
            for (int k = 0; k < n; k++)
            {
                _ys[ny++] = _work[k].yMin;
                _ys[ny++] = _work[k].yMax;
            }
            for (int a = 1; a < ny; a++)
            {
                float v = _ys[a];
                int b = a - 1;
                while (b >= 0 && _ys[b] > v) { _ys[b + 1] = _ys[b]; b--; }
                _ys[b + 1] = v;
            }

            for (int s = 0; s + 1 < ny; s++)
            {
                float y0 = _ys[s], y1 = _ys[s + 1];
                if (y1 - y0 <= 0.001f) continue;
                float mid = (y0 + y1) * 0.5f;
                // 이 띠를 가로지르는 구멍 (x 순, 최대 2개)
                int first = -1, second = -1;
                for (int k = 0; k < n; k++)
                {
                    if (mid < _work[k].yMin || mid > _work[k].yMax) continue;
                    if (first < 0) first = k;
                    else second = k;
                }
                if (first >= 0 && second >= 0 && _work[second].xMin < _work[first].xMin) { int t = first; first = second; second = t; }

                float x = o.xMin;
                if (first >= 0) { Quad(vh, x, y0, _work[first].xMin, y1, c); x = _work[first].xMax; }
                if (second >= 0) { Quad(vh, x, y0, _work[second].xMin, y1, c); x = _work[second].xMax; }
                Quad(vh, x, y0, o.xMax, y1, c);
            }

            // 모서리 — 꼭짓점에서 호까지 부채 (구멍 안쪽 r×r 칸 중 호 바깥)
            for (int k = 0; k < n; k++)
            {
                Rect h = _work[k];
                float r = Mathf.Min(_radius, h.width * 0.5f, h.height * 0.5f);
                if (r <= 0f) continue;
                Corner(vh, new Vector2(h.xMin, h.yMin), new Vector2(h.xMin + r, h.yMin + r), r, 180f, c);
                Corner(vh, new Vector2(h.xMax, h.yMin), new Vector2(h.xMax - r, h.yMin + r), r, 270f, c);
                Corner(vh, new Vector2(h.xMax, h.yMax), new Vector2(h.xMax - r, h.yMax - r), r, 0f, c);
                Corner(vh, new Vector2(h.xMin, h.yMax), new Vector2(h.xMin + r, h.yMax - r), r, 90f, c);
            }
        }

        private static void Quad(VertexHelper vh, float x0, float y0, float x1, float y1, Color32 c)
        {
            if (x1 <= x0 || y1 <= y0) return;
            int i = vh.currentVertCount;
            vh.AddVert(new Vector3(x0, y0), c, Vector4.zero);
            vh.AddVert(new Vector3(x0, y1), c, Vector4.zero);
            vh.AddVert(new Vector3(x1, y1), c, Vector4.zero);
            vh.AddVert(new Vector3(x1, y0), c, Vector4.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i + 2, i + 3, i);
        }

        private void Corner(VertexHelper vh, Vector2 tip, Vector2 center, float r, float startDeg, Color32 c)
        {
            int i0 = vh.currentVertCount;
            vh.AddVert(tip, c, Vector4.zero);
            for (int s = 0; s <= _segments; s++)
            {
                float a = (startDeg + 90f * s / _segments) * Mathf.Deg2Rad;
                vh.AddVert(new Vector3(center.x + Mathf.Cos(a) * r, center.y + Mathf.Sin(a) * r), c, Vector4.zero);
            }
            for (int s = 0; s < _segments; s++) vh.AddTriangle(i0, i0 + 1 + s, i0 + 2 + s);
        }
    }
}
