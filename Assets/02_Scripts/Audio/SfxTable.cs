using System;
using UnityEngine;
using UnityEngine.Audio;

namespace QL.Audio
{
    /// <summary>
    /// U08 소리 표 (S3 규칙 6 — 수치는 코드 밖). 에셋: <c>Assets/07_Audio/Resources/QL_SfxTable.asset</c>
    /// (<see cref="SfxPlayer"/> 가 Resources 로 읽어 모든 씬에 한 번 띄운다 — Game.unity 수정 없음, 보류함 46).
    /// 항목마다 클립 · 볼륨 · 피치 무작위 · 최소 간격 · 동시 수 · 우선순위 · 최대 길이 · 그룹. 클립이 비면 조용히 건너뜀.
    /// </summary>
    [CreateAssetMenu(menuName = "QL/Sfx Table", fileName = "QL_SfxTable")]
    public sealed class SfxTable : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public SfxId id;
            [Tooltip("비어 있으면 조용히 건너뜀")]
            public AudioClip clip;
            [Range(0f, 1f)] public float volume;
            [Tooltip("피치 무작위 ± (0.05 = ±5%)")]
            [Range(0f, 0.3f)] public float pitchJitter;
            [Tooltip("같은 소리 최소 간격 (실시간 초) — 안에 오면 무시")]
            [Min(0f)] public float minInterval;
            [Tooltip("이 소리만의 동시 재생 수 (0 = 제한 없음, 전체 상한만)")]
            [Min(0)] public int maxInstances;
            [Tooltip("풀이 꽉 차면 이 값이 더 낮은 소리를 끊고 들어간다 (같거나 높으면 버림)")]
            public int priority;
            [Tooltip("이 길이(초)까지만 재생 (0 = 끝까지) — 긴 발사음 자르기")]
            [Min(0f)] public float maxDuration;
            public SfxBus bus;
        }

        [Header("Mixer (Assets/07_Audio/QL_Mixer.mixer)")]
        public AudioMixerGroup bgmGroup;
        public AudioMixerGroup sfxGroup;
        public AudioMixerGroup uiGroup;

        [Header("동시 재생 상한 (= AudioSource 풀 크기)")]
        [Min(1)] public int sfxVoices = 10;
        [Min(1)] public int uiVoices = 4;

        [Header("이벤트별 피치")]
        [Tooltip("머지 — 새 등급 2 기준, 1 등급 오를 때마다 더하는 피치")]
        public float mergePitchPerGrade = 0.08f;
        [Tooltip("처치 — 변이 점액이면 곱하는 피치")]
        public float mutantKillPitch = 0.85f;
        [Tooltip("준비 카운트다운 — 이 초 이하에서만 틱")]
        public int countdownTickFrom = 3;

        [Header("배경음 (루프)")]
        public AudioClip bgmMain;
        public AudioClip bgmBattle;
        [Range(0f, 1f)] public float bgmVolume = 0.5f;
        [Tooltip("씬 바뀔 때 크로스페이드 (실시간 초)")]
        [Min(0f)] public float bgmCrossfade = 0.5f;
        [Tooltip("bgm_main 을 트는 씬 이름")]
        public string[] mainBgmScenes = { "Title", "Main", "Intro" };
        [Tooltip("bgm_battle 을 트는 씬 이름 (둘 다 아니면 지금 곡 유지)")]
        public string[] battleBgmScenes = { "Game" };

        [Header("항목")]
        public Entry[] entries = Array.Empty<Entry>();
    }
}
