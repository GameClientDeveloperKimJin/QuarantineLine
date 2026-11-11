using QL.Game.Flow;
using UnityEngine;
using UnityEngine.UI;

namespace QL.UI.Hud
{
    /// <summary>
    /// F03 「작업 시작 (Enter)」 버튼 (InGameScreen/Hud/StartWave — 준비 카운트다운 바로 아래). G11 바로 시작을 화면에 보여 준다.
    /// 보이기 = <see cref="GameLoop.IsPrep"/> 이고 U15 작업 지시서 · U14 영수증이 떠 있지 않을 때 (둘이 화면 가운데를 쓰는 동안은 숨김 — 겹침 없음).
    /// 웨이브 · 일시정지 · 결과 중엔 IsPrep 이 false 라 숨김. 클릭 → <see cref="GameLoop.RequestStartWave"/> (Enter 키는 GameLoop 가 직접 읽는다 — UI 는 키를 읽지 않는다).
    /// 상태 읽기만 매 프레임 (bool 몇 개 — 할당 0), 보임이 바뀔 때만 SetActive.
    /// </summary>
    public sealed class HudStartButton : MonoBehaviour
    {
        [SerializeField] private HudContext context;
        [Tooltip("켜고 끄는 버튼 (이 오브젝트의 자식 — 이 컴포넌트는 늘 켜져 있어야 Update 가 돈다)")]
        [SerializeField] private Button button;
        [Tooltip("U15 작업 지시서 — 떠 있는 동안 숨김")]
        [SerializeField] private WaveOrderCard orderCard;
        [Tooltip("U14 영수증 — 떠 있는 동안 숨김")]
        [SerializeField] private PayslipReceipt receipt;

        private bool _shown;

        public Button Button => button;
        /// <summary>지금 버튼이 보이나.</summary>
        public bool IsShown => _shown;
        /// <summary>클릭으로 바로 시작을 요청한 수 (받아들여진 것만).</summary>
        public int StartCount { get; private set; }

        private void Awake()
        {
            button.onClick.AddListener(OnClicked);
            _shown = false;
            button.gameObject.SetActive(false);
        }

        private void Update()
        {
            bool want = WantsShown();
            if (want == _shown) return;
            _shown = want;
            button.gameObject.SetActive(want);
        }

        private bool WantsShown()
        {
            GameLoop loop = context.Loop;
            if (loop == null || !loop.IsReady || !loop.IsPrep) return false;
            if (orderCard != null && (orderCard.IsShowing || orderCard.IsWaiting)) return false;
            if (receipt != null && receipt.IsShowing) return false;
            return true;
        }

        private void OnClicked()
        {
            GameLoop loop = context.Loop;
            if (loop != null && loop.RequestStartWave()) StartCount++;
        }
    }
}
