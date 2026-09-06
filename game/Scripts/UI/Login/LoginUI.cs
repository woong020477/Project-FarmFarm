// 역할: Google 로그인 입력, 자동 로그인 선택과 최초 닉네임 패널을 연결한다.
using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class LoginUI : MonoBehaviour
{
    [SerializeField]
    private LoginConfig config;
    [SerializeField]
    private Button googleButton;
    [SerializeField]
    private TMP_Text statusText, versionText;
    [SerializeField]
    private Toggle autoLoginToggle;
    [Header("Nickname")]
    [SerializeField]
    private GameObject nicknamePanel;
    [SerializeField]
    private TMP_InputField nicknameInput;
    [SerializeField]
    private Button nicknameButton;
    [SerializeField]
    private TMP_Text nicknameStatus;
    private bool savingNickname;
    private PlayFabBootstrap bootstrap;
    private bool entering;
    private const string AutoKey = "FarmFarm.Google.AutoLogin";
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void FarmGoogleInit(string clientId,string receiver,int autoLogin);
    [DllImport("__Internal")] private static extern void FarmGoogleShow();
    [DllImport("__Internal")] private static extern void FarmGoogleHide();
#endif
    private void Start()
    {
        bootstrap = PlayFabBootstrap.Instance;
        versionText.text = "v" + Application.version;
        autoLoginToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt(AutoKey, 1) == 1);
        autoLoginToggle.onValueChanged.AddListener(SetAutomatic);
        if (bootstrap == null)
        {
            Status("로그인 서비스 연결을 확인해주세요.");
            return;
        }

        nicknamePanel.SetActive(false);
        bootstrap.Ready += EnterFarm;
        bootstrap.Failed += Status;
        bootstrap.NicknameRequired += OpenNickname;
        if (bootstrap.IsReady)
        {
            EnterFarm();
            return;
        }

        if (bootstrap.NeedsNickname)
        {
            OpenNickname();
            return;
        }

        if (config == null || string.IsNullOrWhiteSpace(config.googleClientId))
        {
            Status("Google OAuth 웹 클라이언트 ID 설정이 필요합니다.");
            return;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        FarmGoogleInit(config.googleClientId,gameObject.name,autoLoginToggle.isOn?1:0);
        Status("Google 계정으로 농장에 접속하세요.");
#else
        Status("Google 로그인은 웹 빌드에서 사용할 수 있습니다.");
#endif
    }

    public void GoogleLogin()
    {
        if (entering || bootstrap == null || bootstrap.NeedsNickname)
            return;
        if (config == null || string.IsNullOrWhiteSpace(config.googleClientId))
        {
            Status("LoginConfig에 OAuth 웹 클라이언트 ID를 입력해주세요.");
            return;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        FarmGoogleShow();
#else
        Status("웹 빌드를 브라우저에서 열어 로그인해주세요.");
#endif
    }

    // Invoked only by the GIS bridge. PlayFab verifies signature, issuer and audience.
    public void OnGoogleCredential(string credential)
    {
        if (entering || bootstrap == null || bootstrap.NeedsNickname)
            return;
        Status("계정 확인 중...");
        googleButton.interactable = false;
        bootstrap.LoginGoogle(config.connectionId, credential);
    }

    public void OnGoogleError(string message) => Status(message);
    private void Status(string message)
    {
        statusText.text = message;
        googleButton.interactable = !entering && bootstrap != null && !bootstrap.NeedsNickname;
    }

    private void OpenNickname()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        FarmGoogleHide();
#endif
        googleButton.interactable = false;
        nicknamePanel.SetActive(true);
        nicknameStatus.text = NicknameRules.Hint;
        nicknameButton.interactable = true;
        statusText.text = "농장에서 사용할 이름을 정해주세요.";
    }

    public void SubmitNickname()
    {
        if (savingNickname || bootstrap == null)
            return;
        string nickname = nicknameInput.text;
        if (!NicknameRules.IsValid(nickname))
        {
            nicknameStatus.text = NicknameRules.Hint;
            return;
        }

        savingNickname = true;
        nicknameButton.interactable = false;
        nicknameInput.interactable = false;
        nicknameStatus.text = "닉네임 저장 중...";
        bootstrap.SetNickname(nickname, error =>
        {
            if (this == null)
                return;
            savingNickname = false;
            nicknameButton.interactable = true;
            nicknameInput.interactable = true;
            nicknameStatus.text = error ?? "닉네임이 저장되었습니다.";
        });
    }

    private void SetAutomatic(bool enabled)
    {
        PlayerPrefs.SetInt(AutoKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void EnterFarm()
    {
        if (entering)
            return;
        entering = true;
        googleButton.interactable = false;
        Status("농장 불러오는 중...");
#if UNITY_WEBGL && !UNITY_EDITOR
        FarmGoogleHide();
#endif
        SceneManager.LoadSceneAsync(config.farmScene);
    }

    private void OnDestroy()
    {
        if (bootstrap != null)
        {
            bootstrap.Ready -= EnterFarm;
            bootstrap.Failed -= Status;
            bootstrap.NicknameRequired -= OpenNickname;
        }

        if (autoLoginToggle != null)
            autoLoginToggle.onValueChanged.RemoveListener(SetAutomatic);
    }
}
