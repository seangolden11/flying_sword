using UnityEngine;

[RequireComponent(typeof(LaunchManager))]
public sealed class GameStateManager : MonoBehaviour
{
    const float SpeedLimitDelay = 5f;
    const float SpeedLimitRampDuration = 5f;

    [SerializeField, Min(.001f)] float stopSpeed = .085f;
    [SerializeField, Min(.02f)] float settleTime = .16f;
    bool launchPending;
    float slowTime;
    float executionTime;
    float initialSpeedLimit;

    public bool IsExecuting { get; private set; }
    public bool CanAim => !IsExecuting && !launchPending;

    public bool QueueLaunch()
    {
        if (!CanAim)
            return false;
        foreach (var ball in GetComponent<StageManager>().Balls)
        {
            if (ball.Shot.sqrMagnitude == 0)
                continue;
            launchPending = true;
            return true;
        }
        return false;
    }

    public void ResetRound()
    {
        if (IsExecuting && Physics2D.maxTranslationSpeed != initialSpeedLimit)
            Physics2D.maxTranslationSpeed = initialSpeedLimit;
        launchPending = false;
        IsExecuting = false;
        slowTime = 0;
        executionTime = 0;
    }

    void OnDisable() => ResetRound();

    void FixedUpdate()
    {
        var stages = GetComponent<StageManager>();
        var balls = stages.Balls;
        if (launchPending)
        {
            launchPending = false;
            initialSpeedLimit = Physics2D.maxTranslationSpeed;
            IsExecuting = true;
            GetComponent<LaunchManager>().Launch(balls);
            return;
        }
        if (!IsExecuting)
            return;

        executionTime += Time.fixedDeltaTime;
        float progress = Mathf.Clamp01((executionTime - SpeedLimitDelay) / SpeedLimitRampDuration);
        if (progress >= 1f)
        {
            FinishExecution();
            return;
        }

        float speedLimit = Mathf.Lerp(initialSpeedLimit, 0f, progress);
        if (Physics2D.maxTranslationSpeed != speedLimit)
            Physics2D.maxTranslationSpeed = speedLimit;

        foreach (var ball in balls)
        {
            if (ball.Body.linearVelocity.sqrMagnitude <= stopSpeed * stopSpeed)
                continue;
            slowTime = 0;
            return;
        }

        slowTime += Time.fixedDeltaTime;
        if (slowTime < settleTime)
            return;
        FinishExecution();
    }

    public void FinishExecution()
    {
        if (!IsExecuting)
            return;

        var stages = GetComponent<StageManager>();
        foreach (var ball in stages.Balls)
        {
            ball.Body.linearVelocity = Vector2.zero;
            ball.Body.Sleep();
        }

        ResetRound();
    }
}

