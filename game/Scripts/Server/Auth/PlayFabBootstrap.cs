// 역할: 로그인·계정 정보·닉네임 확인을 거쳐 게임 시작 준비 상태를 알린다.
using System;
using PlayFab;
using PlayFab.AuthenticationModels;
using PlayFab.ClientModels;
using UnityEngine;

public sealed class PlayFabBootstrap : MonoBehaviour
{
    public static PlayFabBootstrap Instance { get; private set; }

    private const string CustomIdKey = "FarmFarm.PlayFab.CustomId";
    private bool isLoggingIn;
    private bool isSavingNickname;
    [SerializeField]
    private bool autoGuestLoginInEditor = true;
    public bool IsReady { get; private set; }
    public bool NeedsNickname { get; private set; }
    public string PlayFabId { get; private set; }
    public string EntityId { get; private set; }
    public string UserName { get; private set; }

    public event Action Ready;
    public event Action NicknameRequired;
    public event Action<string> Failed;
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
#if UNITY_EDITOR
        if(autoGuestLoginInEditor)Login();
#endif
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Login()
    {
        if (IsReady || isLoggingIn || NeedsNickname)
            return;
        if (string.IsNullOrWhiteSpace(PlayFabSettings.staticSettings.TitleId))
        {
            Debug.LogError("PlayFab Title ID가 설정되지 않았습니다.");
            return;
        }

        isLoggingIn = true;
        LoginWithCustomIDRequest request = new()
        {
            CustomId = GetOrCreateCustomId(),
            CreateAccount = true
        };
        PlayFabClientAPI.LoginWithCustomID(request, OnLoginSucceeded, OnRequestFailed);
    }

    public void LoginGoogle(string connectionId, string idToken)
    {
        if (IsReady || isLoggingIn || NeedsNickname)
            return;
        if (string.IsNullOrWhiteSpace(connectionId) || string.IsNullOrWhiteSpace(idToken))
        {
            Failed?.Invoke("Google 인증 응답이 없습니다. 다시 로그인해주세요.");
            return;
        }

        isLoggingIn = true;
        PlayFabClientAPI.LoginWithOpenIdConnect(new LoginWithOpenIdConnectRequest { ConnectionId = connectionId, IdToken = idToken, CreateAccount = true }, OnLoginSucceeded, OnRequestFailed);
    }

    private void OnLoginSucceeded(LoginResult result)
    {
        PlayFabId = result.PlayFabId;
        PlayFabAuthenticationAPI.GetEntityToken(new GetEntityTokenRequest(), OnEntityTokenSucceeded, OnRequestFailed);
    }

    private void OnEntityTokenSucceeded(GetEntityTokenResponse result)
    {
        EntityId = result.Entity?.Id;
        PlayFabClientAPI.GetAccountInfo(new GetAccountInfoRequest(), OnAccountInfoSucceeded, OnRequestFailed);
    }

    private void OnAccountInfoSucceeded(GetAccountInfoResult result)
    {
        string nickname = result.AccountInfo?.TitleInfo?.DisplayName;
        if (!string.IsNullOrWhiteSpace(nickname))
        {
            UserName = nickname;
            CompleteLogin();
            return;
        }

        isLoggingIn = false;
        NeedsNickname = true;
        NicknameRequired?.Invoke();
    }

    public void SetNickname(string nickname, Action<string> completed)
    {
        if (!NeedsNickname || isSavingNickname)
        {
            completed?.Invoke("닉네임을 저장할 수 없는 상태입니다.");
            return;
        }

        if (!NicknameRules.IsValid(nickname))
        {
            completed?.Invoke(NicknameRules.Hint);
            return;
        }

        isSavingNickname = true;
        // Check again before writing: a previous attempt may have been saved even if its response was lost.
        PlayFabClientAPI.GetAccountInfo(new GetAccountInfoRequest(), account =>
        {
            string existing = account.AccountInfo?.TitleInfo?.DisplayName;
            if (!string.IsNullOrWhiteSpace(existing))
            {
                FinishNickname(existing, completed);
                return;
            }

            PlayFabClientAPI.UpdateUserTitleDisplayName(new UpdateUserTitleDisplayNameRequest { DisplayName = nickname }, result =>
            {
                if (string.IsNullOrWhiteSpace(result.DisplayName))
                {
                    isSavingNickname = false;
                    completed?.Invoke("닉네임 응답을 확인하지 못했습니다. 다시 시도해주세요.");
                    return;
                }

                FinishNickname(result.DisplayName, completed);
            }, error => NicknameFailed(error, completed));
        }, error => NicknameFailed(error, completed));
    }

    private void FinishNickname(string nickname, Action<string> completed)
    {
        isSavingNickname = false;
        UserName = nickname;
        completed?.Invoke(null);
        CompleteLogin();
    }

    private void NicknameFailed(PlayFabError error, Action<string> completed)
    {
        isSavingNickname = false;
        string message = error.Error switch
        {
            PlayFabErrorCode.NameNotAvailable => "이미 사용 중인 닉네임입니다. 다른 이름을 입력해주세요.",
            PlayFabErrorCode.ProfaneDisplayName => "사용할 수 없는 표현이 포함되어 있습니다. 다른 이름을 입력해주세요.",
            PlayFabErrorCode.InvalidParams => NicknameRules.Hint,
            _ => "저장하지 못했습니다. 연결을 확인하고 다시 시도해주세요."
        };
        completed?.Invoke(message);
    }

    private void CompleteLogin()
    {
        isLoggingIn = false;
        IsReady = true;
        NeedsNickname = false;
        // A display/cache identifier is not a credential. Never save the Google JWT.
        PlayerPrefs.SetString("FarmFarm.LastAccountId", PlayFabId);
        PlayerPrefs.Save();
        Debug.Log($"PlayFab 로그인 성공\nPlayFabId: {PlayFabId}\nEntityId: {EntityId}");
        Ready?.Invoke();
    }

    private void OnRequestFailed(PlayFabError error)
    {
        isLoggingIn = false;
        IsReady = false;
        Failed?.Invoke("로그인 실패: " + error.ErrorMessage);
        Debug.LogError($"PlayFab 요청 실패\n{error.GenerateErrorReport()}");
    }

    private static string GetOrCreateCustomId()
    {
        if (PlayerPrefs.HasKey(CustomIdKey))
            return PlayerPrefs.GetString(CustomIdKey);
        string customId = Guid.NewGuid().ToString("N");
        PlayerPrefs.SetString(CustomIdKey, customId);
        PlayerPrefs.Save();
        return customId;
    }
}
