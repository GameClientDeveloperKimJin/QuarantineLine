using QL.UI.Tutorial;
using UnityEngine;

namespace QL.Tests.PlayMode.UI
{
    /// <summary>
    /// F03 (T1 1 재발 방지) — 모든 R4 PlayMode 테스트 공통 정리. 테스트 중 머지로 CursorGuide 가 <see cref="CursorGuide.PrefsKey"/> = 1 을 저장하면
    /// 사람이 Play 할 때 첫 판 가이드가 안 뜬다 → SetUp 첫 줄에서 <see cref="Save"/>, TearDown 첫 줄에서 <see cref="Restore"/> (키가 없었으면 지움).
    /// </summary>
    internal struct GuidePrefsGuard
    {
        private bool _saved;
        private bool _hadKey;
        private int _value;

        /// <summary>지금 키 상태를 기억한다.</summary>
        public static GuidePrefsGuard Save()
        {
            return new GuidePrefsGuard
            {
                _saved = true,
                _hadKey = PlayerPrefs.HasKey(CursorGuide.PrefsKey),
                _value = PlayerPrefs.GetInt(CursorGuide.PrefsKey, 0)
            };
        }

        /// <summary>기억한 상태로 되돌린다 (Save 전이면 아무것도 안 함).</summary>
        public void Restore()
        {
            if (!_saved) return;
            if (_hadKey) PlayerPrefs.SetInt(CursorGuide.PrefsKey, _value);
            else PlayerPrefs.DeleteKey(CursorGuide.PrefsKey);
            PlayerPrefs.Save();
        }
    }
}
