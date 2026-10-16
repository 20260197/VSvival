using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CookieBreaker.Placement
{
    // A sibling behind the menus/modals, never an ancestor of their buttons.
    public sealed class PlacementFieldInput : MonoBehaviour, IPointerClickHandler, IPointerDownHandler,
        IPointerUpHandler, IPointerMoveHandler, IDragHandler, IBeginDragHandler, IEndDragHandler,
        IInitializePotentialDragHandler, ICancelHandler, IPointerExitHandler, IPointerEnterHandler
    {
        [SerializeField] private TurretPlacementController placement;
        private readonly PlacementGesture gesture = new PlacementGesture();
        private readonly List<RaycastResult> hits = new List<RaycastResult>();
        public void Configure(TurretPlacementController controller) { placement = controller; }
        private void OnEnable() { if (placement != null) placement.Changed += ModeChanged; }
        private void OnDisable() { if (placement != null) placement.Changed -= ModeChanged; CancelGesture(); }
        private void OnApplicationFocus(bool focused) { if (!focused) CancelGesture(); }
        private void OnApplicationPause(bool paused) { if (paused) CancelGesture(); }
        private void ModeChanged() { if (placement.Mode != FieldMode.Placement) CancelGesture(); }
        private void CancelGesture() { gesture.Cancel(); if (placement != null) placement.CancelPreview(); }

        public void OnPointerDown(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || placement == null || placement.Mode != FieldMode.Placement || !gesture.Begin(data.pointerId)) return;
            if (!placement.BeginPreview(data.position, OverField(data))) gesture.Cancel();
        }
        public void OnPointerMove(PointerEventData data) { Move(data); }
        public void OnDrag(PointerEventData data) { Move(data); }
        public void OnBeginDrag(PointerEventData data) { Move(data); }
        public void OnInitializePotentialDrag(PointerEventData data)
        { data.useDragThreshold = placement == null || placement.Mode != FieldMode.Placement; }
        public void OnPointerEnter(PointerEventData data) { Move(data); }
        public void OnPointerExit(PointerEventData data)
        { if (gesture.Owns(data.pointerId)) placement.UpdatePreview(data.position, false); }
        private void Move(PointerEventData data)
        { if (gesture.Owns(data.pointerId)) placement.UpdatePreview(data.position, OverField(data)); }
        public void OnPointerUp(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || !gesture.Owns(data.pointerId)) return;
            bool commit = gesture.Release(data.pointerId, OverField(data));
            placement.EndPreview(data.position, commit);
        }
        public void OnEndDrag(PointerEventData data)
        { if (data.button == PointerEventData.InputButton.Left && gesture.Owns(data.pointerId)) CancelGesture(); }
        public void OnCancel(BaseEventData data) { CancelGesture(); }
        private bool OverField(PointerEventData data)
        {
            if (EventSystem.current == null || !RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform, data.position, data.pressEventCamera)) return false;
            hits.Clear(); EventSystem.current.RaycastAll(data, hits);
            return hits.Count > 0 && hits[0].gameObject == gameObject;
        }
        public void OnPointerClick(PointerEventData data)
        { if (data.button == PointerEventData.InputButton.Left && placement != null && placement.Mode == FieldMode.Selling) placement.HandleFieldClick(data.position); }
    }
}
