using QL.Core;
using QL.Core.Combat;
using QL.Core.Stats;
using QL.Data;
using UnityEngine;

namespace QL.Game.Slimes
{
    /// <summary>
    /// 점액 하나 (풀 대상, 프리팹 루트). 판정 없음 — 이동 · 반납은 SlimeRegistry, 체력 · 상태이상은 Core 의 SlimeCombat.
    /// 프리팹은 칸 크기 1 · scale 1 기준, 루트 원점 = 바닥 위 발 위치. 연출(출렁임 · 피격 플래시)은 U04 · U06.
    /// 아트 프리팹 (G07): 루트(이 컴포넌트, 회전 없음) + 변형 자식 variantChildNames (모델 인스턴스, 얼굴이 −Z 로 Y 180°).
    /// 변형 · 렌더러는 이름으로 찾아 캐시한다 (직렬화 참조 없음 — 모델이 바뀌어도 끊기지 않는다).
    /// </summary>
    public sealed class SlimeView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("변이 점액 색 (MaterialPropertyBlock _BaseColor). 일반 점액은 머티리얼 색 그대로")]
        [SerializeField] private Color mutantTint = new Color(0.10f, 0.55f, 0.22f, 0.9f);
        [Tooltip("변이 점액을 mutantTint 로 칠한다. 임시 도형 = 켬, 아트 프리팹 = 끔 (텍스처에 보라 핵 · 색이 있다)")]
        [SerializeField] private bool applyTint = true;
        [Tooltip("겉모습 변형 자식 이름 (A/B). 풀 생성 때 SetVariant 로 하나만 켠다. 없으면 변형 없음 (임시 도형)")]
        [SerializeField] private string[] variantChildNames = { "Model_A", "Model_B" };

        private Renderer[] _renderers;
        private GameObject[] _variants;
        private MaterialPropertyBlock _block;
        private Vector3 _baseScale;
        private float _cellSize = 1f;

        /// <summary>풀 슬롯 번호 (SlimeRegistry 가 생성 때 한 번 정함, 고정). 아직 없으면 -1.</summary>
        public int Id { get; private set; } = -1;
        public SlimeType Type { get; private set; }
        public int Lane { get; private set; }
        /// <summary>격리선까지 남은 칸 (V01 3장).</summary>
        public float Dist { get; internal set; }
        /// <summary>체력 · 상태이상. 생성 때 한 번만 new, 꺼낼 때 Reset.</summary>
        public SlimeCombat Combat { get; private set; }
        public float MoveSpeed { get; private set; }
        public int HatchDamage { get; private set; }
        public int Reward { get; private set; }

        /// <summary>SlimeRegistry 내부용 — 살아 있는 목록 위치 (없으면 -1) · 꺼낸 풀 번호.</summary>
        internal int AliveIndex { get; set; } = -1;
        internal int PoolIndex { get; private set; }

        /// <summary>찾은 변형 자식 수 (0 이면 변형 없음).</summary>
        public int VariantCount
        {
            get
            {
                EnsureVariants();
                return _variants.Length;
            }
        }

        /// <summary>켜 둔 변형 번호. 아직 정하지 않았으면 -1.</summary>
        public int Variant { get; private set; } = -1;

        /// <summary>
        /// 변형 하나만 켜고 나머지는 끈다. SlimeRegistry 가 풀 생성 때 슬롯마다 한 번 (스폰 때 부르지 않는다).
        /// 범위 밖이면 나머지로 감는다. 변형이 없으면 아무것도 하지 않는다.
        /// </summary>
        public void SetVariant(int index)
        {
            EnsureVariants();
            int n = _variants.Length;
            if (n == 0) return;
            int v = ((index % n) + n) % n;
            for (int i = 0; i < n; i++)
                if (_variants[i].activeSelf != (i == v)) _variants[i].SetActive(i == v);
            Variant = v;
        }

        private void EnsureVariants()
        {
            if (_variants != null) return;
            int found = 0;
            var tmp = new GameObject[variantChildNames != null ? variantChildNames.Length : 0];
            for (int i = 0; i < tmp.Length; i++)
            {
                Transform c = string.IsNullOrEmpty(variantChildNames[i]) ? null : transform.Find(variantChildNames[i]);
                if (c != null) tmp[found++] = c.gameObject;
            }
            if (found != tmp.Length) System.Array.Resize(ref tmp, found);
            _variants = tmp;
        }

        /// <summary>풀에서 처음 만들어졌을 때 한 번.</summary>
        internal void Init(int id, int poolIndex, float cellSize)
        {
            Id = id;
            PoolIndex = poolIndex;
            _cellSize = cellSize;
            Combat = new SlimeCombat();
            _block = new MaterialPropertyBlock();
            _baseScale = transform.localScale;
            _renderers = GetComponentsInChildren<Renderer>(true);
        }

        /// <summary>풀에서 꺼낼 때. 수치는 SlimeStats(Core), 겉모습은 EnemyData(크기).</summary>
        public void Setup(in SlimeStats stats, EnemyData visual, int lane, float dist)
        {
            Type = stats.Type;
            Lane = lane;
            Dist = dist;
            MoveSpeed = stats.MoveSpeed;
            HatchDamage = stats.HatchDamage;
            Reward = stats.Reward;
            Combat.Reset(stats.MaxHp);

            float scale = visual != null ? visual.Scale : 1f;
            transform.localScale = _baseScale * (scale * _cellSize);

            bool tinted = applyTint && stats.Type == SlimeType.Mutant;
            _block.Clear();
            if (tinted) _block.SetColor(BaseColorId, mutantTint);
            for (int i = 0; i < _renderers.Length; i++)
                _renderers[i].SetPropertyBlock(tinted ? _block : null);
        }
    }
}
