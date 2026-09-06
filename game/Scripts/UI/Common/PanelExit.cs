// 역할: 패널 배경 자체를 클릭한 경우에만 닫기 이벤트를 전달한다.
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public sealed class PanelExit : MonoBehaviour, IPointerClickHandler
{
    [SerializeField]
    private UnityEvent onExit;
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.pointerCurrentRaycast.gameObject == gameObject)
            onExit?.Invoke();
    }
}
