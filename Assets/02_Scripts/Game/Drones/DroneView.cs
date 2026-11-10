using QL.Core;
using UnityEngine;

namespace QL.Game.Drones
{
    /// <summary>
    /// 드론 한 대의 겉모습. 판정 없음 — DroneBoard 가 Core 상태를 읽어 Show · 위치를 정한다.
    /// 프리팹 구조 (G07): 루트(이 컴포넌트 · 크기 보정) + 자식 modelChildName(모델 인스턴스). 모델이 같은 이름으로 바뀌어도
    /// 루트는 그대로 — 등급 부품 · 렌더러는 Init 때 이름으로 한 번 찾아 캐시한다 (직렬화 참조 없음).
    /// 등급 누적: 1 = Body · 2 = + Part_G2 · 3 = + Part_G2 + Part_G3.
    /// 부품 찾는 순서: ① 모델 아래 같은 이름 자식 (GameObject 토글) ② 없으면 재질 이름에 부품 이름이 든 서브메시
    /// (OBJ 의 `o` 만 있고 `g` 가 없으면 Unity 가 메시 하나로 합친다 — 그때는 등급별 재질 배열을 미리 만들어 바꿔 끼운다).
    /// 둘 다 없으면 경고 한 번 + 토글 없이 계속. 틴트는 applyTint 일 때만 MaterialPropertyBlock 의 _BaseColor.
    /// </summary>
    public sealed class DroneView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static bool _warnedMissingParts;

        [Header("모델 (이름으로 찾는다)")]
        [Tooltip("모델 인스턴스 자식 이름. 없으면 루트 아래 전체에서 찾는다 (임시 도형)")]
        [SerializeField] private string modelChildName = "Model";
        [SerializeField] private string grade2PartName = "Part_G2";
        [SerializeField] private string grade3PartName = "Part_G3";
        [Tooltip("UnitData.tintColor 를 _BaseColor 로 칠한다. 임시 도형 = 켬, 아트 프리팹 = 끔 (텍스처에 종류색이 있다)")]
        [SerializeField] private bool applyTint = true;

        [Header("연출값 (칸 단위)")]
        [Tooltip("등급별 크기 배율. index = grade - 1, 길이가 모자라면 마지막 값")]
        [SerializeField] private float[] gradeScale = { 1f, 1.08f, 1.15f };
        [Tooltip("바닥에서 떠 있는 높이")]
        [SerializeField] private float hoverHeight = 0.35f;
        [Tooltip("집어 들었을 때 높이")]
        [SerializeField] private float liftHeight = 0.8f;

        private const int PartGradeCount = 3; // 등급 1 · 2 · 3 (부품 토글 단계)

        private Renderer[] _renderers;
        private MaterialPropertyBlock _block;
        private Vector3 _baseScale;
        private float _cellSize = 1f;
        private bool _initialized;

        // ① 자식 GameObject 방식
        private GameObject _partG2;
        private GameObject _partG3;
        // ② 서브메시 방식 — _partRenderers[k] 에 등급별 재질 배열 _gradeMaterials[k][grade-1]
        private Renderer[] _partRenderers;
        private Material[][][] _gradeMaterials;

        public DroneType Type { get; private set; }
        public int Grade { get; private set; }
        public bool IsLifted { get; private set; }
        /// <summary>이 뷰를 만든 프리팹 (풀 키).</summary>
        public GameObject SourcePrefab { get; private set; }
        /// <summary>등급 부품을 찾았는지 (자식이든 서브메시든).</summary>
        public bool HasGradeParts => _partG2 != null || _partG3 != null || _partRenderers != null;

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

            Transform model = string.IsNullOrEmpty(modelChildName) ? null : transform.Find(modelChildName);
            Transform searchRoot = model != null ? model : transform;
            _renderers = searchRoot.GetComponentsInChildren<Renderer>(true);

            Transform g2 = FindDeep(searchRoot, grade2PartName);
            Transform g3 = FindDeep(searchRoot, grade3PartName);
            if (g2 != null) _partG2 = g2.gameObject;
            if (g3 != null) _partG3 = g3.gameObject;
            if (_partG2 == null && _partG3 == null) BuildSubmeshParts();

            if (!HasGradeParts && !_warnedMissingParts)
            {
                _warnedMissingParts = true;
                Debug.LogWarning("[DroneView] " + name + " 모델에 " + grade2PartName + " · " + grade3PartName +
                                 " 가 없다 — 등급 부품 토글 없이 크기 · 색만 바꾼다", this);
            }
        }

        /// <summary>재질 이름에 부품 이름이 든 서브메시를 찾아 등급별 재질 배열을 만든다 (Init 때 한 번 — 할당은 여기서만).</summary>
        private void BuildSubmeshParts()
        {
            int found = 0;
            for (int i = 0; i < _renderers.Length; i++)
                if (PartSlotGrade(_renderers[i].sharedMaterials) > 0) found++;
            if (found == 0) return;

            _partRenderers = new Renderer[found];
            _gradeMaterials = new Material[found][][];
            int k = 0;
            for (int i = 0; i < _renderers.Length; i++)
            {
                Material[] all = _renderers[i].sharedMaterials;
                if (PartSlotGrade(all) == 0) continue;

                var byGrade = new Material[PartGradeCount][];
                for (int g = 1; g <= PartGradeCount; g++)
                {
                    // 보일 슬롯만 남긴다 — 숨길 슬롯은 null, 끝쪽 null 은 잘라 낸다 (재질이 모자란 서브메시는 그리지 않는다)
                    int keep = all.Length;
                    var mats = new Material[all.Length];
                    for (int s = 0; s < all.Length; s++)
                    {
                        int partGrade = PartGradeOf(all[s]);
                        mats[s] = partGrade == 0 || partGrade <= g ? all[s] : null;
                    }
                    while (keep > 0 && mats[keep - 1] == null) keep--;
                    if (keep != mats.Length) System.Array.Resize(ref mats, keep);
                    byGrade[g - 1] = mats;
                }

                _partRenderers[k] = _renderers[i];
                _gradeMaterials[k] = byGrade;
                k++;
            }
        }

        /// <summary>재질 배열 안에 부품 재질이 있으면 그 등급(2 · 3), 없으면 0.</summary>
        private int PartSlotGrade(Material[] mats)
        {
            for (int s = 0; s < mats.Length; s++)
            {
                int g = PartGradeOf(mats[s]);
                if (g > 0) return g;
            }
            return 0;
        }

        /// <summary>재질 이름 → 부품 등급. 부품 아님(본체 · 도구) = 0.</summary>
        private int PartGradeOf(Material m)
        {
            if (m == null) return 0;
            string n = m.name;
            if (!string.IsNullOrEmpty(grade3PartName) && n.Contains(grade3PartName)) return 3;
            if (!string.IsNullOrEmpty(grade2PartName) && n.Contains(grade2PartName)) return 2;
            return 0;
        }

        private static Transform FindDeep(Transform root, string childName)
        {
            if (string.IsNullOrEmpty(childName)) return null;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform c = root.GetChild(i);
                if (c.name == childName) return c;
                Transform d = FindDeep(c, childName);
                if (d != null) return d;
            }
            return null;
        }

        public void Show(DroneType type, int grade, Color tint)
        {
            EnsureInit();
            Type = type;
            Grade = grade;

            transform.localScale = _baseScale * (_cellSize * ScaleFor(grade));
            ApplyGradeParts(grade);

            if (applyTint)
            {
                _block.SetColor(BaseColorId, tint);
                for (int i = 0; i < _renderers.Length; i++)
                    _renderers[i].SetPropertyBlock(_block);
            }

            if (!gameObject.activeSelf) gameObject.SetActive(true);
        }

        private void ApplyGradeParts(int grade)
        {
            if (_partG2 != null && _partG2.activeSelf != grade >= 2) _partG2.SetActive(grade >= 2);
            if (_partG3 != null && _partG3.activeSelf != grade >= 3) _partG3.SetActive(grade >= 3);

            if (_partRenderers == null) return;
            int g = Mathf.Clamp(grade, 1, PartGradeCount) - 1;
            for (int k = 0; k < _partRenderers.Length; k++)
                _partRenderers[k].sharedMaterials = _gradeMaterials[k][g];
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
