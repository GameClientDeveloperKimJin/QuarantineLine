using MoreMountains.Feedbacks;
using QL.Core;
using QL.FX.Motion;
using QL.Game;
using QL.Game.Bootstrap;
using QL.Game.Slimes;
using UnityEngine;

namespace QL.FX.Feel
{
    /// <summary>손맛 순간 (U18b — Feel MMF_Player 한 개씩).</summary>
    public enum FeelMoment
    {
        Merge = 0,
        Kill = 1,
        Hit = 2,
        Hatch = 3,
        Clean = 4,
        Spawn = 5,
    }

    /// <summary>
    /// U18b — 전투 손맛 순간을 Feel <see cref="MMF_Player"/> 로 (MotionFx.prefab/Feel). 코드는 「위치 · 색 정하고 재생」 한 줄,
    /// 효과 묶음(입자 터짐 · 링 · 카메라 Kick)과 수치는 각 MMF_Player 인스펙터에 있다 (S3 규칙 9 — FxConfig 대신 허용).
    /// <list type="bullet">
    /// <item>순간마다 MMF_Player 1개 (프리팹 안에 미리 — Instantiate 0). 입자는 MMF_Particles 의 Emit (월드 공간) → 여러 곳에서 동시에 터져도 플레이어 하나로 된다</item>
    /// <item>같은 순간이 한 프레임에 여러 번 오면 <see cref="MomentSlot.maxPerFrame"/> 까지만 (동시 처치 · 연타 히트에 눈이 피곤하지 않게)</item>
    /// <item>시작 때 Initialization + 화면 밖에서 한 번 데우기 (첫 재생 할당을 시작으로 당김)</item>
    /// <item>게임 시간 (Scaled) — 일시정지 · 결과 (timeScale 0) 에선 입자 · 피드백이 멈춘다</item>
    /// </list>
    /// 기존 DOTween 연출 (DroneMotion 머지 · 팝, HitFlashFx, SlimeDeathFx, ContaminationFx, CameraShakeFx 피격) 은 그대로 두고 그 위에 덧붙인다 —
    /// 드론 · 점액 Model 의 크기 · 위치는 DroneMotion · SlimeWobbleFx 가 한 곳에서 합쳐 쓰므로 MMF 가 직접 만지지 않는다 (서로 덮어씀).
    /// 구독만 — 게임 상태를 바꾸지 않는다.
    /// </summary>
    public sealed class FeelMomentsFx : MonoBehaviour
    {
        public const int MomentCount = 6;

        [System.Serializable]
        public sealed class MomentSlot
        {
            public MMF_Player player;
            [Tooltip("재생 전 startColor 를 순간 색으로 바꿀 입자 (비우면 인스펙터 색 그대로)")]
            public ParticleSystem[] tinted = new ParticleSystem[0];
            [Tooltip("한 프레임에 이 순간을 재생하는 최대 횟수")]
            public int maxPerFrame = 2;
            [Tooltip("재생 위치 높이 더하기 (월드 y)")]
            public float heightOffset;

            [System.NonSerialized] public int Frame = -1;
            [System.NonSerialized] public int CountInFrame;
            [System.NonSerialized] public int Plays;
            [System.NonSerialized] public int Skipped;
            [System.NonSerialized] public ParticleSystem[] All;
        }

        [Tooltip("FeelMoment 순서: Merge · Kill · Hit · Hatch · Clean · Spawn")]
        [SerializeField] private MomentSlot[] slots = new MomentSlot[MomentCount];
        [SerializeField] private FxConfig fx;
        [SerializeField] private DroneIdleFx motions;
        [Tooltip("씬 인스턴스에서 지정. 비어 있으면 처음 한 번 찾는다")]
        [SerializeField] private GameBootstrap bootstrap;
        [Tooltip("데우기 위치 (화면 밖)")]
        [SerializeField] private Vector3 warmPosition = new Vector3(0f, -200f, 0f);

        public bool IsReady { get; private set; }

        public MMF_Player PlayerOf(FeelMoment m) => Slot(m)?.player;
        public int PlayCountOf(FeelMoment m) => Slot(m) != null ? Slot(m).Plays : 0;
        public int SkippedOf(FeelMoment m) => Slot(m) != null ? Slot(m).Skipped : 0;
        public int MaxPerFrameOf(FeelMoment m) => Slot(m) != null ? Slot(m).maxPerFrame : 0;
        public bool IsPlaying(FeelMoment m) { MMF_Player p = PlayerOf(m); return p != null && p.IsPlaying; }

        /// <summary>그 순간 플레이어 아래 입자 수 합 (테스트 · 캡처용 — 일시정지 멈춤 확인).</summary>
        public int ParticleCountOf(FeelMoment m)
        {
            MomentSlot s = Slot(m);
            if (s == null || s.All == null) return 0;
            int n = 0;
            for (int i = 0; i < s.All.Length; i++) if (s.All[i] != null) n += s.All[i].particleCount;
            return n;
        }

        /// <summary>그 순간 플레이어의 피드백 수 (인스펙터 구성 확인용).</summary>
        public int FeedbackCountOf(FeelMoment m)
        {
            MMF_Player p = PlayerOf(m);
            return p != null && p.FeedbacksList != null ? p.FeedbacksList.Count : 0;
        }

        /// <summary>그 순간 플레이어가 카메라 Kick (CameraShakeFx 경로) 을 쓰는지.</summary>
        public bool UsesCameraKick(FeelMoment m)
        {
            MMF_Player p = PlayerOf(m);
            return p != null && p.GetFeedbackOfType<MMF_QLCameraKick>() != null;
        }

        /// <summary>이 순간이 Feel 이 직접 소유한 플레이어 · 입자만 쓰는지 (timescale · 카메라 shaker 피드백이 없는지).</summary>
        public bool HasForbiddenFeedback(FeelMoment m) => IsForbidden(PlayerOf(m));

        private static bool IsForbidden(MMF_Player p)
        {
            if (p == null || p.FeedbacksList == null) return false;
            for (int i = 0; i < p.FeedbacksList.Count; i++)
            {
                MMF_Feedback f = p.FeedbacksList[i];
                if (f is MMF_TimescaleModifier || f is MMF_FreezeFrame || f is MMF_CameraShake) return true;
            }
            return false;
        }

        /// <summary>S3 규칙 9 검사 (테스트용) — 열린 씬의 모든 MMF_Player 중 금지 피드백 (timescale · FreezeFrame · CameraShake) 을 가진 수 + 메인 카메라의 Feel 카메라 shaker 수.</summary>
        public static int CountForbiddenInScene(out int players)
        {
            MMF_Player[] all = FindObjectsByType<MMF_Player>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            players = all.Length;
            int bad = 0;
            for (int i = 0; i < all.Length; i++) if (IsForbidden(all[i])) bad++;
            Camera main = Camera.main;
            if (main != null) bad += main.GetComponentsInChildren<MMCameraShaker>(true).Length + main.GetComponentsInParent<MMCameraShaker>(true).Length;
            return bad;
        }

        private MomentSlot Slot(FeelMoment m)
        {
            int i = (int)m;
            return slots != null && i >= 0 && i < slots.Length ? slots[i] : null;
        }

        private void Awake()
        {
            if (motions == null) motions = GetComponentInParent<DroneIdleFx>();
        }

        private void Start()
        {
            // 초기화 + 데우기 (화면 밖에서 세기 0 으로 한 번 재생 → 입자 지움). 세기 0 = 카메라 Kick 없음 (시작 프레임 카메라가 흔들리지 않게)
            for (int i = 0; i < slots.Length; i++)
            {
                MomentSlot s = slots[i];
                if (s == null || s.player == null) continue;
                s.player.Initialization();
                s.player.PlayFeedbacks(warmPosition, 0f);
                s.player.StopFeedbacks();
                for (int k = 0; k < s.tinted.Length; k++) if (s.tinted[k] != null) s.tinted[k].Clear(true);
                s.All = s.player.GetComponentsInChildren<ParticleSystem>(true);
                for (int k = 0; k < s.All.Length; k++) s.All[k].Clear(true);
            }
            IsReady = true;
        }

        private void OnEnable()
        {
            GameEvents.DroneMerged += OnMerged;
            GameEvents.SlimeKilled += OnKilled;
            GameEvents.SlimeDamaged += OnDamaged;
            GameEvents.SlimeReachedHatch += OnReached;
            GameEvents.CellCleaned += OnCleaned;
            GameEvents.DroneSpawned += OnSpawned;
        }

        private void OnDisable()
        {
            GameEvents.DroneMerged -= OnMerged;
            GameEvents.SlimeKilled -= OnKilled;
            GameEvents.SlimeDamaged -= OnDamaged;
            GameEvents.SlimeReachedHatch -= OnReached;
            GameEvents.CellCleaned -= OnCleaned;
            GameEvents.DroneSpawned -= OnSpawned;
        }

        // ── 이벤트 ───────────────────────────────────────────

        private void OnMerged(int from, int to, DroneType type, int newGrade)
        {
            if (!EnsureReady()) return;
            DroneMotion m = motions != null ? motions.MotionAt(to) : null;
            Vector3 pos = m != null && m.View != null ? m.View.transform.position : bootstrap.Space.CellToWorld(to);
            Play(FeelMoment.Merge, pos, Tint(type, newGrade), true);
        }

        private void OnSpawned(int cell, DroneType type, int grade)
        {
            if (!EnsureReady()) return;
            Vector3 w = bootstrap.Space.CellToWorld(cell);
            w.y = fx != null ? fx.worldFxHeight : w.y;
            Play(FeelMoment.Spawn, w, Tint(type, grade), true);
        }

        private void OnCleaned(int cell, bool byMerge)
        {
            if (!byMerge || !EnsureReady()) return;
            Vector3 w = bootstrap.Space.CellToWorld(cell);
            w.y = fx != null ? fx.worldFxHeight : w.y;
            Play(FeelMoment.Clean, w, default, false);
        }

        private void OnKilled(SlimeView slime)
        {
            if (slime == null) return;
            Play(FeelMoment.Kill, slime.transform.position, default, false);
        }

        private void OnDamaged(SlimeView slime, int damage, DroneType source)
        {
            if (slime == null) return;
            Play(FeelMoment.Hit, slime.transform.position, default, false);
        }

        private void OnReached(SlimeView slime)
        {
            if (slime == null) return;
            Play(FeelMoment.Hatch, slime.transform.position, default, false);
        }

        // ── 재생 ─────────────────────────────────────────────

        /// <summary>순간 하나 재생 (한 프레임 상한 넘으면 건너뜀). 테스트 · 캡처도 이 길.</summary>
        public bool Play(FeelMoment moment, Vector3 position, Color tint, bool useTint)
        {
            MomentSlot s = Slot(moment);
            if (s == null || s.player == null || !IsReady) return false;
            int frame = Time.frameCount;
            if (s.Frame != frame)
            {
                s.Frame = frame;
                s.CountInFrame = 0;
            }
            if (s.CountInFrame >= s.maxPerFrame)
            {
                s.Skipped++;
                return false;
            }
            s.CountInFrame++;
            if (useTint)
            {
                for (int k = 0; k < s.tinted.Length; k++)
                {
                    ParticleSystem ps = s.tinted[k];
                    if (ps == null) continue;
                    ParticleSystem.MainModule main = ps.main;
                    main.startColor = tint;
                }
            }
            position.y += s.heightOffset;
            s.player.PlayFeedbacks(position);
            s.Plays++;
            return true;
        }

        private Color Tint(DroneType type, int grade)
        {
            Color c = motions != null && motions.Board != null ? motions.Board.TintOf(type, grade) : Color.white;
            c.a = 1f;
            return c;
        }

        private bool EnsureReady()
        {
            if (bootstrap == null) bootstrap = FindAnyObjectByType<GameBootstrap>();
            return bootstrap != null && bootstrap.IsReady && bootstrap.Space != null;
        }
    }
}
