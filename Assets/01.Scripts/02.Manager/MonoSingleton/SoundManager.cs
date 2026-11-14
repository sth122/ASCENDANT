using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Util = DebugLogger<SoundManager>;

/// <summary>
/// 배경음악(BGM) 크로스페이드와 동시에 다수의 2D(UI)/3D(전투·환경) 효과음을
/// 끊김 없이 병렬 재생하는 다중 채널 오디오 매니저
/// </summary>
public class SoundManager : MonoSingleton<SoundManager>, ILoadable
{
    public int LoadOrder => 2;

    private const int Sfx2DChannelCount = 8;
    private const int Sfx3DChannelCount = 16;

    private AudioSource _bgmSourceA;
    private AudioSource _bgmSourceB;
    private bool _isUsingBgmA = true;
    private SoundId _currentBgmId = SoundId.None;
    private CancellationTokenSource _bgmFadeCts;

    private readonly List<AudioSource> _sfx2DPool = new List<AudioSource>(Sfx2DChannelCount);
    private readonly List<AudioSource> _sfx3DPool = new List<AudioSource>(Sfx3DChannelCount);
    private readonly float[] _sfx2DBaseVolumes = new float[Sfx2DChannelCount];
    private readonly float[] _sfx3DBaseVolumes = new float[Sfx3DChannelCount];
    private int _sfx2DCursor = 0;
    private int _sfx3DCursor = 0;

    private SoundDatabaseSO _soundDb;
    private float _currentBgmBaseVolume = 1.0f;

    public float MasterVolume { get; private set; } = 1.0f;
    public float BgmVolume { get; private set; } = 0.7f;
    public float SfxVolume { get; private set; } = 1.0f;

    protected override void Awake()
    {
        _isDDOL = true;
        base.Awake();
        MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("Setting_MasterVolume", 1.0f));
        BgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("Setting_BgmVolume", 0.7f));
        SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("Setting_SfxVolume", 1.0f));
        Application.runInBackground = PlayerPrefs.GetInt("Setting_BackgroundSound", 1) == 1;
        InitializeAudioChannels();
    }

    public void SetVolumes(float master, float bgm, float sfx)
    {
        MasterVolume = Mathf.Clamp01(master);
        BgmVolume = Mathf.Clamp01(bgm);
        SfxVolume = Mathf.Clamp01(sfx);

        // 1. 현재 재생 중인 BGM 볼륨 즉시 반영 (CrossFadeBgmAsync에서 플래그가 이미 반전된 상태 기준)
        AudioSource activeBgmSrc = _isUsingBgmA ? _bgmSourceA : _bgmSourceB;
        if (activeBgmSrc != null && activeBgmSrc.isPlaying)
        {
            activeBgmSrc.volume = Mathf.Clamp01(_currentBgmBaseVolume * BgmVolume * MasterVolume);
        }

        // 2. 현재 재생 중인 2D / 3D SFX 채널 볼륨도 즉시 반영
        float sfxScale = SfxVolume * MasterVolume;
        for (int i = 0; i < _sfx2DPool.Count; i++)
        {
            if (_sfx2DPool[i] != null && _sfx2DPool[i].isPlaying)
            {
                _sfx2DPool[i].volume = Mathf.Clamp01(_sfx2DBaseVolumes[i] * sfxScale);
            }
        }
        for (int i = 0; i < _sfx3DPool.Count; i++)
        {
            if (_sfx3DPool[i] != null && _sfx3DPool[i].isPlaying)
            {
                _sfx3DPool[i].volume = Mathf.Clamp01(_sfx3DBaseVolumes[i] * sfxScale);
            }
        }
    }

    private void InitializeAudioChannels()
    {
        if (_bgmSourceA != null) return;

        GameObject bgmRoot = new GameObject("@BGM_Channels");
        bgmRoot.transform.SetParent(transform);
        _bgmSourceA = CreateChannel(bgmRoot.transform, "BGM_A", is3D: false, loop: true);
        _bgmSourceB = CreateChannel(bgmRoot.transform, "BGM_B", is3D: false, loop: true);

        GameObject sfx2DRoot = new GameObject("@SFX_2D_Pool");
        sfx2DRoot.transform.SetParent(transform);
        for (int i = 0; i < Sfx2DChannelCount; i++)
        {
            _sfx2DPool.Add(CreateChannel(sfx2DRoot.transform, $"SFX_2D_{i}", is3D: false, loop: false));
        }

        GameObject sfx3DRoot = new GameObject("@SFX_3D_Pool");
        sfx3DRoot.transform.SetParent(transform);
        for (int i = 0; i < Sfx3DChannelCount; i++)
        {
            _sfx3DPool.Add(CreateChannel(sfx3DRoot.transform, $"SFX_3D_{i}", is3D: true, loop: false));
        }
    }

    private AudioSource CreateChannel(Transform parent, string channelName, bool is3D, bool loop)
    {
        GameObject go = new GameObject(channelName);
        go.transform.SetParent(parent);
        AudioSource src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = loop;
        src.spatialBlend = is3D ? 1.0f : 0.0f;
        if (is3D)
        {
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = 5.0f;
            src.maxDistance = 45.0f;
        }
        return src;
    }

    public UniTask OnSceneLoadCreate(SceneId scene)
    {
        if (_soundDb == null && DataManager.Instance != null)
        {
            DataManager.Instance.TryGetSingle<SoundDatabaseSO>(out _soundDb);
        }
        return UniTask.CompletedTask;
    }

    public void Init(SceneId scene) => Util.Log($"[{scene}] SoundManager 준비 완료");

    public void OnSceneDestroy(SceneId scene)
    {
        for (int i = 0; i < _sfx3DPool.Count; i++)
        {
            if (_sfx3DPool[i].isPlaying) _sfx3DPool[i].Stop();
        }
    }

    public void PlayBGM(SoundId bgmId, float fadeDuration = 1.0f)
    {
        if (_soundDb == null && DataManager.Instance != null)
        {
            DataManager.Instance.TryGetSingle<SoundDatabaseSO>(out _soundDb);
        }

        if (_soundDb == null || !_soundDb.TryGetEntry(bgmId, out var entry) || entry.Clip == null)
        {
            return;
        }

        AudioSource currentPlaying = _isUsingBgmA ? _bgmSourceA : _bgmSourceB;
        if (_currentBgmId == bgmId && currentPlaying != null && currentPlaying.isPlaying)
        {
            return;
        }

        _currentBgmId = bgmId;
        _currentBgmBaseVolume = entry.DefaultVolume;

        AsyncUtil.CancelAndDispose(ref _bgmFadeCts);
        _bgmFadeCts = CancellationTokenSource.CreateLinkedTokenSource(this.destroyCancellationToken);
        CrossFadeBgmAsync(entry.Clip, fadeDuration, _bgmFadeCts.Token).Forget();
    }

    private async UniTaskVoid CrossFadeBgmAsync(AudioClip nextClip, float duration, CancellationToken ct)
    {
        AudioSource activeSrc = _isUsingBgmA ? _bgmSourceA : _bgmSourceB;
        AudioSource nextSrc = _isUsingBgmA ? _bgmSourceB : _bgmSourceA;
        _isUsingBgmA = !_isUsingBgmA;

        float startVol = activeSrc.isPlaying ? activeSrc.volume : 0f;
        nextSrc.clip = nextClip;
        nextSrc.volume = 0f;
        nextSrc.Play();

        float elapsed = 0f;
        while (elapsed < duration)
        {
            bool canceled = await UniTask.Yield(PlayerLoopTiming.Update, ct).SuppressCancellationThrow();
            if (canceled) return;

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // 페이드 도중 슬라이더로 볼륨을 변경해도 최신 MasterVolume * BgmVolume을 실시간 추적
            float liveTargetVol = Mathf.Clamp01(_currentBgmBaseVolume * BgmVolume * MasterVolume);
            activeSrc.volume = Mathf.Lerp(startVol, 0f, t);
            nextSrc.volume = Mathf.Lerp(0f, liveTargetVol, t);
        }

        activeSrc.Stop();
        activeSrc.clip = null;
        nextSrc.volume = Mathf.Clamp01(_currentBgmBaseVolume * BgmVolume * MasterVolume);
    }
    public void PlaySFX(SoundId sfxId, Vector3 worldPosition = default, float volumeMultiplier = 1.0f)
    {
        if (sfxId == SoundId.None) return;

        if (_soundDb == null && DataManager.Instance != null)
        {
            DataManager.Instance.TryGetSingle<SoundDatabaseSO>(out _soundDb);
        }

        if (_soundDb == null || !_soundDb.TryGetEntry(sfxId, out var entry) || entry.Clip == null) return;

        bool is3D = entry.Is3DSpatial;
        int channelIdx = is3D ? AcquireChannelIndex(_sfx3DPool, ref _sfx3DCursor) : AcquireChannelIndex(_sfx2DPool, ref _sfx2DCursor);
        if (channelIdx < 0) return;

        AudioSource channel = is3D ? _sfx3DPool[channelIdx] : _sfx2DPool[channelIdx];
        float baseVol = entry.DefaultVolume * volumeMultiplier;
        if (is3D)
        {
            _sfx3DBaseVolumes[channelIdx] = baseVol;
            if (worldPosition == default && Camera.main != null)
            {
                worldPosition = Camera.main.transform.position;
            }
            channel.transform.position = worldPosition;
        }
        else
        {
            _sfx2DBaseVolumes[channelIdx] = baseVol;
        }

        float pitchOffset = entry.PitchRandomRange > 0f ? Random.Range(-entry.PitchRandomRange, entry.PitchRandomRange) : 0f;
        channel.pitch = 1.0f + pitchOffset;
        channel.volume = Mathf.Clamp01(baseVol * SfxVolume * MasterVolume);
        channel.clip = entry.Clip;
        channel.Play();
    }

    private int AcquireChannelIndex(List<AudioSource> pool, ref int cursor)
    {
        if (pool.Count == 0) return -1;
        for (int i = 0; i < pool.Count; i++)
        {
            int idx = (cursor + i) % pool.Count;
            if (!pool[idx].isPlaying)
            {
                cursor = (idx + 1) % pool.Count;
                return idx;
            }
        }
        int fallbackIdx = cursor;
        cursor = (cursor + 1) % pool.Count;
        return fallbackIdx;
    }

    protected override void OnDestroy()
    {
        AsyncUtil.CancelAndDispose(ref _bgmFadeCts);
        base.OnDestroy();
    }
}