using UnityEngine;

public sealed class BallController : MonoBehaviour
{
    public Rigidbody2D Body { get; private set; }
    public Vector2 Shot { get; private set; }
    public float BounceMultiplier { get; set; } = 1;
    ShotPreview preview;
    Transform icon;
    TrailRenderer swordTrail;
    float radius;

    public void Initialize(float size, float mass, float damping, PhysicsMaterial2D material,
        Sprite circle, Sprite square, Color color, Material trailMaterial)
    {
        radius = size;
        Body = gameObject.AddComponent<Rigidbody2D>();
        Body.gravityScale = 0;
        Body.mass = mass;
        Body.linearDamping = damping;
        Body.constraints = RigidbodyConstraints2D.FreezeRotation;
        Body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        Body.interpolation = RigidbodyInterpolation2D.Interpolate;
        var collider = gameObject.AddComponent<CircleCollider2D>();
        collider.radius = radius;
        collider.sharedMaterial = material;
        preview = new ShotPreview(Body, collider, square, color);

        var ballIcon = StageManager.Draw(transform, "Ball", circle, color, Vector2.zero, Vector2.one * radius * 2, 2);
        icon = ballIcon.transform;
        Face(Vector2.right);

        swordTrail = new GameObject("Sword trail").AddComponent<TrailRenderer>();
        swordTrail.emitting = false;
        swordTrail.transform.SetParent(icon, false);
        swordTrail.transform.localPosition = new Vector3(.219f, .219f);
        swordTrail.sharedMaterial = trailMaterial != null ? trailMaterial : ballIcon.sharedMaterial;
        swordTrail.time = .11f;
        swordTrail.minVertexDistance = .035f;
        swordTrail.startWidth = .095f;
        swordTrail.endWidth = 0;
        swordTrail.startColor = new Color(color.r, color.g, color.b, .75f);
        swordTrail.endColor = new Color(color.r, color.g, color.b, 0);
        swordTrail.sortingOrder = 1;
    }

    void LateUpdate()
    {
        Vector2 velocity = Body.linearVelocity;
        if (velocity.sqrMagnitude > .000001f)
            Face(velocity);
        swordTrail.emitting = velocity.sqrMagnitude > .04f;
    }

    void Face(Vector2 direction)
    {
        // The drawn sword points 45 degrees above the sprite's local X axis.
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        icon.localRotation = Quaternion.Euler(0, 0, angle - 45f);
    }

    public bool Contains(Vector2 point) => (point - Body.position).sqrMagnitude <= radius * radius;

    public bool OverlapsBox(Vector2 center, float halfSize)
    {
        Vector2 offset = Body.position - center;
        Vector2 closest = new Vector2(Mathf.Clamp(offset.x, -halfSize, halfSize),
            Mathf.Clamp(offset.y, -halfSize, halfSize));
        return (offset - closest).sqrMagnitude <= radius * radius;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        BoostAfterBounce(collision);
        if (collision.collider.TryGetComponent<EnemyTarget>(out var enemy))
            enemy.TakeHit();
    }

    void BoostAfterBounce(Collision2D collision)
    {
        // Walls and enemies use static colliders. Other balls have dynamic bodies.
        if (BounceMultiplier <= 1 ||
            (collision.rigidbody != null && !collision.collider.TryGetComponent<BallController>(out _)))
            return;

        Vector2 velocity = Body.linearVelocity;
        for (int i = 0; i < collision.contactCount; i++)
        {
            var contact = collision.GetContact(i);
            if (Vector2.Dot(velocity, contact.normal) <= .15f ||
                Mathf.Abs(Vector2.Dot(collision.relativeVelocity, contact.normal)) <= .15f)
                continue;

            Body.linearVelocity = Vector2.ClampMagnitude(
                velocity * BounceMultiplier, Physics2D.maxTranslationSpeed);
            break;
        }
    }

    public void LaunchShot()
    {
        Body.linearVelocity = Shot;
        SetShot(Vector2.zero);
    }

    public void SetShot(Vector2 velocity)
    {
        if (Shot.Equals(velocity))
            return;

        Shot = velocity;
        preview.Show(velocity, BounceMultiplier);
        if (velocity.sqrMagnitude > 0)
            Face(velocity);
    }
}
