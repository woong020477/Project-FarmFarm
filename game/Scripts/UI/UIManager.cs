// 역할: 프로필/HUD와 자동 농사 버튼을 연결한다. 기능별 패널의 상세 동작은 전용 UI에 위임한다.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Automatic Farming")]
    [SerializeField]
    private bool autoWater;
    [SerializeField]
    private TMP_Text waterButtonText, plantButtonText, harvestButtonText;
    [SerializeField]
    private Color automationOnColor = new(0, .65f, .15f), automationOffColor = new(.85f, .12f, .12f);
    [SerializeField]
    private Slider barricadeSlider;
    [SerializeField]
    private TMP_Text barricadeText;
    private float nextHudRefresh;
    [Header("Profile Panel")]
    [SerializeField]
    private GameObject profilePanel;
    [SerializeField]
    private Image profileImage;
    [SerializeField]
    private TMP_Text nicknameText;
    [SerializeField]
    private TMP_Text titleText;
    [SerializeField]
    private TMP_Text levelText;
    [Header("Setting Panel")]
    [SerializeField]
    private GameObject settingPanel;
    [SerializeField]
    private SettingsUI settingsUI;
    [Header("Menu Experience")]
    [SerializeField]
    private Slider expSlider;
    [SerializeField]
    private TMP_Text expText;
    [Header("References")]
    [SerializeField]
    private PlayerProfile playerProfile;
    [SerializeField]
    private GameplayUI gameplayUI;
    public bool BlocksPlayerInput => gameplayUI != null && gameplayUI.IsOpen || settingPanel != null && settingPanel.activeSelf;

    public void OpenQuestPanel() => gameplayUI?.OpenQuests();
    public void OpenExpansionPanel() => gameplayUI?.OpenExpansion();
    public void OpenBarracksPanel() => gameplayUI?.OpenBarracks();
    public void OpenDefensePanel() => gameplayUI?.OpenDefense();
    public void OpenEventPanel() => gameplayUI?.OpenEvents();
    public void CloseGameplayPanels() => gameplayUI?.CloseAll();
    public bool AutoWater => autoWater;
    public bool HasActiveAutomation => autoWater;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (playerProfile == null && GameManager.Instance != null)
            playerProfile = GameManager.Instance.PlayerProfile;
        if (playerProfile != null)
            playerProfile.Changed += RefreshProfile;
        RefreshProfile();
        RefreshHud();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextHudRefresh)
            return;
        nextHudRefresh = Time.unscaledTime + .1f;
        RefreshHud();
    }

    public void RefreshHud()
    {
        var manager = GameManager.Instance;
        var drone = manager?.PrimaryDrone;
        ToggleCaption(waterButtonText, "물주기", autoWater);
        ToggleCaption(plantButtonText, "심기", drone != null && drone.AutoPlant);
        ToggleCaption(harvestButtonText, "수확하기", drone != null && drone.AutoHarvest);
        int hp = manager?.Defense != null ? manager.Defense.Health : Defense.MaxHealth;
        if (barricadeSlider != null)
        {
            barricadeSlider.minValue = 0;
            barricadeSlider.maxValue = Defense.MaxHealth;
            barricadeSlider.SetValueWithoutNotify(hp);
        }

        if (barricadeText != null)
        {
            string label = $"바리케이트 {hp}/{Defense.MaxHealth}";
            if (barricadeText.text != label)
                barricadeText.text = label;
        }
    }

    private void ToggleCaption(TMP_Text text, string label, bool on)
    {
        if (text == null)
            return;
        string value = label + (on ? "ON" : "OFF");
        if (text.text != value)
            text.text = value;
        text.color = on ? automationOnColor : automationOffColor;
    }

    private void OnDestroy()
    {
        if (playerProfile != null)
            playerProfile.Changed -= RefreshProfile;
        if (Instance == this)
            Instance = null;
    }

    public void OpenProfilePanel()
    {
        SetPanelActive(profilePanel, true);
        RefreshProfile();
    }

    public void CloseProfilePanel()
    {
        SetPanelActive(profilePanel, false);
    }

    public void OpenSettingPanel()
    {
        gameplayUI?.CloseAll();
        if (settingsUI != null)
            settingsUI.Open();
        else
            SetPanelActive(settingPanel, true);
    }

    public void CloseSettingPanel()
    {
        if (settingsUI != null)
            settingsUI.Close();
        else
            SetPanelActive(settingPanel, false);
    }

    public void OnAutoHarvestButtonClicked()
    {
        GameManager.Instance?.PrimaryDrone?.ToggleAutoHarvest();
        RefreshHud();
    }

    public void OnAutoWaterButtonClicked()
    {
        autoWater = !autoWater;
        RefreshHud();
        Debug.Log($"자동 물주기: {autoWater}");
    }

    public void OnAutoPlantButtonClicked()
    {
        GameManager.Instance?.PrimaryDrone?.ToggleAutoPlant();
        RefreshHud();
    }

    private void RefreshProfile()
    {
        if (playerProfile == null)
            return;
        if (nicknameText != null)
            nicknameText.text = playerProfile.UserName;
        if (titleText != null)
            titleText.text = playerProfile.Title;
        if (levelText != null)
            levelText.text = $"Lv. {playerProfile.Level}";
        float percent = playerProfile.ExperiencePercent;
        if (expSlider != null)
        {
            expSlider.minValue = 0f;
            expSlider.maxValue = 1f;
            expSlider.value = percent;
        }

        if (expText != null)
            expText.text = $"{playerProfile.CurrentExperience}/{playerProfile.TargetExperience} {percent * 100f:0.00}%";
    }

    private static void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null)
            panel.SetActive(active);
    }
}
