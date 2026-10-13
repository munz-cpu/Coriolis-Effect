using UnityEngine;
using UnityEngine.EventSystems;

public sealed class CoriolisPinDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private CoriolisSimulator simulator;
    [SerializeField] private int pinIndex;

    public void OnBeginDrag(PointerEventData data) => simulator.BeginDrag(pinIndex, data);
    public void OnDrag(PointerEventData data) => simulator.Drag(pinIndex, data);
    public void OnEndDrag(PointerEventData data) => simulator.EndDrag(pinIndex, data);
}
