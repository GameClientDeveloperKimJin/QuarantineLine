using UnityEngine;

namespace QL.UI.Data
{
    /// <summary>
    /// 결과 화면 결재 도장 (U13 · 원본 WO-UI-02) — 승리 <c>Assets/04_Resources/Data/UI/Stamp_Cleaned.asset</c> · 패배 <c>Stamp_Rework.asset</c>.
    /// 문구(「청소 완료」 · 「결재 · C-3 관제실」 · 「재작업 요청」)와 주홍 잉크는 그림에 구워져 있어 여기는 그림 · 각도 범위 · 크기만.
    /// 그림은 정면(0°)으로 그려져 있고 결과가 뜰 때마다 [angleMin, angleMax] 에서 무작위로 기울인다.
    /// </summary>
    [CreateAssetMenu(menuName = "QL/UI/Stamp Data", fileName = "Stamp")]
    public sealed class StampData : ScriptableObject
    {
        [Tooltip("도장 그림 (A12 UI_Stamp_Cleaned · UI_Stamp_Rework — 납품 전에는 03_Prefabs/UI/Temp 임시 그림)")]
        [SerializeField] private Sprite sprite;
        [Tooltip("기울기 범위 (도, Z 회전 — 음수 = 시계 방향). 원본 −14 ~ −8")]
        [SerializeField] private float angleMin = -14f;
        [SerializeField] private float angleMax = -8f;
        [Tooltip("찍힌 뒤 크기 (px, 1920×1080 기준 · 그림 비율 2:1)")]
        [SerializeField] private Vector2 size = new Vector2(360f, 180f);

        public Sprite Sprite => sprite;
        public float AngleMin => angleMin;
        public float AngleMax => angleMax;
        public Vector2 Size => size;
    }
}
