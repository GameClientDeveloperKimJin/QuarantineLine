using QL.Game.Flow;
using QL.UI.Tutorial;
using UnityEngine;
using UnityEngine.UI;

namespace QL.UI.Hud
{
    /// <summary>
    /// F03 「작업 시작 (Enter)」 버튼 (InGameScreen/Hud/StartWave — 준비 카운트다운 바로 아래). G11 바로 시작을 화면에 보여 준다.
    /// 보이기 = <see cref="GameLoop.IsPrep"/> 이고 U15 작업 지시서 · U14 영수증이 떠 있지 않을 때 (둘이 화면 가운데를 쓰는 동안은 숨김 — 겹침 없음).
    /// U19: 업무 지원 카드 선택 중 (<see cref="GameLoop.IsChoosingSupport"/> — 영수증 ~ 고른 카드가 칩으로 들어갈 때까지) 숨김.
    /// 웨이브 · 일시정지 · 결과 중엔 IsPrep 이 false 라 숨김. 클릭 → <see cref="GameLoop.RequestStartWave"/> (Enter 키는 GameLoop 가 직접 읽는다 — UI 는 키를 읽지 않는다).
    /// U21: 첫 판 튜토리얼 동안 숨김 (<see cref="TutorialDirector.BlocksStartButton"/> — 튜토리얼 중 바로 시작 방지) · 끝나면 HudPunch 와 함께 나타남.
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
        [Tooltip("U21 튜토리얼 — 튜토리얼 동안 숨김, 끝나면 펀치와 함께 나타남 (비어 있으면 무시)")]
        [SerializeField] private TutorialDirector tutorial;

        private bool _shown;
        private bool _tutorialHeld;
        private HudPunch _punch;

        /// <summary>U21 — 튜토리얼이 끝나 나타날 때 펀치를 친 수.</summary>
        public int RevealPunchCount { get; private set; }

        public Button Button => button;
        /// <summary>지금 버튼이 보이나.</summary>
        public bool IsShown => _shown;
        /// <summary>클릭으로 바로 시작을 요청한 수 (받아들여진 것만).</summary>
        public int StartCount { get; private set; }

        private void Awake()
        {
            button.onClick.AddListener(OnClicked);
            _punch = new HudPunch(button.transform, context.Fx);
            _shown = false;
            button.gameObject.SetActive(false);
        }

        private void Update()
        {
            bool want = WantsShown();
            if (want == _shown) return;
            _shown = want;
            button.gameObject.SetActive(want);
            if (want && _tutorialHeld)
            {
                // U21 — 튜토리얼이 끝나 처음 나타남: 펀치 (카운트다운이 함께 흐르기 시작)
                _tutorialHeld = false;
                _punch.Play();
                RevealPunchCount++;
            }
        }

        private void OnDestroy()
        {
            _punch?.Kill();
        }

        private bool WantsShown()
        {
            GameLoop loop = context.Loop;
            if (loop == null || !loop.IsReady || !loop.IsPrep) return false;
            if (loop.IsChoosingSupport) return false; // U19 — 카드 선택 중엔 바로 시작이 막힌다 (RequestStartWave false) · 선택 화면이 가운데를 쓴다
            if (orderCard != null && (orderCard.IsShowing || orderCard.IsWaiting)) return false;
            if (receipt != null && receipt.IsShowing) return false;
            if (tutorial != null && tutorial.BlocksStartButton)
            {
                _tutorialHeld = true;
                return false;
            }
            return true;
        }

        private void OnClicked()
        {
            GameLoop loop = context.Loop;
            if (loop != null && loop.RequestStartWave()) StartCount++;
        }
    }
}
