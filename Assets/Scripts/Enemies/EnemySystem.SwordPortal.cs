using System.Collections.Generic;
using UnityEngine;

public sealed partial class EnemySystem
{
    const float PortalDuration = 6;
    const float PortalEndDelay = 3;
    const float PortalWindup = .8f;
    const float PortalShotInterval = .45f;
    const float PortalLaneWidth = 5.5f;
    const float PortalSwordLifetime = 1.5f;
    public const float BossRecallCircleSpeed = 30;
    const float PortalRecallSpeed = 70;
    GameObject portalRecallCircle;
    BulletView portalRecallTarget;
    const float PortalCountershotSpeed = 14;
    const int PortalCountershotCount = 20;
    const float PortalCountershotInterval = .08f;
    static readonly Vector2 PortalDirection = new Vector2(.52f, -1).normalized;

    readonly List<BulletView> plantedPortalSwords = new();
    readonly List<BulletView> returningPortalSwords = new();
    readonly Queue<BulletView> queuedPortalSwords = new();
    bool recallingPortalSwords;
    bool portalVolleyStarted;
    float portalCountershotTimer;
    int portalCountershotsRemaining;
    LineRenderer swordPortal;
    LineRenderer portalShield;
    EnemyState swordPortalOwner;
    float portalTime;
    float portalEndTimeRemaining;
    float[] portalLaneTimers;
    float portalHalfWidth;
    public bool HasPortalSwordSource { get; private set; }
    public bool IsSwordPortalActive => swordPortalOwner != null;
    bool PortalBossInvulnerable => !recallingPortalSwords || portalCountershotsRemaining > 0;

    void StartSwordPortal(EnemyState owner)
    {
        owner.UsedSwordPortal = true;
        HasPortalSwordSource = true;
        swordPortalOwner = owner;
        portalTime = PortalDuration;
        portalEndTimeRemaining = PortalEndDelay;
        recallingPortalSwords = false;
        portalVolleyStarted = false;
        portalHalfWidth = geometry.Size.x * .5f - .5f;
        int lanes = Mathf.Max(1, Mathf.CeilToInt(2 * portalHalfWidth / PortalLaneWidth));
        portalLaneTimers = new float[lanes];
        for (int lane = 0; lane < lanes; lane++)
            portalLaneTimers[lane] = PortalWindup + Random.Range(0, PortalShotInterval);

        if (swordPortal == null)
        {
            Material material = areaWarningPrefab.GetComponentInChildren<SpriteRenderer>(true).sharedMaterial;
            swordPortal = CreatePortalRing("Sword Portal", new Vector2(1, .4f), .14f, material);
            portalShield = CreatePortalRing("Portal Shield", new Vector2(1.55f, 1.55f), .065f, material);
        }
        swordPortal.transform.position = geometry.transform.position + Vector3.up * (geometry.Size.y * .5f - .6f);
        swordPortal.transform.localScale = new Vector3(portalHalfWidth, 1, 1);
        portalShield.transform.position = owner.Position;
        swordPortal.enabled = portalShield.enabled = true;
    }

    void TickSwordPortal(EnemyState owner, float dt)
    {
        portalTime -= dt;
        portalShield.transform.position = owner.Position;
        float pulse = .5f + .5f * Mathf.Sin(portalTime * 16);
        swordPortal.startColor = swordPortal.endColor = new Color(.96f, .22f, .72f, .75f + .25f * pulse);
        portalShield.startColor = portalShield.endColor = new Color(.96f, .35f, .8f, .5f + .35f * pulse);
        if (portalTime <= 0)
        {
            TickPortalCounterattack(owner, dt);
            return;
        }
        if (portalTime <= PortalSwordLifetime) return;
        Vector2 origin = swordPortal.transform.position;
        float laneWidth = 2 * portalHalfWidth / portalLaneTimers.Length;
        for (int lane = 0; lane < portalLaneTimers.Length; lane++)
        {
            portalLaneTimers[lane] -= dt;
            if (portalLaneTimers[lane] > 0) continue;
            portalLaneTimers[lane] += PortalShotInterval * Random.Range(.8f, 1.2f);
            float offset = -portalHalfWidth + (lane + Random.value) * laneWidth;
            LaunchPortalSword(origin + Vector2.right * offset, PortalDirection);
        }
    }

    void TickPortalCounterattack(EnemyState owner, float dt)
    {
        Vector2 home = BossSwordHome(owner);
        if (!recallingPortalSwords)
        {
            recallingPortalSwords = true;
            swordPortal.enabled = false;
            portalCountershotTimer = .25f;
            portalCountershotsRemaining = PortalCountershotCount;
            if (plantedPortalSwords.Count > 0)
                OpenBossRecallCircle(ref portalRecallCircle, home);
        }

        bool canRecall = !portalVolleyStarted && plantedPortalSwords.Count > 0 &&
            queuedPortalSwords.Count + returningPortalSwords.Count < PortalCountershotCount;
        if (canRecall)
        {
            if (!plantedPortalSwords.Contains(portalRecallTarget))
                portalRecallTarget = FindNearestPortalSword(portalRecallCircle != null ? portalRecallCircle.transform.position : home);
            if (AdvanceBossRecallCircle(portalRecallCircle, portalRecallTarget.transform.position, dt))
            {
                plantedPortalSwords.Remove(portalRecallTarget);
                portalRecallTarget.ResetTrail(true);
                returningPortalSwords.Add(portalRecallTarget);
                portalRecallTarget = null;
            }
        }
        else if (portalRecallCircle != null)
            portalRecallCircle.SetActive(false);

        for (int i = returningPortalSwords.Count - 1; i >= 0; i--)
        {
            BulletView sword = returningPortalSwords[i];
            if (!AdvanceBossRecallFlight(sword, home, PortalRecallSpeed, dt)) continue;
            sword.ResetTrail(false);
            sword.gameObject.SetActive(false);
            returningPortalSwords.RemoveAt(i);
            queuedPortalSwords.Enqueue(sword);
        }
        if (canRecall || returningPortalSwords.Count > 0) return;

        portalVolleyStarted = true;
        portalCountershotTimer -= dt;
        if (portalCountershotsRemaining > 0 && portalCountershotTimer > 0) return;
        if (portalCountershotsRemaining > 0)
        {
            BulletView sword = queuedPortalSwords.Count > 0 ? queuedPortalSwords.Dequeue() : GetPortalSword();
            FirePortalCountershot(sword, home);
            portalCountershotsRemaining--;
            if (portalCountershotsRemaining == 0)
            {
                owner.Health = Mathf.Max(1, owner.MaxHealth / 5);
                portalShield.enabled = false;
            }
            portalCountershotTimer += PortalCountershotInterval;
        }
        if (portalCountershotsRemaining > 0) return;
        portalEndTimeRemaining -= dt;
        if (portalEndTimeRemaining <= 0)
            EndSwordPortal();
    }

    BulletView FindNearestPortalSword(Vector2 from)
    {
        BulletView nearest = null;
        float distance = float.PositiveInfinity;
        foreach (BulletView sword in plantedPortalSwords)
        {
            float candidate = ((Vector2)sword.transform.position - from).sqrMagnitude;
            if (candidate >= distance) continue;
            nearest = sword;
            distance = candidate;
        }
        return nearest;
    }

    void FirePortalCountershot(BulletView sword, Vector2 origin)
    {
        Vector2 aim = player.Position - origin;
        Vector2 direction = aim.sqrMagnitude > .001f ? aim.normalized : Vector2.left;
        sword.transform.SetPositionAndRotation(origin, Quaternion.Euler(0, 0, Vector2.SignedAngle(Vector2.down, direction)));
        sword.gameObject.SetActive(true);
        sword.ResetTrail(true);
        bullets.Add(new EnemyBullet
        {
            View = sword, Previous = origin, Velocity = direction * PortalCountershotSpeed,
            IsPortalSword = true, IsPortalCountershot = true,
            Lifetime = Mathf.Max(1.25f, aim.magnitude / PortalCountershotSpeed + .25f)
        });
    }

    LineRenderer CreatePortalRing(string label, Vector2 radius, float width, Material material)
    {
        var ring = new GameObject(label).AddComponent<LineRenderer>();
        ring.transform.SetParent(transform, false);
        ring.useWorldSpace = false;
        ring.loop = true;
        ring.positionCount = 64;
        ring.widthMultiplier = width;
        ring.sharedMaterial = material;
        ring.sortingOrder = 14;
        ring.enabled = false;
        for (int i = 0; i < ring.positionCount; i++)
        {
            float angle = i * 2 * Mathf.PI / ring.positionCount;
            ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius.x, Mathf.Sin(angle) * radius.y));
        }
        return ring;
    }

    BulletView GetPortalSword()
    {
        BulletView sword = swordProjectilePool.Count > 0
            ? swordProjectilePool.Pop() : Instantiate(swordProjectilePrefab, transform);
        sword.transform.localScale = Vector3.one * 1.35f;
        return sword;
    }

    void LaunchPortalSword(Vector2 origin, Vector2 direction)
    {
        BulletView sword = GetPortalSword();
        sword.transform.SetPositionAndRotation(origin, Quaternion.Euler(0, 0, Vector2.SignedAngle(Vector2.down, direction)));
        sword.gameObject.SetActive(true);
        sword.ResetTrail(true);
        bullets.Add(new EnemyBullet
        {
            View = sword, Previous = origin, Velocity = direction * 14,
            IsPortalSword = true, Lifetime = PortalSwordLifetime
        });
    }

    void PlantPortalSword(int index)
    {
        BulletView sword = bullets[index].View;
        bullets.RemoveAt(index);
        sword.ResetTrail(false);
        plantedPortalSwords.Add(sword);
        sword.SetDroppedIndicator(true);
    }

    public void ReleasePortalSword(BulletView sword)
    {
        sword.gameObject.SetActive(false);
        swordProjectilePool.Push(sword);
        sword.SetDroppedIndicator(false);
    }

    void EndSwordPortal(bool reset = false)
    {
        swordPortalOwner = null;
        portalRecallTarget = null;
        if (portalRecallCircle != null) portalRecallCircle.SetActive(false);
        if (swordPortal != null) swordPortal.enabled = portalShield.enabled = false;
        if (reset)
        {
            HasPortalSwordSource = false;
            for (int i = bullets.Count - 1; i >= 0; i--)
                if (bullets[i].IsPortalSword) RemoveBullet(i);
            foreach (BulletView sword in plantedPortalSwords)
                ReleasePortalSword(sword);
            plantedPortalSwords.Clear();
        }
        foreach (BulletView sword in returningPortalSwords)
            ReleasePortalSword(sword);
        returningPortalSwords.Clear();
        while (queuedPortalSwords.Count > 0)
            ReleasePortalSword(queuedPortalSwords.Dequeue());
    }
}
