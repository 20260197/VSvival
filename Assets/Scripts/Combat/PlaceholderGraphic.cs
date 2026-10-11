using UnityEngine;

namespace CookieBreaker.Combat
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PlaceholderGraphic : MonoBehaviour
    {
        public enum Shape { Cookie, Disc, Square }
        [SerializeField] private Shape shape;
        private Sprite sprite;
        private Texture2D texture;
        public void Configure(Shape value) { shape = value; }

        private void Awake()
        {
            const int size = 64;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "M1 procedural " + shape;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float px = (x + 0.5f) / size - 0.5f;
                float py = (y + 0.5f) / size - 0.5f;
                Color color = Color.white;
                if (shape != Shape.Square && px * px + py * py > 0.24f) color = Color.clear;
                else if (shape == Shape.Cookie)
                {
                    color = new Color(0.91f, 0.64f, 0.32f, 1);
                    if (Chip(px, py, -0.18f, 0.16f) || Chip(px, py, 0.2f, 0.12f) ||
                        Chip(px, py, 0.08f, -0.2f) || Chip(px, py, -0.25f, -0.16f))
                        color = new Color(0.26f, 0.12f, 0.05f, 1);
                }
                pixels[y * size + x] = color;
            }
            texture.SetPixels(pixels);
            texture.Apply(false);
            sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
            GetComponent<SpriteRenderer>().sprite = sprite;
        }

        private static bool Chip(float x, float y, float cx, float cy)
        {
            return (x - cx) * (x - cx) + (y - cy) * (y - cy) < 0.004f;
        }

        private void OnDestroy()
        {
            if (sprite != null) Destroy(sprite);
            if (texture != null) Destroy(texture);
        }
    }
}
