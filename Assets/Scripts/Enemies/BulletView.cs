using UnityEngine;

public sealed class BulletView : MonoBehaviour
{
    public TrailRenderer trail;
    SpriteRenderer blade;
    Color activeBladeColor;
    DroppedSwordIndicator droppedIndicator;

    public void SetDroppedIndicator(bool visible)
    {
        if (visible && blade == null && transform.Find("Visual/Blade")?.TryGetComponent(out blade) == true)
            activeBladeColor = blade.color;
        DroppedSwordIndicator.SetVisible(ref droppedIndicator, blade, visible);
    }

    public void SetBladeInactive(bool inactive)
    {
        if (blade == null)
        {
            if (transform.Find("Visual/Blade")?.TryGetComponent(out blade) != true) return;
            activeBladeColor = blade.color;
        }
        blade.color = inactive ? SwordSystem.PlantedBlade : activeBladeColor;
        SetDroppedIndicator(inactive);
    }

    public void ResetTrail(bool emitting)
    {
        if (emitting) SetDroppedIndicator(false);
        if (trail == null) return;
        trail.startColor = new Color(1,.08f,.12f,.8f);
        trail.endColor = new Color(.65f,.015f,.04f,0);
        trail.time = .23f;
        trail.startWidth = .075f;
        trail.Clear();
        trail.emitting = emitting;
    }
}
