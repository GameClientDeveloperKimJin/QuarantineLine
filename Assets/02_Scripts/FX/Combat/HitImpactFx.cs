using QL.Core;
using QL.FX.Alert;
using QL.FX.Motion;
using QL.Game;
using QL.Game.Drones;
using QL.Game.Slimes;
using UnityEngine;

namespace QL.FX.Combat
{
    /// <summary>
    /// U24 드론 종류별 피격 이펙트 (CombatFx.prefab). <c>SlimeDamaged(view, damage, source)</c> — 드론 직접 공격만 (V02, 잔열 없음) — 를
    /// 받아 맞은 점액 위에 종류마다 다른 <see cref="HitBurst"/> 를 띄운다:
    /// 흡입 = 소용돌이 돌며 줄어듦 + 방울이 드론 쪽으로 뜯겨 나감 · 소각 = 폭발 펑 + 불씨 + 회색 연기 · 냉각 = 발밑 빙하 물결 + 서리 반짝 + 얼음 조각.
    /// <list type="bullet">
    /// <item>풀: 종류마다 <see cref="FxConfig.hitImpactPoolPerType"/> 개를 Awake 에 미리 — 런타임 Instantiate 0. 넘치면 가장 오래된 것부터 (원형 순서)</item>
    /// <item>같은 점액 · 같은 종류는 <see cref="FxConfig.hitImpactCooldown"/> 안에 다시 안 띄움 (원형 기록 — 할당 0)</item>
    /// <item>방울 방향 = 그 종류 드론 중 가장 가까운 칸 (DroneBoard 칸 순회, 같은 레인 우선) · 3등급이면 크기 · 입자 수 늘림</item>
    /// <item>게임 시간 (Update dt) — 일시정지면 그 자리에 멈춤. 색 = MPB (재질 복제 없음)</item>
    /// </list>
    /// 역할 구분: U18a <c>AttackFxView</c> = 드론 무기 (빔 · 부채꼴 · 분사 · 끝점 입자), U18b <c>FeelMomentsFx</c> Hit = 종류 무관 공통 타격감,
    /// U06 HitFlashFx = 점액 몸 번쩍. 이 컴포넌트 = 맞은 점액 쪽의 종류별 반응. 구독만 — 게임 상태를 바꾸지 않는다.
    /// </summary>
    public sealed class HitImpactFx : MonoBehaviour
    {
        private const int TypeCount = 3;

        [SerializeField] private FxConfig fx;
        [Tooltip("카메라 (빌보드 회전). 비어 있으면 Awake 에 한 번 찾는다")]
        [SerializeField] private FxPool pool;
        [Tooltip("드론 위치 (방울 방향 · 등급). 비어 있으면 처음 한 번 찾는다")]
        [SerializeField] private DroneBoard board;
        [Tooltip("점액 렌더러 캐시 (흡입 · 소각 몸 중앙 높이). 비어 있으면 Start 에 한 번 찾는다")]
        [SerializeField] private HitFlashFx hitFlash;
        [Tooltip("DroneType 순서 — 흡입 · 소각 · 냉각")]
        [SerializeField] private HitBurst suctionPrefab;
        [SerializeField] private HitBurst plasmaPrefab;
        [SerializeField] private HitBurst cryoPrefab;

        private HitBurst[][] _items;
        private int[] _next;
        private int _serial;

        private SlimeView[] _cdView;
        private int[] _cdType;
        private float[] _cdTime;
        private int _cdNext;

        public FxConfig Config => fx;
        public bool IsReady => _items != null;
        /// <summary>Awake 에 만든 이펙트 총수 (이후 늘지 않는다).</summary>
        public int CreatedCount { get; private set; }
        /// <summary>재생 총수 · 쿨다운으로 거른 수.</summary>
        public int PlayCount => _serial;
        public int SkippedCount { get; private set; }

        public int SizeOf(DroneType type) => Slots(type) != null ? Slots(type).Length : 0;
        public HitBurst ItemAt(DroneType type, int i) => Slots(type)?[i];

        /// <summary>그 종류에서 지금 보이는 개수.</summary>
        public int ActiveCount(DroneType type)
        {
            HitBurst[] s = Slots(type);
            if (s == null) return 0;
            int n = 0;
            for (int i = 0; i < s.Length; i++) if (s[i].Active) n++;
            return n;
        }

        private void Awake()
        {
            if (fx == null)
            {
                Debug.LogError("[HitImpactFx] FxConfig 가 비어 있다", this);
                enabled = false;
                return;
            }
            if (pool == null) pool = FindAnyObjectByType<FxPool>(); // 프리팹은 씬의 [FX]/FxPool 을 직렬화할 수 없다 (BurnFx 와 같음)

            int size = Mathf.Max(1, fx.hitImpactPoolPerType);
            _items = new HitBurst[TypeCount][];
            _next = new int[TypeCount];
            _items[(int)DroneType.Suction] = Build(suctionPrefab, size);
            _items[(int)DroneType.Plasma] = Build(plasmaPrefab, size);
            _items[(int)DroneType.Cryo] = Build(cryoPrefab, size);

            int slots = Mathf.Max(8, fx.hitImpactCooldownSlots);
            _cdView = new SlimeView[slots];
            _cdType = new int[slots];
            _cdTime = new float[slots];
        }

        private HitBurst[] Build(HitBurst prefab, int size)
        {
            if (prefab == null) return null;
            var arr = new HitBurst[size];
            for (int i = 0; i < size; i++)
            {
                HitBurst b = Instantiate(prefab, transform);
                b.name = prefab.name + "_" + i.ToString("00");
                b.Init();
                arr[i] = b;
                CreatedCount++;
            }
            return arr;
        }

        private void OnEnable()
        {
            GameEvents.SlimeDamaged += OnDamaged;
        }

        private void OnDisable()
        {
            GameEvents.SlimeDamaged -= OnDamaged;
        }

        private void Start()
        {
            EnsureBoard(); // 웨이브 중 첫 타격에 찾지 않게
            if (hitFlash == null) hitFlash = FindAnyObjectByType<HitFlashFx>();
        }

        private void OnDamaged(SlimeView view, int damage, DroneType source)
        {
            if (_items == null || view == null || !view.gameObject.activeInHierarchy) return;
            HitBurst[] slots = Slots(source);
            if (slots == null) return;

            float now = Time.time;
            if (InCooldown(view, (int)source, now))
            {
                SkippedCount++;
                return;
            }
            _cdView[_cdNext] = view;
            _cdType[_cdNext] = (int)source;
            _cdTime[_cdNext] = now;
            _cdNext = (_cdNext + 1) % _cdView.Length;

            Transform t = view.transform;
            Vector3 foot = t.position;
            Vector3 toward = FindDrone(source, foot, out int grade);
            bool g3 = grade >= 3;
            float scale = t.localScale.y * (g3 ? fx.hitGrade3Scale : 1f);

            int baseCount;
            float duration;
            Color color;
            switch (source)
            {
                case DroneType.Suction:
                    baseCount = fx.suckDropletCount; duration = fx.suckDuration; color = fx.hitSuctionColor; break;
                case DroneType.Plasma:
                    baseCount = fx.plasmaEmberCount; duration = fx.plasmaDuration; color = fx.hitPlasmaColor; break;
                default:
                    baseCount = fx.cryoShardCount; duration = fx.cryoDuration; color = fx.hitCryoColor; break;
            }
            int count = g3 ? Mathf.RoundToInt(baseCount * (1f + fx.hitGrade3CountBonus)) : baseCount;

            Camera cam = pool != null ? pool.ViewCamera : Camera.main;
            Quaternion rot = cam != null ? cam.transform.rotation : Quaternion.identity;
            Vector3 toCam = cam != null ? (cam.transform.position - foot).normalized : Vector3.zero;

            // U24b: 흡입 · 소각 = 점액 몸 중앙 (렌더러 bounds — 스쿼시 · 크기 반영). 냉각 = 발밑 물결이라 기존 높이 그대로
            float height = fx.hitImpactHeight * scale;
            if (source != DroneType.Cryo && hitFlash != null)
            {
                Renderer r = hitFlash.RendererOf(view);
                if (r != null) height = Mathf.Max(0f, r.bounds.center.y - foot.y);
            }

            int k = (int)source;
            HitBurst item = slots[_next[k]];
            _next[k] = (_next[k] + 1) % slots.Length;
            item.Serial = ++_serial;
            item.Play(foot, height, scale, toward, rot, toCam, count, color, duration, fx);
        }

        private void Update()
        {
            if (_items == null) return;
            float dt = Time.deltaTime; // 게임 시간 — 일시정지(timeScale 0) 중엔 멈춘다
            for (int k = 0; k < _items.Length; k++)
            {
                HitBurst[] s = _items[k];
                if (s == null) continue;
                for (int i = 0; i < s.Length; i++)
                    if (s[i].Active) s[i].Tick(dt);
            }
        }

        /// <summary>전부 즉시 끄기.</summary>
        public void StopAll()
        {
            if (_items == null) return;
            for (int k = 0; k < _items.Length; k++)
            {
                HitBurst[] s = _items[k];
                if (s == null) continue;
                for (int i = 0; i < s.Length; i++) s[i].Stop();
            }
        }

        private bool InCooldown(SlimeView view, int type, float now)
        {
            float cd = fx.hitImpactCooldown;
            if (cd <= 0f) return false;
            for (int i = 0; i < _cdView.Length; i++)
                if (_cdView[i] == view && _cdType[i] == type && now - _cdTime[i] < cd) return true;
            return false;
        }

        /// <summary>그 종류 드론 중 가장 가까운 것 (같은 레인 우선) 쪽 수평 단위 벡터. 없으면 격리선 뒤 (−Z, 드론 칸 쪽).</summary>
        private Vector3 FindDrone(DroneType type, Vector3 foot, out int grade)
        {
            grade = 1;
            Vector3 best = Vector3.back;
            if (!EnsureBoard()) return best;
            float bestCost = float.MaxValue;
            int n = board.CellCount;
            for (int i = 0; i < n; i++)
            {
                DroneView d = board.ViewAt(i);
                if (d == null || d.Type != type || d.IsLifted || !d.gameObject.activeInHierarchy) continue;
                Vector3 p = d.transform.position;
                float dx = p.x - foot.x, dz = p.z - foot.z;
                float cost = dx * dx * 4f + dz * dz;
                if (cost >= bestCost) continue;
                bestCost = cost;
                grade = d.Grade;
                Vector3 dir = new Vector3(dx, 0f, dz);
                best = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.back;
            }
            return best;
        }

        private bool EnsureBoard()
        {
            if (board == null) board = FindAnyObjectByType<DroneBoard>();
            return board != null && board.IsReady;
        }

        private HitBurst[] Slots(DroneType type)
        {
            int k = (int)type;
            return _items != null && k >= 0 && k < _items.Length ? _items[k] : null;
        }
    }
}
