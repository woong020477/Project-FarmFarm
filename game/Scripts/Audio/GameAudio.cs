// 역할: 씬 사이에서 하나의 BGM을 유지하고 제한된 효과음 음성을 재사용한다.
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>One persistent music loop and a bounded pool of 2D effect voices.</summary>
public sealed class GameAudio : MonoBehaviour
{
    public static GameAudio Instance { get; private set; }

    [SerializeField]
    private AudioSource musicSource;
    [SerializeField]
    private AudioSource[] effectSources;
    [SerializeField]
    private AudioClip musicClip;
    [SerializeField]
    private AudioClip shotClip;
    [SerializeField]
    private AudioClip harvestClip;
    [SerializeField]
    private AudioClip coinsClip;
    public float MusicVolume { get; private set; }
    public float EffectsVolume { get; private set; }

    private float nextShotTime;
    private bool dirty;
    private bool waitingForGesture;
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        AudioPreferences saved = AudioPreferences.Load();
        SetMusicVolume(saved.Music);
        SetEffectsVolume(saved.Effects);
        dirty = false;
        if (musicSource == null || effectSources == null || effectSources.Length == 0 || musicClip == null)
        {
            Debug.LogError("GameAudio: audio sources or music clip are not assigned.", this);
            enabled = false;
            return;
        }

        musicSource.clip = musicClip;
        musicSource.loop = true;
#if UNITY_WEBGL && !UNITY_EDITOR
        // Browser autoplay policy requires a user gesture; do not repeatedly restart the loop.
        waitingForGesture = true;
#else
        musicSource.Play();
#endif
    }

    private void Update()
    {
        if (!waitingForGesture)
            return;
        bool pressed = Mouse.current?.leftButton.wasPressedThisFrame == true || Keyboard.current?.anyKey.wasPressedThisFrame == true || Touchscreen.current?.primaryTouch.press.wasPressedThisFrame == true;
        if (!pressed)
            return;
        waitingForGesture = false;
        musicSource.Play();
    }

    // Sliders preview immediately; disk/IndexedDB writes happen only on close/pause/quit.
    public void SetMusicVolume(float value)
    {
        MusicVolume = AudioPreferences.Clamp(value);
        if (musicSource != null)
            musicSource.volume = MusicVolume;
        dirty = true;
    }

    public void SetEffectsVolume(float value)
    {
        EffectsVolume = AudioPreferences.Clamp(value);
        if (effectSources != null)
            foreach (AudioSource source in effectSources)
                if (source != null)
                    source.volume = EffectsVolume;
        dirty = true;
    }

    public void Save()
    {
        if (!dirty)
            return;
        new AudioPreferences(MusicVolume, EffectsVolume).Save();
        dirty = false;
    }

    public void PlayShot()
    {
        if (Time.unscaledTime < nextShotTime)
            return;
        nextShotTime = Time.unscaledTime + .045f;
        PlayEffect(shotClip);
    }

    public void PlayHarvest() => PlayEffect(harvestClip);
    public void PlayCoins() => PlayEffect(coinsClip);
    private void PlayEffect(AudioClip clip)
    {
        if (Instance != this || clip == null || EffectsVolume <= 0f || effectSources == null)
            return;
        // Reserve one voice for the sale cue, so a firefight cannot cut off a reward.
        int start = clip == coinsClip ? effectSources.Length - 1 : 0;
        int end = clip == coinsClip ? effectSources.Length : effectSources.Length - 1;
        for (int index = start; index < end; index++)
        {
            AudioSource source = effectSources[index];
            if (source == null || source.isPlaying)
                continue;
            source.clip = clip;
            source.Play();
            return;
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused && Instance == this)
            Save();
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused && Instance == this)
            Save();
    }

    private void OnApplicationQuit()
    {
        if (Instance == this)
            Save();
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;
        Save();
        Instance = null;
    }
}
