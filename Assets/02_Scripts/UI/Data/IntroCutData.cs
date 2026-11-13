using System;
using UnityEngine;

namespace QL.UI.Data
{
    /// <summary>
    /// U23 인트로 그림 컷 — 그림 · 자막 · 효과음 자리 (<c>Assets/04_Resources/Data/UI/IntroCutData.asset</c>).
    /// 순서 = 보여 주는 순서. 그림 원본은 <c>Assets/06_Art/Intro/intro_01 ~ 14.png</c> (자막 없음 — 자막은 TMP 로 얹는다).
    /// </summary>
    [CreateAssetMenu(menuName = "QL/UI/Intro Cut Data", fileName = "IntroCutData")]
    public sealed class IntroCutData : ScriptableObject
    {
        [Serializable]
        public struct Cut
        {
            public Sprite image;
            [TextArea(1, 3)] public string caption;
            [Tooltip("컷이 시작될 때 한 번 (U08 소리 전 — 비워 두면 조용). 14번 도장 컷 = 「쾅」 자리")]
            public AudioClip sfx;
        }

        [SerializeField] private Cut[] cuts = Array.Empty<Cut>();

        [Header("버튼 · 진행 표시")]
        [SerializeField] private string skipLabel = "건너뛰기";
        [Tooltip("{0} = 지금 컷 (1부터) · {1} = 전체 컷 수 — TMP SetText 숫자")]
        [SerializeField] private string progressFormat = "{0} / {1}";

        public int Count => cuts.Length;
        public Cut this[int index] => cuts[index];
        public string SkipLabel => skipLabel;
        public string ProgressFormat => progressFormat;
    }
}
