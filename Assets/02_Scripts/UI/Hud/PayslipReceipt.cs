using System;
using DG.Tweening;
using DG.Tweening.Core;
using MoreMountains.Feedbacks;
using QL.Core;
using QL.Core.Flow;
using QL.FX;
using QL.Game;
using QL.Game.Flow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QL.UI.Hud
{
    /// <summary>
    /// U14 수당 영수증 (InGameScreen/Hud/Payslip — 코인 패널 바로 아래, 앵커 Top Right · Pivot Top). 시안 1-4 · 원본 WO-UI-04.
    /// <c>WaveCleared(index, pay)</c> 중 마지막 웨이브가 아닐 때만 (index &lt; WaveCount − 1 — 마지막은 결과 화면 U13 이 보너스를 보여 줌):
    /// <list type="number">
    /// <item>0.00 종이 높이 0 → 끝 (RectMask2D 클립 — 종이는 클립 아래쪽에 붙어 있어 찢긴 끝부터 밀려 나온다) + 「웨이브 N 클리어!」 배너</item>
    /// <item>0.40 항목 줄 — 줄마다 Fade + X 8 → 0, 0.15초 간격 (값 0 인 항목은 안 띄움, 미리 만든 줄 최대 4 를 켜고 끔)</item>
    /// <item>0.80 합계 0 → Pay.Total · 1.10 「지급」 도장 (Scale 1.6 → 1 InBack, 각도 −14° ~ −8° 무작위)</item>
    /// <item>1.60 ScaleY 1 → 0 + 코인 패널 가운데 쪽으로 · 1.90 HUD 코인 <see cref="HudCoin.Release"/> · 2.00 끝 (<see cref="Finished"/>)</item>
    /// </list>
    /// 지갑은 CoinChanged(수당) 순간 이미 늘었다 — WaveCleared(먼저 옴) 때 <see cref="HudCoin.Hold"/> 로 표시만 붙잡는다.
    /// 게임 시간 (일시정지 때 같이 멈춤 — 재개하면 이어서). Sequence 하나를 Awake 에 만들어 SetAutoKill(false) + Restart, 할당 없음.
    /// 수치 · 문구 · 색은 FxConfig 「U14」. 이 컴포넌트는 늘 켜진 바깥 오브젝트에 두고, 보였다 숨는 것은 <see cref="panel"/>.
    /// U15 (작업 지시서): 같은 프레임 WavePrepStarted 로 시작하므로 <see cref="IsPlaying"/> 이면 <see cref="Finished"/> 를 기다린다.
    /// </summary>
    public sealed class PayslipReceipt : MonoBehaviour
    {
        public const int MaxRows = 4;

        [SerializeField] private HudContext context;
        [Tooltip("표시값을 붙잡고 풀 HUD 코인 (코인 패널) — 말려 올라가는 목표도 이 패널 가운데")]
        [SerializeField] private HudCoin coin;
        [SerializeField] private WaveClearBanner banner;

        [Header("구조")]
        [Tooltip("보였다 숨는 본체 (Pivot Top — ScaleY · 이동)")]
        [SerializeField] private RectTransform panel;
        [Tooltip("RectMask2D — 높이 0 → 종이 높이")]
        [SerializeField] private RectTransform clip;
        [Tooltip("종이 한 장 (클립 아래쪽에 붙음, 높이 = 줄 수로 계산)")]
        [SerializeField] private RectTransform sheet;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text teamText;
        [SerializeField] private TMP_Text waveText;
        [SerializeField] private CanvasGroup[] rowGroups = new CanvasGroup[MaxRows];
        [Tooltip("줄 안에서 X 8 → 0 으로 미끄러지는 내용 (줄마다 하나)")]
        [SerializeField] private RectTransform[] rowContents = new RectTransform[MaxRows];
        [SerializeField] private TMP_Text[] rowLabels = new TMP_Text[MaxRows];
        [SerializeField] private TMP_Text[] rowValues = new TMP_Text[MaxRows];
        [Tooltip("합계 · 바코드 · 다음 근무 · 도장 묶음 (줄 수에 따라 위아래로)")]
        [SerializeField] private RectTransform footer;
        [SerializeField] private TMP_Text totalLabel;
        [SerializeField] private TMP_Text totalValue;
        [Tooltip("U18b 합계 굴림 끝 Feel (Footer/TotalFeel) — 합계 숫자 스프링 펀치. 수치는 그 MMF_Player 인스펙터. 게임 시간")]
        [SerializeField] private MMF_Player totalFeel;
        [SerializeField] private TMP_Text nextText;
        [SerializeField] private RectTransform stamp;
        [SerializeField] private Graphic stampGraphic;

        [Header("배치 (px — 종이 위에서부터)")]
        [Tooltip("첫 항목 줄 위끝")]
        [SerializeField] private float rowsTop = 178f;
        [SerializeField] private float rowHeight = 42f;
        [Tooltip("마지막 줄 아래 → 묶음(footer) 위끝 간격")]
        [SerializeField] private float footerGap = 6f;
        [Tooltip("묶음 높이 + 아래 여백 + 찢긴 끝 (종이 높이 = rowsTop + 줄 × rowHeight + footerGap + 이 값)")]
        [SerializeField] private float footerHeight = 300f;

        private Sequence _sequence;
        private float _print;
        private float _total;
        private float _stamp;
        private float _roll;
        private readonly float[] _rows = new float[MaxRows];
        private float _sheetHeight;
        private int _rowCount;
        private int _amount;
        private int _shownTotal;
        private bool _held;
        private Vector2 _basePos;
        private Vector2 _rollDelta;

        /// <summary>U15 — 영수증이 돌고 있다 (WaveCleared → 끝 2.0초). 일시정지 중에도 true.</summary>
        public bool IsPlaying { get; private set; }
        /// <summary>U15 — 영수증이 끝났다 (2.0초, 한 번). 판 끝(GameEnded)으로 멈추면 부르지 않는다.</summary>
        public event Action Finished;
        /// <summary>U08 자리 — 종이가 출력되기 시작할 때 (프린터 효과음). 지금은 듣는 곳 없음.</summary>
        public event Action Printed;

        public RectTransform Panel => panel;
        public RectTransform Clip => clip;
        public RectTransform Sheet => sheet;
        public RectTransform Stamp => stamp;
        public TMP_Text WaveText => waveText;
        public TMP_Text TotalValue => totalValue;
        /// <summary>U18b — 합계 굴림 끝 Feel (MMF_Player).</summary>
        public MMF_Player TotalFeel => totalFeel;
        public bool HasTotalFeel => totalFeel != null;
        public int TotalFeelPlays { get; private set; }
        public TMP_Text NextText => nextText;
        public bool IsShowing => panel.gameObject.activeSelf;
        /// <summary>이번 영수증에 켜진 항목 줄 수.</summary>
        public int RowCount => _rowCount;
        public float SheetHeight => _sheetHeight;
        /// <summary>이번 영수증 합계 (= Pay.Total).</summary>
        public int Amount => _amount;
        public int PlayCount { get; private set; }
        public float Duration => context.Fx.payslipEndAt;
        /// <summary>시퀀스가 만들어진 횟수 (Awake 1 — 재사용 확인).</summary>
        public int SequenceBuilds { get; private set; }
        /// <summary>시퀀스 진행 (초, 게임 시간).</summary>
        public float Elapsed => _sequence != null ? _sequence.Elapsed(false) : 0f;
        public TMP_Text RowLabel(int i) => rowLabels[i];
        public TMP_Text RowValue(int i) => rowValues[i];
        public bool RowActive(int i) => rowGroups[i].gameObject.activeSelf;

        private void Awake()
        {
            FxConfig fx = context.Fx;
            _basePos = panel.anchoredPosition;

            Graphic[] gs = panel.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < gs.Length; i++) gs[i].raycastTarget = false;

            // 바뀌지 않는 글자 — 한 번
            titleText.SetText(fx.payslipTitle);
            teamText.SetText(fx.payslipTeam);
            totalLabel.SetText(fx.payslipTotalLabel);

            DOGetter<float> getPrint = () => _print;
            DOGetter<float> getTotal = () => _total;
            DOGetter<float> getStamp = () => _stamp;
            DOGetter<float> getRoll = () => _roll;
            _sequence = DOTween.Sequence()
                .Insert(0f, DOTween.To(getPrint, SetPrint, 1f, fx.payslipPrintDuration).From(0f, false).SetEase(fx.payslipPrintEase));
            for (int i = 0; i < MaxRows; i++)
            {
                int row = i;
                DOGetter<float> get = () => _rows[row];
                DOSetter<float> set = t => SetRow(row, t);
                _sequence.Insert(fx.payslipRowsAt + i * fx.payslipRowInterval,
                    DOTween.To(get, set, 1f, fx.payslipRowDuration).From(0f, false).SetEase(Ease.OutQuad));
            }
            _sequence
                .Insert(fx.payslipTotalAt, DOTween.To(getTotal, SetTotal, 1f, fx.payslipTotalDuration).From(0f, false).SetEase(fx.payslipTotalEase))
                .Insert(fx.payslipStampAt, DOTween.To(getStamp, SetStamp, 1f, fx.payslipStampDuration).From(0f, false).SetEase(fx.payslipStampEase))
                .Insert(fx.payslipRollUpAt, DOTween.To(getRoll, SetRoll, 1f, fx.payslipRollUpDuration).From(0f, false).SetEase(fx.payslipRollUpEase))
                .InsertCallback(fx.payslipTotalAt + fx.payslipTotalDuration, PlayTotalFeel)
                .InsertCallback(fx.payslipCoinAt, ReleaseCoin)
                .InsertCallback(fx.payslipEndAt, Finish)
                .SetAutoKill(false)
                .Pause();
            SequenceBuilds++;

            for (int i = 0; i < MaxRows; i++) rowGroups[i].gameObject.SetActive(false);
            panel.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            GameEvents.WaveCleared += OnWaveCleared;
            GameEvents.GameEnded += OnGameEnded;
        }

        private void OnDisable()
        {
            GameEvents.WaveCleared -= OnWaveCleared;
            GameEvents.GameEnded -= OnGameEnded;
        }

        private void OnDestroy()
        {
            _sequence?.Kill();
        }

        private void OnWaveCleared(int index, PayBreakdown pay)
        {
            GameLoop loop = context.Loop;
            GameFlow flow = loop != null ? loop.Flow : null;
            if (flow == null || index >= flow.WaveCount - 1) return; // 마지막 웨이브 → 결과 화면 (U13)
            Play(index, in pay);
        }

        private void Play(int index, in PayBreakdown pay)
        {
            FxConfig fx = context.Fx;
            if (IsPlaying) ReleaseCoin(); // 겹친 경우(준비 5초 > 2초라 없어야 함) — 앞 영수증 몫은 바로 풀고 새로

            // 내용 — 값 0 인 항목은 줄을 띄우지 않는다
            waveText.SetText(fx.payslipWaveFormat, index + 1);
            int n = 0;
            if (pay.ClearBonus > 0)
            {
                rowLabels[n].SetText(fx.payslipClearLabel);
                rowValues[n].SetText(fx.payslipValueFormat, pay.ClearBonus);
                n++;
            }
            if (pay.CleanReward > 0)
            {
                rowLabels[n].SetText(fx.payslipCleanFormat, pay.CleanCount);
                rowValues[n].SetText(fx.payslipValueFormat, pay.CleanReward);
                n++;
            }
            _rowCount = n;
            for (int i = 0; i < MaxRows; i++)
            {
                bool on = i < n;
                if (rowGroups[i].gameObject.activeSelf != on) rowGroups[i].gameObject.SetActive(on);
                SetRow(i, 0f);
            }
            nextText.SetText(fx.payslipNextFormat, index + 2);
            _amount = pay.Total;
            _shownTotal = -1;
            SetTotal(0f);

            // 배치 — 줄 수만큼 묶음을 내리고 종이 높이를 맞춘다
            footer.anchoredPosition = new Vector2(footer.anchoredPosition.x, -(rowsTop + n * rowHeight + footerGap));
            _sheetHeight = rowsTop + n * rowHeight + footerGap + footerHeight;
            sheet.sizeDelta = new Vector2(sheet.sizeDelta.x, _sheetHeight);
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, _sheetHeight);

            stamp.localEulerAngles = new Vector3(0f, 0f, UnityEngine.Random.Range(fx.payslipStampAngleMin, fx.payslipStampAngleMax));
            SetStamp(0f);
            SetRoll(0f); // 앞 영수증이 말려 올라간 채면 원위치부터 — 그다음 목표를 잰다
            _rollDelta = RollDelta();
            SetPrint(0f);

            // 표시값 붙잡기 — 바로 뒤 CoinChanged(수당) 가 와도 HUD 숫자는 영수증 끝(1.9초)까지 그대로
            if (coin != null && _amount > 0)
            {
                coin.Hold(_amount);
                _held = true;
            }
            if (banner != null) banner.Play(index + 1);

            if (!panel.gameObject.activeSelf) panel.gameObject.SetActive(true);
            IsPlaying = true;
            PlayCount++;
            _sequence.Restart();
            Printed?.Invoke();
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
            if (_sequence != null && IsPlaying) _sequence.Play();
        }

        /// <summary>판 끝 — 즉시 숨김 (HUD 코인은 GameEnded 에서 스스로 지갑 값으로 맞춘다).</summary>
        private void OnGameEnded(ResultSummary summary)
        {
            if (_sequence != null && _sequence.IsActive()) _sequence.Pause();
            _held = false;
            IsPlaying = false;
            if (banner != null) banner.Hide();
            if (panel.gameObject.activeSelf) panel.gameObject.SetActive(false);
        }

        private void ReleaseCoin()
        {
            if (!_held) return;
            _held = false;
            coin.Release(_amount);
        }

        private void Finish()
        {
            ReleaseCoin();
            if (panel.gameObject.activeSelf) panel.gameObject.SetActive(false);
            IsPlaying = false;
            Finished?.Invoke();
        }

        /// <summary>패널 위끝 → 코인 패널 가운데 (패널 부모 로컬).</summary>
        private Vector2 RollDelta()
        {
            if (coin == null) return Vector2.zero;
            RectTransform target = (RectTransform)coin.transform;
            RectTransform parent = (RectTransform)panel.parent;
            Vector3 world = target.TransformPoint(target.rect.center);
            Vector2 local = parent.InverseTransformPoint(world);
            Vector2 top = parent.InverseTransformPoint(panel.TransformPoint(new Vector3(panel.rect.center.x, panel.rect.yMax, 0f)));
            return local - top;
        }

        private void SetPrint(float t)
        {
            _print = t;
            clip.sizeDelta = new Vector2(clip.sizeDelta.x, _sheetHeight * Mathf.Clamp01(t));
        }

        private void SetRow(int i, float t)
        {
            _rows[i] = t;
            rowGroups[i].alpha = t;
            RectTransform c = rowContents[i];
            c.anchoredPosition = new Vector2(context.Fx.payslipRowOffsetX * (1f - t), c.anchoredPosition.y);
        }

        /// <summary>U18b — 합계 숫자 굴림이 끝난 순간.</summary>
        private void PlayTotalFeel()
        {
            if (totalFeel == null) return;
            totalFeel.PlayFeedbacks();
            TotalFeelPlays++;
        }

        private void SetTotal(float t)
        {
            _total = t;
            int v = Mathf.RoundToInt(_amount * Mathf.Clamp01(t));
            if (v == _shownTotal) return;
            _shownTotal = v;
            totalValue.SetText(context.Fx.payslipValueFormat, v);
        }

        private void SetStamp(float t)
        {
            _stamp = t;
            float s = Mathf.LerpUnclamped(context.Fx.payslipStampScaleFrom, 1f, t);
            stamp.localScale = new Vector3(s, s, 1f);
            Color c = stampGraphic.color;
            c.a = t != 0f ? 1f : 0f; // InBack 은 처음에 음수 — 시작 순간부터 보이게
            stampGraphic.color = c;
        }

        private void SetRoll(float t)
        {
            _roll = t;
            panel.localScale = new Vector3(1f, 1f - t, 1f);
            panel.anchoredPosition = _basePos + _rollDelta * t;
        }
    }
}
