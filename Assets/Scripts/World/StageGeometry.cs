using UnityEngine;

public sealed class StageGeometry : MonoBehaviour
{
    [SerializeField] Vector2 size = new(44, 13.6f);
    [SerializeField] BoxCollider2D[] surfaces;
    public Vector2 Size => size;

    public bool OverlapsTerrain(Vector2 center, Vector2 halfSize)
    {
        var area = new Rect(center - halfSize, halfSize * 2);
        foreach (BoxCollider2D surface in surfaces)
        {
            if (surface == null || !surface.enabled || !surface.gameObject.activeInHierarchy)
                continue;
            Bounds bounds = surface.bounds;
            if (new Rect((Vector2)bounds.min, (Vector2)bounds.size).Overlaps(area))
                return true;
        }
        return false;
    }

    public bool ClipTerrain(Vector2 from, Vector2 to, Vector2 padding, out Vector2 clipped)
        => ClipTerrain(from, to, padding, out clipped, out _);

    public bool ClipTerrain(Vector2 from, Vector2 to, Vector2 padding, out Vector2 clipped, out Vector2 normal)
    {
        clipped = to;
        normal = Vector2.zero;
        Vector2 travel = to - from;
        float length = travel.magnitude;
        if (length <= .000001f)
            return false;
        var ray = new Ray(from, travel / length);
        float nearest = length;
        bool hit = false;
        foreach (BoxCollider2D surface in surfaces)
        {
            if (surface == null || !surface.enabled || !surface.gameObject.activeInHierarchy)
                continue;
            Bounds bounds = surface.bounds;
            bounds.Expand(new Vector3(padding.x * 2, padding.y * 2, 2));
            if (!bounds.IntersectRay(ray, out float distance) || distance > nearest)
                continue;
            nearest = distance;
            Vector2 contact = from + travel / length * distance;
            Vector2 local = contact - (Vector2)bounds.center;
            Vector2 extent = bounds.extents;
            normal = Mathf.Abs(Mathf.Abs(local.x) - extent.x) < Mathf.Abs(Mathf.Abs(local.y) - extent.y)
                ? new Vector2(Mathf.Sign(local.x), 0)
                : new Vector2(0, Mathf.Sign(local.y));
            hit = true;
        }
        if (hit)
            clipped = from + travel / length * Mathf.Max(0, nearest - .01f);
        return hit;
    }
}
