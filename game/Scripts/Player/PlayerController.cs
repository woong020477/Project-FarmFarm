// 역할: 플레이어 이동과 물주기 작업을 처리한다. 다른 농사 작업은 드론이 담당한다.
using UnityEngine;
using UnityEngine.InputSystem;

/*
 * PlayerController는 플레이어 캐릭터의 이동과 상호작용을 처리하는 MonoBehaviour이다.
 * 물리 이동은 FixedUpdate, 표시 보간은 Rigidbody2D가 담당한다.
 */
[RequireComponent(typeof(Rigidbody2D), typeof(FarmAgentOrigin))]
public class PlayerController : MonoBehaviour, IFarmAgent
{
    [Header("Identity")]
    [SerializeField]
    private string persistentId = "player";
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
    private Rigidbody2D body;
    private FarmAgentOrigin origin;
    private UnitAnimation animationView;
    public Vector3 ActionPosition => origin != null ? origin.Position : transform.position;
    private Vector2 PhysicsActionPosition => body.position + (Vector2)(ActionPosition - transform.position);

    private Vector2 manualMoveDirection;
    private Vector2Int previousDirection;
    private FarmTaskTarget? currentTask;
    private Vector2? currentWaypoint;
    private bool preferXAxis = true;
    public string PersistentId => persistentId;
    public Vector3 Position => transform.position;
    public CropCarrier CropCarrier => cropCarrier;
    public float BaseMoveSpeed => moveSpeed;
    private float CurrentMoveSpeed => moveSpeed * (GameEvents.Instance == null ? 1f : GameEvents.Instance.GetMultiplier(StatType.PlayerSpeed));

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.gravityScale = 0;
        body.constraints |= RigidbodyConstraints2D.FreezeRotation;
        body.angularVelocity = 0;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        origin = GetComponent<FarmAgentOrigin>();
        origin.Initialize();
        animationView = GetComponent<UnitAnimation>();
        if (cropCarrier != null)
            cropCarrier.SetOwnerId(persistentId);
    }

    private void Update()
    {
        ReadManualInput();
        if (manualMoveDirection != Vector2.zero)
        {
            CancelAutoNavigation();
            return;
        }

        RefreshAutoTask();
    }

    private void FixedUpdate()
    {
        if (animationView != null && animationView.IsActing)
            return;
        if (manualMoveDirection != Vector2.zero)
        {
            MoveManually();
            return;
        }

        MoveAutomatically();
    }

    private void ReadManualInput()
    {
        if (UIManager.Instance != null && UIManager.Instance.BlocksPlayerInput)
        {
            manualMoveDirection = Vector2.zero;
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            manualMoveDirection = Vector2.zero;
            return;
        }

        manualMoveDirection = ReadCardinalDirection(keyboard);
        if (!keyboard.spaceKey.wasPressedThisFrame)
            return;
        CancelAutoNavigation();
        if (GameManager.Instance != null && GameManager.Instance.TryWaterAt(ActionPosition))
            animationView?.PlayAction(Vector2.zero);
    }

    private void MoveManually()
    {
        Vector2 nextPosition = body.position + manualMoveDirection * (CurrentMoveSpeed * Time.fixedDeltaTime);
        body.MovePosition(nextPosition);
    }

    private void RefreshAutoTask()
    {
        UIManager uiManager = UIManager.Instance;
        GameManager gameManager = GameManager.Instance;
        if (uiManager == null || gameManager == null || !uiManager.AutoWater)
        {
            CancelAutoNavigation();
            return;
        }

        if (currentTask.HasValue)
        {
            FarmTaskTarget task = currentTask.Value;
            if (task.WorkType != FarmWorkType.Water || !gameManager.IsTaskAvailable(task, cropCarrier))
                CancelAutoNavigation();
        }

        if (!gameManager.TryFindClosestTask(ActionPosition, FarmWorkType.Water, false, out FarmTaskTarget bestTask))
            return;
        if (!currentTask.HasValue)
            AssignAutoTask(bestTask);
    }

    private void AssignAutoTask(FarmTaskTarget task)
    {
        currentTask = task;
        currentWaypoint = null;
        preferXAxis = true;
    }

    private void MoveAutomatically()
    {
        if (!currentTask.HasValue)
            return;
        FarmTaskTarget task = currentTask.Value;
        if (!GameManager.Instance.IsTaskAvailable(task, cropCarrier))
        {
            CancelAutoNavigation();
            return;
        }

        if (IsAtTaskPosition(task))
        {
            if (GameManager.Instance.TryPerformTask(task, cropCarrier))
                animationView?.PlayAction(Vector2.zero);
            CancelAutoNavigation();
            return;
        }

        if (!currentWaypoint.HasValue)
            currentWaypoint = CalculateNextWaypoint(task);
        Vector2 interactionPosition = PhysicsActionPosition;
        Vector2 nextInteractionPosition = Vector2.MoveTowards(interactionPosition, currentWaypoint.Value, CurrentMoveSpeed * Time.fixedDeltaTime);
        Vector2 movementDelta = nextInteractionPosition - interactionPosition;
        body.MovePosition(body.position + movementDelta);
        if (Vector2.Distance(nextInteractionPosition, currentWaypoint.Value) <= arrivalDistance)
            currentWaypoint = null;
    }

    private Vector2 CalculateNextWaypoint(FarmTaskTarget task)
    {
        Vector2 interactionPosition = PhysicsActionPosition;
        Vector3Int currentCell = task.FarmPlot.WorldToCell(interactionPosition);
        Vector2 currentCellCenter = task.FarmPlot.GetCellCenterWorld(currentCell);
        if (Mathf.Abs(interactionPosition.x - currentCellCenter.x) > arrivalDistance)
            return new Vector2(currentCellCenter.x, interactionPosition.y);
        if (Mathf.Abs(interactionPosition.y - currentCellCenter.y) > arrivalDistance)
            return new Vector2(interactionPosition.x, currentCellCenter.y);
        int xDifference = task.CellPosition.x - currentCell.x;
        int yDifference = task.CellPosition.y - currentCell.y;
        if (xDifference != 0 && yDifference != 0)
        {
            bool moveOnXAxis = preferXAxis;
            if (moveOnXAxis)
                return GetXAxisWaypoint(task.FarmPlot, currentCell, xDifference);
            return GetYAxisWaypoint(task.FarmPlot, currentCell, yDifference);
        }

        if (xDifference != 0)
            return GetXAxisWaypoint(task.FarmPlot, currentCell, xDifference);
        if (yDifference != 0)
            return GetYAxisWaypoint(task.FarmPlot, currentCell, yDifference);
        return task.FarmPlot.GetCellCenterWorld(task.CellPosition);
    }

    private Vector2 GetXAxisWaypoint(FarmPlotController farmPlot, Vector3Int currentCell, int difference)
    {
        Vector3Int nextCell = currentCell + new Vector3Int(difference > 0 ? 1 : -1, 0, 0);
        return farmPlot.GetCellCenterWorld(nextCell);
    }

    private Vector2 GetYAxisWaypoint(FarmPlotController farmPlot, Vector3Int currentCell, int difference)
    {
        Vector3Int nextCell = currentCell + new Vector3Int(0, difference > 0 ? 1 : -1, 0);
        return farmPlot.GetCellCenterWorld(nextCell);
    }

    private bool IsAtTaskPosition(FarmTaskTarget task)
    {
        Vector3Int currentCell = task.FarmPlot.WorldToCell(PhysicsActionPosition);
        if (currentCell != task.CellPosition)
            return false;
        Vector2 targetPosition = task.FarmPlot.GetCellCenterWorld(task.CellPosition);
        return Vector2.Distance(PhysicsActionPosition, targetPosition) <= arrivalDistance;
    }

    private Vector2 ReadCardinalDirection(Keyboard keyboard)
    {
        bool left = keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed;
        bool right = keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed;
        bool up = keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed;
        bool down = keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed;
        int horizontal = left == right ? 0 : left ? -1 : 1;
        int vertical = up == down ? 0 : down ? -1 : 1;
        if (horizontal != 0 && vertical != 0)
        {
            if (previousDirection.y != 0)
                horizontal = 0;
            else
                vertical = 0;
        }

        Vector2Int direction = new(horizontal, vertical);
        if (direction != Vector2Int.zero)
            previousDirection = direction;
        return direction;
    }

    private void CancelAutoNavigation()
    {
        currentTask = null;
        currentWaypoint = null;
    }

    public void RestorePosition(Vector3 position)
    {
        transform.position = position;
        body.position = position;
        CancelAutoNavigation();
    }
}
