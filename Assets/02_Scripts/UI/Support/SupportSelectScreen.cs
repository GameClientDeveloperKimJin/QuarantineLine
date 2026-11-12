using System;
using System.Collections.Generic;
using DG.Tweening;
using DG.Tweening.Core;
using QL.Core;
using QL.Core.Support;
using QL.FX;
using QL.Game;
using QL.Game.Flow;
using QL.UI.Hud;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace QL.UI.Support
{
    /// <summary>
    /// U19 업무 지원 신청 화면 (UIRoot/SupportScreen — 자기 Canvas, 정렬 = 인게임 위 · 휴게/결과 아래). 시안 1 · 기획서 「선택 화면 UI · 연출」.
    /// <c>SupportOffered</c> (웨이브 1 · 2 클리어 — 준비 시간이 이미 멈춤) → U14 영수증이 돌고 있으면 그 끝 (<see cref="PayslipReceipt.Finished"/>) 뒤에 연다.
    /// 등장: 0.00 어둡게 (U12 와 같은 0.85 규칙) · 0.15 카드 3장 아래 +80 → 0 (OutBack, 0.08초씩). 호버 = 1.05 · Y+10.
    /// 클릭 또는 1 · 2 · 3 (숫자패드 포함) — <b>첫 입력만</b>, 그 뒤 연출 끝까지 잠금:
    /// 0.00 「승인」 도장 1.6 → 1 (InBack) · 0.15 카드 펀치 · 나머지 2장 아래 −60 + Fade · 0.45 고른 카드 → HUD 칩 자리 (Scale 0.2, InQuad) + 어둡게 걷힘 ·
    /// (HUD 칩 줄은 열려 있는 동안 숨김 — 이때 다시 보임) · 0.80 닫힘 → <see cref="GameLoop.PickSupport"/> (= 준비 시간 재개 · ModifiersChanged → HUD 칩) → 칩 펀치. 작업 지시서 (U15) 는 그 뒤 (SupportPicked).
    /// 실제 시간 (SetUpdate(true)) — 단 일시정지 (Esc 휴게) 동안엔 시퀀스도 멈추고 입력을 받지 않는다 (닫으면 그대로 이어서).
    /// 다른 길로 골라졌으면 (SupportPicked 가 먼저 옴 — 테스트 도우미) 즉시 닫는다. 판 끝 · 다시 하기 = 즉시 닫음.
    /// 상태를 바꾸는 호출은 <see cref="GameLoop.PickSupport"/> 하나 (S3 규칙 1). 시퀀스 2개 · 카드 호버 3개는 Awake 에 한 번 (SetAutoKill(false) + Restart).
    /// 다시 뽑기 (R) 는 직급 인턴이라 잠김 — 버튼을 두지 않는다.
    /// </summary>
    public sealed class SupportSelectScreen : MonoBehaviour
    {
        public const int MaxCards = 3;

        [SerializeField] private HudContext context;
        [Tooltip("돌고 있으면 끝(Finished)을 기다린다 (영수증 → 선택 → 지시서)")]
        [SerializeField] private PayslipReceipt payslip;
        [Tooltip("고른 카드가 날아갈 HUD 칩 줄")]
        [SerializeField] private SupportChipBar chipBar;
        [Tooltip("열려 있는 동안 숨기는 아래쪽 HUD (드론 투입 버튼 · 관제실 메신저) — 카드 · 키캡 · 아래 줄과 겹치지 않게 (16:10 실측). 고른 카드가 날아가기 시작하면 다시 보임")]
        [SerializeField] private CanvasGroup[] hideWhileOpen = new CanvasGroup[0];

        [Header("구조")]
        [Tooltip("보였다 숨는 본체 (켜고 끔)")]
        [SerializeField] private GameObject content;
        [SerializeField] private CanvasGroup contentGroup;
        [SerializeField] private Image dim;
        [Tooltip("제목 · 부제 · 아래 「적용 중 지원」 줄 — 어둡게와 같이 나타나고 걷힘")]
        [SerializeField] private CanvasGroup chrome;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text subtitleLabel;
        [SerializeField] private SupportCardView[] cards = new SupportCardView[MaxCards];
        [Tooltip("카드 사이 거리 (px, 가운데 기준) — 장 수에 맞춰 가운데 정렬")]
        [SerializeField] private float cardSpacing = 440f;

        [Header("아래 「적용 중 지원」 줄")]
        [SerializeField] private TMP_Text activeLabel;
        [SerializeField] private SupportChip[] previewChips = new SupportChip[2];
        [SerializeField] private RectTransform emptySlot;
        [SerializeField] private TMP_Text emptyLabel;
        [Tooltip("줄 칸 사이 (px)")]
        [SerializeField] private float rowGap = 16f;

        private Sequence _open;
        private Sequence _pick;
        private float _dim;
        private float _dimOut;
        private float _stamp;
        private float _punch;
        private float _out;
        private float _fly;
        private int _count;
        private int _picked = -1;
        private bool _waiting;
        private bool _shown;
        private bool _locked;
        private bool _committing;
        private bool _pendingCommit;
        private bool _pausedSeq;
        private int _offerWave = -1;
        private readonly int[] _cardIndex = new int[MaxCards];

        /// <summary>화면이 떠 있다 (열림 ~ 닫힘).</summary>
        public bool IsShowing => _shown;
        /// <summary>SupportOffered 를 받고 영수증 끝을 기다리는 중.</summary>
        public bool IsWaiting => _waiting;
        /// <summary>첫 입력을 받아 연출 중 (잠금).</summary>
        public bool IsLocked => _locked;
        public int CardCount => _count;
        public SupportCardView Card(int i) => cards[i];
        public TMP_Text TitleLabel => titleLabel;
        public TMP_Text SubtitleLabel => subtitleLabel;
        public SupportChip PreviewChip(int i) => previewChips[i];
        public RectTransform EmptySlot => emptySlot;
        public float DimAlpha => dim.color.a;
        /// <summary>고른 자리 (0부터, 없으면 −1).</summary>
        public int PickedSlot => _picked;
        public int OpenCount { get; private set; }
        /// <summary>받아들인 입력 수 (첫 입력만 — 연타는 세지 않음).</summary>
        public int ChooseCount { get; private set; }
        /// <summary>잠금 중 들어와 버린 입력 수.</summary>
        public int IgnoredCount { get; private set; }
        /// <summary>PickSupport 를 부른 수 (성공한 것만).</summary>
        public int CommitCount { get; private set; }
        public int SequenceBuilds { get; private set; }
        public float PickElapsed => _pick != null ? _pick.Elapsed(false) : 0f;
        public float OpenElapsed => _open != null ? _open.Elapsed(false) : 0f;
        public float PickDuration => context.Fx.supportFlyAt + context.Fx.supportFlyDuration;

        /// <summary>화면이 닫혔다 (고름 끝 · 다른 길로 골라짐 · 판 끝).</summary>
        public event Action Closed;

        private void Awake()
        {
            FxConfig fx = context.Fx;
            for (int i = 0; i < cards.Length; i++)
            {
                cards[i].Init(i, fx);
                cards[i].Clicked += OnCardClicked;
            }
            titleLabel.SetText(fx.supportTitle);
            titleLabel.color = fx.supportTitleColor;
            subtitleLabel.color = fx.supportSubtitleColor;
            activeLabel.SetText(fx.supportActiveLabel);
            emptyLabel.SetText(fx.supportEmptySlot);
            dim.raycastTarget = true; // 뒤 HUD · 보드 클릭 막기

            DOGetter<float> getDim = () => _dim;
            DOGetter<float> getStamp = () => _stamp;
            DOGetter<float> getPunch = () => _punch;
            DOGetter<float> getOut = () => _out;
            DOGetter<float> getFly = () => _fly;
            DOGetter<float> getDimOut = () => _dimOut;
            _open = DOTween.Sequence()
                .Insert(0f, DOTween.To(getDim, SetDim, 1f, fx.supportDimDuration).From(0f, false).SetEase(Ease.Linear));
            for (int i = 0; i < cards.Length; i++)
            {
                SupportCardView card = cards[i];
                DOGetter<float> get = () => card.In;
                DOSetter<float> set = card.SetIn;
                _open.Insert(fx.supportCardsAt + i * fx.supportCardStagger,
                    DOTween.To(get, set, 1f, fx.supportCardInDuration).From(0f, false).SetEase(fx.supportCardInEase));
            }
            _open.SetUpdate(true).SetAutoKill(false).Pause();

            float flyEnd = fx.supportFlyAt + fx.supportFlyDuration;
            _pick = DOTween.Sequence()
                .Insert(0f, DOTween.To(getStamp, SetStamp, 1f, fx.supportStampDuration).From(0f, false).SetEase(fx.supportStampEase))
                .Insert(fx.supportStampDuration, DOTween.To(getPunch, SetPunch, 1f, fx.supportPunchDuration).From(0f, false).SetEase(Ease.Linear))
                .Insert(fx.supportOthersOutAt, DOTween.To(getOut, SetOut, 1f, fx.supportOthersOutDuration).From(0f, false).SetEase(Ease.OutQuad))
                .Insert(fx.supportFlyAt, DOTween.To(getFly, SetFly, 1f, fx.supportFlyDuration).From(0f, false).SetEase(fx.supportFlyEase))
                .Insert(fx.supportFlyAt, DOTween.To(getDimOut, SetDimOut, 1f, fx.supportFlyDuration).From(0f, false).SetEase(Ease.Linear))
                .InsertCallback(fx.supportFlyAt, ShowChipBar)
                .InsertCallback(flyEnd, OnPickSequenceEnd)
                .SetUpdate(true)
                .SetAutoKill(false)
                .Pause();
            SequenceBuilds++;

            content.SetActive(false);
        }

        private void OnEnable()
        {
            GameEvents.SupportOffered += OnSupportOffered;
            GameEvents.SupportPicked += OnSupportPicked;
            GameEvents.StateChanged += OnStateChanged;
            GameEvents.GameEnded += OnGameEnded;
            if (payslip != null) payslip.Finished += OnPayslipFinished;
        }

        private void OnDisable()
        {
            GameEvents.SupportOffered -= OnSupportOffered;
            GameEvents.SupportPicked -= OnSupportPicked;
            GameEvents.StateChanged -= OnStateChanged;
            GameEvents.GameEnded -= OnGameEnded;
            if (payslip != null) payslip.Finished -= OnPayslipFinished;
        }

        private void OnDestroy()
        {
            _open?.Kill();
            _pick?.Kill();
        }

        private void Update()
        {
            GameLoop loop = context.Loop;
            if (loop == null || !loop.IsReady) return;

            // 놓친 제시 따라잡기 (구독 전에 온 SupportOffered 등) — 영수증이 없을 때만
            if (!_shown && !_waiting && loop.IsChoosingSupport && (payslip == null || !payslip.IsPlaying))
            {
                Open(loop.Support.OfferWaveIndex);
                return;
            }
            if (!_shown) return;

            bool prep = loop.Flow.State == GameState.Prep;
            if (_pendingCommit && prep) Commit(); // 연출 끝에 일시정지였다 — 돌아오면 고른다
            if (_locked || !prep) return;

            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) Choose(0);
            else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) Choose(1);
            else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) Choose(2);
        }

        // ── 열기 ─────────────────────────────────────────────

        private void OnSupportOffered(int clearedWaveIndex, SupportCardSpec[] offered)
        {
            _offerWave = clearedWaveIndex;
            if (payslip != null && payslip.IsPlaying)
            {
                _waiting = true; // 같은 프레임 WaveCleared 로 영수증이 먼저 돌고 있다 (V02 순서)
                return;
            }
            Open(clearedWaveIndex);
        }

        private void OnPayslipFinished()
        {
            if (!_waiting) return;
            _waiting = false;
            GameLoop loop = context.Loop;
            if (loop != null && loop.IsChoosingSupport) Open(_offerWave);
        }

        private void Open(int clearedWaveIndex)
        {
            GameLoop loop = context.Loop;
            SupportCardInfo[] table = chipBar != null ? chipBar.Table : null;
            if (loop == null || loop.Support == null || table == null) return;
            _waiting = false;
            if (clearedWaveIndex < 0) clearedWaveIndex = loop.Support.OfferWaveIndex;

            FxConfig fx = context.Fx;
            var support = loop.Support;
            SupportCardSpec[] offer = loop.CurrentOffer;
            _count = Mathf.Min(offer.Length, cards.Length);
            float left = -(_count - 1) * 0.5f * cardSpacing;
            for (int i = 0; i < cards.Length; i++)
            {
                SupportCardView card = cards[i];
                bool on = i < _count;
                if (card.gameObject.activeSelf != on) card.gameObject.SetActive(on);
                card.ResetMotion();
                card.Interactive = on;
                if (!on) continue;
                RectTransform rt = (RectTransform)card.transform;
                rt.anchoredPosition = new Vector2(left + i * cardSpacing, rt.anchoredPosition.y);
                int ci = support.GetOfferCardIndex(i);
                _cardIndex[i] = ci;
                SupportCardInfo info = table[ci];
                card.Fill(info, support.StacksOf(ci), SupportCardInfoTable.CategoryName(fx, info.Category));
            }

            int waveCount = loop.Flow.WaveCount;
            int next = clearedWaveIndex + 2; // 다음 웨이브 번호 (1부터)
            subtitleLabel.SetText(next >= waveCount ? fx.supportSubtitleLastFormat : fx.supportSubtitleFormat, clearedWaveIndex + 1, next);
            FillPreviewRow(loop, table);

            _picked = -1;
            _locked = false;
            _committing = false;
            _pendingCommit = false;
            _pausedSeq = false;
            _stamp = 0f; _punch = 0f; _out = 0f; _fly = 0f; _dimOut = 0f;
            SetDim(0f);
            _pick.Pause();
            content.SetActive(true);
            contentGroup.blocksRaycasts = true;
            contentGroup.interactable = true;
            _shown = true;
            OpenCount++;
            if (chipBar != null) chipBar.SetHidden(true); // 아래 줄이 같은 칩을 보여 준다 — 카드와 겹치지 않게
            SetHudHidden(true);
            _open.Restart();
            if (loop.Flow.State == GameState.Paused) PauseSequences(true);
        }

        /// <summary>아래 줄 — 이미 적용 중인 칩 (HUD 와 같은 것) + 빈 칸 「이번 선택」. 가운데 정렬.</summary>
        private void FillPreviewRow(GameLoop loop, SupportCardInfo[] table)
        {
            FxConfig fx = context.Fx;
            IReadOnlyList<int> active = loop.ActiveSupports;
            int wave = loop.Support.Resolver.Wave;
            int n = 0;
            for (int i = 0; i < active.Count && n < previewChips.Length; i++)
            {
                int c = active[i];
                if ((uint)c >= (uint)table.Length) continue;
                previewChips[n].Show(table[c], loop.Support.StacksOf(c), loop.Support.RemainingOf(c), wave, fx);
                n++;
            }
            for (int i = n; i < previewChips.Length; i++) previewChips[i].Hide();

            // 가로 배치 — 라벨 · 칩들 · 빈 칸 (각 너비 그대로, 가운데 정렬)
            RectTransform label = activeLabel.rectTransform;
            float total = label.rect.width;
            for (int i = 0; i < n; i++) total += rowGap + ((RectTransform)previewChips[i].transform).rect.width;
            total += rowGap + emptySlot.rect.width;
            float x = -total * 0.5f;
            Place(label, ref x);
            for (int i = 0; i < n; i++) Place((RectTransform)previewChips[i].transform, ref x);
            Place(emptySlot, ref x);
        }

        private void Place(RectTransform rt, ref float x)
        {
            float w = rt.rect.width;
            rt.anchoredPosition = new Vector2(x + w * rt.pivot.x, rt.anchoredPosition.y);
            x += w + rowGap;
        }

        // ── 고르기 ───────────────────────────────────────────

        private void OnCardClicked(SupportCardView card) => Choose(card.Slot);

        /// <summary>
        /// 자리 slot (0부터) 을 고른다 — 클릭 · 1 · 2 · 3 과 같은 길. 첫 입력만 받고 연출 끝까지 잠근다.
        /// 일시정지 · 범위 밖 · 이미 잠김이면 false.
        /// </summary>
        public bool Choose(int slot)
        {
            GameLoop loop = context.Loop;
            if (!_shown || loop == null || !loop.IsReady) return false;
            if (_locked) { IgnoredCount++; return false; }
            if (slot < 0 || slot >= _count || loop.Flow.State != GameState.Prep) return false;

            _locked = true;
            _picked = slot;
            ChooseCount++;
            contentGroup.interactable = false;
            for (int i = 0; i < _count; i++)
            {
                cards[i].Interactive = false;
                if (i != slot) cards[i].ClearHover();
            }
            // 날아갈 곳 — HUD 칩 자리 가운데 (카드 자리 로컬)
            SupportCardView picked = cards[slot];
            if (chipBar != null)
            {
                RectTransform target = chipBar.SlotFor(_cardIndex[slot]);
                RectTransform home = (RectTransform)picked.transform;
                Vector3 world = target.TransformPoint(target.rect.center);
                Vector2 local = home.InverseTransformPoint(world);
                picked.SetFlyTarget(local - picked.Motion.anchoredPosition);
            }
            if (_open.IsPlaying()) _open.Complete(); // 등장 중이었으면 끝 모양으로
            _pick.Restart();
            return true;
        }

        private void ShowChipBar()
        {
            if (chipBar != null) chipBar.SetHidden(false);
            SetHudHidden(false);
        }

        private void SetHudHidden(bool hidden)
        {
            for (int i = 0; i < hideWhileOpen.Length; i++)
            {
                CanvasGroup g = hideWhileOpen[i];
                if (g == null) continue;
                g.alpha = hidden ? 0f : 1f;
                g.blocksRaycasts = !hidden;
            }
        }

        /// <summary>열려 있는 동안 숨긴 HUD 가 지금 숨겨져 있나 (테스트).</summary>
        public bool HudHidden => hideWhileOpen.Length > 0 && hideWhileOpen[0] != null && hideWhileOpen[0].alpha < 0.5f;

        private void OnPickSequenceEnd()
        {
            _pendingCommit = true;
            GameLoop loop = context.Loop;
            if (loop != null && loop.IsReady && loop.Flow.State == GameState.Prep) Commit();
        }

        /// <summary>연출 끝 — GameLoop.PickSupport (준비 시간 재개 · 칩 생김) → 닫기 → 칩 펀치.</summary>
        private void Commit()
        {
            _pendingCommit = false;
            GameLoop loop = context.Loop;
            int slot = _picked;
            int cardIndex = slot >= 0 ? _cardIndex[slot] : -1;
            _committing = true;
            bool ok = loop != null && loop.PickSupport(slot);
            _committing = false;
            if (ok)
            {
                CommitCount++;
                Close();
                if (chipBar != null) chipBar.Punch(cardIndex);
                return;
            }
            // 거절 (선택 대기가 이미 끝남 등) — 화면만 닫는다
            if (loop == null || !loop.IsChoosingSupport) Close();
        }

        private void OnSupportPicked(SupportCardSpec card, int stack)
        {
            if (_committing) return; // 우리가 부른 PickSupport — Commit 이 닫는다
            _waiting = false;
            if (_shown) Close(); // 다른 길로 골라졌다
        }

        // ── 닫기 · 일시정지 · 판 끝 ──────────────────────────

        private void OnStateChanged(GameState state)
        {
            if (!_shown) return;
            if (state == GameState.Paused) PauseSequences(true);
            else if (state == GameState.Prep) PauseSequences(false);
        }

        private void PauseSequences(bool pause)
        {
            if (pause)
            {
                if (_pausedSeq) return;
                _pausedSeq = true;
                _openWasPlaying = _open.IsPlaying();
                _pickWasPlaying = _pick.IsPlaying();
                if (_openWasPlaying) _open.Pause();
                if (_pickWasPlaying) _pick.Pause();
                contentGroup.blocksRaycasts = false; // 휴게 화면이 위에서 받는다
                return;
            }
            if (!_pausedSeq) return;
            _pausedSeq = false;
            if (_openWasPlaying) _open.Play();
            if (_pickWasPlaying) _pick.Play();
            contentGroup.blocksRaycasts = true;
        }

        private bool _openWasPlaying;
        private bool _pickWasPlaying;

        private void OnGameEnded(ResultSummary summary)
        {
            _waiting = false;
            if (_shown) Close();
        }

        private void Close()
        {
            if (!_shown) return;
            _shown = false;
            _locked = false;
            _pendingCommit = false;
            _pausedSeq = false;
            _open.Pause();
            _pick.Pause();
            for (int i = 0; i < cards.Length; i++) cards[i].Interactive = false;
            contentGroup.blocksRaycasts = false;
            content.SetActive(false);
            ShowChipBar();
            Closed?.Invoke();
        }

        /// <summary>시퀀스를 그 시점으로 옮긴다 (멈춘 채 — 캡처용). pick = 고른 뒤 시퀀스.</summary>
        public void Seek(bool pick, float seconds)
        {
            Sequence s = pick ? _pick : _open;
            if (s == null) return;
            s.Goto(seconds, false);
        }

        /// <summary><see cref="Seek"/> 뒤 이어서.</summary>
        public void Resume(bool pick)
        {
            Sequence s = pick ? _pick : _open;
            if (s != null && _shown) s.Play();
        }

        // ── 진행값 → 그림 ────────────────────────────────────

        private void SetDim(float t)
        {
            _dim = t;
            ApplyDim();
        }

        private void SetDimOut(float t)
        {
            _dimOut = t;
            ApplyDim();
        }

        private void ApplyDim()
        {
            float k = Mathf.Clamp01(_dim) * (1f - Mathf.Clamp01(_dimOut));
            Color c = context.Fx.breakDimColor;
            c.a = context.Fx.supportDimAlpha * k;
            dim.color = c;
            chrome.alpha = k;
        }

        private void SetStamp(float t)
        {
            _stamp = t;
            if (_picked >= 0) cards[_picked].SetStamp(t);
        }

        private void SetPunch(float t)
        {
            _punch = t;
            if (_picked >= 0) cards[_picked].SetPunch(t);
        }

        private void SetOut(float t)
        {
            _out = t;
            for (int i = 0; i < _count; i++) if (i != _picked) cards[i].SetOut(t);
        }

        private void SetFly(float t)
        {
            _fly = t;
            if (_picked >= 0) cards[_picked].SetFly(t);
        }
    }
}
