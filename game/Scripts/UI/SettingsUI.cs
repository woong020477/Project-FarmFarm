// 역할: 볼륨을 즉시 미리 듣고 패널을 닫을 때 저장한다. 크레딧은 별도 모달로 관리한다.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Settings/credit modal presentation. Slider changes preview; dismissals commit.</summary>
public sealed class SettingsUI : MonoBehaviour
{
    [SerializeField]
    private GameObject settingPanel;
    [SerializeField]
    private GameObject creditPanel;
    [SerializeField]
    private Slider musicSlider;
    [SerializeField]
    private Slider effectsSlider;
    [SerializeField]
    private TMP_Text musicText;
    [SerializeField]
    private TMP_Text effectsText;
    [SerializeField]
    private ScrollRect creditScroll;
    private void OnEnable()
    {
        musicSlider.onValueChanged.AddListener(SetMusic);
        effectsSlider.onValueChanged.AddListener(SetEffects);
    }

    private void OnDisable()
    {
        musicSlider.onValueChanged.RemoveListener(SetMusic);
        effectsSlider.onValueChanged.RemoveListener(SetEffects);
        GameAudio.Instance?.Save();
    }

    public void Open()
    {
        CloseCredits();
        Synchronize();
        settingPanel.SetActive(true);
        settingPanel.transform.SetAsLastSibling();
    }

    public void Close()
    {
        GameAudio.Instance?.Save();
        Synchronize();
        CloseCredits();
        settingPanel.SetActive(false);
    }

    public void OpenCredits()
    {
        creditPanel.SetActive(true);
        creditPanel.transform.SetAsLastSibling();
        if (creditScroll != null)
            creditScroll.verticalNormalizedPosition = 1f;
    }

    public void CloseCredits() => creditPanel.SetActive(false);
    private void Synchronize()
    {
        AudioPreferences values = GameAudio.Instance == null ? AudioPreferences.Load() : new AudioPreferences(GameAudio.Instance.MusicVolume, GameAudio.Instance.EffectsVolume);
        musicSlider.SetValueWithoutNotify(values.Music);
        effectsSlider.SetValueWithoutNotify(values.Effects);
        Caption(musicText, "BGM", values.Music);
        Caption(effectsText, "SFX", values.Effects);
    }

    private void SetMusic(float value)
    {
        GameAudio.Instance?.SetMusicVolume(value);
        Caption(musicText, "BGM", value);
    }

    private void SetEffects(float value)
    {
        GameAudio.Instance?.SetEffectsVolume(value);
        Caption(effectsText, "SFX", value);
    }

    private static void Caption(TMP_Text text, string label, float value) => text.text = $"{label}  {Mathf.RoundToInt(AudioPreferences.Clamp(value) * 100f)}%";
}
