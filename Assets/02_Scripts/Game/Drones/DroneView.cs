using QL.Core;
using UnityEngine;

namespace QL.Game.Drones
{
    /// <summary>
    /// 드론 한 대의 겉모습. 판정 없음 — DroneBoard 가 Core 상태를 읽어 Show · 위치를 정한다.
    /// 색은 MaterialPropertyBlock 의 _BaseColor (재질 복제 안 함). 프리팹은 칸 크기 1 기준으로 만든다.
    /// </summary>
    public sealed class DroneView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Renderer[] renderers;

        [Header("연출값 (칸 단위)")]
        [Tooltip("등급별 크기 배율. index = grade - 1, 길이가 모자라면 마지막 값")]
        [SerializeField] private float[] gradeScale = { 1f, 1.15f, 1.3f };
        [Tooltip("바닥에서 떠 있는 높이")]
        [SerializeField] private float hoverHeight = 0.35f;
        [Tooltip("집어 들었을 때 높이")]
        [SerializeField] private float liftHeight = 0.8f;

        private MaterialPropertyBlock _block;
        private Vector3 _baseScale;
        private float _cellSize = 1f;
        private bool _initialized;

        public DroneType Type { get; private set; }
        public int Grade { get; private set; }
        public bool IsLifted { get; private set; }
        /// <summary>이 뷰를 만든 프리팹 (풀 키).</summary>
        public GameObject SourcePrefab { get; private set; }

        private float Height => (IsLifted ? liftHeight : hoverHeight) * _cellSize;

        /// <summary>풀 생성 때 한 번.</summary>
        public void Init(GameObject sourcePrefab, float cellSize)
        {
            SourcePrefab = sourcePrefab;
            _cellSize = cellSize;
            EnsureInit();
        }

        private void EnsureInit()
        {
            if (_initialized) return;
            _initialized = true;
            _block = new MaterialPropertyBlock();
            _baseScale = transform.localScale;
            if (renderers == null || renderers.Length == 0)
                renderers = GetComponentsInChildren<Renderer>(true);
        }

        public void Show(DroneType type, int grade, Color tint)
        {
            EnsureInit();
            Type = type;
            Grade = grade;

            transform.localScale = _baseScale * (_cellSize * ScaleFor(grade));

            _block.SetColor(BaseColorId, tint);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].SetPropertyBlock(_block);

            if (!gameObject.activeSelf) gameObject.SetActive(true);
        }

        /// <summary>집으면 liftHeight, 내려놓으면 hoverHeight. 수평 위치는 그대로.</summary>
        public void SetLifted(bool lifted)
        {
            IsLifted = lifted;
            Vector3 p = transform.position;
            p.y = Height;
            transform.position = p;
        }

        /// <summary>바닥 점 위 현재 높이(뜸 · 들림)에 놓는다.</summary>
        public void PlaceOver(Vector3 floorPoint)
        {
            floorPoint.y = Height;
            transform.position = floorPoint;
        }

        private float ScaleFor(int grade)
        {
            if (gradeScale == null || gradeScale.Length == 0) return 1f;
            int i = grade - 1;
            if (i < 0) i = 0;
            if (i >= gradeScale.Length) i = gradeScale.Length - 1;
            return gradeScale[i];
        }
    }
}
