using UnityEngine;

namespace CookieBreaker.Combat
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaPanel : MonoBehaviour
    {
        private RectTransform panel;
        private Rect previous;
        private Vector2Int previousSize;

        private void OnEnable() { panel = GetComponent<RectTransform>(); Apply(); }
        private void Update() { Apply(); }

        private void Apply()
        {
            if (Screen.width <= 0 || Screen.height <= 0) return;
            Rect area = Screen.safeArea;
            Vector2Int size = new Vector2Int(Screen.width, Screen.height);
            if (area == previous && size == previousSize) return;
            previous = area;
            previousSize = size;
            panel.anchorMin = new Vector2(area.xMin / size.x, area.yMin / size.y);
            panel.anchorMax = new Vector2(area.xMax / size.x, area.yMax / size.y);
            panel.offsetMin = panel.offsetMax = Vector2.zero;
        }
    }
}
