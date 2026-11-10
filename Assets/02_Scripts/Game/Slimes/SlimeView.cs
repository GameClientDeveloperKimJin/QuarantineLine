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
    /// </summary>
    public sealed class SlimeView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Renderer[] renderers;
        [Tooltip("변이 점액 색 (MaterialPropertyBlock _BaseColor). 일반 점액은 머티리얼 색 그대로")]
        [SerializeField] private Color mutantTint = new Color(0.10f, 0.55f, 0.22f, 0.9f);

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

        /// <summary>풀에서 처음 만들어졌을 때 한 번.</summary>
        internal void Init(int id, int poolIndex, float cellSize)
        {
            Id = id;
            PoolIndex = poolIndex;
            _cellSize = cellSize;
            Combat = new SlimeCombat();
            _block = new MaterialPropertyBlock();
            _baseScale = transform.localScale;
            if (renderers == null || renderers.Length == 0)
                renderers = GetComponentsInChildren<Renderer>(true);
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

            bool tinted = stats.Type == SlimeType.Mutant;
            _block.Clear();
            if (tinted) _block.SetColor(BaseColorId, mutantTint);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].SetPropertyBlock(tinted ? _block : null);
        }
    }
}
