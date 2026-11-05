using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public sealed partial class SwordSystem : MonoBehaviour
{
    const float SwordGravity = 18f;
    const float OrbitRadius = 2.1f;
    const float OrbitSpeed = 1.65f;
    const float GuardOrbitSpeed = 3.3f;
    const float MergeOrbitSpeed = 4.95f;
    const float FollowPathLength = 20f;
    const float FollowSampleSpacing = .025f;
    const float FollowGap = .12f;
    const float FollowSpeed = 5f;
    const float FollowSmoothTime = .42f;
    const float LaunchSpeed = 16f;
    const float LaunchInterval = .12f;
    const float EnemyHitRadius = .46f;
    const float ReboundSpeed = 6f;
    const float ReboundSideSpeed = 1.5f;
    const float EnemyContactMargin = .02f;
    const float HitEffectTime = .22f;
    const float InterceptRange = 1.8f;
    const float InterceptRecovery = .22f;
    const float InterceptContact = .22f;
    const float ReturnSpeed = 12f;
    const float RecallRadius = 1f;
    const float CrossSlashRadius = 2f;
    const float BulletKnockbackSpeed = 3.5f;
    const float RecallDisplayTime = .45f;
    const float GuardRadius = 2.85f;
    const float MergeContactRadius = .24f;
    static readonly Color PlantedBlade = new Color32(135, 154, 171, 255);
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
    }

    public void ResetSwords()
    {
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
        guarding = merging = followWasActive = IsFollowingCursor = false;
        recallArea.enabled = guardArea.enabled = false;
        followPath.Clear();
        followHead = followVelocity = Vector2.zero;
        followDirection = Vector2.up;
        int initialCount = 0;
        foreach (SwordDefinition definition in equipment.Definitions)
            if (definition.initiallyEquipped)
                initialCount += definition.startingCount;
        int index = 0;
        foreach (SwordDefinition definition in equipment.Definitions)
        {
            for (int i = 0; i < definition.startingCount; i++)
            {
                SwordInstance sword = CreateSword(definition.displayName, definition.kind);
                swords.Add(sword);
                if (!definition.initiallyEquipped)
                {
                    sword.State = SwordState.Stored;
                    sword.Root.gameObject.SetActive(false);
                    continue;
                }
                Vector2 outward = OrbitDirection(2 * Mathf.PI * index++ / Mathf.Max(1, initialCount));
                sword.Root.localPosition = RiderPosition + outward * OrbitRadius;
                PointSword(sword, outward);
                orbiting.Add(sword);
            }
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
        instance.blade.color = SwordKindColor(kind);
        instance.groundMark.enabled = false;
        instance.trail.emitting = false;
        instance.trail.Clear();
        instance.trail.time = .22f;
        instance.trail.startWidth = .085f;
        instance.trail.startColor = new Color(.38f, 1, .92f, .65f);
        instance.trail.endColor = new Color(.38f, 1, .92f, 0);
        instance.Refresh(0);
        instance.fusionCount.font = GameFonts.Body;
        instance.gameObject.SetActive(true);
        return new SwordInstance { View = instance, Kind = kind };
    }

    void ReleaseSword(SwordInstance sword)
    {
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
                    (Vector2)bullet.Root.position - player.Position) > .25f * .25f)
                continue;
            enemySystem.RemoveBullet(i);
            player.TakeHit();
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
    static Vector2 ReturnTarget(Vector2 position) =>
        position.sqrMagnitude > 0 ? position.normalized * OrbitRadius : Vector2.right * OrbitRadius;
    static Vector2 OrbitDirection(float angle) => new(Mathf.Cos(angle), Mathf.Sin(angle));
    bool IsPlantedInRange(SwordInstance sword, Vector2 center, float radius) =>
        sword.State == SwordState.Planted &&
        ((Vector2)sword.Root.localPosition - center).sqrMagnitude <= radius * radius;

    public void Tick(float dt)
    {
        if (view == null || Time.deltaTime <= 0 || IsPlayerDead)
            return;

        var keyboard = Keyboard.current;
        recallTimer -= dt;
        recallArea.enabled = recallTimer > 0;

        if (keyboard?.digit5Key.wasPressedThisFrame == true)
        {
            IsFollowingCursor = !IsFollowingCursor;
            followHead = Vector2.zero;
            followVelocity = Vector2.zero;
        }

        foreach (SwordInstance sword in orbiting)
            sword.PreviousPosition = sword.Root.localPosition;

        Vector2 playerTravel = player.Position - player.PreviousPosition;
        foreach (SwordInstance sword in orbiting)
            sword.Root.localPosition += (Vector3)playerTravel;
        if (mergeSword != null)
            mergeSword.Root.localPosition += (Vector3)playerTravel;

        bool mergeHeld = keyboard != null && (keyboard.digit1Key.isPressed || keyboard.digit1Key.wasPressedThisFrame);
        if (mergeHeld && !merging)
            BeginMerge();
        if (!mergeHeld && merging)
            FinishMerge();
        merging = mergeHeld && mergeSword != null;
        guarding = !merging && keyboard != null && keyboard.digit4Key.isPressed && orbiting.Count > 0;
        guardArea.transform.localPosition = RiderPosition;
        guardArea.enabled = guarding;
        launchCooldown = Mathf.Max(0, launchCooldown - dt);
        var mouse = Mouse.current;
        Vector2 cursor = mouse == null ? followHead :
            (Vector2)transform.InverseTransformPoint(view.ScreenToWorldPoint(mouse.position.ReadValue()));
        if (mouse != null && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
        {
            bool launch = mouse.leftButton.wasPressedThisFrame ||
                (mouse.leftButton.isPressed && launchCooldown <= 0);
            bool recall = mouse.rightButton.wasPressedThisFrame || mouse.rightButton.isPressed;
            bool crossSlash = keyboard?.digit3Key.wasPressedThisFrame == true;
            if (launch || recall || crossSlash)
            {
                if (launch && orbiting.Count > 0)
                {
                    LaunchSword(cursor);
                    launchCooldown = LaunchInterval / SwordSpeedMultiplier;
                }
                if (crossSlash)
                    CrossSlash(cursor);
                if (recall)
                    RecallSwords(cursor);
            }
        }

        UpdateHitBursts(dt);
        int swordsAtFrameStart = swords.Count;
        for (int i = 0; i < swordsAtFrameStart; i++)
        {
            SwordInstance sword = swords[i];
            if (sword.State == SwordState.Flying)
                AdvanceFlight(sword, dt);
            else if (sword.State == SwordState.Falling)
                AdvanceFall(sword, dt);
            else if (sword.State == SwordState.Returning)
                AdvanceReturn(sword, dt);
            else if (sword.State == SwordState.Planted)
                AdvancePlanted(sword, dt);
            sword.View.Refresh(sword.FusionSize);
        }

        if (IsFollowingCursor && !guarding && !merging)
        {
            if (!followWasActive)
                StartFollowPath(cursor);
            FollowCursor(ClampTarget(cursor), dt, entering: !followWasActive);
            followWasActive = true;
            InterceptWithFormation();
            RemoveArrivedBullets();
            return;
        }

        if (followWasActive)
        {
            foreach (SwordInstance sword in orbiting)
                sword.Trail.emitting = false;
            followWasActive = false;
        }

        float rotationSpeed = merging ? MergeOrbitSpeed : guarding ? GuardOrbitSpeed : OrbitSpeed;
        orbitAngle = Mathf.Repeat(orbitAngle + rotationSpeed * SwordSpeedMultiplier * dt, 2 * Mathf.PI);
        for (int i = 0; i < orbiting.Count; i++)
        {
            SwordInstance sword = orbiting[i];
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

            if (merging && TouchesMergeSword(previous, next))
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
                Vector2 bulletPosition = bullet.Root.localPosition;
                bool crossed = DistanceToSegmentSquared(Vector2.zero,
                    sword.PreviousPosition - bullet.Previous, position - bulletPosition)
                    <= InterceptContact * InterceptContact;
                bool touchesBlade = DistanceToSegmentSquared(bulletPosition, bladeTip, bladeBase)
                    <= InterceptContact * InterceptContact;
                if (!crossed && !touchesBlade)
                    continue;

                Vector2 knockback = BulletKnockback(bullet);
                InterceptBullet(j);
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

    void LaunchSword(Vector2 cursor)
    {
        Vector2 target = ClampTarget(cursor);
        SwordInstance sword = orbiting[0];
        float nearest = ((Vector2)sword.Root.localPosition - target).sqrMagnitude;
        foreach (SwordInstance candidate in orbiting)
        {
            float distance = ((Vector2)candidate.Root.localPosition - target).sqrMagnitude;
            if (distance > nearest || (distance == nearest && candidate.FusionSize <= sword.FusionSize))
                continue;

            sword = candidate;
            nearest = distance;
        }

        orbiting.Remove(sword);
        BeginFlight(sword, target);
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
            if (!IsPlantedInRange(sword, cursor, CrossSlashRadius))
                continue;

            BeginFlight(sword, ClampTarget(2 * aim - (Vector2)sword.Root.localPosition));
            sword.Blade.color = sword.FusionSize > 0 ? FusionBlade : SwordKindColor(sword.Kind);
            sword.GroundMark.enabled = false;
        }
    }

    void ShowSkillArea(Vector2 cursor, float radius, Color color)
    {
        recallArea.color = color;
        recallArea.transform.localScale = Vector2.one * (2 * radius);
        recallArea.transform.localPosition = cursor;
        recallArea.enabled = true;
        recallTimer = RecallDisplayTime;
    }

    void RecallSwords(Vector2 cursor)
    {
        ShowSkillArea(cursor, RecallRadius, new Color32(96, 237, 255, 230));
        foreach (SwordInstance sword in swords)
        {
            if (!IsPlantedInRange(sword, cursor, RecallRadius))
                continue;

            BeginRecall(sword);
        }
    }

    void BeginRecall(SwordInstance sword)
    {
        sword.State = SwordState.Returning;
        sword.Trail.emitting = false;
        sword.CurveEnemy = null;
        sword.Blade.color = sword.FusionSize > 0 ? FusionBlade : SwordKindColor(sword.Kind);
        sword.GroundMark.enabled = false;
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
