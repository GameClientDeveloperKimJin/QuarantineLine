using UnityEngine;

namespace QL.UI.Data
{
    /// <summary>
    /// 휴게 시간 카드 팁 줄 문구 (U12) — <c>Assets/04_Resources/Data/UI/TipTable.asset</c>.
    /// 카드는 열 때 무작위 한 줄에서 시작해 <see cref="QL.FX.FxConfig.breakTipInterval"/> 마다 다음 줄로 넘긴다 (끝 → 처음).
    /// 문구는 「TIP」 머리말 없이 본문만 (머리말은 카드에 따로 있다). 1920×1080 기준 24px 두 줄 안에 들어가게 짧게.
    /// </summary>
    [CreateAssetMenu(menuName = "QL/UI/Tip Table", fileName = "TipTable")]
    public sealed class TipTable : ScriptableObject
    {
        [TextArea(1, 3)]
        [SerializeField] private string[] tips = new string[0];

        public int Count => tips != null ? tips.Length : 0;

        /// <summary>i 번째 팁 (0 ≤ i &lt; Count).</summary>
        public string Get(int i) => tips[i];
    }
}
