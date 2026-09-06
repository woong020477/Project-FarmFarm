// 역할: 퀘스트·확장·막사·방호·이벤트 화면의 표시와 버튼 입력을 관리한다.
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class GameplayUI : MonoBehaviour
{
    [SerializeField]
    private GameObject questPanel, expandPanel, barracksPanel, defensePanel, eventPanel;
    [SerializeField]
    private RectTransform questContent, expandContent, barracksContent;
    [SerializeField]
    private Button rowButtonPrefab;
    [SerializeField]
    private TMP_Text questHeader, expandHeader, barracksHeader, defenseText, eventText, statusText;
    [SerializeField]
    private FarmUI farmUI;
    [SerializeField]
    private MarketUI marketUI;
    [SerializeField]
    private Button repairButton;
    [SerializeField]
    private TMP_Text repairText, repairStatus;
    private Progression progression;
    private float nextRefresh;
    public bool IsOpen => Active(questPanel) || Active(expandPanel) || Active(barracksPanel) || Active(defensePanel) || Active(eventPanel);

    private static bool Active(GameObject panel) => panel != null && panel.activeSelf;
    private void Start()
    {
        progression = GameManager.Instance.Progression;
        progression.Changed += Rebuild;
        GameManager.Instance.Inventory.InventoryUpdated += Rebuild;
        GameManager.Instance.Events.Changed += Rebuild;
        Rebuild();
    }

    private void OnDestroy()
    {
        if (progression != null)
            progression.Changed -= Rebuild;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.Inventory.InventoryUpdated -= Rebuild;
            GameManager.Instance.Events.Changed -= Rebuild;
        }
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh)
            return;
        nextRefresh = Time.unscaledTime + .5f;
        RefreshText();
    }

    public void RepairBarricade() => progression?.RepairBarricade();
    public void OpenQuests() => Open(questPanel);
    public void OpenExpansion() => Open(expandPanel);
    public void OpenBarracks() => Open(barracksPanel);
    public void OpenDefense() => Open(defensePanel);
    public void OpenEvents() => Open(eventPanel);
    private void Open(GameObject panel)
    {
        CloseAll();
        farmUI?.Close();
        marketUI?.Close();
        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
        Rebuild();
    }

    public void CloseAll()
    {
        foreach (var p in new[]
        {
            questPanel,
            expandPanel,
            barracksPanel,
            defensePanel,
            eventPanel
        }

        )
            if (p != null)
                p.SetActive(false);
    }

    private void Clear(RectTransform content)
    {
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            var child = content.GetChild(i).gameObject;
            child.SetActive(false);
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }

    private void Row(RectTransform content, string text, Action action, bool allowed = true, Sprite icon = null)
    {
        var button = Instantiate(rowButtonPrefab, content);
        button.gameObject.SetActive(true);
        button.GetComponentInChildren<TMP_Text>(true).text = text;
        button.interactable = allowed;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => action());
        if (icon != null)
        {
            var go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            go.layer = button.gameObject.layer;
            go.transform.SetParent(button.transform, false);
            var image = go.GetComponent<Image>();
            image.sprite = icon;
            image.preserveAspect = true;
            image.raycastTarget = false;
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, .5f);
            rect.anchoredPosition = new Vector2(28, 0);
            rect.sizeDelta = new Vector2(44, 44);
            var label = button.GetComponentInChildren<TMP_Text>(true);
            label.rectTransform.offsetMin = new Vector2(56, 4);
        }
    }

    public void Rebuild()
    {
        if (progression == null)
            return;
        var manager = GameManager.Instance;
        var state = progression.State;
        bool ready = progression.IsReady && !progression.IsBusy && !state.recoveryRequired;
        Clear(questContent);
        Clear(expandContent);
        Clear(barracksContent);
        foreach (var quest in state.quests)
        {
            var captured = quest;
            CropDefinition definition = null;
            foreach (var d in manager.CropDefinitions)
                if (d.ServerId == quest.itemId)
                {
                    definition = d;
                    break;
                }

            string name = definition == null ? quest.itemId : definition.DisplayName;
            long stock = definition == null ? 0 : manager.Warehouse.GetConfirmedStock(definition.CropType);
            int xp = state.dailyCompleted < 10 ? quest.experience : quest.experience / 10;
            Row(questContent, $"{quest.title} · {name} {quest.amount}개 ({stock}개 보유)\n보상 {quest.gold}G / {xp} EXP · 제출", () => progression.SubmitQuest(captured.id), ready && stock >= quest.amount);
        }

        if (state.quests.Length == 0)
            Row(questContent, "접속 중 2분마다 퀘스트가 생성됩니다.", () =>
            {
            }, false);
        Row(expandContent, $"다음 농지 해금 · Lv.{state.plots + 1} 필요 ({state.plots}/8)", progression.UnlockPlot, ready && state.plots < 8 && state.level > state.plots);
        Row(expandContent, $"드론 구매 · {100 * state.drones}G / Lv.{state.drones + 1} 필요", progression.BuyDrone, ready && state.drones < 64 && state.level > state.drones && manager.Inventory.Gold >= 100 * state.drones);
        foreach (var drone in manager.Drones)
        {
            var d = drone;
            Row(expandContent, $"{d.name} · 자동 심기 {(d.AutoPlant ? "ON" : "OFF")}", () =>
            {
                d.ToggleAutoPlant();
                Rebuild();
            });
            Row(expandContent, $"{d.name} · 자동 수확 {(d.AutoHarvest ? "ON" : "OFF")}", () =>
            {
                d.ToggleAutoHarvest();
                Rebuild();
            });
        }

        for (int tier = 0; tier < 3; tier++)
        {
            int selected = tier;
            int price = new[]
            {
                50,
                150,
                400
            }[tier];
            price = Mathf.Max(1, Mathf.RoundToInt(price * (GameEvents.Instance == null ? 1 : GameEvents.Instance.GetMultiplier(StatType.MercenaryCost))));
            Row(barracksContent, $"{new[] { "보병", "탱크", "헬기" }[tier]} · 인구 1 · {price}G\n체력 {20 * (tier + 1)} / 공격 {manager.Defense.AttackDamage(tier)} / 초당 {1 + tier * .5f:0.0}회", () => progression.Hire(selected), ready && state.mercenaries.Length < state.level && manager.Inventory.Gold >= price, manager.Defense.UnitIcon(tier));
        }

        RefreshText();
    }

    private void RefreshText()
    {
        if (progression == null)
            return;
        var state = progression.State;
        var manager = GameManager.Instance;
        questHeader.text = $"퀘스트 · 다음 생성 {Math.Max(0, state.nextQuestAt - progression.Now)}초\n오늘 기본 경험치 {Math.Min(state.dailyCompleted, 10)}/10회 · 이후 10%";
        expandHeader.text = $"농장 확장 · Lv.{state.level} · {manager.Inventory.Gold}G";
        barracksHeader.text = $"막사 · 인구 {state.mercenaries.Length}/{state.level} · {manager.Inventory.Gold}G";
        long protection = Math.Max(0, state.protectedUntil - progression.Now);
        defenseText.text = $"바리케이드 {manager.Defense.Health}/10000\n" + (protection > 0 ? $"보호 중 {TimeSpan.FromSeconds(protection):hh\\:mm\\:ss}" : "기지 방어 중") + "\n공격 1회당 피해 1\n붕괴 시 소지금 20% 손실, 12시간 보호\n방어 피해는 모아서 서버에 저장합니다.";
        eventText.text = manager.Events.DescribeActiveEvents();
        bool disconnected = manager.Clock == null || !manager.Clock.IsConnectionAvailable;
        if (statusText != null)
        {
            statusText.text = "서버 연결 대기";
            statusText.gameObject.SetActive(disconnected);
        }

        int repair = Mathf.Min(Defense.MaxHealth - manager.Defense.Health, Mathf.Max(0, manager.Inventory.Gold));
        if (repairButton != null)
            repairButton.interactable = progression.IsReady && !progression.IsBusy && !state.recoveryRequired && manager.Inventory.CanSubmitInventoryMutation && protection == 0 && repair > 0;
        if (repairText != null)
            repairText.text = protection > 0 ? "보호 종료 후 자동 복구" : $"바리케이트 수리하기\n{repair} HP / {repair}G";
        if (repairStatus != null)
            repairStatus.text = progression.Status == "서버 연결 대기" || progression.Status == "동기화 완료" ? "1 HP당 1G · 보유 골드만큼 수리" : progression.Status;
    }
}
