using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PlayerController : MonoBehaviour
{
    public const int MaxHealth = 10;
    const float DashCooldown = 2f;
    const float DashDuration = .16f;
    const float DashSpeed = 21f;
    [SerializeField] SpriteRenderer hero;
    [SerializeField] TrailRenderer mountTrail;
    [SerializeField] float moveSpeed = 6;
    public float MoveSpeed => moveSpeed;
    [SerializeField] Vector2 spawnPosition = new(-12, 0);
    StageGeometry geometry;
    CameraFollow cameraFollow;
    float hurtTimer;
    float dashCooldownRemaining;
    float dashTimeRemaining;
    Vector2 dashDirection;

    public int Health { get; private set; } = MaxHealth;
    public bool IsDead => Health == 0;
    public Vector2 Position => transform.position;
    public Vector2 PreviousPosition { get; private set; }

    public void Initialize(StageGeometry stageGeometry, CameraFollow follow)
    {
        geometry = stageGeometry;
        cameraFollow = follow;
    }

    public void ResetPlayer()
    {
        Health = MaxHealth;
        hurtTimer = 0;
        dashCooldownRemaining = 0;
        dashTimeRemaining = 0;
        hero.color = Color.white;
        mountTrail.emitting = false;
        transform.position = spawnPosition;
        PreviousPosition = spawnPosition;
        mountTrail.Clear();
        cameraFollow.ResetView();
    }

    public void Tick(float dt)
    {
        PreviousPosition = Position;
        hurtTimer = Mathf.Max(0, hurtTimer - dt);
        dashCooldownRemaining = Mathf.Max(0, dashCooldownRemaining - dt);
        hero.color = hurtTimer > 0 ? new Color(1, .4f, .4f) : Color.white;
        var keyboard = Keyboard.current;
        if (keyboard == null)
            return;
        Vector2 input = new(
            (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
            (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
        bool dashPressed = keyboard.leftShiftKey.wasPressedThisFrame || keyboard.rightShiftKey.wasPressedThisFrame;
        if (dashPressed && dashCooldownRemaining == 0 && input != Vector2.zero)
        {
            dashDirection = input.normalized;
            dashTimeRemaining = DashDuration;
            dashCooldownRemaining = DashCooldown;
        }

        float dashStep = Mathf.Min(dt, dashTimeRemaining);
        dashTimeRemaining -= dashStep;
        Vector2 movement = dashDirection * (DashSpeed * dashStep)
            + Vector2.ClampMagnitude(input, 1) * (moveSpeed * (dt - dashStep));
        Vector2 next = Position + movement;
        Vector2 limit = geometry.Size * .5f - new Vector2(3.35f, .65f);
        next.x = Mathf.Clamp(next.x, -limit.x, limit.x);
        next.y = Mathf.Clamp(next.y, -limit.y, limit.y);
        Vector2 clearance = new(.8f, .4f);
        geometry.ClipTerrain(Position, new Vector2(next.x, Position.y), clearance, out Vector2 horizontal);
        geometry.ClipTerrain(horizontal, new Vector2(horizontal.x, next.y), clearance, out next);
        transform.position = next;
        mountTrail.emitting = (next - PreviousPosition).sqrMagnitude > 0;
    }

    public void TakeHit()
    {
        if (hurtTimer > 0 || IsDead)
            return;
        Health = Mathf.Max(0, Health - 1);
        hurtTimer = .6f;
        hero.color = new Color(1, .4f, .4f);
        cameraFollow.Shake();
        if (IsDead)
            mountTrail.emitting = false;
    }
}
