using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public sealed partial class SwordSystem : MonoBehaviour
{
    const float SwordGravity = 18f;
    const float OrbitRadius = 2.1f;
    const float GuardOrbitSpeed = 3.3f;
    const float MergeOrbitSpeed = 4.95f;
    const float FollowPathLength = 20f;
    const float FollowSampleSpacing = .025f;
    const float FollowGap = .12f;
    const float FollowSpeed = 5f;
    const float FollowSmoothTime = .42f;
    const float LaunchSpeed = 16f;
    const float AutoAttackRange = 10f;
    const float LaunchInterval = .12f;
    const float EnemyHitRadius = .46f;
    const float ReboundSpeed = 6f;
    const float ReboundSideSpeed = 1.5f;
    const float EnemyContactMargin = .02f;
    const float HitEffectTime = .22f;
    const float InterceptRange = 3f;
    const float InterceptSpeedMultiplier = 1.25f;
    const float InterceptLeadTime = .25f;
    const float InterceptRecovery = .22f;
    const float InterceptContact = .22f;
    const float ReturnSpeed = 12f;
    const float RecallRadius = 1f;
    const float CrossSlashRadius = 2f;
    const float BulletKnockbackSpeed = 3.5f;
    const float RecallDisplayTime = .45f;
    const float GuardRadius = 2.85f;
    const float MergeContactRadius = .24f;
    internal static readonly Color PlantedBlade = new Color32(135, 154, 171, 255);
    static readonly Color FusionBlade = new Color32(255, 209, 105, 255);
    static readonly Color ActiveBlade = new Color32(113, 248, 225, 255);
    [SerializeField] SwordView swordPrefab;
    [SerializeField] HitEffectView hitEffectPrefab;
    [SerializeField] SpriteRenderer recallArea;
    [SerializeField] SpriteRenderer guardArea;
    [SerializeField, Min(.1f)] float swordRecallCooldown = 3;
    readonly Stack<SwordView> swordPool = new();
    readonly List<SwordInstance> orbiting = new();
    readonly List<HitBurst> activeHits = new();
    readonly Stack<HitBurst> availableHits = new();
    readonly List<(Vector2 point, float distance)> followPath = new();
    StageGeometry stage;
    PlayerController player;
    EnemySystem enemySystem;
    EquipmentSystem equipment;
    CameraFollow cameraFollow;
    Camera view;
    EnemyState lockedEnemy;
    SwordInstance mergeSword;
    bool guarding, merging, followWasActive;
    Vector2 followHead, followVelocity, followDirection;
    float orbitAngle, recallTimer, launchCooldown;
    List<SwordInstance> swords => equipment.Instances;
    IReadOnlyList<EnemyBullet> bullets => enemySystem.Bullets;
    Vector2 RiderPosition => player.Position;
    Vector2 GridSize => stage.Size;
    float SwordSpeedMultiplier => equipment.SpeedMultiplier;
    float EffectiveReturnSpeed => ReturnSpeed * equipment.SpeedMultiplier * equipment.RecallMultiplier;
    bool IsPlayerDead => player.IsDead;
    public bool IsFollowingCursor { get; private set; }
    public bool HasReturningSword => swords.Exists(sword => sword.State == SwordState.Returning);
    public int ActiveSwordCount => orbiting.Count;
    public SwordInstance MergingSword => mergeSword;
    public EnemyState LockedEnemy => lockedEnemy != null && lockedEnemy.Health > 0 ? lockedEnemy : null;

    sealed class HitBurst
    {
        public HitEffectView View;
        public SpriteRenderer[] Sparks => View.sparks;
        public Vector2 Origin;
        public Color Color;
        public float Remaining;
    }

    public void Initialize(StageGeometry geometry, PlayerController rider, EnemySystem enemies,
        EquipmentSystem inventory, CameraFollow follow)
    {
        stage = geometry;
        player = rider;
        enemySystem = enemies;
        equipment = inventory;
        cameraFollow = follow;
        view = follow.View;
        theftMarkerPrefab = Resources.Load<GameObject>("Effects/EnemyTheftCircle");
    }

    public void ResetSwords(int? normalSwordCount = null, int orderSwordCount = 0)
    {
        ClearGiantWind();
        CancelLootClaim();
        ResetEnemyThefts();
        foreach (SwordInstance sword in swords)
            ReleaseSword(sword);
        swords.Clear();
        orbiting.Clear();
        if (mergeSword != null)
            ReleaseSword(mergeSword);
        mergeSword = null;
        UpdateHitBursts(HitEffectTime);
        lockedEnemy = null;
        orbitAngle = recallTimer = launchCooldown = 0;
        formationTime = 0;
        nextCurveSlot = 0;
        guarding = merging = followWasActive = IsFollowingCursor = false;
        recallArea.enabled = guardArea.enabled = false;
        followPath.Clear();
        followHead = followVelocity = Vector2.zero;
        followDirection = Vector2.up;
        int StartingCount(SwordDefinition definition) =>
            definition.kind == SwordKind.Normal && normalSwordCount.HasValue
                ? normalSwordCount.Value : definition.startingCount;

        foreach (SwordDefinition definition in equipment.Definitions)
        {
            int count = StartingCount(definition);
            for (int i = 0; i < count; i++)
            {
                SwordInstance sword = CreateSword(definition.displayName, definition.kind);
                swords.Add(sword);
                if (!definition.initiallyEquipped)
                {
                    sword.State = SwordState.Stored;
                    sword.Root.gameObject.SetActive(false);
                    continue;
                }
                orbiting.Add(sword);
            }
        }
        AddOrderSwords(orderSwordCount);
        AddStoryBossSword();
        foreach (SwordInstance sword in orbiting)
        {
            sword.Root.localPosition = RestPosition(sword);
            PointSword(sword, RestDirection);
        }
        equipment.NotifyChanged();
    }

    SwordInstance CreateSword(string label, SwordKind kind = SwordKind.Normal)
    {
        SwordView instance = swordPool.Count > 0 ? swordPool.Pop() : Instantiate(swordPrefab, transform);
        instance.transform.SetParent(transform, false);
        instance.name = label;
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localScale = Vector3.one;
        instance.transform.localRotation = Quaternion.identity;
        instance.visual.localPosition = Vector3.zero;
        instance.visual.localScale = Vector3.one;
        instance.visual.localRotation = Quaternion.identity;
        instance.blade.sprite = swordPrefab.blade.sprite;
        instance.blade.color = SwordKindColor(kind);
        instance.groundMark.enabled = false;
        instance.SetDroppedIndicator(false);
        instance.trail.emitting = false;
        instance.trail.Clear();
        instance.trail.time = .20f;
        instance.trail.startWidth = .065f;
        instance.trail.startColor = new Color(.38f, 1, .92f, .65f);
        instance.trail.endColor = new Color(.38f, 1, .92f, 0);
        instance.Refresh(0);
        instance.gameObject.SetActive(true);
        return new SwordInstance { View = instance, Kind = kind };
    }

    void ReleaseSword(SwordInstance sword)
    {
        if (sword.IsEnemyWeapon) CopySwordRenderers(swordPrefab.transform, sword.Root);
        sword.Trail.emitting = false;
        sword.Trail.Clear();
        sword.Root.gameObject.SetActive(false);
        swordPool.Push(sword.View);
    }

    void RemoveArrivedBullets()
    {
        for (int i = bullets.Count - 1; i >= 0; i--)
        {
            EnemyBullet bullet = bullets[i];
            if (DistanceToSegmentSquared(Vector2.zero, bullet.Previous - player.PreviousPosition,
                    (Vector2)bullet.Root.position - player.Position) > Mathf.Pow(.25f + bullet.ContactRadius, 2))
                continue;
            if (bullet.GiantChaseOwner == null)
            {
                enemySystem.RemoveBullet(i);
                if (!bullet.IsPracticeShot) player.TakeHit();
                continue;
            }
            int healthBeforeHit = player.Health;
            player.TakeHit();
            if (player.Health < healthBeforeHit) enemySystem.RepelGiantSwordAfterPlayerHit();
        }
    }

    void SpawnHitBurst(Vector2 position, Color color)
    {
        HitBurst burst;
        if (availableHits.Count > 0)
            burst = availableHits.Pop();
        else
            burst = new HitBurst { View = Instantiate(hitEffectPrefab, transform) };
        burst.View.gameObject.SetActive(true);
        burst.Origin = position;
        burst.Color = color;
        burst.Remaining = HitEffectTime;
        activeHits.Add(burst);
        DrawHitBurst(burst);
    }

    Color SwordKindColor(SwordKind kind) => equipment.Definition(kind).bladeColor;

    bool SwordMarksTarget(SwordInstance sword)
    {
        if (sword.FusionSize == 0)
            return equipment.Definition(sword.Kind).marksTarget;
        foreach (SwordKind kind in sword.FusedKinds)
            if (equipment.Definition(kind).marksTarget)
                return true;
        return false;
    }

    int SwordDamage(SwordInstance sword)
    {
        if (sword.FusionSize == 0)
            return equipment.Definition(sword.Kind).damage;
        int damage = 0;
        foreach (SwordKind kind in sword.FusedKinds)
            damage += equipment.Definition(kind).damage;
        return damage;
    }

    static float SwordScale(SwordInstance sword) => 1 + .22f * sword.FusionSize;
    static Vector2 OrbitDirection(float angle) => new(Mathf.Cos(angle), Mathf.Sin(angle));
    bool IsPlantedInRange(SwordInstance sword, Vector2 center, float radius) =>
        sword.State == SwordState.Planted &&
        ((Vector2)sword.Root.localPosition - center).sqrMagnitude <= radius * radius;

    public void Tick(float dt)
    {
        if (view == null || Time.deltaTime <= 0 || IsPlayerDead)
            return;

        if (!enemySystem.HasPortalSwordSource)
            foreach (SwordInstance sword in swords)
                if (sword.CapturedPortalSword) ReturnBorrowedSword(sword);
        RemoveSpentBorrowedSwords();
        ReleaseCapturedSwordsFromDefeatedOwners();
        recallTimer -= dt;
        recallArea.enabled = recallTimer > 0;

        foreach (SwordInstance sword in orbiting)
            sword.PreviousPosition = sword.Root.localPosition;

        Vector2 playerTravel = player.Position - player.PreviousPosition;
        if (guarding || merging || IsFollowingCursor)
            foreach (SwordInstance sword in orbiting)
                sword.Root.localPosition += (Vector3)playerTravel;
        if (mergeSword != null)
            mergeSword.Root.localPosition += (Vector3)playerTravel;

        guardArea.transform.localPosition = RiderPosition;
        guardArea.enabled = guarding;
        launchCooldown = Mathf.Max(0, launchCooldown - dt);
        var mouse = Mouse.current;
        Vector2 cursor = mouse == null ? followHead :
            (Vector2)transform.InverseTransformPoint(view.ScreenToWorldPoint(mouse.position.ReadValue()));
        bool pointerAvailable = EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject();
        bool recallActive = mouse != null && pointerAvailable;
        if (recallActive)
            ShowSkillArea(cursor, RecallRadius, new Color32(96, 237, 255, 230), duration: 0);
        bool claimingLoot = UpdateLootClaim(cursor, recallActive, dt);
        if (recallActive && !claimingLoot)
        {
            CommandRecoverableSwords(cursor);
            StealEnemySword(cursor);
        }

        Vector2? attackTarget = FindAutoAttackTarget(out EnemyBullet projectile, out EnemyState targetEnemy);
        UpdateFormationMotion(dt);
        if (launchCooldown <= 0 && orbiting.Count > 0 &&
            (TryLaunchCapturedGiant() || (attackTarget.HasValue && LaunchSword(attackTarget.Value, projectile, targetEnemy))))
            launchCooldown = LaunchInterval / SwordSpeedMultiplier;

        UpdateHitBursts(dt);
        UpdateGiantWind(dt);
        int swordsAtFrameStart = swords.Count;
        for (int i = 0; i < swordsAtFrameStart; i++)
        {
            SwordInstance sword = swords[i];
            if (sword.CapturedPortalSword && !enemySystem.HasPortalSwordSource)
                ReturnBorrowedSword(sword);
            if (sword.State == SwordState.Preparing)
                AdvancePreparation(sword, dt);
            else if (sword.State == SwordState.OrderAttack)
                AdvanceOrderSword(sword, dt);
            else if (sword.State == SwordState.Flying)
                AdvanceFlight(sword, dt);
            else if (sword.State == SwordState.Falling)
                AdvanceFall(sword, dt);
            else if (sword.State == SwordState.Returning)
                AdvanceReturn(sword, dt);
            else if (sword.State == SwordState.Planted)
                AdvancePlanted(sword, dt);
            sword.View.Refresh(sword.FusionSize);
            sword.View.SetDroppedIndicator(sword.State == SwordState.Falling || sword.State == SwordState.Planted);
        }

        if (IsFollowingCursor && !guarding && !merging)
        {
            if (!followWasActive)
                StartFollowPath(cursor);
            FollowCursor(ClampTarget(cursor), dt, entering: !followWasActive);
            followWasActive = true;
            InterceptWithFormation();
            TickEnemyThefts(dt);
            RemoveArrivedBullets();
            return;
        }

        if (followWasActive)
        {
            foreach (SwordInstance sword in orbiting)
                sword.Trail.emitting = false;
            followWasActive = false;
        }

        float rotationSpeed = merging ? MergeOrbitSpeed : GuardOrbitSpeed;
        orbitAngle = Mathf.Repeat(orbitAngle + rotationSpeed * SwordSpeedMultiplier * dt, 2 * Mathf.PI);
        for (int i = 0; i < orbiting.Count; i++)
        {
            SwordInstance sword = orbiting[i];
            if (!guarding && !merging)
            {
                AdvanceRestingSword(sword, dt);
                continue;
            }
            float offset = merging ? sword.MergeOrbitOffset : 2 * Mathf.PI * i / orbiting.Count;
            float targetAngle = (orbitAngle + offset) * Mathf.Rad2Deg;
            Vector2 previous = sword.Root.localPosition;
            Vector2 offsetFromRider = previous - RiderPosition;
            float previousAngle = Mathf.Atan2(offsetFromRider.y, offsetFromRider.x) * Mathf.Rad2Deg;
            float angle = Mathf.MoveTowardsAngle(previousAngle, targetAngle, 360f * SwordSpeedMultiplier * dt);
            Vector2 outward = OrbitDirection(angle * Mathf.Deg2Rad);
            float radius = Mathf.MoveTowards(offsetFromRider.magnitude, guarding ? GuardRadius : OrbitRadius, EffectiveReturnSpeed * dt);
            Vector2 next = RiderPosition + outward * radius;
            sword.Root.localPosition = next;
            SetFusionSize(sword, orbitVisual: true);
            PointSword(sword, outward);

            if (merging && !sword.IsEnemyWeapon && TouchesMergeSword(previous, next))
            {
                orbiting.RemoveAt(i--);
                swords.Remove(sword);
                ReleaseSword(sword);
                if (sword.FusionSize > 0)
                    mergeSword.FusedKinds.AddRange(sword.FusedKinds);
                else
                    mergeSword.FusedKinds.Add(sword.Kind);
                mergeSword.FusionSize = mergeSword.FusedKinds.Count;
                equipment.NotifyChanged();
                mergeSword.View.Refresh(mergeSword.FusionSize);
                SetFusionSize(mergeSword, orbitVisual: true);
                cameraFollow.Shake();
            }
        }
        if (guarding)
            InterceptWithFormation();
        TickEnemyThefts(dt);
        RemoveArrivedBullets();
    }

    void InterceptWithFormation()
    {
        for (int i = orbiting.Count - 1; i >= 0; i--)
        {
            SwordInstance sword = orbiting[i];
            Vector2 position = sword.Root.localPosition;
            Vector2 bladeBase = transform.InverseTransformPoint(sword.Visual.TransformPoint(new Vector3(0, .46f)));
            Vector2 bladeTip = transform.InverseTransformPoint(sword.Visual.position);
            for (int j = bullets.Count - 1; j >= 0; j--)
            {
                EnemyBullet bullet = bullets[j];
                if (!IsActiveProjectile(bullet) || bullet == sword.LastGiantIntercept) continue;
                Vector2 bulletPosition = bullet.Root.localPosition;
                bool crossed = DistanceToSegmentSquared(Vector2.zero,
                    sword.PreviousPosition - bullet.Previous, position - bulletPosition)
                    <= Mathf.Pow(InterceptContact + bullet.ContactRadius, 2);
                bool touchesBlade = DistanceToSegmentSquared(bulletPosition, bladeTip, bladeBase)
                    <= Mathf.Pow(InterceptContact + bullet.ContactRadius, 2);
                if (!crossed && !touchesBlade)
                    continue;

                Vector2 knockback = BulletKnockback(bullet);
                InterceptBullet(j, sword);
                sword.BlocksRemaining--;
                if (sword.BlocksRemaining <= 0)
                {
                    orbiting.RemoveAt(i);
                    BeginFall(sword);
                    sword.FallVelocity = knockback;
                    break;
                }
                sword.Root.localPosition += (Vector3)(knockback * .04f);
            }
        }
    }

    static float DistanceToSegmentSquared(Vector2 point, Vector2 from, Vector2 to)
    {
        Vector2 travel = to - from;
        float progress = travel.sqrMagnitude > 0
            ? Mathf.Clamp01(Vector2.Dot(point - from, travel) / travel.sqrMagnitude) : 0;
        return (point - (from + travel * progress)).sqrMagnitude;
    }

    Vector2? FindAutoAttackTarget(out EnemyBullet projectile, out EnemyState targetEnemy)
    {
        Vector2 origin = player.Position;
        Vector2? target = null;
        float nearest = AutoAttackRange * AutoAttackRange;
        projectile = null;
        targetEnemy = null;
        EnemyBullet giant = enemySystem.ChasingGiantSword;
        if (giant != null && IsActiveProjectile(giant))
        {
            Vector2 position = giant.Root.position;
            if ((position - origin).sqrMagnitude > nearest || stage.ClipTerrain(origin, position, Vector2.zero, out _))
                return null;

            projectile = giant;
            return transform.InverseTransformPoint(position);
        }

        foreach (EnemyBullet bullet in bullets)
        {
            if (!IsActiveProjectile(bullet))
                continue;

            Vector2 position = bullet.Root.position;
            float distance = (position - origin).sqrMagnitude;
            if (distance > nearest || stage.ClipTerrain(origin, position, Vector2.zero, out _))
                continue;

            projectile = bullet;
            target = transform.InverseTransformPoint(position);
            nearest = distance;
        }

        if (target.HasValue)
            return target;

        foreach (EnemyState enemy in enemySystem.Enemies)
        {
            if (enemy.Health <= 0 || enemySystem.IsInvulnerable(enemy))
                continue;

            Vector2 position = enemy.Position;
            float distance = (position - origin).sqrMagnitude;
            if (distance > nearest || stage.ClipTerrain(origin, position, Vector2.zero, out _))
                continue;

            targetEnemy = enemy;
            target = transform.InverseTransformPoint(position);
            nearest = distance;
        }

        return target;
    }

    static bool IsActiveProjectile(EnemyBullet bullet)
    {
        if (bullet.Spent || bullet.ReachedTarget || (bullet.Owner != null && bullet.State != BossSwordState.Flying))
            return false;

        Transform root = bullet.PlayerSword != null ? bullet.PlayerSword.Root :
            bullet.View != null ? bullet.View.transform : null;
        return root != null && root.gameObject.activeInHierarchy;
    }

    bool TryLaunchCapturedGiant()
    {
        EnemyState boss = enemySystem.GiantSwordBoss;
        if (boss == null || boss.Health <= 0 || enemySystem.IsInvulnerable(boss) ||
            (boss.Position - player.Position).sqrMagnitude > AutoAttackRange * AutoAttackRange)
            return false;

        SwordInstance sword = orbiting.Find(candidate => candidate.CapturedGiantSword);
        if (sword == null || stage.ClipTerrain(sword.Root.position, boss.Position, Vector2.zero, out _))
            return false;

        orbiting.Remove(sword);
        PrepareGiantLaunch(sword, transform.InverseTransformPoint(boss.Position));
        return true;
    }

    bool LaunchSword(Vector2 point, EnemyBullet projectile, EnemyState targetEnemy)
    {
        Vector2 target = ClampTarget(point);
        SwordInstance sword = null;
        float nearest = float.PositiveInfinity;
        foreach (SwordInstance candidate in orbiting)
        {
            if (candidate.CapturedGiantSword || (sword != null && sword.IsEnemyWeapon && !candidate.IsEnemyWeapon))
                continue;

            float distance = ((Vector2)candidate.Root.localPosition - target).sqrMagnitude;
            bool preferred = candidate.IsEnemyWeapon && (sword == null || !sword.IsEnemyWeapon);
            if (!preferred && (distance > nearest || (distance == nearest && candidate.FusionSize <= sword.FusionSize)))
                continue;

            sword = candidate;
            nearest = distance;
        }

        if (sword == null) return false;
        orbiting.Remove(sword);
        PrepareLaunch(sword, target);
        sword.TargetProjectile = projectile;
        sword.LaunchEnemy = targetEnemy;
        return true;
    }

    Vector2 ClampTarget(Vector2 point)
    {
        Vector2 edge = GridSize * .5f - Vector2.one * .65f;
        return new Vector2(Mathf.Clamp(point.x, -edge.x, edge.x), Mathf.Clamp(point.y, -edge.y, edge.y));
    }

    void CrossSlash(Vector2 cursor)
    {
        ShowSkillArea(cursor, CrossSlashRadius, new Color32(255, 137, 95, 230));
        Vector2 aim = ClampTarget(cursor);
        foreach (SwordInstance sword in swords)
        {
            if (sword.BorrowedUsed || !IsPlantedInRange(sword, cursor, CrossSlashRadius))
                continue;

            BeginFlight(sword, ClampTarget(2 * aim - (Vector2)sword.Root.localPosition));
            sword.Blade.color = sword.IsEnemyWeapon ? sword.CapturedBladeColor : sword.FusionSize > 0 ? FusionBlade : SwordKindColor(sword.Kind);
            sword.GroundMark.enabled = false;
        }
    }

    void ShowSkillArea(Vector2 cursor, float radius, Color color, float duration = RecallDisplayTime)
    {
        recallArea.color = color;
        recallArea.transform.localScale = Vector2.one * (2 * radius);
        recallArea.transform.localPosition = cursor;
        recallArea.enabled = true;
        recallTimer = duration;
    }

    void CommandRecoverableSwords(Vector2 cursor)
    {
        foreach (SwordInstance sword in swords)
        {
            if ((sword.BorrowedFrom != null && !sword.PermanentLoot) ||
                (sword.State != SwordState.Falling && sword.State != SwordState.Planted && sword.State != SwordState.EnemyHeld) ||
                ((Vector2)sword.Root.localPosition - cursor).sqrMagnitude > RecallRadius * RecallRadius)
                continue;

            if (sword.IsOrderSword)
                StartOrderAttack(sword);
            else
                BeginRecall(sword);
        }
    }

    void StealEnemySword(Vector2 cursor)
    {
        Vector2 worldCursor = transform.TransformPoint(cursor);
        if (!enemySystem.TryStealSword(worldCursor, RecallRadius, out EnemyState owner, out BulletView captured, out bool giant, out EnemyBullet stolenBasicSword))
            return;
        SwordInstance sword = CreateSword("Captured sword");
        sword.BorrowedFrom = owner;
        sword.StolenBasicSword = stolenBasicSword;
        sword.CapturedGiantSword = giant;
        sword.CapturedPortalSword = owner == null && !giant;
        CopyCapturedAppearance(sword, captured);
        ApplyOwnedSwordColors(sword);
        sword.Root.position = captured.transform.position;
        if (giant)
        {
            sword.BlocksRemaining = 5;
            captured.gameObject.SetActive(false);
        }
        else if (sword.CapturedPortalSword) enemySystem.ReleasePortalSword(captured);
        swords.Add(sword);
        BeginRecall(sword);
        equipment.NotifyChanged();
    }

    void RemoveSpentBorrowedSwords()
    {
        bool changed = false;
        for (int i = swords.Count - 1; i >= 0; i--)
        {
            if (!swords[i].PendingRemoval) continue;
            orbiting.Remove(swords[i]);
            ReleaseSword(swords[i]);
            swords.RemoveAt(i);
            changed = true;
        }
        if (changed) equipment.NotifyChanged();
    }

    void BeginRecall(SwordInstance sword)
    {
        if ((sword.CapturedGiantSword || sword.BorrowedFrom != null) && sword.BorrowedUsed)
        {
            ReturnBorrowedSword(sword);
            return;
        }
        bool wasEnemyHeld = sword.State == SwordState.EnemyHeld;
        sword.LastGiantIntercept = null;
        sword.State = SwordState.Returning;
        sword.ReturnPhase = 0;
        sword.ReturnSettleTime = 0;
        sword.RestVelocity = Vector2.zero;
        Vector2 home = RestPosition(sword);
        sword.ReturnOvershoot = (home-(Vector2)sword.Root.localPosition).normalized*.22f;
        sword.Trail.Clear();
        sword.Trail.time = .18f;
        sword.Trail.startWidth = .06f;
        ApplyOwnedSwordColors(sword);
        sword.Trail.emitting = true;
        sword.CurveEnemy = null;
        sword.Blade.color = ReadyBladeColor(sword);
        sword.GroundMark.enabled = false;
        if (wasEnemyHeld) equipment.NotifyChanged();
    }

    void UpdateHitBursts(float dt)
    {
        for (int i = activeHits.Count - 1; i >= 0; i--)
        {
            HitBurst burst = activeHits[i];
            burst.Remaining = Mathf.Max(0, burst.Remaining - dt);
            DrawHitBurst(burst);
            if (burst.Remaining <= 0)
            {
                activeHits.RemoveAt(i);
                burst.View.gameObject.SetActive(false);
                availableHits.Push(burst);
            }
        }
    }

    static void DrawHitBurst(HitBurst burst)
    {
        float remaining = burst.Remaining / HitEffectTime;
        Color color = burst.Color;
        color.a = remaining;
        for (int i = 0; i < burst.Sparks.Length; i++)
        {
            SpriteRenderer spark = burst.Sparks[i];
            spark.enabled = remaining > 0;
            if (!spark.enabled)
                continue;

            Vector2 direction = OrbitDirection(i * 2 * Mathf.PI / burst.Sparks.Length);
            spark.transform.localPosition = burst.Origin + direction * (.1f + .7f * (1 - remaining));
            spark.color = color;
        }
    }

    List<SwordInstance> SeparateSword(SwordInstance sword)
    {
        if (sword.IsEnemyWeapon) return new List<SwordInstance> { sword };
        SwordKind[] kinds = sword.FusionSize > 0 ? sword.FusedKinds.ToArray() : new[] { sword.Kind };
        var separated = new List<SwordInstance>(kinds.Length);
        for (int i = 0; i < kinds.Length; i++)
        {
            SwordInstance part = i == 0 ? sword : CreateSword("Split sword", kinds[i]);
            if (i > 0)
                swords.Add(part);
            part.Kind = kinds[i];
            part.FusionSize = 0;
            part.FusedKinds.Clear();
            part.BlocksRemaining = 1;
            part.Visual.localScale = Vector3.one;
            part.Visual.localPosition = Vector3.zero;
            part.Blade.color = SwordKindColor(part.Kind);
            part.CurveEnemy = null;
            part.Trail.emitting = false;
            part.Trail.time = .22f;
            part.Trail.startWidth = .085f;
            part.Trail.startColor = new Color(.38f, 1, .92f, .65f);
            part.Trail.endColor = new Color(.38f, 1, .92f, 0);
            part.Root.localPosition = sword.Root.localPosition;
            separated.Add(part);
        }
        equipment.NotifyChanged();
        return separated;
    }

    public void PrepareInventory()
    {
        if (mergeSword != null)
            FinishMerge();
        merging = false;
        int count = swords.Count;
        for (int i = 0; i < count; i++)
        {
            SwordInstance sword = swords[i];
            if (sword.FusionSize == 0)
                continue;
            orbiting.Remove(sword);
            foreach (SwordInstance part in SeparateSword(sword))
                BeginRecall(part);
        }
        equipment.NotifyChanged();
    }

    internal void SetSwordEquipped(SwordInstance sword, bool equipped, bool notify = true)
    {
        if (!swords.Contains(sword) || (sword.State != SwordState.Stored) == equipped)
            return;
        sword.Trail.emitting = false;
        sword.Trail.Clear();
        if (!equipped)
        {
            orbiting.Remove(sword);
            sword.State = SwordState.Stored;
            sword.Root.gameObject.SetActive(false);
            if (notify)
                equipment.NotifyChanged();
            return;
        }

        sword.Root.gameObject.SetActive(true);
        sword.Root.localPosition = RiderPosition + Vector2.up * OrbitRadius;
        sword.PreviousPosition = sword.Root.localPosition;
        sword.BlocksRemaining = 1;
        sword.GroundMark.enabled = false;
        BeginRecall(sword);
        if (notify) equipment.NotifyChanged();
    }

    static void PointSword(SwordInstance sword, Vector2 direction)
    {
        if (direction.sqrMagnitude > 0)
            sword.Root.localRotation = Quaternion.Euler(0, 0, Vector2.SignedAngle(Vector2.down, direction));
    }

}
