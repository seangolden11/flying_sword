using System.Collections.Generic;
using UnityEngine;

sealed class ShotPreview
{
    const float Duration = .9f;
    const float DotSize = .085f;
    const float MinDotSpacing = .24f;
    const float ContactGap = .008f;
    const int MaxContactsPerStep = 4;

    readonly Rigidbody2D body;
    readonly CircleCollider2D source;
    readonly Sprite sprite;
    readonly Color color;
    readonly List<SpriteRenderer> dots = new();
    readonly List<RaycastHit2D> hits = new();
    readonly ContactFilter2D filter = new() { useTriggers = false };
    int visibleDots;
    Vector2 lastDot;

    public ShotPreview(Rigidbody2D body, CircleCollider2D source, Sprite sprite, Color color)
    {
        this.body = body;
        this.source = source;
        this.sprite = sprite;
        this.color = Color.Lerp(color, Color.white, .35f);
    }

    public void Show(Vector2 velocity, float boost)
    {
        visibleDots = 0;
        if (velocity.sqrMagnitude > 0)
            Trace(velocity, boost);

        for (int i = visibleDots; i < dots.Count; i++)
            dots[i].enabled = false;
    }

    void Trace(Vector2 velocity, float boost)
    {
        Vector2 position = body.position;
        lastDot = position;
        float speedLimit = Physics2D.maxTranslationSpeed;
        float step = Time.fixedDeltaTime;
        Physics2D.SyncTransforms();

        for (float elapsed = 0; elapsed < Duration && velocity.sqrMagnitude > .0072f; elapsed += step)
        {
            velocity = Vector2.ClampMagnitude(velocity / (1 + body.linearDamping * step), speedLimit);
            float remaining = step;
            for (int contact = 0; contact < MaxContactsPerStep && remaining > .0001f; contact++)
            {
                float speed = velocity.magnitude;
                if (speed < .085f)
                    break;

                Vector2 direction = velocity / speed;
                if (!FindContact(position, direction, speed * remaining, out var hit))
                {
                    position += velocity * remaining;
                    break;
                }

                position += direction * hit.distance;
                AddDot(position);
                remaining -= hit.distance / speed;

                float normalSpeed = Vector2.Dot(velocity, hit.normal);
                var targetBody = hit.collider.attachedRigidbody;
                float impulseShare = targetBody != null && targetBody.bodyType == RigidbodyType2D.Dynamic
                    ? targetBody.mass / (body.mass + targetBody.mass)
                    : 1;
                float bounce = normalSpeed < -Physics2D.bounceThreshold ? source.sharedMaterial.bounciness : 0;
                velocity -= (1 + bounce) * impulseShare * normalSpeed * hit.normal;
                if (Vector2.Dot(velocity, hit.normal) > .15f)
                    velocity = Vector2.ClampMagnitude(velocity * boost, speedLimit);

                position += hit.normal * ContactGap;
            }
            AddDot(position);
        }
    }

    bool FindContact(Vector2 position, Vector2 direction, float distance, out RaycastHit2D closest)
    {
        int count = Physics2D.CircleCast(position, source.radius, direction, filter, hits, distance);
        closest = default;
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            var hit = hits[i];
            if (hit.collider == source || hit.distance >= nearest || Vector2.Dot(direction, hit.normal) >= 0)
                continue;

            closest = hit;
            nearest = hit.distance;
        }
        return closest.collider != null;
    }

    void AddDot(Vector2 position)
    {
        if ((position - lastDot).sqrMagnitude < MinDotSpacing * MinDotSpacing)
            return;
        lastDot = position;

        if (visibleDots == dots.Count)
        {
            var dot = StageManager.Draw(body.transform, "Trajectory dot", sprite, color,
                Vector2.zero, Vector2.one * DotSize, 2);
            dot.transform.localRotation = Quaternion.Euler(0, 0, 45);
            dots.Add(dot);
        }

        var marker = dots[visibleDots++];
        marker.transform.position = position;
        marker.enabled = true;
    }
}
