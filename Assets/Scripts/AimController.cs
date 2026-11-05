using UnityEngine;
using UnityEngine.InputSystem;

public sealed class AimController : MonoBehaviour
{
    [SerializeField, Min(.01f)] float maxDragDistance = 1.5f;
    [SerializeField, Min(.01f)] float maxLaunchSpeed = 48;
    [SerializeField, Min(0)] float deadZone = .06f;
    BallController dragging;
    Vector2 grabPoint;

    public void CancelDrag() => dragging = null;

    void Update()
    {
        var stages = GetComponent<StageManager>();
        var state = GetComponent<GameStateManager>();
        var keyboard = Keyboard.current;
        if (state.IsExecuting && keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
        {
            state.FinishExecution();
            return;
        }
        if (!state.CanAim)
            return;

        var mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 point = stages.View.ScreenToWorldPoint(mouse.position.ReadValue());
            if (mouse.leftButton.wasPressedThisFrame)
            {
                foreach (var ball in stages.Balls)
                {
                    if (!ball.Contains(point))
                        continue;
                    dragging = ball;
                    grabPoint = point;
                    break;
                }
            }

            if (dragging != null)
            {
                Vector2 pull = grabPoint - point;
                Vector2 velocity = pull.magnitude <= deadZone ? Vector2.zero :
                    Vector2.ClampMagnitude(pull, maxDragDistance) * (maxLaunchSpeed / maxDragDistance);
                dragging.SetShot(velocity);
                if (!mouse.leftButton.isPressed)
                    CancelDrag();
            }

            if (mouse.rightButton.wasPressedThisFrame)
            {
                foreach (var ball in stages.Balls)
                {
                    if (!ball.Contains(point))
                        continue;
                    if (dragging == ball)
                        CancelDrag();
                    ball.SetShot(Vector2.zero);
                    break;
                }
            }
        }

        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame && state.QueueLaunch())
            CancelDrag();
    }
}
