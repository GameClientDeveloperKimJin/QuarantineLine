using System;
using DG.Tweening;
using DG.Tweening.Core;
using QL.Core;
using QL.Game.Flow;
using QL.Core.Waves;
using QL.FX;
using QL.FX.Alert;
using QL.Game;
using QL.UI.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace QL.UI.Hud
{
    /// <summary>
    /// U15 작업 지시서 (InGameScreen/Hud/WaveOrder — 해치 HP 패널 바로 아래, 앵커 Top Center · Pivot Top). 시안 1-1 · 원본 WO-UI-01.
    /// 웨이브 시작 알림 (U06 배너 대신). <c>WavePrepStarted(index, forecast)</c> 마다 — 영수증(<see cref="PayslipReceipt.IsPlaying"/>)이 돌고 있으면
    /// <see cref="PayslipReceipt.Finished"/> 뒤에, 아니면 바로 (웨이브 1):
    /// <list type="number">
    /// <item>0.00 슬롯 Fade 0 → 1 · 0.10 종이 높이 0 → 끝 (RectMask2D — 종이는 클립 아래쪽에 붙어 찢긴 끝부터 나온다) + ShakeRotation</item>
    /// <item>0.60 「접수」 도장 (Scale 1.6 → 1 InBack + Fade, 각도 −18° ~ −10° 무작위) · 0.78 종이 PunchScale + 작은 카메라 흔들림 (<see cref="CameraShakeFx.Kick"/>)</item>
    /// <item>1.20 Scale 1 → 0.1 + 웨이브 패널 가운데로 · 1.60 끝 — 숨김 · 웨이브 패널 Punch (<see cref="HudWave.PunchAbsorb"/>) · <see cref="Finished"/></item>
    /// </list>
    /// 값 = forecast 그대로 (점액 수 · 변이 수 · 주의 레인 = CautionLane + 1, −1 이면 「없음」). 문구는 <see cref="WaveOrderText"/> SO, 시점 · 세기는 FxConfig 「U15」.
    /// 카운트다운(<see cref="HudCountdown"/>)은 WavePrepStarted 부터 지시서 끝까지 숨김 (R1 결정 — 기다리는 동안 포함).
    /// 종이를 클릭하면 1.20 흡수로 건너뛴다. 종이(Paper) 말고는 raycastTarget 끔 — 연출 중에도 소환 · 머지가 된다.
    /// 게임 시간 (일시정지 때 같이 멈춤). Sequence 하나를 Awake 에 만들어 SetAutoKill(false) + Restart, 할당 없음.
    /// 이 컴포넌트는 늘 켜진 바깥 오브젝트에 두고, 보였다 숨는 것은 <see cref="panel"/>.
    /// </summary>
    public sealed class WaveOrderCard : MonoBehaviour, IPointerClickHandler
    {
        public const int RowCount = 5;
        public const int RowSite = 0;
        public const int RowJob = 1;
        public const int RowLoad = 2;
        public const int RowCaution = 3;
        public const int RowOwner = 4;

        [SerializeField] private HudContext context;
        [SerializeField] private WaveOrderText text;
        [Tooltip("돌고 있으면 끝(Finished)을 기다린다")]
        [SerializeField] private PayslipReceipt payslip;
        [Tooltip("지시서가 떠 있는 동안 숨김")]
        [SerializeField] private HudCountdown countdown;
        [Tooltip("흡수 목표 (패널 가운데) · 끝에 Punch")]
        [SerializeField] private HudWave wavePanel;

        [Header("구조")]
        [Tooltip("보였다 숨는 본체 (Pivot Top — 흡수 Scale · 이동)")]
        [SerializeField] private RectTransform panel;
        [Tooltip("도장 충격 PunchScale (Pivot Top)")]
        [SerializeField] private RectTransform punch;
        [Tooltip("슬롯 (UI_Slot, 패널 밖 — 흡수 때 따라가지 않고 제자리에서 꺼짐) — 발광 Fade")]
        [SerializeField] private CanvasGroup slot;
        [Tooltip("RectMask2D — 높이 0 → 종이 높이")]
        [SerializeField] private RectTransform clip;
        [Tooltip("종이 한 장 (클립 아래쪽에 붙음)")]
        [SerializeField] private RectTransform sheet;
        [Tooltip("출력 중 흔들림 (Pivot Top)")]
        [SerializeField] private RectTransform body;
        [Tooltip("클릭을 받는 종이 그림 (이것만 raycastTarget)")]
        [SerializeField] private Graphic paper;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text numberText;
        [SerializeField] private TMP_Text teamText;
        [SerializeField] private TMP_Text[] rowLabels = new TMP_Text[RowCount];
        [SerializeField] private TMP_Text[] rowValues = new TMP_Text[RowCount];
        [Tooltip("「접수」 도장 (클립 밖 — 1.6배가 잘리지 않게)")]
        [SerializeField] private RectTransform stamp;
        [SerializeField] private Graphic stampGraphic;

        private Sequence _sequence;
        private float _slot;
        private float _print;
        private float _stamp;
        private float _stampFade;
        private float _absorb;
        private float _sheetHeight;
        private Vector2 _basePos;
        private Vector2 _absorbDelta;
        private bool _waiting;
        private int _pendingIndex;
        private WaveForecast _pending;
        private CameraShakeFx _shake;
        private string[] _sites;

        /// <summary>지시서가 화면에 있다 (출력 ~ 흡수 끝). 일시정지 중에도 true.</summary>
        public bool IsShowing { get; private set; }
        /// <summary>WavePrepStarted 를 받고 영수증 끝을 기다리는 중.</summary>
        public bool IsWaiting => _waiting;
        /// <summary>U16 — 지시서가 시작됐다 (index 0부터). 영수증을 기다렸으면 그 끝에.</summary>
        public event Action<int> Started;
        /// <summary>U16 — 지시서가 웨이브 패널로 빨려 들어가 끝났다 (1.6초, 한 번). 판 끝(GameEnded)으로 멈추면 부르지 않는다.</summary>
        public event Action<int> Finished;

        public RectTransform Panel => panel;
        public RectTransform Clip => clip;
        public RectTransform Sheet => sheet;
        public RectTransform Stamp => stamp;
        public Graphic Paper => paper;
        public CanvasGroup Slot => slot;
        public TMP_Text TitleText => titleText;
        public TMP_Text NumberText => numberText;
        public TMP_Text TeamText => teamText;
        public TMP_Text RowLabel(int i) => rowLabels[i];
        public TMP_Text RowValue(int i) => rowValues[i];
        public WaveOrderText Text => text;
        public float SheetHeight => _sheetHeight;
        /// <summary>마지막으로 보인 (보이는) 웨이브 (0부터, 없으면 −1).</summary>
        public int ShownIndex { get; private set; } = -1;
        public WaveForecast ShownForecast { get; private set; }
        public int PlayCount { get; private set; }
        public int SkipCount { get; private set; }
        /// <summary>시퀀스가 만들어진 횟수 (Awake 1 — 재사용 확인).</summary>
        public int SequenceBuilds { get; private set; }
        /// <summary>시퀀스 진행 (초, 게임 시간).</summary>
        public float Elapsed => _sequence != null ? _sequence.Elapsed(false) : 0f;
        /// <summary>끝 (흡수 끝 = 숨김) 시점.</summary>
        public float Duration => context.Fx.orderAbsorbAt + context.Fx.orderAbsorbDuration;
        public float StampAngle => stamp.localEulerAngles.z > 180f ? stamp.localEulerAngles.z - 360f : stamp.localEulerAngles.z;

        private void Awake()
        {
            FxConfig fx = context.Fx;
            _basePos = panel.anchoredPosition;
            _sheetHeight = sheet.sizeDelta.y;

            Graphic[] gs = GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < gs.Length; i++) gs[i].raycastTarget = gs[i] == paper; // 종이만 클릭 — 나머지는 아래로 통과
            slot.blocksRaycasts = false;
            slot.interactable = false;

            // 바뀌지 않는 글자 — 한 번
            titleText.SetText(text.Title);
            teamText.SetText(text.Team);
            rowLabels[RowSite].SetText(text.SiteLabel);
            rowLabels[RowJob].SetText(text.JobLabel);
            rowLabels[RowLoad].SetText(text.LoadLabel);
            rowLabels[RowCaution].SetText(text.CautionLabel);
            rowLabels[RowOwner].SetText(text.OwnerLabel);
            rowValues[RowJob].SetText(text.Job);
            rowValues[RowOwner].SetText(text.Owner);

            DOGetter<float> getSlot = () => _slot;
            DOGetter<float> getPrint = () => _print;
            DOGetter<float> getStamp = () => _stamp;
            DOGetter<float> getStampFade = () => _stampFade;
            DOGetter<float> getAbsorb = () => _absorb;
            _sequence = DOTween.Sequence()
                .Insert(0f, DOTween.To(getSlot, SetSlot, 1f, fx.orderSlotFadeDuration).From(0f, false).SetEase(Ease.Linear))
                .Insert(fx.orderPrintAt, DOTween.To(getPrint, SetPrint, 1f, fx.orderPrintDuration).From(0f, false).SetEase(fx.orderPrintEase))
                .Insert(fx.orderPrintAt, body.DOShakeRotation(fx.orderShakeDuration, new Vector3(0f, 0f, fx.orderShakeAngle), fx.orderShakeVibrato, 90f, true))
                .Insert(fx.orderStampAt, DOTween.To(getStamp, SetStamp, 1f, fx.orderStampDuration).From(0f, false).SetEase(fx.orderStampEase))
                .Insert(fx.orderStampAt, DOTween.To(getStampFade, SetStampFade, 1f, fx.orderStampFadeDuration).From(0f, false).SetEase(Ease.Linear))
                .Insert(fx.orderPunchAt, punch.DOPunchScale(Vector3.one * fx.orderPunchStrength, fx.orderPunchDuration, fx.orderPunchVibrato, fx.orderPunchElasticity))
                .InsertCallback(fx.orderPunchAt, KickCamera)
                .Insert(fx.orderAbsorbAt, DOTween.To(getAbsorb, SetAbsorb, 1f, fx.orderAbsorbDuration).From(0f, false).SetEase(fx.orderAbsorbEase))
                .InsertCallback(fx.orderAbsorbAt + fx.orderAbsorbDuration, Finish)
                .SetAutoKill(false)
                .Pause();
            SequenceBuilds++;

            panel.gameObject.SetActive(false);
            slot.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            GameEvents.WavePrepStarted += OnWavePrepStarted;
            GameEvents.GameEnded += OnGameEnded;
            if (payslip != null) payslip.Finished += OnPayslipFinished;
        }

        private void OnDisable()
        {
            GameEvents.WavePrepStarted -= OnWavePrepStarted;
            GameEvents.GameEnded -= OnGameEnded;
            if (payslip != null) payslip.Finished -= OnPayslipFinished;
        }

        private void OnDestroy()
        {
            _sequence?.Kill();
        }

        private void OnWavePrepStarted(int index, WaveForecast forecast)
        {
            _pendingIndex = index;
            _pending = forecast;
            if (countdown != null) countdown.SetSuppressed(true); // 기다리는 동안부터 — 지시서 끝까지
            // 같은 프레임 WaveCleared → 영수증이 먼저 시작해 있다 (V02 프레임 순서) — 그 끝을 기다린다
            if (payslip != null && payslip.IsPlaying)
            {
                _waiting = true;
                return;
            }
            _waiting = false;
            Play(_pendingIndex, in _pending);
        }

        private void OnPayslipFinished()
        {
            if (!_waiting) return;
            _waiting = false;
            Play(_pendingIndex, in _pending);
        }

        private void Play(int index, in WaveForecast forecast)
        {
            ShownIndex = index;
            ShownForecast = forecast;

            numberText.SetText(text.NumberFormat, index + 1);
            rowValues[RowSite].SetText(SiteOf(index));
            rowValues[RowLoad].SetText(text.LoadFormat, forecast.SlimeCount, forecast.MutantCount);
            if (forecast.CautionLane >= 0) rowValues[RowCaution].SetText(text.CautionFormat, forecast.CautionLane + 1);
            else rowValues[RowCaution].SetText(text.CautionNone);

            FxConfig fx = context.Fx;
            stamp.localEulerAngles = new Vector3(0f, 0f, UnityEngine.Random.Range(fx.orderStampAngleMin, fx.orderStampAngleMax));
            SetSlot(0f);
            SetPrint(0f);
            SetStamp(0f);
            SetStampFade(0f);
            SetAbsorb(0f); // 원위치부터 — 그다음 목표를 잰다
            _absorbDelta = AbsorbDelta();

            if (!panel.gameObject.activeSelf) panel.gameObject.SetActive(true);
            if (!slot.gameObject.activeSelf) slot.gameObject.SetActive(true);
            IsShowing = true;
            if (countdown != null) countdown.SetSuppressed(true);
            PlayCount++;
            _sequence.Restart();
            Started?.Invoke(index);
        }

        /// <summary>
        /// 웨이브 index (0부터) 현장 이름 = WaveData 그 웨이브 bannerText 의 구분자 앞 (「C-1 화물칸」). 처음 부를 때 웨이브 전부를 한 번 잘라 캐시 —
        /// 그 뒤로는 할당 없음. 데이터가 없거나 범위 밖이면 빈 문자열.
        /// </summary>
        public string SiteOf(int index)
        {
            if (_sites == null)
            {
                var b = context.Bootstrap;
                var waves = b != null && b.Data != null && b.Data.Waves != null ? b.Data.Waves.Waves : null;
                if (waves == null) return string.Empty;
                _sites = new string[waves.Count];
                for (int i = 0; i < _sites.Length; i++) _sites[i] = text.SiteFromBanner(waves[i].BannerText);
            }
            return index >= 0 && index < _sites.Length ? _sites[index] : string.Empty;
        }

        /// <summary>종이 클릭 — 흡수 전이면 바로 흡수 구간으로 (도장 · 출력은 끝 모양으로, 카메라 흔들림은 건너뜀).</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            SkipToAbsorb();
        }

        /// <summary><see cref="OnPointerClick"/> 와 같은 길 — 흡수 전이면 1.20 으로.</summary>
        public void SkipToAbsorb()
        {
            if (!IsShowing || _sequence == null) return;
            float at = context.Fx.orderAbsorbAt;
            if (_sequence.Elapsed(false) >= at) return;
            SkipCount++;
            _sequence.Goto(at, _sequence.IsPlaying());
        }

        /// <summary>시퀀스를 그 시점으로 옮긴다 (멈춘 채 — 캡처 · 할당 측정용). <see cref="Resume"/> 으로 이어서.</summary>
        public void Seek(float seconds)
        {
            if (_sequence == null) return;
            _sequence.Goto(seconds, false);
        }

        /// <summary><see cref="Seek"/> 뒤 이어서 재생.</summary>
        public void Resume()
        {
            if (_sequence != null && IsShowing) _sequence.Play();
        }

        /// <summary>판 끝 — 즉시 숨김 · 기다림 취소 (끝남 알림 없음).</summary>
        private void OnGameEnded(ResultSummary summary)
        {
            if (_sequence != null && _sequence.IsActive()) _sequence.Pause();
            _waiting = false;
            Hide();
        }

        private void Hide()
        {
            IsShowing = false;
            if (panel.gameObject.activeSelf) panel.gameObject.SetActive(false);
            if (slot.gameObject.activeSelf) slot.gameObject.SetActive(false);
            if (countdown != null && !_waiting) countdown.SetSuppressed(false); // 다음 지시서를 기다리는 중이면 그대로 숨김
        }

        private void Finish()
        {
            Hide();
            if (wavePanel != null) wavePanel.PunchAbsorb();
            Finished?.Invoke(ShownIndex);
        }

        private void KickCamera()
        {
            if (_shake == null) _shake = FindAnyObjectByType<CameraShakeFx>();
            if (_shake != null) _shake.Kick(context.Fx.orderCameraKickStrength, context.Fx.orderCameraKickDuration);
        }

        /// <summary>종이 가운데가 웨이브 패널 가운데에 닿도록 패널 피벗(위 가운데)을 옮길 거리 (패널 부모 로컬).</summary>
        private Vector2 AbsorbDelta()
        {
            if (wavePanel == null) return Vector2.zero;
            RectTransform target = (RectTransform)wavePanel.transform;
            RectTransform parent = (RectTransform)panel.parent;
            Vector2 center = parent.InverseTransformPoint(target.TransformPoint(target.rect.center));
            Vector2 pivot = parent.InverseTransformPoint(panel.TransformPoint(new Vector3(panel.rect.center.x, panel.rect.yMax, 0f)));
            float s = context.Fx.orderAbsorbScale;
            return center + new Vector2(0f, _sheetHeight * 0.5f * s) - pivot;
        }

        private void SetSlot(float t)
        {
            _slot = t;
            slot.alpha = t * (1f - Mathf.Clamp01(_absorb));
        }

        private void SetPrint(float t)
        {
            _print = t;
            clip.sizeDelta = new Vector2(clip.sizeDelta.x, _sheetHeight * Mathf.Clamp01(t));
        }

        private void SetStamp(float t)
        {
            _stamp = t;
            float s = Mathf.LerpUnclamped(context.Fx.orderStampScaleFrom, 1f, t);
            stamp.localScale = new Vector3(s, s, 1f);
        }

        private void SetStampFade(float t)
        {
            _stampFade = t;
            Color c = stampGraphic.color;
            c.a = Mathf.Clamp01(t);
            stampGraphic.color = c;
        }

        private void SetAbsorb(float t)
        {
            _absorb = t;
            float s = Mathf.LerpUnclamped(1f, context.Fx.orderAbsorbScale, t);
            panel.localScale = new Vector3(s, s, 1f);
            panel.anchoredPosition = _basePos + _absorbDelta * t;
            slot.alpha = _slot * (1f - Mathf.Clamp01(t)); // 종이가 떠나면 슬롯 발광도 꺼짐
        }
    }
}
