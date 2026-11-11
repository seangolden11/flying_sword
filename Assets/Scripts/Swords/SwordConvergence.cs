using UnityEngine;

public struct SwordConvergence
{
    Vector2 origin;
    Vector2 departure;
    Vector2 approach;
    float duration;
    float elapsed;

    public bool IsConfigured => duration > 0;

    public static SwordConvergence Begin(Vector2 origin, Vector2 target, int slot, int count, float duration)
    {
        Vector2 forward = (target - origin).normalized;
        if (forward == Vector2.zero) forward = Vector2.right;
        Vector2 side = Vector2.Perpendicular(forward);
        float angle = (slot + .25f) * (2 * Mathf.PI / count);
        float spread = Mathf.Clamp(Vector2.Distance(origin, target) * .34f, .85f, 3.2f);
        return new SwordConvergence
        {
            origin = origin,
            departure = forward * spread * .65f + side * (Mathf.Sin(angle) * spread),
            approach = (-forward * Mathf.Cos(angle) + side * Mathf.Sin(angle)) * spread,
            duration = duration
        };
    }

    public Vector2 Advance(Vector2 destination, float dt)
    {
        elapsed = Mathf.Min(duration, elapsed + dt);
        float t = elapsed / duration;
        Vector2 entry = Vector2.Lerp(origin, origin + departure, t);
        Vector2 middle = Vector2.Lerp(origin + departure, destination + approach, t);
        Vector2 exit = Vector2.Lerp(destination + approach, destination, t);
        return Vector2.Lerp(Vector2.Lerp(entry, middle, t), Vector2.Lerp(middle, exit, t), t);
    }
}
