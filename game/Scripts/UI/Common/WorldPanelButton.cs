// 역할: 월드 오브젝트 입력을 해당 기능 패널 열기로 연결한다.
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public sealed class WorldPanelButton : MonoBehaviour
{
    [SerializeField]
    private UIManager ui;
    private Collider2D area;
    private void Awake() => area = GetComponent<Collider2D>();
    private void Update()
    {
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame || EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;
        var camera = GameManager.Instance?.CameraController?.GetComponent<Camera>();
        if (camera != null && area.OverlapPoint(camera.ScreenToWorldPoint(Mouse.current.position.ReadValue())))
            ui.OpenBarracksPanel();
    }
}
