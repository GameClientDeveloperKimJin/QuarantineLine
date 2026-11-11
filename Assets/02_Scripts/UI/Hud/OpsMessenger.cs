using QL.Core;
using QL.FX;
using QL.FX.Alert;
using QL.Game;
using QL.Game.Bootstrap;
using QL.Game.Flow;
using QL.Game.Slimes;
using QL.UI.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QL.UI.Hud
{
    /// <summary>
    /// U16 관제실 메신저 (InGameScreen/Hud/OpsMessenger — 화면 왼쪽 아래, 앵커 Bottom Left). 시안 1-3 · 원본 WO-UI-03.
    /// 기존 이벤트 5종을 트리거로 (<see cref="OpsTrigger"/>) 관제실 대사 한 줄을 「입력 중 ●●●」 뒤에 말풍선으로 띄우고, 대사에 나온 위치 바닥에 핑 (<see cref="LanePingFx"/>).
    /// <list type="bullet">
    /// <item>대사 · 우선순위 · 핑 여부는 <see cref="OpsLineTable"/> SO, 시점 · 세기는 FxConfig 「U16」. 같은 트리거 대사는 목록 순서대로 돌림. <c>{lane}</c> = 1부터</item>
    /// <item>같은 프레임에 여러 개 → 가장 높은 것 하나 (LateUpdate 에서 결정). 전체 쿨다운 8초 — 그 안엔 마지막으로 띄운 것보다 우선순위가 높을 때만 바로, 같거나 낮으면 버림 (쌓지 않음)</item>
    /// <item>해치 위기 · 최고 등급 결합은 판에 1번 (다시 하기 = 씬 재로드로 초기화)</item>
    /// <item>휴게(Paused) · 결과(Result) 중엔 새 메시지 없음 (<see cref="GameLoop.CanAct"/>). 떠 있는 것은 게임 시간이라 같이 멈춤</item>
    /// <item>0.00 입력 줄 · 0.50 이전 말풍선 위로 밀림 + 흐려짐, 새 말풍선 팝, 핑 · 팝 + 4.0 접힘 → 풀 반납. 말풍선은 화면에 최대 2개 (세 번째가 오면 가장 오래된 것부터 접힘)</item>
    /// </list>
    /// 위치 = R3 <c>GridSpace</c> 읽기 (변이 = 그 점액 레인 · 위치, 격리선에서 최대 opsPingPathMaxDist 칸 (U17 — 10 = 스폰선, 게이트가 보임) · 피격 = 그 레인 격리선 <c>PathToWorld(lane, 0)</c> · 오염 = <c>CellToWorld(cell)</c>).
    /// 할당: 메시지마다 {lane} 치환 문자열 1회만. 대기 · 연출 프레임 0. 클릭은 받지 않는다 (raycastTarget 끔).
    /// </summary>
    public sealed class OpsMessenger : MonoBehaviour
    {
        /// <summary>화면에 동시에 두는 말풍선 수 (원본 규칙).</summary>
        public const int MaxVisible = 2;
        private const int TriggerCount = 5;

        [SerializeField] private HudContext context;
        [SerializeField] private OpsLineTable table;

        [Header("구조")]
        [SerializeField] private TMP_Text channelText;
        [Tooltip("플레이 시간 mm:ss (Time.timeSinceLevelLoad — U12 근무 시간과 같은 값)")]
        [SerializeField] private TMP_Text clockText;
        [Tooltip("말풍선 풀 (미리 3개 — 2개 + 접히는 중 1개). 아래 칸 위치 = 첫 말풍선의 위치")]
        [SerializeField] private OpsBubble[] bubbles;
        [SerializeField] private GameObject typingRow;
        [SerializeField] private TMP_Text typingText;
        [Tooltip("「●●●」 점 3개 — 알파 Fade 루프")]
        [SerializeField] private Graphic[] typingDots;
        [Tooltip("비어 있으면 처음 한 번 찾는다 ([FX]/AlertFx)")]
        [SerializeField] private LanePingFx ping;

        private readonly int[] _entry = new int[TriggerCount];
        private readonly int[] _rotation = new int[TriggerCount];
        private float _bottomY;
        private float _now;
        private float _lastShownAt = float.NegativeInfinity;
        private int _lastPriority = int.MaxValue;
        private int _serial;
        private int _clockSeconds = -1;
        private bool _usedCritical;
        private bool _usedTopMerge;

        // 이번 프레임 후보 (가장 높은 것 하나)
        private bool _hasCandidate;
        private OpsTrigger _candTrigger;
        private int _candPriority;
        private int _candLane;
        private bool _candHasPos;
        private Vector3 _candPos;

        // 입력 중인 메시지
        private bool _typing;
        private float _typingT;
        private string _msgLine;
        private bool _msgPing;
        private Vector3 _msgPos;

        public OpsLineTable Table => table;
        public LanePingFx PingFx => EnsurePing();
        public bool IsTyping => _typing;
        public float TypingElapsed => _typingT;
        public GameObject TypingRow => typingRow;
        public Graphic TypingDot(int i) => typingDots[i];
        public TMP_Text ChannelText => channelText;
        public TMP_Text ClockText => clockText;
        public TMP_Text TypingText => typingText;
        public int BubbleCount => bubbles.Length;
        public OpsBubble BubbleAt(int i) => bubbles[i];
        public float BottomY => _bottomY;
        /// <summary>마지막으로 받아들인 메시지 (입력 중 포함).</summary>
        public OpsTrigger LastTrigger { get; private set; }
        public string LastLine => _msgLine;
        /// <summary>마지막 메시지 레인 (0부터, 없으면 −1).</summary>
        public int LastLane { get; private set; } = -1;
        /// <summary>받아들인 메시지 수 (입력 시작 기준).</summary>
        public int AcceptedCount { get; private set; }
        /// <summary>쿨다운 · 우선순위로 버린 수.</summary>
        public int DroppedCount { get; private set; }
        /// <summary>말풍선이 팝한 수.</summary>
        public int PopCount { get; private set; }
        /// <summary>쿨다운 남은 시간 (초, 게임 시간).</summary>
        public float CooldownLeft => Mathf.Max(0f, context.Fx.opsCooldown - (_now - _lastShownAt));

        /// <summary>화면에 떠 있는 말풍선 (접히는 중 제외).</summary>
        public int VisibleCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < bubbles.Length; i++)
                    if (bubbles[i].State == OpsBubble.Phase.Shown) n++;
                return n;
            }
        }

        /// <summary>켜져 있는 말풍선 (접히는 중 포함).</summary>
        public int ActiveBubbleCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < bubbles.Length; i++)
                    if (bubbles[i].State != OpsBubble.Phase.Free) n++;
                return n;
            }
        }

        private void Awake()
        {
            FxConfig fx = context.Fx;
            for (int i = 0; i < TriggerCount; i++) _entry[i] = table != null ? table.IndexOf((OpsTrigger)i) : -1;
            _bottomY = bubbles[0].GetComponent<RectTransform>().anchoredPosition.y;

            Graphic[] gs = GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < gs.Length; i++) gs[i].raycastTarget = false; // 소환 · 드래그를 막지 않는다

            if (table != null)
            {
                channelText.SetText(table.Channel);
                typingText.SetText(table.Typing);
            }
            string speaker = table != null ? table.Speaker : string.Empty;
            string longest = LongestLine();
            for (int i = 0; i < bubbles.Length; i++) bubbles[i].Init(fx, speaker, longest);
            typingRow.SetActive(false);
        }

        private void Start()
        {
            EnsurePing(); // 첫 핑 때 찾지 않게 (웨이브 중 Find 없음)
        }

        /// <summary>표에서 가장 긴 대사 (치환 전 — {lane} 이 숫자보다 길어 치환 뒤보다 길거나 같다).</summary>
        private string LongestLine()
        {
            string longest = string.Empty;
            if (table == null) return longest;
            for (int e = 0; e < table.EntryCount; e++)
            {
                string[] lines = table.EntryAt(e).lines;
                if (lines == null) continue;
                for (int l = 0; l < lines.Length; l++)
                    if (lines[l] != null && lines[l].Length > longest.Length) longest = lines[l];
            }
            return longest;
        }

        private void OnEnable()
        {
            GameEvents.HatchHpChanged += OnHatchHp;
            GameEvents.SlimeSpawned += OnSlimeSpawned;
            GameEvents.SlimeReachedHatch += OnSlimeReachedHatch;
            GameEvents.CellContaminated += OnCellContaminated;
            GameEvents.DroneMerged += OnDroneMerged;
            GameEvents.GameEnded += OnGameEnded;
        }

        private void OnDisable()
        {
            GameEvents.HatchHpChanged -= OnHatchHp;
            GameEvents.SlimeSpawned -= OnSlimeSpawned;
            GameEvents.SlimeReachedHatch -= OnSlimeReachedHatch;
            GameEvents.CellContaminated -= OnCellContaminated;
            GameEvents.DroneMerged -= OnDroneMerged;
            GameEvents.GameEnded -= OnGameEnded;
        }

        // ── 트리거 ───────────────────────────────────────────

        private void OnHatchHp(int now, int max)
        {
            if (_usedCritical) return;
            GameBootstrap b = context.Bootstrap;
            int criticalHp = b != null && b.Data != null ? b.Data.Config.HatchCriticalHp : 0;
            if (now > 0 && now <= criticalHp) Offer(OpsTrigger.HatchCritical, -1, false, default); // = HatchModel.IsCritical
        }

        private void OnSlimeSpawned(SlimeView slime)
        {
            if (slime == null || slime.Type != SlimeType.Mutant) return;
            GameBootstrap b = context.Bootstrap;
            if (b == null || !b.IsReady) return;
            // 그 점액 레인 · 위치 — opsPingPathMaxDist 칸까지 (U17: U10 뒤 게이트가 화면에 보여 8 → 10 = 스폰선. 더 긴 길이 오면 안쪽으로 당김)
            float dist = Mathf.Min(slime.Dist, context.Fx.opsPingPathMaxDist);
            Offer(OpsTrigger.MutantSpawned, slime.Lane, true, b.Space.PathToWorld(slime.Lane, dist));
        }

        private void OnSlimeReachedHatch(SlimeView slime)
        {
            if (slime == null) return;
            GameBootstrap b = context.Bootstrap;
            if (b == null || !b.IsReady) return;
            Offer(OpsTrigger.HatchBreached, slime.Lane, true, b.Space.PathToWorld(slime.Lane, 0f));
        }

        private void OnCellContaminated(int cell, int lane, float duration)
        {
            GameBootstrap b = context.Bootstrap;
            if (b == null || !b.IsReady) return;
            Offer(OpsTrigger.CellContaminated, lane, true, b.Space.CellToWorld(cell));
        }

        private void OnDroneMerged(int from, int to, DroneType type, int newGrade)
        {
            if (_usedTopMerge) return;
            GameBootstrap b = context.Bootstrap;
            if (b == null || !b.IsReady || newGrade < b.Catalog.MaxGrade(type)) return;
            Offer(OpsTrigger.TopGradeMerge, -1, false, default);
        }

        private void OnGameEnded(ResultSummary summary)
        {
            // 결과 화면 — 새 메시지 없음 · 입력 중이던 것 취소 (떠 있는 말풍선 · 핑은 timeScale 0 으로 그대로 멈춤)
            _hasCandidate = false;
            StopTyping();
        }

        /// <summary>이번 프레임 후보 — 우선순위가 더 높은 (숫자 작은) 것만 남긴다. 휴게 · 결과 중엔 무시.</summary>
        private void Offer(OpsTrigger trigger, int lane, bool hasPos, Vector3 pos)
        {
            int e = _entry[(int)trigger];
            if (e < 0 || !CanSpeak()) return;
            int priority = table.EntryAt(e).priority;
            if (_hasCandidate && priority >= _candPriority) return;
            _hasCandidate = true;
            _candTrigger = trigger;
            _candPriority = priority;
            _candLane = lane;
            _candHasPos = hasPos;
            _candPos = pos;
        }

        private bool CanSpeak()
        {
            GameLoop loop = context.Loop;
            return loop != null && loop.IsReady && loop.CanAct;
        }

        private void LateUpdate()
        {
            if (!_hasCandidate) return;
            _hasCandidate = false;
            if (!CanSpeak()) return;

            bool cooling = _now - _lastShownAt < context.Fx.opsCooldown;
            if (cooling && _candPriority >= _lastPriority)
            {
                DroppedCount++;
                return;
            }
            Begin();
        }

        private void Begin()
        {
            OpsTrigger t = _candTrigger;
            OpsLineTable.Entry entry = table.EntryAt(_entry[(int)t]);
            string line = table.Line(t, _rotation[(int)t]);
            _rotation[(int)t]++;
            _msgLine = _candLane >= 0 ? OpsLineTable.Format(line, _candLane) : line; // 메시지당 1회 할당 허용
            _msgPing = entry.ping && _candHasPos;
            _msgPos = _candPos;
            if (t == OpsTrigger.HatchCritical) _usedCritical = true;
            if (t == OpsTrigger.TopGradeMerge) _usedTopMerge = true;

            LastTrigger = t;
            LastLane = _candLane;
            AcceptedCount++;
            _lastShownAt = _now;
            _lastPriority = _candPriority;

            _typing = true; // 입력 중에 다른 메시지가 끼어들면 그것으로 바꾸고 0.00 부터
            _typingT = 0f;
            if (!typingRow.activeSelf) typingRow.SetActive(true);
            ApplyDots();
        }

        // ── 시간 ─────────────────────────────────────────────

        private void Update()
        {
            int secs = Mathf.FloorToInt(Time.timeSinceLevelLoad);
            if (secs != _clockSeconds)
            {
                _clockSeconds = secs;
                clockText.SetText("{0:00}:{1:00}", secs / 60, secs % 60);
            }

            float dt = Time.deltaTime;
            if (dt <= 0f) return; // 일시정지 · 결과 — 그대로 멈춤
            _now += dt;
            FxConfig fx = context.Fx;

            for (int i = 0; i < bubbles.Length; i++)
            {
                OpsBubble b = bubbles[i];
                if (b.State != OpsBubble.Phase.Shown) continue;
                b.Tick(dt);
                if (b.Age >= fx.opsBubbleLife) b.Collapse();
            }

            if (_typing)
            {
                _typingT += dt;
                if (_typingT >= fx.opsTypingDuration) Pop();
                else ApplyDots();
            }
        }

        private void ApplyDots()
        {
            FxConfig fx = context.Fx;
            for (int i = 0; i < typingDots.Length; i++)
            {
                float t = _typingT - i * fx.opsDotStagger;
                float f = t <= 0f ? 0f : Mathf.PingPong(t * 2f / fx.opsDotPeriod, 1f);
                typingDots[i].canvasRenderer.SetAlpha(Mathf.Lerp(1f, fx.opsDotAlphaMin, f));
            }
        }

        private void StopTyping()
        {
            _typing = false;
            if (typingRow.activeSelf) typingRow.SetActive(false);
        }

        /// <summary>0.50 — 입력 줄 숨김 · 이전 말풍선 밀림 (2개였으면 가장 오래된 것 접힘) · 새 말풍선 팝 · 핑.</summary>
        private void Pop()
        {
            StopTyping();

            // 떠 있는 것이 이미 MaxVisible 개면 가장 오래된 것부터 접는다
            while (VisibleCount >= MaxVisible)
            {
                OpsBubble oldest = Oldest(OpsBubble.Phase.Shown);
                if (oldest == null) break;
                oldest.Collapse();
            }
            for (int i = 0; i < bubbles.Length; i++)
                if (bubbles[i].State == OpsBubble.Phase.Shown && !bubbles[i].IsDim) bubbles[i].Push();

            OpsBubble next = Free();
            if (next == null)
            {
                next = Oldest(OpsBubble.Phase.Collapsing); // 접히는 중인 것을 바로 반납
                if (next == null) return;
                next.ReleaseNow();
            }
            next.Show(_msgLine, _bottomY, ++_serial);
            PopCount++;

            if (_msgPing)
            {
                LanePingFx p = EnsurePing();
                if (p != null) p.Ping(_msgPos);
            }
        }

        private OpsBubble Free()
        {
            for (int i = 0; i < bubbles.Length; i++)
                if (bubbles[i].State == OpsBubble.Phase.Free) return bubbles[i];
            return null;
        }

        private OpsBubble Oldest(OpsBubble.Phase phase)
        {
            OpsBubble best = null;
            for (int i = 0; i < bubbles.Length; i++)
            {
                OpsBubble b = bubbles[i];
                if (b.State != phase) continue;
                if (best == null || b.Serial < best.Serial) best = b;
            }
            return best;
        }

        private LanePingFx EnsurePing()
        {
            if (ping == null) ping = FindAnyObjectByType<LanePingFx>();
            return ping;
        }
    }
}
