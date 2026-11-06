using UnityEngine;

public sealed class BulletView : MonoBehaviour
{
    public TrailRenderer trail;

    public void ResetTrail(bool emitting)
    {
        if (trail == null) return;
        trail.startColor = new Color(1,.08f,.12f,.8f);
        trail.endColor = new Color(.65f,.015f,.04f,0);
        trail.time = .23f;
        trail.startWidth = .075f;
        trail.Clear();
        trail.emitting = emitting;
    }
}
