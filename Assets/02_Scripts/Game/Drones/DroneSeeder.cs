using System;
using QL.Core;
using QL.Game.Bootstrap;
using UnityEngine;

namespace QL.Game.Drones
{
    /// <summary>
    /// 테스트 · 확인용 시작 배치 (소환 버튼 대신 쓰는 임시 도구, 코인 안 씀).
    /// G03 에서 소환이 생기면 R1 지시에 따라 끈다.
    /// </summary>
    public sealed class DroneSeeder : MonoBehaviour
    {
        [Serializable]
        public struct Entry
        {
            public int lane;
            public int row;
            public DroneType type;
            [Min(1)] public int grade;

            public Entry(int lane, int row, DroneType type, int grade)
            {
                this.lane = lane;
                this.row = row;
                this.type = type;
                this.grade = grade;
            }
        }

        [SerializeField] private GameBootstrap bootstrap;
        [SerializeField] private DroneBoard board;

        [Tooltip("확인 시나리오: 흡입 1등급 4 · 소각 1등급 2 · 냉각 2등급 1 · 냉각 1등급 1")]
        [SerializeField] private Entry[] entries =
        {
            new Entry(0, 0, DroneType.Suction, 1),
            new Entry(1, 0, DroneType.Suction, 1),
            new Entry(3, 0, DroneType.Suction, 1),
            new Entry(4, 0, DroneType.Suction, 1),
            new Entry(1, 1, DroneType.Plasma, 1),
            new Entry(3, 1, DroneType.Plasma, 1),
            new Entry(0, 2, DroneType.Cryo, 2),
            new Entry(4, 2, DroneType.Cryo, 1),
        };

        private void Start()
        {
            if (bootstrap == null || !bootstrap.IsReady || board == null || !board.IsReady)
            {
                Debug.LogError("[DroneSeeder] GameBootstrap 또는 DroneBoard 가 준비되지 않았다", this);
                return;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                Entry e = entries[i];
                int index = bootstrap.Grid.IndexOf(e.lane, e.row);
                if (index < 0)
                {
                    Debug.LogError("[DroneSeeder] entries[" + i + "] 칸 범위 밖 (" + e.lane + ", " + e.row + ")", this);
                    continue;
                }
                try
                {
                    bootstrap.Grid.Place(index, e.type, e.grade);
                }
                catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
                {
                    Debug.LogError("[DroneSeeder] entries[" + i + "] 배치 실패: " + ex.Message, this);
                }
            }

            board.SyncAll();
        }
    }
}
