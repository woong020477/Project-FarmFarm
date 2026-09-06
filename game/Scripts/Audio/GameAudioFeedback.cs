// 역할: 농사·전투·판매의 성공 이벤트를 오디오 재생에 연결한다.
using System.Collections.Generic;
using UnityEngine;

/// <summary>Maps confirmed gameplay events to sound without coupling domain rules to audio.</summary>
public sealed class GameAudioFeedback : MonoBehaviour
{
    [SerializeField]
    private GameManager manager;
    private readonly List<FarmPlotController> plots = new();
    private Defense defense;
    private PlayFabInventoryService inventory;
    private void Start()
    {
        if (manager == null)
        {
            Debug.LogError("GameAudioFeedback: GameManager is missing.", this);
            return;
        }

        defense = manager.Defense;
        inventory = manager.Inventory;
        if (defense != null)
            defense.ShotFired += OnShot;
        if (inventory != null)
            inventory.SaleCompleted += OnSale;
        foreach (FarmPlotController plot in manager.FarmPlots)
        {
            if (plot == null)
                continue;
            plots.Add(plot);
            plot.Harvested += OnHarvest;
        }
    }

    private void OnShot()
    {
        if (isActiveAndEnabled)
            GameAudio.Instance?.PlayShot();
    }

    private void OnHarvest()
    {
        if (isActiveAndEnabled)
            GameAudio.Instance?.PlayHarvest();
    }

    private void OnSale()
    {
        if (isActiveAndEnabled)
            GameAudio.Instance?.PlayCoins();
    }

    private void OnDestroy()
    {
        if (defense != null)
            defense.ShotFired -= OnShot;
        if (inventory != null)
            inventory.SaleCompleted -= OnSale;
        foreach (FarmPlotController plot in plots)
            if (plot != null)
                plot.Harvested -= OnHarvest;
    }
}
