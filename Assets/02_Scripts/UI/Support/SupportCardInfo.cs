using System.Text;
using QL.Core;
using QL.Core.Support;
using QL.Core.Stats;
using QL.Core.Units;
using QL.Data;
using QL.FX;
using QL.Game.Support;
using UnityEngine;

namespace QL.UI.Support
{
    /// <summary>툴팁 「지금 값」 줄 종류 (U19).</summary>
    public enum SupportTipKind
    {
        None,
        /// <summary>드론 스탯 — 「3등급 흡입 드론 45 → 54」 (StatResolver.Get · 기본 = 최고 등급 UnitStats).</summary>
        Drone,
        /// <summary>처치 보상 — 「점액 처치 보상 5 → 6」 (GetGlobalInt).</summary>
        Kill,
        /// <summary>머지 코인 — 「머지 1회 코인 +5」.</summary>
        Merge,
        /// <summary>횟수형 — 「남은 n회」.</summary>
        Remain
    }

    /// <summary>
    /// 카드 1장의 화면 글자 · 색 · 아이콘 (U19). 판 시작 뒤 처음 쓸 때 한 번 만든다 (<see cref="SupportCardInfoTable"/>) — 그 뒤로는 읽기만 (할당 0).
    /// 숫자가 바뀌는 곳 (칩 수치 · 툴팁 · 중첩) 은 형식 문자열 + 숫자로 TMP SetText 한다.
    /// </summary>
    public sealed class SupportCardInfo
    {
        public int CardIndex;
        public SupportCardData Data;
        public string Id;
        public string Name;
        public CardCategory Category;
        public Sprite Icon;
        /// <summary>카드 위 설명 — {value} 치환 · 괄호 설명 뺌.</summary>
        public string CardDescription;
        /// <summary>툴팁 설명 — {value} 치환 · 전부.</summary>
        public string FullDescription;
        /// <summary>카드 큰 수치 글자.</summary>
        public string Big;
        /// <summary>큰 수치 아래 배지 (없으면 null).</summary>
        public string Note;
        /// <summary>결재란 위 왼쪽 — 지속 · 횟수형 · n회 · 즉시.</summary>
        public string Kind;
        public int MaxStacks;
        /// <summary>효과가 켜지는 웨이브 (1부터, 0 = 항상 — C12 = 3).</summary>
        public int ActiveWave;
        /// <summary>횟수형 효과가 있다 (칩 배지 「남은 n」).</summary>
        public bool Counted;
        /// <summary>즉시형뿐 (칩 없음 — C06 · C08 · C09).</summary>
        public bool InstantOnly;
        /// <summary>칩 이름 + 수치 (TMP 형식 — {0} = 수치 크기). 수치 없는 카드는 이름만 (<see cref="ChipValueMagnitude"/> 0).</summary>
        public string ChipFormat;
        /// <summary>한 장 수치 크기 (부호 없음). 지속형은 중첩 수만큼 곱해 보여 준다.</summary>
        public float ChipValueMagnitude;
        public bool ChipValueStacks;
        public SupportTipKind TipKind;
        /// <summary>툴팁 셋째 줄 형식 ({0} 기본 · {1} 최종 — 종류에 따라 하나만).</summary>
        public string TipFormat;
        public DroneType TipType;
        public StatKey TipStat;
        public int TipGrade;
        /// <summary>툴팁 머리 — 「야근 수당 C12」.</summary>
        public string TipHeader;

        public Color CategoryColor;
    }

    /// <summary>
    /// 카드 풀 전체의 <see cref="SupportCardInfo"/> (풀 인덱스 순). GameBootstrap 이 준비된 뒤 처음 부를 때 한 번 만든다.
    /// 문구 · 색 · 카드별 손질은 FxConfig 「U19」, 이름 · 설명 · 아이콘 · 효과 값은 카드 SO 그대로 (읽기만).
    /// </summary>
    public static class SupportCardInfoTable
    {
        private const string ValueToken = "{value}";
        private const char Minus = '−';

        public static SupportCardInfo[] Build(SupportCardRunner runner, FxConfig fx, UnitCatalog catalog, GameDataSet data)
        {
            if (runner == null || fx == null) return new SupportCardInfo[0];
            int n = runner.CardCount;
            var table = new SupportCardInfo[n];
            for (int i = 0; i < n; i++) table[i] = BuildOne(i, runner, fx, catalog, data);
            return table;
        }

        private static SupportCardInfo BuildOne(int index, SupportCardRunner runner, FxConfig fx, UnitCatalog catalog, GameDataSet data)
        {
            SupportCardSpec spec = runner.GetCard(index);
            SupportCardData so = runner.GetData(index);
            var info = new SupportCardInfo
            {
                CardIndex = index,
                Data = so,
                Id = spec.Id,
                Name = so != null ? so.DisplayName : spec.Id,
                Category = spec.Category,
                Icon = so != null ? so.Icon : null,
                MaxStacks = spec.MaxStacks,
                CategoryColor = CategoryColor(fx, spec.Category)
            };

            CardEffectSpec[] effects = spec.Effects;
            CardEffectSpec main = effects.Length > 0 ? effects[0] : default(CardEffectSpec);
            bool instantOnly = effects.Length > 0;
            int count = 0;
            for (int e = 0; e < effects.Length; e++)
            {
                if (effects[e].Kind != CardEffectKind.Instant) instantOnly = false;
                if (effects[e].Kind == CardEffectKind.Counted) { info.Counted = true; count += effects[e].Count; }
                if (effects[e].ActiveWave > 0) info.ActiveWave = effects[e].ActiveWave;
            }
            info.InstantOnly = instantOnly;

            // 값 · 단위 ({value} 바로 뒤 글자 — 「{value}초」 · 「{value}칸」)
            string desc = so != null && so.Description != null ? so.Description : string.Empty;
            bool hasValue = desc.IndexOf(ValueToken, System.StringComparison.Ordinal) >= 0;
            string unit = UnitAfterValue(desc);
            string valueText = hasValue ? ValueText(in main) : string.Empty;
            info.FullDescription = hasValue ? desc.Replace(ValueToken, valueText) : desc;
            info.CardDescription = StripNote(info.FullDescription);

            // 큰 수치 · 배지 — 손질 표가 먼저
            SupportCardLook look = FindLook(fx, spec.Id);
            if (!string.IsNullOrEmpty(look.big)) info.Big = look.big;
            else if (hasValue) info.Big = valueText + unit;
            else if (count > 0) info.Big = count + "회";
            else info.Big = string.Empty;
            if (!string.IsNullOrEmpty(look.note)) info.Note = look.note;
            else if (info.ActiveWave > 0) info.Note = fx.supportWaveOnlyFormat.Replace("{0}", info.ActiveWave.ToString());

            // 결재란 위 — 종류
            if (instantOnly) info.Kind = fx.supportKindInstant;
            else if (info.Counted) info.Kind = fx.supportKindCountedFormat.Replace("{0}", count.ToString());
            else info.Kind = fx.supportKindModifier;

            // 칩 — 이름 + 수치 형식
            BuildChip(info, in main, hasValue, unit);

            // 툴팁
            info.TipHeader = info.Name + "  <size=80%>" + spec.Id + "</size>";
            BuildTip(info, in main, fx, catalog, data);
            return info;
        }

        public static Color CategoryColor(FxConfig fx, CardCategory category)
        {
            int c = (int)category;
            return fx.supportCategoryColors != null && c >= 0 && c < fx.supportCategoryColors.Length ? fx.supportCategoryColors[c] : Color.white;
        }

        public static string CategoryName(FxConfig fx, CardCategory category)
        {
            int c = (int)category;
            return fx.supportCategoryNames != null && c >= 0 && c < fx.supportCategoryNames.Length ? fx.supportCategoryNames[c] : string.Empty;
        }

        private static SupportCardLook FindLook(FxConfig fx, string id)
        {
            SupportCardLook[] looks = fx.supportCardLooks;
            if (looks != null)
                for (int i = 0; i < looks.Length; i++)
                    if (looks[i].id == id) return looks[i];
            return default(SupportCardLook);
        }

        /// <summary>{value} 자리 글자 — Percent 「+20%」 · Add 「−15」 「+0.5」 · 예비 드론 대수는 부호 없음.</summary>
        public static string ValueText(in CardEffectSpec e)
        {
            float v = e.Op == ModifierOp.Percent ? e.Value * 100f : e.Value;
            bool signed = !(e.Kind == CardEffectKind.Instant && e.Stat == StatKey.FreeDrone);
            var sb = new StringBuilder(8);
            if (signed) sb.Append(v < 0f ? Minus : '+');
            sb.Append(Number(Mathf.Abs(v)));
            if (e.Op == ModifierOp.Percent) sb.Append('%');
            return sb.ToString();
        }

        private static string Number(float v)
        {
            float r = Mathf.Round(v);
            if (Mathf.Abs(v - r) < 0.001f) return ((int)r).ToString();
            return v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string UnitAfterValue(string desc)
        {
            int at = desc.IndexOf(ValueToken, System.StringComparison.Ordinal);
            if (at < 0) return string.Empty;
            int start = at + ValueToken.Length;
            int end = start;
            while (end < desc.Length && !char.IsWhiteSpace(desc[end]) && desc[end] != ',' && desc[end] != '(' && desc[end] != '.') end++;
            return desc.Substring(start, end - start);
        }

        /// <summary>「 (」 뒤 괄호 설명을 뺀다 (카드 위는 짧게 — 전부는 툴팁).</summary>
        private static string StripNote(string s)
        {
            int p = s.IndexOf(" (", System.StringComparison.Ordinal);
            return p > 0 ? s.Substring(0, p) : s;
        }

        private static void BuildChip(SupportCardInfo info, in CardEffectSpec main, bool hasValue, string unit)
        {
            info.ChipValueMagnitude = 0f;
            info.ChipValueStacks = false;
            if (!hasValue || info.InstantOnly)
            {
                info.ChipFormat = info.Name;
                return;
            }

            float v = main.Op == ModifierOp.Percent ? main.Value * 100f : main.Value;
            float mag = Mathf.Abs(v);
            bool integer = Mathf.Abs(mag - Mathf.Round(mag)) < 0.001f;
            var sb = new StringBuilder(info.Name.Length + 24);
            sb.Append(info.Name).Append("  <color=#").Append(ColorUtility.ToHtmlStringRGB(info.CategoryColor)).Append('>');
            sb.Append(v < 0f ? Minus : '+');
            sb.Append(integer ? "{0}" : "{0:1}");
            if (main.Op == ModifierOp.Percent) sb.Append('%');
            sb.Append(unit).Append("</color>");
            info.ChipFormat = sb.ToString();
            info.ChipValueMagnitude = mag;
            info.ChipValueStacks = main.Kind == CardEffectKind.Modifier; // 지속형 중첩 = 값 합 (횟수형은 횟수가 합쳐짐 — 값 그대로)
        }

        private static void BuildTip(SupportCardInfo info, in CardEffectSpec main, FxConfig fx, UnitCatalog catalog, GameDataSet data)
        {
            info.TipKind = SupportTipKind.None;
            info.TipFormat = string.Empty;
            if (info.Counted)
            {
                info.TipKind = SupportTipKind.Remain;
                info.TipFormat = fx.supportTipRemainFormat;
                return;
            }
            if (main.Kind != CardEffectKind.Modifier) return;

            switch (main.Stat)
            {
                case StatKey.Damage:
                case StatKey.AttackInterval:
                case StatKey.Range:
                case StatKey.SlowDuration:
                case StatKey.SlowRate:
                {
                    if (catalog == null) return;
                    DroneType type = main.TargetAll ? DroneType.Suction : main.TargetType;
                    int grade = catalog.MaxGrade(type);
                    int t = (int)type;
                    string droneName = fx.supportDroneNames != null && t < fx.supportDroneNames.Length ? fx.supportDroneNames[t] : type.ToString();
                    string pair;
                    switch (main.Stat)
                    {
                        case StatKey.AttackInterval: pair = " 드론 간격 {0:2}초 → {1:2}초"; break;
                        case StatKey.SlowDuration: pair = " 드론 감속 {0:1}초 → {1:1}초"; break;
                        case StatKey.SlowRate: pair = " 드론 감속률 {0}% → {1}%"; break;
                        case StatKey.Range: pair = " 드론 사거리 {0}칸 → {1}칸"; break;
                        default: pair = " 드론 {0} → {1}"; break;
                    }
                    info.TipKind = SupportTipKind.Drone;
                    info.TipType = type;
                    info.TipStat = main.Stat;
                    info.TipGrade = grade;
                    info.TipFormat = fx.supportTipGradePrefix.Replace("{0}", grade.ToString()) + droneName + pair;
                    return;
                }
                case StatKey.KillReward:
                    if (data == null || data.GetEnemy(SlimeType.Blob) == null) return;
                    info.TipKind = SupportTipKind.Kill;
                    info.TipFormat = fx.supportTipKillFormat;
                    return;
                case StatKey.MergeReward:
                    info.TipKind = SupportTipKind.Merge;
                    info.TipFormat = fx.supportTipMergeFormat;
                    return;
            }
        }

        /// <summary>드론 스탯 기본 값 (최고 등급 UnitStats).</summary>
        public static float BaseStat(UnitCatalog catalog, DroneType type, int grade, StatKey stat)
        {
            UnitStats s;
            if (catalog == null || !catalog.TryGet(type, grade, out s)) return 0f;
            switch (stat)
            {
                case StatKey.Damage: return s.Damage;
                case StatKey.AttackInterval: return s.AttackInterval;
                case StatKey.Range: return s.Range;
                case StatKey.SlowDuration: return s.SlowDuration;
                case StatKey.SlowRate: return s.SlowRate;
                default: return 0f;
            }
        }
    }
}
