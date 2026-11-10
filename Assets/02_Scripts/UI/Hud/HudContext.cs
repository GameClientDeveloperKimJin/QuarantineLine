using QL.FX;
using QL.Game.Bootstrap;
using QL.Game.Flow;
using QL.Game.Summon;
using QL.UI.Screens;
using UnityEngine;

namespace QL.UI.Hud
{
    /// <summary>
    /// HUD 요소들이 같이 읽는 것 — 연출 수치(FxConfig) · GameLoop(UIRoot 경유) · GameBootstrap · SummonController.
    /// 프리팹은 씬 오브젝트를 직렬화할 수 없어 처음 쓸 때 한 번 찾는다 (UIRoot 와 같은 방식). 읽기 전용 — 상태를 바꾸는 호출은
    /// 소환 버튼의 <see cref="SummonController.RequestSummon"/> 하나뿐 (S3 규칙 1).
    /// </summary>
    public sealed class HudContext : MonoBehaviour
    {
        [SerializeField] private UIRoot root;
        [SerializeField] private FxConfig fx;

        private GameBootstrap _bootstrap;
        private SummonController _summon;

        public FxConfig Fx => fx;

        public GameLoop Loop => root != null ? root.Loop : null;

        public GameBootstrap Bootstrap
        {
            get
            {
                if (_bootstrap == null) _bootstrap = FindAnyObjectByType<GameBootstrap>();
                return _bootstrap;
            }
        }

        public SummonController Summon
        {
            get
            {
                if (_summon == null) _summon = FindAnyObjectByType<SummonController>();
                return _summon;
            }
        }
    }
}
