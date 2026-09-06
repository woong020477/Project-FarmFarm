// 역할: 입력 좌표를 카메라 이동으로 변환하고 드래그·추적·부드러운 이동을 수행한다.
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class CameraController : MonoBehaviour
{
    [Header("Follow")]
    [SerializeField]
    private Transform followTarget;
    [SerializeField]
    private Vector2 followOffset;
    [Min(0.01f)]
    [SerializeField]
    private float smoothTime = 0.2f;
    [Header("Drag")]
    [SerializeField]
    private bool allowDrag = true;
    [Min(0)]
    [SerializeField]
    private float dragThreshold = 6;
    private Vector3 velocity;
    private Vector3 destination;
    private bool hasDestination;
    private Camera view;
    private Vector2 pressPoint, previousPointer;
    private bool pointerCaptured, dragging;
    private int touchId;
    private readonly List<RaycastResult> uiHits = new();
    public bool IsOverview { get; private set; }
    public bool IsDragging => dragging;
    public Camera View => view != null ? view : GetComponent<Camera>();

    private void Awake()
    {
        view = GetComponent<Camera>();
        ReleaseFollow();
    }

    private void Update()
    {
        if (!allowDrag || IsOverview)
        {
            CancelDrag();
            return;
        }

        var touch = Touchscreen.current?.primaryTouch;
        bool touching = touch != null && (touch.press.isPressed || touch.press.wasReleasedThisFrame);
        bool down = touching ? touch.press.wasPressedThisFrame : Mouse.current?.leftButton.wasPressedThisFrame == true;
        bool held = touching ? touch.press.isPressed : Mouse.current?.leftButton.isPressed == true;
        Vector2 point = touching ? touch.position.ReadValue() : Mouse.current == null ? Vector2.zero : Mouse.current.position.ReadValue();
        if (down)
        {
            touchId = touching ? touch.touchId.ReadValue() : -1;
            pointerCaptured = !PointerBlocked(point, touchId);
            pressPoint = previousPointer = point;
            dragging = false;
        }

        if (!held)
        {
            CancelDrag();
            return;
        }

        if (!pointerCaptured)
            return;
        if (!dragging && (point - pressPoint).sqrMagnitude < dragThreshold * dragThreshold)
            return;
        if (!dragging)
        {
            ReleaseFollow();
            dragging = true;
        }

        PanPixels(point - previousPointer);
        previousPointer = point;
    }

    private bool PointerBlocked(Vector2 point, int id)
    {
        if (EventSystem.current != null)
        {
            uiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point, pointerId = id }, uiHits);
            if (uiHits.Count > 0)
                return true;
        }

        var hit = Physics2D.OverlapPoint(View.ScreenToWorldPoint(point));
        return hit != null && hit.GetComponent<WorldPanelButton>() != null;
    }

    public void PanPixels(Vector2 pointerDelta)
    {
        if (IsOverview)
            return;
        ReleaseFollow();
        float units = View.orthographicSize * 2 / Mathf.Max(1, View.pixelHeight);
        transform.position -= new Vector3(pointerDelta.x * units, pointerDelta.y * units, 0);
    }

    public void ReleaseFollow()
    {
        followTarget = null;
        hasDestination = false;
        velocity = Vector3.zero;
    }

    public void SetOverview(bool active)
    {
        IsOverview = active;
        CancelDrag();
        ReleaseFollow();
    }

    private void CancelDrag()
    {
        pointerCaptured = false;
        dragging = false;
    }

    private void OnDisable() => CancelDrag();
    private void OnApplicationFocus(bool focus)
    {
        if (!focus)
            CancelDrag();
    }

    private void LateUpdate()
    {
        Vector3 targetPosition;
        if (followTarget != null)
        {
            targetPosition = new Vector3(followTarget.position.x + followOffset.x, followTarget.position.y + followOffset.y, transform.position.z);
        }
        else if (hasDestination)
        {
            targetPosition = destination;
        }
        else
        {
            return;
        }

        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        if (followTarget == null && Vector3.SqrMagnitude(transform.position - targetPosition) <= 0.0001f)
        {
            transform.position = targetPosition;
            hasDestination = false;
            velocity = Vector3.zero;
        }
    }

    public void Follow(Transform target, bool teleport)
    {
        followTarget = target;
        hasDestination = false;
        velocity = Vector3.zero;
        if (teleport && target != null)
            transform.position = new Vector3(target.position.x + followOffset.x, target.position.y + followOffset.y, transform.position.z);
    }

    public void MoveSmooth(Vector3 worldPosition)
    {
        followTarget = null;
        destination = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);
        hasDestination = true;
        velocity = Vector3.zero;
    }

    public void Teleport(Vector3 worldPosition)
    {
        followTarget = null;
        hasDestination = false;
        velocity = Vector3.zero;
        transform.position = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);
    }
}
