using QL.Core;
using QL.Core.Flow;
using QL.Core.Support;
using QL.Game;
using QL.Game.Flow;
using QL.Game.Slimes;
using QL.UI.Screens;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace QL.Audio
{
    /// <summary>
    /// U08 효과음 · 배경음. GameEvents 를 듣고 소리만 낸다 (게임 상태 변경 없음).
    /// - 띄우기: <see cref="RuntimeInitializeOnLoadMethodAttribute"/> 로 Resources 의 <see cref="SfxTable"/> 를 읽어 「[Audio]」 를 한 번 만들고
    ///   DontDestroyOnLoad (모든 씬 공통 — Game.unity 수정 금지, 보류함 46).
    /// - 구독: 씬이 열릴 때마다 다시 붙인다 (GameBootstrap.OnDestroy 가 GameEvents.ClearAll 로 구독을 지우므로).
    /// - 풀: Sfx · Ui AudioSource 를 미리 만들어 재사용 (Instantiate 0 · 재생 프레임 할당 0). 꽉 차면 우선순위 낮은 것을 끊거나 새 소리를 버림.
    /// - 일시정지: Sfx 그룹만 AudioSource.Pause, Ui 그룹(클릭 · 휴게 · 결과)은 그대로.
    /// - 배경음: 씬 이름 → bgm_main / bgm_battle, 같은 곡이면 끊지 않고 이어서, 다르면 실시간 크로스페이드.
    /// - 버튼 소리: 씬이 열릴 때 Selectable · MainMenuItem 에 <see cref="UiSoundHook"/> 를 붙인다 (UI 코드 수정 없음).
    /// - 씬에 AudioListener 가 없으면 (Main · Intro) 자기 리스너를 켠다.
    /// </summary>
    public sealed class SfxPlayer : MonoBehaviour
    {
        public const string TableResourcePath = "QL_SfxTable";
        // Mixer 노출 파라미터 이름 (QL_Mixer.mixer — dB)
        public const string MasterVolumeParam = "MasterVolume";
        public const string BgmVolumeParam = "BgmVolume";
        public const string SfxVolumeParam = "SfxVolume";
        public const string UiVolumeParam = "UiVolume";

        private const float StopFade = 0.06f; // maxDuration 자를 때 딸깍 방지

        private sealed class Voice
        {
            public AudioSource Source;
            public SfxId Id;
            public int Priority;
            public float BaseVolume;
            public float MaxDuration;
            public bool Active;
            public bool Paused;
        }

        public static SfxPlayer Instance { get; private set; }

        private SfxTable _table;
        private readonly SfxTable.Entry[] _entries = new SfxTable.Entry[(int)SfxId.Count];
        private readonly bool[] _has = new bool[(int)SfxId.Count];
        private readonly float[] _lastPlay = new float[(int)SfxId.Count];
        private Voice[] _sfx;
        private Voice[] _ui;
        private AudioSource _bgmA;
        private AudioSource _bgmB;
        private bool _bgmAIsCurrent = true;
        private float _bgmFade = 1f;     // 0 → 1 (1 = 끝)
        private float _bgmOutStartVolume;
        private AudioListener _listener;
        private bool _gamePaused;
        private bool _resubscribeNextFrame;
        private bool _subscribed;

        public SfxTable Table => _table;
        public bool IsGamePaused => _gamePaused;
        public AudioClip CurrentBgm => (_bgmAIsCurrent ? _bgmA : _bgmB).clip;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            if (Instance != null) return;
            var table = Resources.Load<SfxTable>(TableResourcePath);
            if (table == null) return; // 소리 표 없음 — 조용히 무음
            var go = new GameObject("[Audio]");
            DontDestroyOnLoad(go);
            go.AddComponent<SfxPlayer>().Init(table);
        }

        /// <summary>UI 소리 (클릭 · 호버) — <see cref="UiSoundHook"/> 가 부른다. 플레이어가 없으면 무시.</summary>
        public static void PlayUi(SfxId id)
        {
            if (Instance != null) Instance.Play(id, 1f);
        }

        /// <summary>Mixer 그룹 볼륨 (0~1 선형 → dB). 설정 화면이 생기면 여기에 연결. 노출 파라미터가 없으면 false.</summary>
        public bool SetVolume(string mixerParam, float linear)
        {
            AudioMixer mixer = _table != null && _table.sfxGroup != null ? _table.sfxGroup.audioMixer : null;
            if (mixer == null) return false;
            float db = linear > 0.0001f ? 20f * Mathf.Log10(linear) : -80f;
            return mixer.SetFloat(mixerParam, db);
        }

        private void Init(SfxTable table)
        {
            Instance = this;
            _table = table;
            for (int i = 0; i < _lastPlay.Length; i++) _lastPlay[i] = float.NegativeInfinity;
            SfxTable.Entry[] list = table.entries;
            for (int i = 0; i < list.Length; i++)
            {
                int id = (int)list[i].id;
                if (id < 0 || id >= _entries.Length) continue;
                _entries[id] = list[i];
                _has[id] = list[i].clip != null;
            }

            _sfx = MakeVoices("Sfx", table.sfxVoices, table.sfxGroup);
            _ui = MakeVoices("Ui", table.uiVoices, table.uiGroup);
            _bgmA = MakeSource("BgmA", table.bgmGroup);
            _bgmB = MakeSource("BgmB", table.bgmGroup);
            _bgmA.loop = true;
            _bgmB.loop = true;

            _listener = gameObject.AddComponent<AudioListener>();
            _listener.enabled = false;

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Unsubscribe();
            if (Instance == this) Instance = null;
        }

        private Voice[] MakeVoices(string label, int count, AudioMixerGroup group)
        {
            var voices = new Voice[Mathf.Max(1, count)];
            for (int i = 0; i < voices.Length; i++)
                voices[i] = new Voice { Source = MakeSource(label + i, group) };
            return voices;
        }

        private AudioSource MakeSource(string label, AudioMixerGroup group)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f; // 2D
            src.outputAudioMixerGroup = group;
            return src;
        }

        // ── 씬 ──────────────────────────────────────────────

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            StopVoices(_sfx);
            _gamePaused = false;
            Unsubscribe();
            Subscribe();
            _resubscribeNextFrame = true; // 옛 씬의 ClearAll 이 늦게 와도 한 번 더
            HookUi();
            UpdateListener();
            PlayBgmFor(scene.name);
        }

        private void UpdateListener()
        {
            bool other = false;
            AudioListener[] all = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
                if (all[i] != _listener && all[i].isActiveAndEnabled) { other = true; break; }
            _listener.enabled = !other;
        }

        private static void HookUi()
        {
            Selectable[] sels = FindObjectsByType<Selectable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < sels.Length; i++)
            {
                if (sels[i] is Scrollbar) continue;
                AddHook(sels[i].gameObject);
            }
            MainMenuItem[] items = FindObjectsByType<MainMenuItem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < items.Length; i++) AddHook(items[i].gameObject);
        }

        private static void AddHook(GameObject go)
        {
            if (!go.TryGetComponent(out UiSoundHook _)) go.AddComponent<UiSoundHook>();
        }

        // ── 구독 ────────────────────────────────────────────

        private void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;
            GameEvents.DroneSpawned += OnDroneSpawned;
            GameEvents.DroneMerged += OnDroneMerged;
            GameEvents.SlimeKilled += OnSlimeKilled;
            GameEvents.SlimeReachedHatch += OnSlimeReachedHatch;
            GameEvents.SlimeDamaged += OnSlimeDamaged;
            GameEvents.WaveStarted += OnWaveStarted;
            GameEvents.WaveCleared += OnWaveCleared;
            GameEvents.GameEnded += OnGameEnded;
            GameEvents.StateChanged += OnStateChanged;
            GameEvents.CellCleaned += OnCellCleaned;
            GameEvents.CellContaminated += OnCellContaminated;
            GameEvents.PrepTick += OnPrepTick;
            GameEvents.SupportOffered += OnSupportOffered;
            GameEvents.HatchDamageBlocked += OnHatchDamageBlocked;
        }

        private void Unsubscribe()
        {
            _subscribed = false;
            GameEvents.DroneSpawned -= OnDroneSpawned;
            GameEvents.DroneMerged -= OnDroneMerged;
            GameEvents.SlimeKilled -= OnSlimeKilled;
            GameEvents.SlimeReachedHatch -= OnSlimeReachedHatch;
            GameEvents.SlimeDamaged -= OnSlimeDamaged;
            GameEvents.WaveStarted -= OnWaveStarted;
            GameEvents.WaveCleared -= OnWaveCleared;
            GameEvents.GameEnded -= OnGameEnded;
            GameEvents.StateChanged -= OnStateChanged;
            GameEvents.CellCleaned -= OnCellCleaned;
            GameEvents.CellContaminated -= OnCellContaminated;
            GameEvents.PrepTick -= OnPrepTick;
            GameEvents.SupportOffered -= OnSupportOffered;
            GameEvents.HatchDamageBlocked -= OnHatchDamageBlocked;
        }

        private void OnDroneSpawned(int cell, DroneType type, int grade) => Play(SfxId.Summon, 1f);

        private void OnDroneMerged(int from, int to, DroneType type, int newGrade) =>
            Play(SfxId.Merge, 1f + _table.mergePitchPerGrade * (newGrade - 2));

        private void OnSlimeKilled(SlimeView slime) =>
            Play(SfxId.Kill, slime != null && slime.Type == SlimeType.Mutant ? _table.mutantKillPitch : 1f);

        private void OnSlimeReachedHatch(SlimeView slime) => Play(SfxId.HatchHit, 1f);

        // 발사 이벤트가 없어 드론 직접 공격의 맞음(SlimeDamaged — 잔열 제외)을 발사음으로 쓴다. 최소 간격이 연사를 묶는다.
        private void OnSlimeDamaged(SlimeView slime, int damage, DroneType source)
        {
            switch (source)
            {
                case DroneType.Suction: Play(SfxId.FireSuction, 1f); break;
                case DroneType.Plasma: Play(SfxId.FirePlasma, 1f); break;
                case DroneType.Cryo: Play(SfxId.FireCryo, 1f); break;
            }
        }

        private void OnWaveStarted(int index, int total, string banner) => Play(SfxId.WaveStart, 1f);
        private void OnWaveCleared(int index, PayBreakdown pay) => Play(SfxId.WaveClear, 1f);

        private void OnGameEnded(ResultSummary summary) =>
            Play(summary.Outcome == GameOutcome.Victory ? SfxId.ResultWin : SfxId.ResultLose, 1f);

        private void OnStateChanged(GameState state)
        {
            if (state == GameState.Paused)
            {
                if (_gamePaused) return;
                _gamePaused = true;
                PauseVoices(_sfx, true);
                Play(SfxId.Pause, 1f);
            }
            else if (_gamePaused)
            {
                _gamePaused = false;
                PauseVoices(_sfx, false);
            }
        }

        private void OnCellCleaned(int cell, bool byMerge)
        {
            if (byMerge) Play(SfxId.Clean, 1f);
        }

        private void OnCellContaminated(int cell, int lane, float duration) => Play(SfxId.Contaminate, 1f);

        private void OnPrepTick(int secondsLeft)
        {
            if (secondsLeft > 0 && secondsLeft <= _table.countdownTickFrom) Play(SfxId.CountdownTick, 1f);
        }

        private void OnSupportOffered(int clearedWaveIndex, SupportCardSpec[] cards) => Play(SfxId.CardAppear, 1f);
        private void OnHatchDamageBlocked(SlimeView slime) => Play(SfxId.Insurance, 1f);

        // ── 재생 ────────────────────────────────────────────

        /// <summary>한 번 재생. 클립 없음 · 최소 간격 안 · 동시 수 초과 · 풀 꽉 참(우선순위 낮음)이면 조용히 무시.</summary>
        public void Play(SfxId id, float pitchMul)
        {
            int i = (int)id;
            if (i < 0 || i >= _has.Length || !_has[i]) return;
            ref SfxTable.Entry e = ref _entries[i];
            Voice[] pool = e.bus == SfxBus.Ui ? _ui : _sfx;
            if (e.bus == SfxBus.Sfx && _gamePaused) return;

            float now = Time.unscaledTime;
            if (now - _lastPlay[i] < e.minInterval) return;
            if (e.maxInstances > 0 && CountPlaying(pool, id) >= e.maxInstances) return;

            Voice v = FindVoice(pool, e.priority);
            if (v == null) return;
            _lastPlay[i] = now;

            AudioSource src = v.Source;
            if (src.isPlaying) src.Stop();
            src.clip = e.clip;
            src.volume = e.volume;
            float jitter = e.pitchJitter > 0f ? Random.Range(-e.pitchJitter, e.pitchJitter) : 0f;
            src.pitch = pitchMul * (1f + jitter);
            v.Id = id;
            v.Priority = e.priority;
            v.BaseVolume = e.volume;
            v.MaxDuration = e.maxDuration;
            v.Active = true;
            v.Paused = false;
            src.Play();
        }

        private static int CountPlaying(Voice[] pool, SfxId id)
        {
            int n = 0;
            for (int i = 0; i < pool.Length; i++)
                if (pool[i].Active && pool[i].Id == id && (pool[i].Paused || pool[i].Source.isPlaying)) n++;
            return n;
        }

        private static Voice FindVoice(Voice[] pool, int priority)
        {
            Voice lowest = null;
            for (int i = 0; i < pool.Length; i++)
            {
                Voice v = pool[i];
                if (!v.Active || (!v.Paused && !v.Source.isPlaying)) return v;
                if (lowest == null || v.Priority < lowest.Priority) lowest = v;
            }
            return lowest != null && lowest.Priority < priority ? lowest : null;
        }

        private static void PauseVoices(Voice[] pool, bool pause)
        {
            for (int i = 0; i < pool.Length; i++)
            {
                Voice v = pool[i];
                if (!v.Active) continue;
                if (pause)
                {
                    if (!v.Source.isPlaying) continue;
                    v.Source.Pause();
                    v.Paused = true;
                }
                else if (v.Paused)
                {
                    v.Source.UnPause();
                    v.Paused = false;
                }
            }
        }

        private static void StopVoices(Voice[] pool)
        {
            if (pool == null) return;
            for (int i = 0; i < pool.Length; i++)
            {
                pool[i].Source.Stop();
                pool[i].Active = false;
                pool[i].Paused = false;
            }
        }

        private void Update()
        {
            if (_resubscribeNextFrame)
            {
                _resubscribeNextFrame = false;
                Unsubscribe();
                Subscribe();
            }
            TrimVoices(_sfx);
            TrimVoices(_ui);
            TickBgm(Time.unscaledDeltaTime);
        }

        // maxDuration 이 있는 소리를 끝 직전 짧게 줄여 멈춘다 (source.time 은 일시정지 중 멈춰 있음).
        private static void TrimVoices(Voice[] pool)
        {
            for (int i = 0; i < pool.Length; i++)
            {
                Voice v = pool[i];
                if (!v.Active || v.Paused) continue;
                AudioSource src = v.Source;
                if (!src.isPlaying) { v.Active = false; continue; }
                if (v.MaxDuration <= 0f) continue;
                float left = v.MaxDuration - src.time;
                if (left <= 0f) { src.Stop(); v.Active = false; }
                else if (left < StopFade) src.volume = v.BaseVolume * (left / StopFade);
            }
        }

        // ── 배경음 ──────────────────────────────────────────

        private void PlayBgmFor(string sceneName)
        {
            AudioClip clip;
            if (Contains(_table.battleBgmScenes, sceneName)) clip = _table.bgmBattle;
            else if (Contains(_table.mainBgmScenes, sceneName)) clip = _table.bgmMain;
            else return; // 목록에 없는 씬 — 지금 곡 유지
            CrossfadeTo(clip);
        }

        private static bool Contains(string[] names, string name)
        {
            if (names == null) return false;
            for (int i = 0; i < names.Length; i++)
                if (names[i] == name) return true;
            return false;
        }

        /// <summary>배경음 바꾸기 (null = 꺼짐). 지금 곡과 같으면 그대로 이어서.</summary>
        public void CrossfadeTo(AudioClip clip)
        {
            AudioSource cur = _bgmAIsCurrent ? _bgmA : _bgmB;
            if (clip != null && cur.clip == clip && cur.isPlaying) return;
            if (clip == null && !cur.isPlaying) return;

            AudioSource next = _bgmAIsCurrent ? _bgmB : _bgmA;
            next.Stop();
            next.clip = clip;
            next.volume = 0f;
            if (clip != null) next.Play();
            _bgmOutStartVolume = cur.isPlaying ? cur.volume : 0f;
            _bgmAIsCurrent = !_bgmAIsCurrent;
            _bgmFade = _table.bgmCrossfade > 0f ? 0f : 1f;
            ApplyBgmFade();
        }

        private void TickBgm(float dt)
        {
            if (_bgmFade >= 1f) return;
            _bgmFade = Mathf.Min(1f, _bgmFade + dt / _table.bgmCrossfade);
            ApplyBgmFade();
        }

        private void ApplyBgmFade()
        {
            AudioSource cur = _bgmAIsCurrent ? _bgmA : _bgmB;
            AudioSource prev = _bgmAIsCurrent ? _bgmB : _bgmA;
            cur.volume = _table.bgmVolume * _bgmFade;
            prev.volume = _bgmOutStartVolume * (1f - _bgmFade);
            if (_bgmFade >= 1f && prev.isPlaying) prev.Stop();
        }
    }
}
