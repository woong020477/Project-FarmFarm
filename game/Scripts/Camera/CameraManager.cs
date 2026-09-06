// 역할: 농지 포커스와 기지 전체 보기 등 카메라 연출의 전환을 관리한다.
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CameraManager : MonoBehaviour
{
    public static CameraManager Instance { get; private set; }
    private CameraController cameraController => GameManager.Instance != null ? GameManager.Instance.CameraController : null;
    private Transform player => GameManager.Instance != null && GameManager.Instance.Player != null ? GameManager.Instance.Player.transform : null;

    private Vector3 farmReturnPosition, overviewReturnPosition;
    private float overviewReturnSize;
    private bool farmFocused, overviewActive, pixelPerfectWasEnabled;
    private UnityEngine.Rendering.Universal.PixelPerfectCamera pixelPerfect;
    public bool IsOverview => overviewActive;

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
        cameraController?.ReleaseFollow();
    }

    private void OnDestroy()
    {
        EndOverview();
        if (Instance == this)
            Instance = null;
    }

    public void FocusFarm(FarmPlotController farmPlot)
    {
        if (farmPlot == null || cameraController == null)
            return;
        EndOverview();
        if (!farmFocused)
            farmReturnPosition = cameraController.transform.position;
        farmFocused = true;
        SmoothMoveTo(farmPlot.FocusPosition);
    }

    public void CloseFarmFocus()
    {
        if (!farmFocused)
            return;
        EndOverview();
        farmFocused = false;
        cameraController?.MoveSmooth(farmReturnPosition);
    }

    public void BeginOverview()
    {
        if (overviewActive || cameraController == null || GameManager.Instance?.Defense?.Navigation == null)
            return;
        var view = cameraController.View;
        overviewReturnPosition = view.transform.position;
        overviewReturnSize = view.orthographicSize;
        pixelPerfect = view.GetComponent<UnityEngine.Rendering.Universal.PixelPerfectCamera>();
        pixelPerfectWasEnabled = pixelPerfect != null && pixelPerfect.enabled;
        if (pixelPerfect != null)
            pixelPerfect.enabled = false;
        overviewActive = true;
        cameraController.SetOverview(true);
        var bounds = GameManager.Instance.Defense.Navigation.barrierBounds;
        view.orthographicSize = OverviewSize(bounds, view.aspect);
        cameraController.Teleport(bounds.center);
    }

    public static float OverviewSize(Rect bounds, float aspect) => Mathf.Max(bounds.height * .5f, bounds.width * .5f / Mathf.Max(.1f, aspect)) + 4;
    public void EndOverview()
    {
        if (!overviewActive)
            return;
        overviewActive = false;
        if (cameraController != null)
        {
            cameraController.View.orthographicSize = overviewReturnSize;
            cameraController.Teleport(overviewReturnPosition);
            cameraController.SetOverview(false);
        }

        if (pixelPerfect != null)
            pixelPerfect.enabled = pixelPerfectWasEnabled;
    }

    private void OnApplicationFocus(bool focus)
    {
        if (!focus)
            EndOverview();
    }

    public void FollowPlayer(bool teleport = false)
    {
        if (cameraController != null)
            cameraController.Follow(player, teleport);
    }

    public void SmoothMoveTo(Vector3 worldPosition)
    {
        cameraController?.MoveSmooth(worldPosition);
    }

    public void TeleportTo(Vector3 worldPosition)
    {
        cameraController?.Teleport(worldPosition);
    }
}
