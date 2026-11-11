using System;
using UnityEngine;

namespace QL.UI.Data
{
    /// <summary>U16 관제실 메신저 트리거 (원본 WO-UI-03 대사 표 순서). 우선순위 숫자는 SO 에 있다 — 이 순서와 무관.</summary>
    public enum OpsTrigger
    {
        /// <summary>해치 HP 위기 (HatchHpChanged · 0 &lt; now ≤ hatchCriticalHp) — 판에 1번.</summary>
        HatchCritical = 0,
        /// <summary>변이 점액 등장 (SlimeSpawned · Type == Mutant) — 핑 = 그 점액 위치.</summary>
        MutantSpawned = 1,
        /// <summary>해치 피격 (SlimeReachedHatch) — 핑 = 그 레인 격리선.</summary>
        HatchBreached = 2,
        /// <summary>칸 오염 (CellContaminated) — 핑 = 그 칸.</summary>
        CellContaminated = 3,
        /// <summary>최고 등급 결합 (DroneMerged · newGrade = MaxGrade) — 판에 1번.</summary>
        TopGradeMerge = 4,
    }

    /// <summary>
    /// 관제실 메신저 대사 (U16 · 원본 WO-UI-03) — <c>Assets/04_Resources/Data/UI/OpsLineTable.asset</c>.
    /// 트리거마다 우선순위(1 = 가장 높음) · 레인 핑 여부 · 대사 목록 (순서대로 돌림). <c>{lane}</c> 은 화면 레인 번호 (1부터) 로 바뀐다.
    /// 머리 · 이름 · 입력 줄 문구도 여기. 코드 수정 없이 대사 · 우선순위 · 핑을 바꾼다.
    /// 말풍선은 24px 두 줄 — 한 줄이 길면 <c>\n</c> 으로 직접 끊는다 (OpsMessengerTests 가 두 줄 안에 드는지 잰다).
    /// </summary>
    [CreateAssetMenu(menuName = "QL/UI/Ops Line Table", fileName = "OpsLineTable")]
    public sealed class OpsLineTable : ScriptableObject
    {
        public const string LaneToken = "{lane}";

        [Serializable]
        public struct Entry
        {
            public OpsTrigger trigger;
            [Tooltip("1 = 가장 높음. 쿨다운 중엔 마지막으로 띄운 것보다 숫자가 작을 때만 끼어든다")]
            [Min(1)] public int priority;
            [Tooltip("대사에 나온 위치 바닥에 핑")]
            public bool ping;
            [Tooltip("순서대로 돌림. {lane} = 레인 번호 (1부터)")]
            [TextArea(1, 3)] public string[] lines;
        }

        [Header("머리 · 이름")]
        [SerializeField] private string channel = "관제실 채널 · 업무 중";
        [SerializeField] private string speaker = "박 대리 · 관제실";
        [SerializeField] private string typing = "김 사원 입력 중";

        [Header("트리거")]
        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public string Channel => channel;
        public string Speaker => speaker;
        public string Typing => typing;
        public int EntryCount => entries != null ? entries.Length : 0;
        public Entry EntryAt(int i) => entries[i];

        /// <summary>그 트리거 칸 번호 (없으면 −1). 시작 때 한 번 캐시해서 쓴다.</summary>
        public int IndexOf(OpsTrigger trigger)
        {
            if (entries == null) return -1;
            for (int i = 0; i < entries.Length; i++)
                if (entries[i].trigger == trigger) return i;
            return -1;
        }

        /// <summary>대사 수 (칸이 없으면 0).</summary>
        public int LineCount(OpsTrigger trigger)
        {
            int i = IndexOf(trigger);
            return i >= 0 && entries[i].lines != null ? entries[i].lines.Length : 0;
        }

        /// <summary>순서 돌림 대사 (rotation 을 대사 수로 나눈 나머지). 없으면 빈 문자열.</summary>
        public string Line(OpsTrigger trigger, int rotation)
        {
            int i = IndexOf(trigger);
            if (i < 0 || entries[i].lines == null || entries[i].lines.Length == 0) return string.Empty;
            string[] lines = entries[i].lines;
            int n = lines.Length;
            return lines[((rotation % n) + n) % n] ?? string.Empty;
        }

        /// <summary>{lane} 을 화면 레인 번호(lane0 + 1)로. 토큰이 없으면 같은 문자열 (할당 없음), 있으면 1회 할당.</summary>
        public static string Format(string line, int lane0)
        {
            if (string.IsNullOrEmpty(line) || line.IndexOf(LaneToken, StringComparison.Ordinal) < 0) return line ?? string.Empty;
            return line.Replace(LaneToken, LaneLabel(lane0));
        }

        private static readonly string[] LaneLabels = { "1", "2", "3", "4", "5", "6", "7", "8", "9" };

        private static string LaneLabel(int lane0)
        {
            int n = lane0 + 1;
            return n >= 1 && n <= LaneLabels.Length ? LaneLabels[n - 1] : n.ToString();
        }
    }
}
