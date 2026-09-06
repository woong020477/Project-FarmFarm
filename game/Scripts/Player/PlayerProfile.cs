// 역할: 플레이어 표시 이름·레벨·경험치 상태와 변경 알림을 관리한다.
using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerProfile : MonoBehaviour
{
    public const long ExperiencePerLevel = 10000L;
    [Header("Profile")]
    [SerializeField]
    private string title = "없음";
    [Header("Progression")]
    [Min(1)]
    [SerializeField]
    private long level = 1L;
    [Min(0)]
    [SerializeField]
    private long currentExperience;
    private PlayFabBootstrap bootstrap;
    private string userName = "User";
    public string UserName => userName;
    public string Title => string.IsNullOrWhiteSpace(title) ? "없음" : title;
    public long Level => level;
    public long CurrentExperience => currentExperience;
    public long TargetExperience => ExperiencePerLevel;
    public float ExperiencePercent => (float)currentExperience / ExperiencePerLevel;

    public event Action Changed;
    private void Start()
    {
        bootstrap = PlayFabBootstrap.Instance;
        if (bootstrap == null)
            return;
        if (bootstrap.IsReady)
            RefreshUserName();
        else
            bootstrap.Ready += RefreshUserName;
    }

    private void OnDestroy()
    {
        if (bootstrap != null)
            bootstrap.Ready -= RefreshUserName;
    }

    public void AddExperience(long amount)
    {
        if (amount <= 0L)
            return;
        long levelsGained = amount / ExperiencePerLevel;
        long remainingExperience = amount % ExperiencePerLevel;
        long combinedExperience = currentExperience + remainingExperience;
        if (combinedExperience >= ExperiencePerLevel)
        {
            combinedExperience -= ExperiencePerLevel;
            levelsGained++;
        }

        level = level > long.MaxValue - levelsGained ? long.MaxValue : level + levelsGained;
        currentExperience = combinedExperience;
        Changed?.Invoke();
        FarmSaveService.Instance?.RequestCriticalSave();
    }

    public void SetTitle(string newTitle)
    {
        title = string.IsNullOrWhiteSpace(newTitle) ? "없음" : newTitle.Trim();
        Changed?.Invoke();
        FarmSaveService.Instance?.RequestCriticalSave();
    }

    public void Restore(long savedLevel, long savedExperience, string savedTitle)
    {
        long normalizedExperience = Math.Max(0L, savedExperience);
        long bonusLevels = normalizedExperience / ExperiencePerLevel;
        level = Math.Max(1L, savedLevel);
        level = level > long.MaxValue - bonusLevels ? long.MaxValue : level + bonusLevels;
        currentExperience = normalizedExperience % ExperiencePerLevel;
        title = string.IsNullOrWhiteSpace(savedTitle) ? "없음" : savedTitle;
        Changed?.Invoke();
    }

    private void RefreshUserName()
    {
        if (bootstrap == null)
            return;
        userName = string.IsNullOrWhiteSpace(bootstrap.UserName) ? bootstrap.PlayFabId : bootstrap.UserName;
        if (string.IsNullOrWhiteSpace(userName))
            userName = "User";
        Changed?.Invoke();
    }
}
