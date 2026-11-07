using UnityEngine;

public sealed class DroppedSwordIndicator : MonoBehaviour
{
    static Sprite triangle;
    SpriteRenderer blade;

    public static void SetVisible(ref DroppedSwordIndicator indicator, SpriteRenderer blade, bool visible)
    {
        if (visible && indicator == null && blade != null)
        {
            var marker = new GameObject("Dropped Sword Triangle");
            marker.transform.SetParent(blade.transform.parent, false);
            indicator = marker.AddComponent<DroppedSwordIndicator>();
            indicator.blade = blade;
            var renderer = marker.AddComponent<SpriteRenderer>();
            renderer.sprite = Triangle();
            renderer.color = new Color32(255, 220, 51, 255);
            renderer.sortingLayerID = blade.sortingLayerID;
            renderer.sortingOrder = blade.sortingOrder + 12;
        }

        if (indicator != null && indicator.gameObject.activeSelf != visible)
            indicator.gameObject.SetActive(visible);
    }

    void LateUpdate()
    {
        Bounds bounds = blade.bounds;
        Vector3 scale = transform.parent.lossyScale;
        float length = blade.sprite.bounds.size.y * Mathf.Abs(blade.transform.lossyScale.y);
        float size = Mathf.Clamp(.35f * Mathf.Sqrt(length / .5f), .28f, 1.05f);
        transform.SetPositionAndRotation(new Vector3(bounds.center.x, bounds.max.y + .18f + size * .5f,
            bounds.center.z), Quaternion.identity);
        transform.localScale = new Vector3(size / Mathf.Abs(scale.x), size / Mathf.Abs(scale.y), 1);
    }

    static Sprite Triangle()
    {
        if (triangle != null) return triangle;
        const int resolution = 32;
        var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color32[resolution * resolution];
        for (int y = 0; y < resolution; y++)
        {
            float halfWidth = Mathf.Max(0, (y - 3) * .56f);
            for (int x = 0; x < resolution; x++)
            {
                float edge = halfWidth - Mathf.Abs(x + .5f - resolution * .5f);
                byte alpha = y >= 3 && y <= 28 ? (byte)(255 * Mathf.Clamp01(edge + .5f)) : (byte)0;
                pixels[y * resolution + x] = new Color32(255, 255, 255, alpha);
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        triangle = Sprite.Create(texture, new Rect(0, 0, resolution, resolution), new Vector2(.5f, .5f), resolution);
        triangle.hideFlags = HideFlags.HideAndDontSave;
        return triangle;
    }
}
