// 역할: 누르고 있는 동안만 기지 전체 보기를 유지하는 UI 입력 어댑터.
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class CameraHold : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private bool holding;
    public void OnPointerDown(PointerEventData data)
    {
        if (data.button != PointerEventData.InputButton.Left)
            return;
        holding = true;
        GameManager.Instance?.Cameras?.BeginOverview();
    }

    public void OnPointerUp(PointerEventData data) => Release();
    private void OnDisable() => Release();
    private void Release()
    {
        if (!holding)
            return;
        holding = false;
        GameManager.Instance?.Cameras?.EndOverview();
    }
}
