using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using DG.Tweening;

/// <summary>
/// 게임 전역에서 사용하는 오디오 매니저 싱글톤.
/// - SFX 재생 (풀링)
/// - BGM 재생 (루프 + 전환 시 크로스페이드)
/// - 믹서 파라미터를 이용한 먹먹한(Muffle) 효과
/// key(string) -> AudioClip 매핑으로 문자열 호출만으로 재생 가능
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Serializable]
    public class SoundEntry
    {
        public string key;
        public AudioClip clip;
    }

    [Header("Mixer")]
    [SerializeField] private AudioMixer mixer;
    [SerializeField] private string bgmVolumeParam = "BGMVolume";
    [SerializeField] private string muffleParam = "Muk";
    [SerializeField] private AudioMixerGroup sfxMixerGroup;
    [SerializeField] private AudioMixerGroup bgmMixerGroup;

    [Header("Muffle 효과 설정")]
    [SerializeField] private float muffleTweenTime = 0.3f;
    [SerializeField] private float normalCutoff = 10000f;
    [SerializeField] private float muffledCutoff = 650f;
    [SerializeField] private float normalBgmVolume = 0f;
    [SerializeField] private float muffledBgmVolume = -1f;

    [Header("BGM 소스 (크로스페이드용 2채널)")]
    [SerializeField] AudioClip startBGM;
    [SerializeField] private AudioSource bgmSourceA;
    [SerializeField] private AudioSource bgmSourceB;
    [SerializeField] private float bgmCrossfadeTime = 1f;
    [SerializeField, Range(0f, 1f)] private float bgmMaxVolume = 1f;

    [Header("SFX 풀")]
    [SerializeField] private AudioSource sfxSourcePrefabOrTemplate;
    [SerializeField] private int sfxPoolSize = 8;

    [Header("사운드 라이브러리 (문자열 키로 재생)")]
    [SerializeField] private List<SoundEntry> bgmLibrary = new List<SoundEntry>();
    [SerializeField] private List<SoundEntry> sfxLibrary = new List<SoundEntry>();

    private Dictionary<string, AudioClip> _bgmDict;
    private Dictionary<string, AudioClip> _sfxDict;

    private List<AudioSource> _sfxPool;
    private int _sfxPoolCursor = 0;

    private AudioSource _activeBgmSource;
    private AudioSource _inactiveBgmSource;
    private string _currentBgmKey;

    private Tween _cutoffTween;
    private Tween _bgmMixerVolumeTween;
    private Tween _bgmFadeOutTween;
    private Tween _bgmFadeInTween;

    private bool _isMuffled = false;
    public bool IsMuffled => _isMuffled;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildDictionaries();
        InitSfxPool();
        InitBgmSources();
    }

    void Start()
    {
        if (startBGM != null)
        {
            PlayBgmClip(startBGM, true);
        }
    }

    private void BuildDictionaries()
    {
        _bgmDict = new Dictionary<string, AudioClip>();
        foreach (var entry in bgmLibrary)
        {
            if (entry == null || string.IsNullOrEmpty(entry.key) || entry.clip == null) continue;
            if (!_bgmDict.ContainsKey(entry.key)) _bgmDict.Add(entry.key, entry.clip);
        }

        _sfxDict = new Dictionary<string, AudioClip>();
        foreach (var entry in sfxLibrary)
        {
            if (entry == null || string.IsNullOrEmpty(entry.key) || entry.clip == null) continue;
            if (!_sfxDict.ContainsKey(entry.key)) _sfxDict.Add(entry.key, entry.clip);
        }
    }

    private void InitSfxPool()
    {
        _sfxPool = new List<AudioSource>();
        _sfxPoolCursor = 0;
        if (sfxSourcePrefabOrTemplate == null) return;

        ConfigureSfxSource(sfxSourcePrefabOrTemplate);
        _sfxPool.Add(sfxSourcePrefabOrTemplate);

        int poolSize = Mathf.Max(1, sfxPoolSize);
        for (int i = 1; i < poolSize; i++)
        {
            GameObject sourceObject = new GameObject($"SfxSource_{i}");
            sourceObject.transform.SetParent(transform, false);

            AudioSource src = sourceObject.AddComponent<AudioSource>();
            CopySfxSourceSettings(src);
            ConfigureSfxSource(src);
            _sfxPool.Add(src);
        }
    }

    private void CopySfxSourceSettings(AudioSource destination)
    {
        destination.outputAudioMixerGroup = sfxSourcePrefabOrTemplate.outputAudioMixerGroup;
        destination.mute = sfxSourcePrefabOrTemplate.mute;
        destination.bypassEffects = sfxSourcePrefabOrTemplate.bypassEffects;
        destination.bypassListenerEffects = sfxSourcePrefabOrTemplate.bypassListenerEffects;
        destination.bypassReverbZones = sfxSourcePrefabOrTemplate.bypassReverbZones;
        destination.priority = sfxSourcePrefabOrTemplate.priority;
        destination.panStereo = sfxSourcePrefabOrTemplate.panStereo;
        destination.spatialBlend = sfxSourcePrefabOrTemplate.spatialBlend;
        destination.reverbZoneMix = sfxSourcePrefabOrTemplate.reverbZoneMix;
        destination.dopplerLevel = sfxSourcePrefabOrTemplate.dopplerLevel;
        destination.spread = sfxSourcePrefabOrTemplate.spread;
        destination.rolloffMode = sfxSourcePrefabOrTemplate.rolloffMode;
        destination.minDistance = sfxSourcePrefabOrTemplate.minDistance;
        destination.maxDistance = sfxSourcePrefabOrTemplate.maxDistance;
    }

    private void ConfigureSfxSource(AudioSource source)
    {
        source.playOnAwake = false;
        source.loop = false;
        if (sfxMixerGroup != null) source.outputAudioMixerGroup = sfxMixerGroup;
    }

    private void InitBgmSources()
    {
        if (bgmSourceA == null || bgmSourceB == null) return;

        foreach (var src in new[] { bgmSourceA, bgmSourceB })
        {
            src.playOnAwake = false;
            src.loop = true;
            src.volume = 0f;
            if (bgmMixerGroup != null) src.outputAudioMixerGroup = bgmMixerGroup;
        }

        _activeBgmSource = bgmSourceA;
        _inactiveBgmSource = bgmSourceB;
    }

    // ---------------- SFX ----------------

    /// <summary> 라이브러리에 등록된 key로 SFX 재생 </summary>
    public void PlaySfx(string key, float volumeScale = 1f, float pitch = 1f)
    {
        if (_sfxDict == null || !_sfxDict.TryGetValue(key, out AudioClip clip) || clip == null)
        {
            Debug.LogWarning($"[AudioManager] SFX key를 찾을 수 없습니다: {key}");
            return;
        }
        PlaySfx(clip, volumeScale, pitch);
    }

    /// <summary> AudioClip을 직접 넘겨서 SFX 재생 </summary>
    public void PlaySfx(AudioClip clip, float volumeScale = 1f, float pitch = 1f)
    {
        if (clip == null) return;
        if (_sfxPool == null || _sfxPool.Count == 0) InitSfxPool();
        if (_sfxPool == null || _sfxPool.Count == 0) return;

        AudioSource src = null;
        for (int i = 0; i < _sfxPool.Count; i++)
        {
            AudioSource candidate = _sfxPool[_sfxPoolCursor];
            _sfxPoolCursor = (_sfxPoolCursor + 1) % _sfxPool.Count;
            if (candidate == null) continue;

            src = candidate;
            break;
        }

        if (src == null) return;

        src.pitch = pitch;
        src.volume = Mathf.Clamp01(volumeScale);
        src.clip = clip;
        src.Play();
    }

    // ---------------- BGM ----------------

    /// <summary> 라이브러리에 등록된 key로 BGM 재생 (동일 key 재생 중이면 무시) </summary>
    public void PlayBgm(string key, bool crossfade = true)
    {
        if (_bgmDict == null || !_bgmDict.TryGetValue(key, out AudioClip clip) || clip == null)
        {
            Debug.LogWarning($"[AudioManager] BGM key를 찾을 수 없습니다: {key}");
            return;
        }

        if (_currentBgmKey == key && _activeBgmSource != null && _activeBgmSource.isPlaying) return;

        _currentBgmKey = key;
        PlayBgmClip(clip, crossfade);
    }

    private void PlayBgmClip(AudioClip clip, bool crossfade)
    {
        if (clip == null || _activeBgmSource == null || _inactiveBgmSource == null) return;

        _bgmFadeOutTween?.Kill();
        _bgmFadeInTween?.Kill();

        AudioSource fadeOutSource = _activeBgmSource;
        AudioSource fadeInSource = _inactiveBgmSource;

        fadeInSource.clip = clip;
        fadeInSource.volume = 0f;
        fadeInSource.Play();

        float fadeTime = crossfade ? bgmCrossfadeTime : 0f;

        _bgmFadeOutTween = fadeOutSource
            .DOFade(0f, fadeTime)
            .OnComplete(() => fadeOutSource.Stop());

        _bgmFadeInTween = fadeInSource.DOFade(bgmMaxVolume, fadeTime);

        _activeBgmSource = fadeInSource;
        _inactiveBgmSource = fadeOutSource;
    }

    public void StopBgm(float fadeTime = 0.5f)
    {
        if (_activeBgmSource == null) return;

        _bgmFadeOutTween?.Kill();
        AudioSource src = _activeBgmSource;
        _bgmFadeOutTween = src.DOFade(0f, fadeTime).OnComplete(() => src.Stop());
        _currentBgmKey = null;
    }

    // ---------------- Muffle (믹서 로우패스 + 볼륨) ----------------

    /// <summary>
    /// 원하는 muffled 상태를 넘겨서 호출.
    /// 현재 상태(_isMuffled)와 동일하면 아무 것도 하지 않고 즉시 return.
    /// (기존 Update()에서 매 프레임 호출되던 문제를 이 가드로 대체)
    /// </summary>
    public void SetMuffled(bool muffled)
    {
        if (_isMuffled == muffled) return;
        _isMuffled = muffled;

        _cutoffTween?.Kill();
        _bgmMixerVolumeTween?.Kill();

        float targetCutoff = muffled ? muffledCutoff : normalCutoff;
        float targetVolume = muffled ? muffledBgmVolume : normalBgmVolume;

        mixer.GetFloat(muffleParam, out float currentCutoff);
        mixer.GetFloat(bgmVolumeParam, out float currentVolume);

        _cutoffTween = DOTween.To(
            () => currentCutoff,
            x =>
            {
                currentCutoff = x;
                mixer.SetFloat(muffleParam, x);
            },
            targetCutoff,
            muffleTweenTime);

        _bgmMixerVolumeTween = DOTween.To(
            () => currentVolume,
            x =>
            {
                currentVolume = x;
                mixer.SetFloat(bgmVolumeParam, x);
            },
            targetVolume,
            muffleTweenTime);
    }

    public void ToggleMuffle()
    {
        SetMuffled(!_isMuffled);
    }

    // ---------------- 마스터/개별 볼륨 제어 (믹서 파라미터를 dB로 노출해둔 경우) ----------------

    /// <summary> volume01: 0~1 값을 dB로 변환하여 믹서 파라미터에 적용 </summary>
    public void SetMixerVolume(string exposedParam, float volume01)
    {
        if (mixer == null || string.IsNullOrEmpty(exposedParam)) return;
        float dB = volume01 <= 0.0001f ? -80f : Mathf.Log10(volume01) * 20f;
        mixer.SetFloat(exposedParam, dB);
    }

    private void OnDestroy()
    {
        _cutoffTween?.Kill();
        _bgmMixerVolumeTween?.Kill();
        _bgmFadeOutTween?.Kill();
        _bgmFadeInTween?.Kill();
    }
}
