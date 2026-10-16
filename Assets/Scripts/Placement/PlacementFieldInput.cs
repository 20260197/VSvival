using UnityEngine;
using UnityEngine.EventSystems;

namespace CookieBreaker.Placement
{
    // A sibling behind the menus/modals, never an ancestor of their buttons.
    public sealed class PlacementFieldInput : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private TurretPlacementController placement;
        public void Configure(TurretPlacementController controller) { placement = controller; }
        public void OnPointerClick(PointerEventData data)
        { if (data.button == PointerEventData.InputButton.Left && placement != null) placement.HandleFieldClick(data.position); }
    }
}
