// 역할: 작업 탐색, 비행 이동, 심기/수확, 창고 반납으로 이어지는 드론 실행 루프.
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
[RequireComponent(typeof(FarmAgentOrigin))]
public sealed class DroneController : MonoBehaviour, IFarmAgent
{
    private string persistentId;
    [Header("References")]
    [SerializeField]
    private CropCarrier cropCarrier;
    [Header("Movement")]
    [Min(0.1f)]
    [SerializeField]
    private float moveSpeed = 4f;
    [Min(0.001f)]
    [SerializeField]
    private float arrivalDistance = 0.02f;
    [Header("Automation")]
    [SerializeField]
    private bool autoHarvest = true;
    [SerializeField]
    private bool autoPlant = true;
    private Rigidbody2D body;
    private FarmAgentOrigin origin;
    private UnitAnimation animationView;
    private bool harvestWindup;
    public Vector3 ActionPosition => origin != null ? origin.Position : transform.position;

    private FarmTaskTarget? currentTask;
    private WarehouseController targetWarehouse;
    private float nextTaskSearchTime;
    public string PersistentId => persistentId;
    public Vector3 Position => transform.position;
    public CropCarrier CropCarrier => cropCarrier;
    public bool AutoHarvest => autoHarvest;
    public bool AutoPlant => autoPlant;
    private float CurrentMoveSpeed => moveSpeed * (GameEvents.Instance == null ? 1f : GameEvents.Instance.GetMultiplier(StatType.DroneSpeed));

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        origin = GetComponent<FarmAgentOrigin>();
        origin.Initialize();
        animationView = GetComponent<UnitAnimation>();
        foreach (Collider2D droneCollider in GetComponentsInChildren<Collider2D>(true))
            droneCollider.isTrigger = true;
    }

    private void Update()
    {
        if (harvestWindup)
            return;
        if (Time.unscaledTime < nextTaskSearchTime)
            return;
        nextTaskSearchTime = Time.unscaledTime + 0.2f;
        RefreshTask();
    }

    private void FixedUpdate()
    {
        if (animationView != null && animationView.IsActing)
            return;
        if (cropCarrier != null && cropCarrier.IsCarrying)
        {
            MoveToWarehouse();
            return;
        }

        MoveToTask();
    }

    public void ToggleAutoHarvest()
    {
        autoHarvest = !autoHarvest;
        if (!autoHarvest && currentTask.HasValue && currentTask.Value.WorkType == FarmWorkType.Harvest)
        {
            currentTask = null;
            harvestWindup = false;
        }
    }

    public void ToggleAutoPlant()
    {
        autoPlant = !autoPlant;
        if (!autoPlant && currentTask.HasValue && currentTask.Value.WorkType == FarmWorkType.Plant)
            currentTask = null;
    }

    public void SetBaseMoveSpeed(float speed)
    {
        moveSpeed = Mathf.Max(0.1f, speed);
    }

    public void SetPersistentId(string id)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            persistentId = id;
            if (cropCarrier != null)
                cropCarrier.SetOwnerId(id);
        }
    }

    public void RestorePosition(Vector3 position)
    {
        transform.position = position;
        if (body != null)
            body.position = position;
        currentTask = null;
        targetWarehouse = null;
        harvestWindup = false;
    }

    private void RefreshTask()
    {
        GameManager gameManager = GameManager.Instance;
        if (gameManager == null)
            return;
        if (cropCarrier != null && cropCarrier.IsCarrying)
        {
            currentTask = null;
            if (targetWarehouse == null)
                gameManager.TryFindClosestWarehouse(transform.position, out targetWarehouse);
            return;
        }

        targetWarehouse = null;
        if (currentTask.HasValue && gameManager.IsTaskAvailable(currentTask.Value, cropCarrier))
            return;
        currentTask = null;
        if (autoHarvest && cropCarrier != null && cropCarrier.CanCarry && gameManager.TryFindClosestTask(ActionPosition, FarmWorkType.Harvest, true, out FarmTaskTarget harvestTask))
        {
            currentTask = harvestTask;
            return;
        }

        if (autoPlant && gameManager.TryFindClosestTask(ActionPosition, FarmWorkType.Plant, true, out FarmTaskTarget plantTask))
            currentTask = plantTask;
    }

    private void MoveToTask()
    {
        if (!currentTask.HasValue || GameManager.Instance == null)
            return;
        FarmTaskTarget task = currentTask.Value;
        if (!GameManager.Instance.IsTaskAvailable(task, cropCarrier))
        {
            currentTask = null;
            harvestWindup = false;
            return;
        }

        Vector2 interactionPosition = ActionPosition;
        Vector2 destination = task.FarmPlot.GetCellCenterWorld(task.CellPosition);
        if (Vector2.Distance(interactionPosition, destination) <= arrivalDistance)
        {
            if (task.WorkType == FarmWorkType.Harvest && !harvestWindup && animationView != null)
            {
                harvestWindup = true;
                animationView.PlayAction(Vector2.zero);
                return;
            }

            GameManager.Instance.TryPerformTask(task, cropCarrier);
            harvestWindup = false;
            currentTask = null;
            return;
        }

        Vector2 nextInteractionPosition = Vector2.MoveTowards(interactionPosition, destination, CurrentMoveSpeed * Time.fixedDeltaTime);
        body.MovePosition(body.position + nextInteractionPosition - interactionPosition);
    }

    private void MoveToWarehouse()
    {
        if (targetWarehouse == null || cropCarrier == null || !cropCarrier.IsCarrying)
        {
            targetWarehouse = null;
            return;
        }

        Vector2 destination = targetWarehouse.DepositPosition;
        Vector2 center = ActionPosition;
        Vector2 nextPosition = Vector2.MoveTowards(center, destination, CurrentMoveSpeed * Time.fixedDeltaTime);
        body.MovePosition(body.position + nextPosition - center);
    }
}
