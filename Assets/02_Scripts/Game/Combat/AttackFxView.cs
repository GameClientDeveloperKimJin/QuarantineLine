using QL.Core;
using QL.Game.Bootstrap;
using UnityEngine;

namespace QL.Game.Combat
{
    /// <summary>
    /// 공격 표시 하나 (풀 대상). 받은 월드 위치에 그리기만 한다 — 타겟 · 사거리 · 피해 계산 없음 (DroneCombat 이 넘긴다).
    /// 선(LineRenderer) + 바닥 쿼드 1장, 재질 1개 공유(Mat_AttackFx, 가산). 텍스처 · 색은 MaterialPropertyBlock.
    /// 텍스처 · 블렌딩 · 피벗은 05_Docs/_아트납품.md 「A10」.
    /// <para>
    /// U18a (R4 — R1 예외 승인, 보이는 것만): 선 · 쿼드는 <see cref="Tick"/> 에서 두께 펄스 · 흐름 · 깜박임 · 퍼짐 · 사라짐.
    /// 입자는 모든 표시가 같이 쓰는 ParticleSystem 6개 (<see cref="SharedRoot"/> — 풀의 첫 표시가 Awake 에서 한 번 만들고 데움, 시작 때만 할당)에
    /// <c>Emit(EmitParams)</c> 로 뿌린다 — 발사마다 Instantiate · 할당 0, MMF_Player 안 씀 (발사가 초당 수십 번).
    /// 공통: 발사구 머즐 플래시 (드론 노즐 쪽 섬광이 톡 커졌다 줄어드는 펀치) · 맞은 곳 임팩트 (스파크 · 작은 링).
    /// 흡입: 빔 위를 점액 → 드론으로 흐르는 입자 + 끝 소용돌이 · 소각: 부채꼴 안 불꽃 (노이즈 흔들림) + 끝 불씨 ·
    /// 냉각: 분사 서리 가루 + 맞은 곳 서리 반짝 · 3등급 빙결 링: 바닥 링 + 얼음 파편.
    /// 등급은 <c>from</c> 칸의 드론 등급을 읽어 (GameBootstrap Grid — 읽기만) 굵기 · 밝기 · 입자 수를 올린다.
    /// G13 (보류함 36): DroneCombat 은 등급을 넘기는 오버로드 (<c>grade</c>) 를 부른다 — Grid 조회는 등급 없는 오버로드 (테스트) 만.
    /// 시간은 게임 시간 (일시정지 = DroneCombat 이 Advance 를 안 부르고 입자도 timeScale 0 에서 멈춤).
    /// </para>
    /// </summary>
    public sealed class AttackFxView : MonoBehaviour
    {
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private const int MaxGrade = 3;

        [SerializeField] private LineRenderer line;
        [SerializeField] private MeshRenderer quad;

        [Header("텍스처 (Assets/04_Resources/FX)")]
        [SerializeField] private Texture2D beamTexture;
        [SerializeField] private Texture2D coneTexture;
        [SerializeField] private Texture2D frostTexture;
        [SerializeField] private Texture2D ringTexture;
        [Tooltip("U18a 입자 — 둥근 빛 (머즐 · 흐름 · 불꽃)")]
        [SerializeField] private Texture2D glowTexture;
        [Tooltip("U18a 입자 — 스파크 (임팩트 · 불씨 · 얼음 파편)")]
        [SerializeField] private Texture2D sparkTexture;

        [Header("종류 기본색 (UnitData.tintColor 아님)")]
        [SerializeField] private Color suctionColor = new Color(0.15f, 0.95f, 0.95f, 1f);
        [SerializeField] private Color plasmaColor = new Color(1f, 0.55f, 0.12f, 1f);
        [SerializeField] private Color cryoColor = new Color(0.55f, 0.85f, 1f, 1f);

        [Header("연출값 (칸 단위)")]
        [Tooltip("보이는 시간 (초)")]
        [SerializeField, Min(0.01f)] private float duration = 0.1f;
        [Tooltip("흡입 빔 두께")]
        [SerializeField] private float beamWidth = 0.18f;
        [Tooltip("냉각 분사 두께")]
        [SerializeField] private float frostWidth = 0.12f;
        [Tooltip("냉각 끝 Frost_Burst 크기")]
        [SerializeField] private float burstSize = 0.9f;
        [Tooltip("바닥에서 띄우는 높이 (선 · 쿼드)")]
        [SerializeField] private float height = 0.3f;
        [Tooltip("Cone_Gradient 꼭짓점 v (A10: 피벗 (0.5, 0.047))")]
        [SerializeField] private float coneApexV = 0.047f;
        [Tooltip("Ring 반지름 / 텍스처 폭 (A10: 104/256)")]
        [SerializeField] private float ringRadiusFraction = 104f / 256f;

        [Header("U18a 등급 (index = 등급 − 1)")]
        [Tooltip("선 · 쿼드 · 입자 굵기 배율")]
        [SerializeField] private float[] gradeWidth = { 1f, 1.35f, 1.75f };
        [Tooltip("선 · 쿼드 HDR 밝기 (1 = 원래 색, 블룸 U07 은 1 넘는 부분에 걸린다)")]
        [SerializeField] private float[] gradeIntensity = { 1.1f, 1.45f, 1.9f };
        [Tooltip("입자 수 배율")]
        [SerializeField] private float[] gradeCount = { 1f, 1.6f, 2.3f };

        [Header("U18a 선 · 쿼드 움직임")]
        [Tooltip("빔 두께 펄스 세기 (± 비율) · 표시 시간 동안 펄스 수")]
        [SerializeField, Range(0f, 1f)] private float pulseAmount = 0.35f;
        [SerializeField, Min(0f)] private float pulseCount = 2f;
        [Tooltip("빔 텍스처 흐름 속도 (텍스처 반복 / 초, + = 드론 쪽으로)")]
        [SerializeField] private float beamScroll = 9f;
        [Tooltip("빔 텍스처 반복 수 (선 길이 1칸당)")]
        [SerializeField, Min(0.1f)] private float beamTilePerCell = 0.7f;
        [Tooltip("이 비율부터 사라지기 시작 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float fadeFrom = 0.55f;
        [Tooltip("부채꼴 · 링이 처음 크기 비율 → 1 로 퍼짐")]
        [SerializeField, Range(0.1f, 1f)] private float spreadFrom = 0.75f;
        [Tooltip("소각 부채꼴 아지랑이 깜박임 세기 · 빠르기 (Hz)")]
        [SerializeField, Range(0f, 1f)] private float heatFlicker = 0.25f;
        [SerializeField, Min(0f)] private float heatFlickerHz = 38f;

        [Header("U18a 입자 공통")]
        [Tooltip("입자 재질 밝기 (HDR — 가산, 과하면 화면이 하얘진다)")]
        [SerializeField, Min(0f)] private float particleIntensity = 1.6f;
        [Tooltip("입자 시스템 하나당 최대 수 (동시 15대 발사 상한)")]
        [SerializeField, Min(16)] private int maxParticles = 600;
        [Tooltip("발사구 = 드론 중심에서 타겟 쪽으로 이 거리 (칸) · 바닥에서 높이 (칸 — 드론 몸통(hover 0.35)에 가려지지 않게 앞 · 위)")]
        [SerializeField] private float muzzleForward = 0.42f;
        [SerializeField] private float muzzleHeight = 0.5f;
        [Tooltip("머즐 섬광 크기 (칸) · 수명 (초)")]
        [SerializeField] private float muzzleSize = 0.55f;
        [SerializeField, Min(0.01f)] private float muzzleLife = 0.09f;
        [Tooltip("임팩트 스파크 수 (1등급) · 속도 (칸/초) · 수명 · 크기")]
        [SerializeField, Min(0)] private int impactSparks = 3;
        [SerializeField] private float impactSparkSpeed = 4f;
        [SerializeField, Min(0.01f)] private float impactSparkLife = 0.22f;
        [SerializeField] private float impactSparkSize = 0.1f;
        [Tooltip("임팩트 링 크기 (칸) · 수명")]
        [SerializeField] private float impactRingSize = 0.6f;
        [SerializeField, Min(0.01f)] private float impactRingLife = 0.2f;

        [Header("U18a 흡입")]
        [Tooltip("빔 위 흐르는 입자 수 (1등급) · 속도 (칸/초) · 크기")]
        [SerializeField, Min(0)] private int suctionFlow = 4;
        [SerializeField] private float suctionFlowSpeed = 7f;
        [SerializeField] private float suctionFlowSize = 0.2f;
        [Tooltip("끝 소용돌이 링 크기 (칸) · 수명 · 도는 점 수 · 반지름")]
        [SerializeField] private float swirlSize = 0.9f;
        [SerializeField, Min(0.01f)] private float swirlLife = 0.24f;
        [SerializeField, Min(0)] private int swirlDots = 3;
        [SerializeField] private float swirlRadius = 0.35f;

        [Header("U18a 소각")]
        [Tooltip("부채꼴 안 불꽃 수 (1등급) · 속도 (칸/초) · 크기 · 수명")]
        [SerializeField, Min(0)] private int plasmaFlames = 6;
        [SerializeField] private float plasmaFlameSpeed = 2.6f;
        [SerializeField] private float plasmaFlameSize = 0.45f;
        [SerializeField, Min(0.01f)] private float plasmaFlameLife = 0.32f;
        [Tooltip("불꽃 노이즈 흔들림 세기 · 빈도")]
        [SerializeField] private float plasmaNoise = 0.8f;
        [SerializeField] private float plasmaNoiseFrequency = 1.6f;
        [Tooltip("끝 불씨 수 (1등급) · 수명")]
        [SerializeField, Min(0)] private int plasmaEmbers = 3;
        [SerializeField, Min(0.01f)] private float plasmaEmberLife = 0.5f;

        [Header("U18a 냉각")]
        [Tooltip("분사 서리 가루 수 (1등급) · 속도 (칸/초) · 크기 · 수명")]
        [SerializeField, Min(0)] private int cryoFlakes = 5;
        [SerializeField] private float cryoFlakeSpeed = 7f;
        [SerializeField] private float cryoFlakeSize = 0.24f;
        [SerializeField, Min(0.01f)] private float cryoFlakeLife = 0.38f;
        [Tooltip("맞은 곳 서리 반짝 수")]
        [SerializeField, Min(0)] private int cryoGlints = 3;
        [Tooltip("3등급 빙결 얼음 파편 수 · 속도 (반지름 / 초 배율)")]
        [SerializeField, Min(0)] private int freezeShards = 12;
        [SerializeField] private float freezeShardSpeed = 3.5f;

        private enum Kind { None, Beam, Frost, Cone, Ring }

        private MaterialPropertyBlock _lineBlock;
        private MaterialPropertyBlock _quadBlock;
        private float _cellSize = 1f;

        // 지금 표시 (Tick 이 움직인다)
        private Kind _kind;
        private Color _color;
        private float _intensity = 1f;
        private float _lineWidth;
        private float _lineTile = 1f;
        private float _scroll;
        private Vector3 _quadScale;
        private Vector3 _quadAnchor;
        private Vector3 _quadForward;
        private float _quadPivotV;

        /// <summary>남은 표시 시간. 0 이하면 반납 대상.</summary>
        public float Remaining { get; private set; }
        public float Duration => duration;
        /// <summary>마지막 Show 가 읽은 등급 (1~3).</summary>
        public int LastGrade { get; private set; } = 1;
        /// <summary>지금 선 두께 (월드).</summary>
        public float LineWidth => line != null && line.enabled ? line.widthMultiplier : 0f;
        public bool IsLineVisible => line != null && line.enabled;
        public bool IsQuadVisible => quad != null && quad.enabled;

        // ── 같이 쓰는 입자 (U18a) ─────────────────────────────

        private enum Ps { Glow, Spark, Frost, Fire, RingFlat, Swirl, Count }

        private static Transform s_root;
        private static readonly ParticleSystem[] s_ps = new ParticleSystem[(int)Ps.Count];
        private static GameBootstrap s_bootstrap;
        private static uint s_rng = 0x9E3779B9u;

        /// <summary>같이 쓰는 입자 시스템 부모 (없으면 null).</summary>
        public static Transform SharedRoot => s_root;
        public static int SharedSystemCount => s_root != null ? (int)Ps.Count : 0;

        /// <summary>같이 쓰는 입자 시스템 전체의 지금 입자 수.</summary>
        public static int SharedParticleCount
        {
            get
            {
                if (s_root == null) return 0;
                int n = 0;
                for (int i = 0; i < s_ps.Length; i++) n += s_ps[i].particleCount;
                return n;
            }
        }

        /// <summary>종류별 확인용 — 0 Glow · 1 Spark · 2 Frost · 3 Fire · 4 RingFlat · 5 Swirl.</summary>
        public static int SharedParticleCountOf(int system) => s_root != null ? s_ps[system].particleCount : 0;

        /// <summary>같이 쓰는 입자를 모두 지운다 (테스트).</summary>
        public static void ClearShared()
        {
            if (s_root == null) return;
            for (int i = 0; i < s_ps.Length; i++) s_ps[i].Clear(true);
        }

        private void Awake()
        {
            // 풀 생성(DroneCombat.Awake → Pool → Instantiate)에서 불린다 — 시작 때 한 번만 만들고 데운다
            if (s_root == null) BuildShared(transform.parent);
            if (s_bootstrap == null) s_bootstrap = FindAnyObjectByType<GameBootstrap>();
        }

        /// <summary>풀에서 처음 꺼낼 때 한 번 (DroneCombat).</summary>
        public void Init(float cellSize)
        {
            if (_lineBlock != null) return;
            _cellSize = cellSize;
            _lineBlock = new MaterialPropertyBlock();
            _quadBlock = new MaterialPropertyBlock();
            if (line != null)
            {
                line.positionCount = 2;
                line.useWorldSpace = true;
            }
        }

        /// <summary>시간 진행. 다 보였으면 true.</summary>
        public bool Tick(float dt)
        {
            Remaining -= dt;
            if (Remaining <= 0f) return true;
            Animate(dt);
            return false;
        }

        public Color ColorOf(DroneType type)
        {
            switch (type)
            {
                case DroneType.Suction: return suctionColor;
                case DroneType.Plasma: return plasmaColor;
                default: return cryoColor;
            }
        }

        /// <summary>흡입 — 드론 → 빔 끝 (가장 먼 맞은 점액). 등급 = from 칸 드론.</summary>
        public void ShowBeam(Vector3 from, Vector3 to, DroneType type) => ShowBeam(from, to, type, GradeAt(from));

        /// <summary>흡입 — 등급을 받는다 (G13 · 보류함 36, DroneCombat).</summary>
        public void ShowBeam(Vector3 from, Vector3 to, DroneType type, int grade)
        {
            int g = Begin(Kind.Beam, type, grade);
            SetLine(from, to, beamWidth * Pick(gradeWidth, g), beamTexture);
            HideQuad();

            Vector3 a = Lifted(from), b = Lifted(to);
            Muzzle(a, b, g);
            Impact(b, g);
            SuctionFlow(a, b, g);
            Swirl(b, g);
        }

        /// <summary>냉각 — 드론 → 타겟 선 + 끝에 Frost_Burst. 등급 = from 칸 드론.</summary>
        public void ShowFrost(Vector3 from, Vector3 to, DroneType type) => ShowFrost(from, to, type, GradeAt(from));

        /// <summary>냉각 — 등급을 받는다 (G13 · 보류함 36, DroneCombat).</summary>
        public void ShowFrost(Vector3 from, Vector3 to, DroneType type, int grade)
        {
            int g = Begin(Kind.Frost, type, grade);
            SetLine(from, to, frostWidth * Pick(gradeWidth, g), frostTexture);
            float s = burstSize * _cellSize * Pick(gradeWidth, g);
            SetFlatQuad(Lifted(to), Vector3.forward, s, s, 0.5f, frostTexture);

            Vector3 a = Lifted(from), b = Lifted(to);
            Muzzle(a, b, g);
            Impact(b, g);
            CryoSpray(a, b, g);
            CryoGlint(b, g);
        }

        /// <summary>소각 — 꼭짓점(드론) → 먼 끝 중심까지 부채꼴, 먼 끝 폭 farWidth (월드). 등급 = apex 칸 드론.</summary>
        public void ShowCone(Vector3 apex, Vector3 farCenter, float farWidth, DroneType type) => ShowCone(apex, farCenter, farWidth, type, GradeAt(apex));

        /// <summary>소각 — 등급을 받는다 (G13 · 보류함 36, DroneCombat).</summary>
        public void ShowCone(Vector3 apex, Vector3 farCenter, float farWidth, DroneType type, int grade)
        {
            int g = Begin(Kind.Cone, type, grade);
            HideLine();
            apex = Lifted(apex);
            farCenter = Lifted(farCenter);
            Vector3 d = farCenter - apex;
            float length = d.magnitude;
            if (length <= 0f) { HideQuad(); return; }
            float quadHeight = length / (1f - coneApexV);
            SetFlatQuad(apex, d / length, farWidth, quadHeight, coneApexV, coneTexture);

            Muzzle(apex, farCenter, g);
            PlasmaFlames(apex, farCenter, farWidth, g);
            PlasmaEmbers(farCenter, farWidth, g);
        }

        /// <summary>냉각 3등급 빙결 — 중심에 반경 radius (월드) 링.</summary>
        public void ShowRing(Vector3 center, float radius, DroneType type)
        {
            Begin(Kind.Ring, type, MaxGrade);
            LastGrade = MaxGrade; // 빙결은 냉각 3등급만
            _intensity = Pick(gradeIntensity, MaxGrade);
            HideLine();
            float size = radius / ringRadiusFraction;
            Vector3 c = Lifted(center);
            SetFlatQuad(c, Vector3.forward, size, size, 0.5f, ringTexture);
            FreezeBurst(c, radius);
        }

        // ── 내부 — 선 · 쿼드 ──────────────────────────────────

        /// <summary>표시 시작 — 시간 · 종류 · 등급 (1~3 으로 자름). 돌려주는 값 = 등급.</summary>
        private int Begin(Kind kind, DroneType type, int grade)
        {
            Remaining = duration;
            _kind = kind;
            _color = ColorOf(type);
            _scroll = 0f;
            int g = Mathf.Clamp(grade, 1, MaxGrade);
            LastGrade = g;
            _intensity = Pick(gradeIntensity, g);
            return g;
        }

        private static int GradeAt(Vector3 world)
        {
            if (s_bootstrap == null || !s_bootstrap.IsReady) return 1;
            int cell = s_bootstrap.Space.WorldToCell(world);
            if (cell < 0) return 1;
            var c = s_bootstrap.Grid.Get(cell);
            return c.HasDrone ? Mathf.Clamp(c.Grade, 1, MaxGrade) : 1;
        }

        private static float Pick(float[] values, int grade)
        {
            if (values == null || values.Length == 0) return 1f;
            int i = Mathf.Clamp(grade - 1, 0, values.Length - 1);
            return values[i];
        }

        private Vector3 Lifted(Vector3 p)
        {
            p.y = height * _cellSize;
            return p;
        }

        private float Progress => 1f - Mathf.Clamp01(Remaining / duration);

        private float Fade(float k) => k <= fadeFrom ? 1f : 1f - (k - fadeFrom) / Mathf.Max(0.0001f, 1f - fadeFrom);

        private void SetLine(Vector3 from, Vector3 to, float width, Texture2D tex)
        {
            if (line == null) return;
            line.enabled = true;
            Vector3 a = Lifted(from), b = Lifted(to);
            line.SetPosition(0, a);
            line.SetPosition(1, b);
            _lineWidth = width * _cellSize;
            _lineTile = Mathf.Max(0.1f, Vector3.Distance(a, b) / _cellSize * beamTilePerCell);
            _lineBlock.Clear();
            if (tex != null) _lineBlock.SetTexture(BaseMapId, tex);
            ApplyLine(0f);
        }

        /// <summary>두께 펄스 · 텍스처 흐름 (드론 쪽) · 사라짐 — 수치만 바꿔 같은 MPB 를 다시 넣는다 (할당 없음).</summary>
        private void ApplyLine(float k)
        {
            float pulse = 1f + pulseAmount * Mathf.Sin(k * pulseCount * 2f * Mathf.PI);
            float fade = Fade(k);
            line.widthMultiplier = _lineWidth * pulse * (0.4f + 0.6f * fade);
            _lineBlock.SetVector(BaseMapStId, new Vector4(_lineTile, 1f, _scroll, 0f));
            _lineBlock.SetColor(BaseColorId, Hdr(_color, _intensity * fade));
            line.SetPropertyBlock(_lineBlock);
        }

        /// <summary>바닥에 눕힌 쿼드. 텍스처 +V = forward, pivotV 지점이 anchor 에 온다.</summary>
        private void SetFlatQuad(Vector3 anchor, Vector3 forward, float width, float length, float pivotV, Texture2D tex)
        {
            if (quad == null) return;
            _quadAnchor = anchor;
            _quadForward = forward;
            _quadPivotV = pivotV;
            _quadScale = new Vector3(width, length, 1f);
            quad.transform.rotation = Quaternion.LookRotation(Vector3.down, forward); // 면이 위, 로컬 +Y = forward
            _quadBlock.Clear();
            if (tex != null) _quadBlock.SetTexture(BaseMapId, tex);
            ApplyQuad(0f);
            quad.enabled = true;
        }

        /// <summary>부채꼴 · 링 · 서리는 spreadFrom → 1 로 퍼짐 · 소각은 아지랑이 깜박임 · 끝에 사라짐.</summary>
        private void ApplyQuad(float k)
        {
            float spread = Mathf.Lerp(spreadFrom, 1f, 1f - (1f - Mathf.Clamp01(k / 0.5f)) * (1f - Mathf.Clamp01(k / 0.5f)));
            Vector3 s = _quadScale;
            if (_kind == Kind.Cone) s.y *= spread; // 길이만 뻗음 (꼭짓점 고정)
            else s = new Vector3(s.x * spread, s.y * spread, 1f);
            Transform t = quad.transform;
            t.position = _quadAnchor + _quadForward * ((0.5f - _quadPivotV) * s.y);
            t.localScale = s;

            float a = Fade(k);
            if (_kind == Kind.Cone) a *= 1f - heatFlicker * (0.5f + 0.5f * Mathf.Sin(k * duration * heatFlickerHz * 2f * Mathf.PI));
            _quadBlock.SetColor(BaseColorId, Hdr(_color, _intensity * a));
            quad.SetPropertyBlock(_quadBlock);
        }

        private void Animate(float dt)
        {
            float k = Progress;
            if (line != null && line.enabled)
            {
                _scroll += beamScroll * dt;
                if (_scroll > 1000f) _scroll -= 1000f;
                ApplyLine(k);
            }
            if (quad != null && quad.enabled) ApplyQuad(k);
        }

        private static Color Hdr(Color c, float intensity) => new Color(c.r * intensity, c.g * intensity, c.b * intensity, c.a);

        private void HideLine()
        {
            if (line != null) line.enabled = false;
        }

        private void HideQuad()
        {
            if (quad != null) quad.enabled = false;
        }

        // ── 내부 — 입자 (같이 쓰는 시스템에 Emit) ──────────────

        private void Muzzle(Vector3 drone, Vector3 target, int g)
        {
            Vector3 dir = Flat(target - drone);
            Vector3 p = drone + dir * (muzzleForward * _cellSize);
            p.y = muzzleHeight * _cellSize;
            float w = Pick(gradeWidth, g);
            Color c = _color;
            Emit(Ps.Glow, p, Vector3.zero, muzzleSize * w * _cellSize, muzzleLife, Color.Lerp(c, Color.white, 0.35f), 0f);
            Emit(Ps.Glow, p, Vector3.zero, muzzleSize * 0.5f * w * _cellSize, muzzleLife * 0.8f, Color.white, 0f);
            int n = Count(2, g);
            for (int i = 0; i < n; i++)
            {
                Vector3 v = (dir + Side(dir) * Rand(-0.5f, 0.5f) + Vector3.up * Rand(0f, 0.3f)) * (impactSparkSpeed * Rand(0.6f, 1f) * _cellSize);
                Emit(Ps.Spark, p, v, impactSparkSize * w * _cellSize, impactSparkLife * 0.6f, Color.Lerp(c, Color.white, 0.5f), 0f);
            }
        }

        private void Impact(Vector3 p, int g)
        {
            float w = Pick(gradeWidth, g);
            Color c = Color.Lerp(_color, Color.white, 0.4f);
            int n = Count(impactSparks, g);
            for (int i = 0; i < n; i++)
            {
                Vector3 v = new Vector3(Rand(-1f, 1f), Rand(0.3f, 1f), Rand(-1f, 1f)).normalized * (impactSparkSpeed * Rand(0.5f, 1f) * _cellSize);
                Emit(Ps.Spark, p, v, impactSparkSize * w * _cellSize, impactSparkLife * Rand(0.7f, 1f), c, 0f);
            }
            Emit(Ps.RingFlat, p, Vector3.zero, impactRingSize * w * _cellSize, impactRingLife, _color, 0f);
            Emit(Ps.Glow, p, Vector3.zero, impactRingSize * 0.7f * w * _cellSize, impactRingLife * 0.5f, c, 0f);
        }

        /// <summary>흡입 — 빔 위 아무 점에서 드론 쪽으로 흐름 (점액 → 드론, 「빨아들임」). 드론에 닿을 때쯤 사라지게 수명 = 거리 / 속도.</summary>
        private void SuctionFlow(Vector3 drone, Vector3 end, int g)
        {
            Vector3 d = drone - end;
            float len = d.magnitude;
            if (len <= 0.001f) return;
            Vector3 dir = d / len;
            float speed = suctionFlowSpeed * _cellSize;
            float w = Pick(gradeWidth, g);
            int n = Count(suctionFlow, g);
            for (int i = 0; i < n; i++)
            {
                float t = Rand(0.05f, 0.85f); // 끝 → 드론 쪽 비율
                Vector3 p = end + d * t + Side(dir) * (Rand(-0.06f, 0.06f) * _cellSize * w);
                float life = Mathf.Clamp((1f - t) * len / speed, 0.05f, 0.4f);
                Emit(Ps.Glow, p, dir * speed, suctionFlowSize * w * _cellSize * Rand(0.7f, 1f), life, _color, 0f);
            }
        }

        /// <summary>흡입 끝 — 줄어드는 링 + 안쪽으로 도는 점.</summary>
        private void Swirl(Vector3 p, int g)
        {
            float w = Pick(gradeWidth, g);
            Emit(Ps.Swirl, p, Vector3.zero, swirlSize * w * _cellSize, swirlLife, _color, Rand(0f, 360f));
            int n = Count(swirlDots, g);
            float r = swirlRadius * w * _cellSize;
            float phase = Rand(0f, Mathf.PI * 2f);
            for (int i = 0; i < n; i++)
            {
                float a = phase + i * (Mathf.PI * 2f / Mathf.Max(1, n));
                Vector3 radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 tangent = new Vector3(-radial.z, 0f, radial.x);
                Vector3 v = (tangent * 1.6f - radial) * (r / swirlLife);
                Emit(Ps.Glow, p + radial * r, v, suctionFlowSize * w * _cellSize, swirlLife, _color, 0f);
            }
        }

        /// <summary>소각 — 부채꼴 안 아무 점에서 앞 · 위로 번지는 불꽃 (Fire 시스템 노이즈 · 색 변화).</summary>
        private void PlasmaFlames(Vector3 apex, Vector3 far, float farWidth, int g)
        {
            Vector3 d = far - apex;
            Vector3 dir = Flat(d);
            Vector3 side = Side(dir);
            float w = Pick(gradeWidth, g);
            int n = Count(plasmaFlames, g);
            for (int i = 0; i < n; i++)
            {
                float t = Rand(0.08f, 1f);
                Vector3 p = apex + d * t + side * (Rand(-0.42f, 0.42f) * farWidth * t);
                Vector3 v = dir * (plasmaFlameSpeed * Rand(0.6f, 1.1f) * _cellSize) + Vector3.up * (0.8f * _cellSize);
                Emit(Ps.Fire, p, v, plasmaFlameSize * w * _cellSize * (0.55f + 0.6f * t), plasmaFlameLife * Rand(0.7f, 1f), Color.white, Rand(0f, 360f));
            }
        }

        private void PlasmaEmbers(Vector3 far, float farWidth, int g)
        {
            Vector3 side = Vector3.right;
            float w = Pick(gradeWidth, g);
            Color c = Color.Lerp(_color, new Color(1f, 0.9f, 0.4f, 1f), 0.5f);
            int n = Count(plasmaEmbers, g);
            for (int i = 0; i < n; i++)
            {
                Vector3 p = far + side * (Rand(-0.5f, 0.5f) * farWidth);
                Vector3 v = new Vector3(Rand(-0.6f, 0.6f), Rand(1.2f, 2.2f), Rand(-0.6f, 0.6f)) * _cellSize;
                Emit(Ps.Spark, p, v, impactSparkSize * 0.8f * w * _cellSize, plasmaEmberLife * Rand(0.6f, 1f), c, 0f);
            }
        }

        /// <summary>냉각 — 발사구에서 타겟 쪽으로 뿜어져 느려지는 서리 가루 (Frost 시스템 감속).</summary>
        private void CryoSpray(Vector3 drone, Vector3 target, int g)
        {
            Vector3 d = target - drone;
            Vector3 dir = Flat(d);
            Vector3 side = Side(dir);
            Vector3 muzzle = drone + dir * (muzzleForward * _cellSize);
            float w = Pick(gradeWidth, g);
            Color c = Color.Lerp(_color, Color.white, 0.3f);
            int n = Count(cryoFlakes, g);
            for (int i = 0; i < n; i++)
            {
                Vector3 p = Vector3.Lerp(muzzle, target, Rand(0f, 0.35f));
                Vector3 v = (dir + side * Rand(-0.18f, 0.18f) + Vector3.up * Rand(-0.05f, 0.15f)) * (cryoFlakeSpeed * Rand(0.6f, 1f) * _cellSize);
                Emit(Ps.Frost, p, v, cryoFlakeSize * w * _cellSize * Rand(0.6f, 1f), cryoFlakeLife * Rand(0.7f, 1f), c, Rand(0f, 360f));
            }
        }

        /// <summary>냉각 — 맞은 점액에 서리 반짝 (작은 흰 별 · 거의 제자리).</summary>
        private void CryoGlint(Vector3 p, int g)
        {
            float w = Pick(gradeWidth, g);
            int n = Count(cryoGlints, g);
            for (int i = 0; i < n; i++)
            {
                Vector3 q = p + new Vector3(Rand(-0.25f, 0.25f), Rand(0f, 0.3f), Rand(-0.25f, 0.25f)) * _cellSize;
                Emit(Ps.Frost, q, Vector3.up * (0.3f * _cellSize), cryoFlakeSize * 0.8f * w * _cellSize, 0.3f, Color.white, Rand(0f, 360f));
            }
        }

        /// <summary>3등급 빙결 — 바닥 링이 퍼지고 얼음 파편이 바깥으로 터짐.</summary>
        private void FreezeBurst(Vector3 c, float radius)
        {
            Color ice = Color.Lerp(_color, Color.white, 0.45f);
            Emit(Ps.RingFlat, c, Vector3.zero, radius * 2.2f, 0.35f, _color, 0f);
            Emit(Ps.Glow, c, Vector3.zero, radius * 1.2f, 0.18f, ice, 0f);
            int n = freezeShards;
            float phase = Rand(0f, Mathf.PI * 2f);
            for (int i = 0; i < n; i++)
            {
                float a = phase + i * (Mathf.PI * 2f / Mathf.Max(1, n)) + Rand(-0.2f, 0.2f);
                Vector3 radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 v = (radial + Vector3.up * Rand(0.2f, 0.6f)) * (radius * freezeShardSpeed * Rand(0.7f, 1f));
                Emit(Ps.Spark, c + radial * (radius * 0.3f), v, impactSparkSize * 1.4f * _cellSize, 0.35f * Rand(0.8f, 1f), ice, 0f);
            }
            for (int i = 0; i < n / 2; i++)
            {
                Vector3 q = c + new Vector3(Rand(-1f, 1f), 0f, Rand(-1f, 1f)) * (radius * 0.8f);
                Emit(Ps.Frost, q, Vector3.up * (0.6f * _cellSize), cryoFlakeSize * 1.3f * _cellSize, 0.45f, ice, Rand(0f, 360f));
            }
        }

        private int Count(int baseCount, int grade) => Mathf.RoundToInt(baseCount * Pick(gradeCount, grade));

        private static Vector3 Flat(Vector3 d)
        {
            d.y = 0f;
            float m = d.magnitude;
            return m > 0.0001f ? d / m : Vector3.forward;
        }

        private static Vector3 Side(Vector3 dir) => new Vector3(dir.z, 0f, -dir.x);

        /// <summary>표시용 난수 (게임 난수 IRandom · UnityEngine.Random 을 건드리지 않게 따로) — xorshift32, 할당 없음.</summary>
        private static float Rand(float min, float max)
        {
            uint x = s_rng;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            s_rng = x;
            return min + (max - min) * ((x & 0xFFFFFF) / (float)0x1000000);
        }

        private static void Emit(Ps system, Vector3 position, Vector3 velocity, float size, float life, Color color, float rotation)
        {
            if (s_root == null) return;
            var p = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startSize = size,
                startLifetime = life,
                startColor = color,
                rotation = rotation,
                applyShapeToPosition = false
            };
            s_ps[(int)system].Emit(p, 1);
        }

        // ── 같이 쓰는 입자 만들기 (시작 때 한 번) ─────────────

        private void BuildShared(Transform parent)
        {
            Material mat = line != null ? line.sharedMaterial : quad != null ? quad.sharedMaterial : null;
            if (mat == null) return;

            var root = new GameObject("AttackFxParticles (U18a)").transform;
            root.SetParent(parent, false);
            s_root = root;

            Color hdr = new Color(particleIntensity, particleIntensity, particleIntensity, 1f);
            s_ps[(int)Ps.Glow] = Make(root, "Glow", mat, glowTexture, hdr, ParticleSystemRenderMode.Billboard,
                Curve(0f, 0.6f, 0.15f, 1f, 1f, 0f));
            ParticleSystem spark = Make(root, "Spark", mat, sparkTexture, hdr, ParticleSystemRenderMode.Stretch,
                Curve(0f, 1f, 0.5f, 0.8f, 1f, 0f));
            var main = spark.main;
            main.gravityModifier = 0.5f;
            s_ps[(int)Ps.Spark] = spark;

            ParticleSystem frost = Make(root, "Frost", mat, frostTexture, hdr, ParticleSystemRenderMode.Billboard,
                Curve(0f, 0.5f, 0.25f, 1f, 1f, 0.2f));
            var limit = frost.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 0f;
            limit.dampen = 0.12f; // 뿜어진 뒤 느려짐
            var rot = frost.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
            s_ps[(int)Ps.Frost] = frost;

            ParticleSystem fire = Make(root, "Fire", mat, glowTexture, hdr, ParticleSystemRenderMode.Billboard,
                Curve(0f, 0.5f, 0.3f, 1.15f, 1f, 0.1f));
            var noise = fire.noise;
            noise.enabled = true;
            noise.strength = plasmaNoise;
            noise.frequency = plasmaNoiseFrequency;
            noise.scrollSpeed = 1.5f;
            noise.quality = ParticleSystemNoiseQuality.Low;
            var col = fire.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(Gradient4(
                new Color(1f, 0.95f, 0.7f), new Color(1f, 0.6f, 0.15f), new Color(0.9f, 0.2f, 0.05f)));
            s_ps[(int)Ps.Fire] = fire;

            s_ps[(int)Ps.RingFlat] = Make(root, "RingFlat", mat, ringTexture, hdr, ParticleSystemRenderMode.HorizontalBillboard,
                Curve(0f, 0.3f, 0.4f, 0.85f, 1f, 1f));
            s_ps[(int)Ps.Swirl] = Make(root, "Swirl", mat, ringTexture, hdr, ParticleSystemRenderMode.HorizontalBillboard,
                Curve(0f, 1f, 0.6f, 0.45f, 1f, 0.1f));

            // 데우기 — 버퍼를 최대 수까지 한 번 채웠다 비운다 (웨이브 중 첫 발사에 메모리를 늘리지 않게)
            Vector3 hidden = new Vector3(0f, -1000f, 0f);
            for (int i = 0; i < s_ps.Length; i++)
            {
                var p = new ParticleSystem.EmitParams { position = hidden, startLifetime = 0.01f, startSize = 0f, applyShapeToPosition = false };
                s_ps[i].Emit(p, maxParticles);
                s_ps[i].Simulate(0.02f, false, false);
                s_ps[i].Clear(false);
                s_ps[i].Play(false);
            }
        }

        private ParticleSystem Make(Transform root, string name, Material mat, Texture2D tex, Color hdr,
                                    ParticleSystemRenderMode mode, AnimationCurve size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 1f;
            main.loop = true;
            main.playOnAwake = false;
            main.startSpeed = 0f;
            main.startLifetime = 0.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.maxParticles = maxParticles;
            main.useUnscaledTime = false; // 일시정지(timeScale 0) 때 멈춤

            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, size);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(Gradient4(Color.white, Color.white, Color.white));

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = mode;
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingFudge = 0f;
            if (mode == ParticleSystemRenderMode.Stretch)
            {
                r.velocityScale = 0.05f;
                r.lengthScale = 1.5f;
            }
            var block = new MaterialPropertyBlock();
            if (tex != null) block.SetTexture(BaseMapId, tex);
            block.SetColor(BaseColorId, hdr);
            r.SetPropertyBlock(block);
            return ps;
        }

        private static AnimationCurve Curve(float t0, float v0, float t1, float v1, float t2, float v2)
        {
            return new AnimationCurve(new Keyframe(t0, v0), new Keyframe(t1, v1), new Keyframe(t2, v2));
        }

        /// <summary>색 3단계 + 알파 1 → 1 → 0 (끝에 사라짐).</summary>
        private static Gradient Gradient4(Color a, Color b, Color c)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 0.4f), new GradientColorKey(c, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.5f), new GradientAlphaKey(0f, 1f) });
            return g;
        }
    }
}
