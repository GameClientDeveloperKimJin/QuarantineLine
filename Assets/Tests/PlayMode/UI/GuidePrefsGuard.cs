using QL.UI.Tutorial;
using UnityEngine;

namespace QL.Tests.PlayMode.UI
{
    /// <summary>
    /// F03 (T1 1 재발 방지) — 모든 R4 PlayMode 테스트 공통 정리. 테스트 중 머지로 CursorGuide 가 <see cref="CursorGuide.PrefsKey"/> = 1 을 저장하면
    /// 사람이 Play 할 때 첫 판 가이드가 안 뜬다 → SetUp 첫 줄에서 <see cref="Save"/>, TearDown 첫 줄에서 <see cref="Restore"/> (키가 없었으면 지움).
    /// U18a: Save 는 키를 1 (가이드 끝남) 로 둔다 — 첫 판이 필요하면 테스트가 지운다. Save ~ Restore 동안 CursorGuide.EditorResetSuppressed = true — UIRoot 의 디버그 editorResetOnPlay 가 켜져 있어도 테스트가 키를 잃지 않는다.
    /// </summary>
    internal struct GuidePrefsGuard
    {
        private bool _saved;
        private bool _hadKey;
        private int _value;

        /// <summary>지금 키 상태를 기억한다.</summary>
        public static GuidePrefsGuard Save()
        {
#if UNITY_EDITOR
            CursorGuide.EditorResetSuppressed = true; // U18a — 프리팹 디버그 값(editorResetOnPlay)과 무관하게
#endif
            var g = new GuidePrefsGuard
            {
                _saved = true,
                _hadKey = PlayerPrefs.HasKey(CursorGuide.PrefsKey),
                _value = PlayerPrefs.GetInt(CursorGuide.PrefsKey, 0)
            };
            // U18a — 가이드가 뜨면 준비 시간을 잡는다 (G12). 가이드를 보지 않는 테스트가 사람의 키 상태에 따라 웨이브에 못 가지 않게 기본 = 끝남.
            // 첫 판이 필요한 테스트 (CursorGuideTests) 는 Save 뒤 키를 지운다.
            PlayerPrefs.SetInt(CursorGuide.PrefsKey, 1);
            return g;
        }

        /// <summary>기억한 상태로 되돌린다 (Save 전이면 아무것도 안 함).</summary>
        public void Restore()
        {
            if (!_saved) return;
#if UNITY_EDITOR
            CursorGuide.EditorResetSuppressed = false;
#endif
            if (_hadKey) PlayerPrefs.SetInt(CursorGuide.PrefsKey, _value);
            else PlayerPrefs.DeleteKey(CursorGuide.PrefsKey);
            PlayerPrefs.Save();
        }
    }
}
