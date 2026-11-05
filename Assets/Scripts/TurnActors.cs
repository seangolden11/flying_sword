using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed partial class TurnActors : MonoBehaviour
{
    const float CellSize = .8f;
    const int Cells = 125;
    [SerializeField, Min(1)] int initialSwordCount = 3;
    public int InitialSwordCount => Mathf.Max(1, initialSwordCount);
    [SerializeField, Min(0)] float launchOvershoot = .8f;
    [Header("Weak enemy homing")]
    [SerializeField, Min(0)] float enemyHomingRange = 2f;
    [SerializeField, Range(0, 45)] float enemyHomingHalfAngle = 20f;
    [SerializeField, Range(0, 180)] float enemyHomingTurnSpeed = 60f;
    const float OrbitRadius = 2.1f;
    const float OrbitSpeed = 1.65f;
    const float GuardOrbitSpeed = 3.3f;
    const float FollowPathLength = 20f;
    const float FollowSampleSpacing = .025f;
    const float FollowGap = .12f;
    const float FollowSpeed = 5f;
    const float FollowSmoothTime = .42f;
    const float LaunchSpeed = 16f;
    const float LaunchInterval = .12f;
    const float EnemyHitRadius = .46f;
    const float BladeLength = .46f;
    const float BladeHalfWidth = .075f;
    const float ReboundSpeed = 8.5f;
    const float ReboundMinDistance = 2.8f;
    const float ReboundMaxDistance = 4.8f;
    const float HitEffectTime = .22f;
    const float InterceptRange = 1.8f;
    const float InterceptContact = .22f;
    const float InterceptRecoil = .13f;
    const float InterceptRecovery = .22f;
    const float ReturnSpeed = 12f;
    const float AutoRecallDelay = 5f;
    const float RecallRadius = 2f;
    const float RecallDisplayTime = .45f;
    const float GuardRadius = 2.85f;
    const float MergeContactRadius = .24f;
    const float ShakeDuration = .12f;

    static readonly Color PlantedBlade = new Color32(135, 154, 171, 255);
    static readonly Color FusionBlade = new Color32(255, 209, 105, 255);
    static readonly Color ActiveBlade = new Color32(113, 248, 225, 255);
    public static Vector2 GridSize => Vector2.one * (Cells * CellSize);
    public int ActiveSwordCount => orbiting.Count;

    enum SwordState { Orbiting, Flying, Rebounding, Planted, Intercepting, Returning }

    sealed class Sword
    {
        public int Id;
        public int DamageBonus;
        public WeaponKind Kind;
        public readonly List<Sword> Components = new();
        public readonly HashSet<Enemy> FlightHits = new();
        public readonly Dictionary<Enemy, float> GuardHits = new();
        public bool ChainUsed, IsChainFlight;
        public Enemy ChainTarget;
        public float ChainRemaining;
        public Transform Root;
        public Transform Visual;
        public TrailRenderer Trail;
        public SpriteRenderer Blade;
        public SpriteRenderer GroundMark;
        public Vector2 Target;
        public Vector2 PreviousPosition;
        public SwordState State;
        public float MergeOrbitOffset;
        public int FusionSize;
        public int InterceptsRemaining;
        public Bullet InterceptTarget;
        public float Recovery;
        public Vector2 FlightDirection;
        public Vector2 LaunchDirection;
        public float FlightDistanceRemaining;
        public readonly HashSet<Enemy> ReturnHits = new();
        public float AutoRecallRemaining;
    }

    sealed class Bullet
    {
        public Transform Root;
        public Vector2 Previous;
        public bool Reserved;
    }

    readonly List<Bullet> bullets = new();
    sealed class HitBurst
    {
        public readonly SpriteRenderer[] Sparks = new SpriteRenderer[8];
        public Vector2 Origin;
        public Color Color;
        public float Remaining;
    }

    readonly List<HitBurst> activeHits = new();
    readonly Stack<HitBurst> availableHits = new();
    readonly List<Sword> swords = new();
    int nextWeaponId;
    readonly List<Sword> orbiting = new();
    Sprite square;
    Sprite bladeSprite;
    SpriteRenderer recallArea, guardArea;
    bool guarding;
    public bool IsAutoInterceptOn { get; private set; }
    public bool IsFollowingCursor { get; private set; }
    Vector2 followHead;
    Vector2 followVelocity;
    Vector2 followDirection;
    readonly List<(Vector2 point, float distance)> followPath = new();
    Material trailMaterial;
    bool followWasActive;
    bool merging;
    Sword mergeSword;

    float shakeTimer;
    Vector3 cameraRestPosition;
    Camera view;
    float orbitAngle;
    float recallTimer;
    float launchCooldown;

    public void Initialize(Sprite sprite, Camera camera, Material swordTrailMaterial)
    {
        square = sprite;
        trailMaterial = swordTrailMaterial;
        view = camera;
        cameraRestPosition = view.transform.position;
        bladeSprite = MakeBladeSprite();
        DrawGrid();
        var hero = StageManager.Draw(transform, "Hero", square, new Color32(246, 246, 251, 255),
            Vector2.zero, Vector2.one * .46f, 4);
        hero.transform.localRotation = Quaternion.Euler(0, 0, 45);
        heroRenderer = hero;
        recallArea = StageManager.Draw(transform, "Recall range", MakeRecallSprite(),
            new Color32(96, 237, 255, 230), Vector2.zero, Vector2.one * (2 * RecallRadius), 2);
        recallArea.enabled = false;
        guardArea = StageManager.Draw(transform, "Sword guard", recallArea.sprite,
            new Color32(114, 183, 255, 180), Vector2.zero, Vector2.one * (2 * GuardRadius), 2);
        guardArea.enabled = false;
    }

    public void ResetArena()
    {
        ResetSurvival();
        foreach (Sword sword in swords)
        {
            DestroyWeaponObjects(sword);
        }

        foreach (Bullet bullet in bullets)
        {
            bullet.Root.gameObject.SetActive(false);
            Destroy(bullet.Root.gameObject);
        }
        bullets.Clear();
        UpdateHitBursts(HitEffectTime);
        UpdateHitEffects(0);
        swords.Clear();
        nextWeaponId = 0;
        orbiting.Clear();
        orbitAngle = 0;
        recallTimer = 0;
        launchCooldown = 0;
        recallArea.enabled = false;
        guarding = false;
        IsFollowingCursor = false;
        IsAutoInterceptOn = false;
        followWasActive = false;
        followPath.Clear();
        followHead = Vector2.zero;
        followVelocity = Vector2.zero;
        followDirection = Vector2.up;
        guardArea.enabled = false;
        merging = false;

        shakeTimer = 0;
        view.transform.position = cameraRestPosition;
        if (mergeSword != null)
        {
            DestroyWeaponObjects(mergeSword);
            mergeSword = null;
        }


        for (int i = 0; i < InitialSwordCount; i++)
        {
            Sword sword = CreateSword($"Sword {i + 1}");
            ApplyWeaponAppearance(sword);
            swords.Add(sword);
            orbiting.Add(sword);
            Vector2 outward = OrbitDirection(2 * Mathf.PI * i / InitialSwordCount);
            sword.Root.localPosition = outward * OrbitRadius;
            SetFusionSize(sword, orbitVisual: true);
            PointSword(sword, outward);
        }
    }

    Sword CreateSword(string label)
    {
        var root = new GameObject(label).transform;
        root.SetParent(transform, false);
        var visual = new GameObject("Sword visual").transform;
        visual.SetParent(root, false);
        var groundMark = StageManager.Draw(root, "Ground mark", square, new Color32(76, 101, 120, 255),
            Vector2.zero, new Vector2(.35f, .055f), 2);
        groundMark.enabled = false;
        var blade = StageManager.Draw(visual, "Blade", bladeSprite, ActiveBlade,
            new Vector2(0, .23f), new Vector2(.15f, .46f), 3);
        StageManager.Draw(visual, "Guard", square, new Color32(205, 170, 102, 255),
            new Vector2(0, .46f), new Vector2(.30f, .055f), 3);
        StageManager.Draw(visual, "Grip", square, new Color32(157, 111, 80, 255),
            new Vector2(0, .55f), new Vector2(.065f, .15f), 3);

        var trail = new GameObject("Sword afterimage").AddComponent<TrailRenderer>();
        trail.transform.SetParent(visual, false);
        trail.sharedMaterial = trailMaterial != null ? trailMaterial : blade.sharedMaterial;
        trail.time = .22f;
        trail.minVertexDistance = .025f;
        trail.startWidth = .085f;
        trail.endWidth = 0;
        trail.startColor = new Color(.38f, 1, .92f, .65f);
        trail.endColor = new Color(.38f, 1, .92f, 0);
        trail.sortingOrder = 2;
        trail.emitting = false;

        return new Sword { Id = ++nextWeaponId, Root = root, Visual = visual, Blade = blade, GroundMark = groundMark, Trail = trail };
    }

    void Update()
    {
        if (view == null || Time.deltaTime <= 0)
            return;

        var keyboard = Keyboard.current;
        if (IsGameOver || IsChoosingReward)
            return;

        float dt = Time.deltaTime;
        UpdateSurvival(dt);
        if (IsGameOver || IsChoosingReward)
            return;
        UpdateWeaponEffects(dt);
        if (IsGameOver || IsChoosingReward) return;
        recallTimer -= dt;
        recallArea.enabled = recallTimer > 0;
        if (IsSkillUnlocked(Skill.AutoIntercept) && keyboard?.digit2Key.wasPressedThisFrame == true)
            IsAutoInterceptOn = !IsAutoInterceptOn;

        if (IsSkillUnlocked(Skill.CursorFollow) && keyboard?.digit5Key.wasPressedThisFrame == true)
        {
            IsFollowingCursor = !IsFollowingCursor;
            followHead = Vector2.zero;
            followVelocity = Vector2.zero;
        }

        bool mergeHeld = IsSkillUnlocked(Skill.Fusion) && keyboard != null && (keyboard.digit1Key.isPressed || keyboard.digit1Key.wasPressedThisFrame);
        if (mergeHeld && !merging)
            BeginMerge();
        if (!mergeHeld && merging)
            FinishMerge();
        merging = mergeHeld && mergeSword != null;
        guarding = IsSkillUnlocked(Skill.Guard) && !merging && keyboard != null && keyboard.digit4Key.isPressed && orbiting.Count > 0;
        guardArea.enabled = guarding;
        launchCooldown = Mathf.Max(0, launchCooldown - dt);
        var mouse = Mouse.current;
        if (suppressCombatUntilMouseReleased && (mouse == null || (!mouse.leftButton.isPressed && !mouse.rightButton.isPressed)))
            suppressCombatUntilMouseReleased = false;
        Vector2 cursor = mouse == null ? followHead :
            (Vector2)transform.InverseTransformPoint(view.ScreenToWorldPoint(mouse.position.ReadValue()));
        if (mouse != null && !suppressCombatUntilMouseReleased)
        {
            bool launch = mouse.leftButton.wasPressedThisFrame ||
                (mouse.leftButton.isPressed && launchCooldown <= 0);
            bool recall = mouse.rightButton.wasPressedThisFrame || mouse.rightButton.isPressed;
            bool crossSlash = IsSkillUnlocked(Skill.CrossSlash) && keyboard?.digit3Key.wasPressedThisFrame == true;
            if (launch || recall || crossSlash)
            {
                if (launch && orbiting.Count > 0)
                {
                    LaunchSword(cursor);
                    launchCooldown = LaunchInterval;
                }
                if (crossSlash)
                    CrossSlash(cursor);
                if (recall)
                    RecallSwords(cursor);
            }
        }

        foreach (Sword sword in orbiting)
            sword.PreviousPosition = sword.Root.localPosition;

        UpdateHitEffects(dt);
        UpdateBullets(dt);
        for (int swordIndex = 0; swordIndex < swords.Count; swordIndex++)
        {
            Sword sword = swords[swordIndex];
            if (sword.State == SwordState.Flying)
                AdvanceFlight(sword, dt);
            else if (sword.State == SwordState.Rebounding)
                AdvanceRebound(sword, dt);
            else if (sword.State == SwordState.Intercepting)
                AdvanceAutoIntercept(sword, dt);
            else if (sword.State == SwordState.Returning)
                AdvanceReturn(sword, dt);
            else if (sword.State == SwordState.Planted)
                AdvancePlanted(sword, dt);
            ConstrainSwordToView(sword);
            if (IsChoosingReward) break;
        }

        FlushPendingSwords();
        if (IsChoosingReward) return;

        if (IsAutoInterceptOn)
        {
            foreach (Sword sword in swords)
            {
                if (sword.State == SwordState.Planted)
                    TryStartAutoIntercept(sword);
            }
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
            foreach (Sword sword in orbiting)
                sword.Trail.emitting = false;
            followWasActive = false;
        }

        orbitAngle = Mathf.Repeat(orbitAngle + (guarding ? GuardOrbitSpeed : OrbitSpeed) * dt, 2 * Mathf.PI);
        for (int i = 0; i < orbiting.Count; i++)
        {
            Sword sword = orbiting[i];
            float offset = merging ? sword.MergeOrbitOffset : 2 * Mathf.PI * i / orbiting.Count;
            float targetAngle = (orbitAngle + offset) * Mathf.Rad2Deg;
            Vector2 previous = sword.Root.localPosition;
            float previousAngle = Mathf.Atan2(previous.y, previous.x) * Mathf.Rad2Deg;
            float angle = Mathf.MoveTowardsAngle(previousAngle, targetAngle, 360f * dt);
            Vector2 outward = OrbitDirection(angle * Mathf.Deg2Rad);
            float radius = Mathf.MoveTowards(previous.magnitude, guarding ? GuardRadius : OrbitRadius, ReturnSpeed * dt);
            Vector2 next = outward * radius;
            sword.Root.localPosition = next;
            SetFusionSize(sword, orbitVisual: true);
            PointSword(sword, outward);

            if (merging && TouchesMergeSword(previous, next))
            {
                AbsorbIntoMerge(sword);
                i--;
                shakeTimer = ShakeDuration;
            }
        }
        if (guarding)
        {
            UpdateGuardDamage();
            if (IsChoosingReward || IsGameOver) return;
            InterceptWithFormation();
        }
        RemoveArrivedBullets();
    }

    void InterceptWithFormation()
    {
        for (int i = orbiting.Count - 1; i >= 0; i--)
        {
            Sword sword = orbiting[i];
            Vector2 position = sword.Root.localPosition;
            Vector2 bladeBase = transform.InverseTransformPoint(sword.Visual.TransformPoint(new Vector3(0, .46f)));
            Vector2 bladeTip = transform.InverseTransformPoint(sword.Visual.position);
            for (int j = bullets.Count - 1; j >= 0; j--)
            {
                Bullet bullet = bullets[j];
                Vector2 bulletPosition = bullet.Root.localPosition;
                bool crossed = DistanceToSegmentSquared(Vector2.zero,
                    sword.PreviousPosition - bullet.Previous, position - bulletPosition)
                    <= InterceptContact * InterceptContact;
                bool touchesBlade = DistanceToSegmentSquared(bulletPosition, bladeTip, bladeBase)
                    <= InterceptContact * InterceptContact;
                if (!crossed && !touchesBlade)
                    continue;

                InterceptBullet(j);
                orbiting.RemoveAt(i);
                PlantSword(sword);
                break;
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

    void RemoveArrivedBullets()
    {
        for (int i = bullets.Count - 1; i >= 0; i--)
        {
            if (((Vector2)bullets[i].Root.localPosition).sqrMagnitude <= .25f * .25f)
            {
                DamagePlayer();
                RemoveBullet(i);
            }
        }
    }

    void StartFollowPath(Vector2 cursor)
    {
        followPath.Clear();
        followVelocity = Vector2.zero;
        if (orbiting.Count == 0)
        {
            followHead = Vector2.zero;
            followDirection = cursor.sqrMagnitude > .001f ? cursor.normalized : Vector2.up;
            followPath.Add((followHead - followDirection * FollowPathLength, 0));
            followPath.Add((followHead, FollowPathLength));
            return;
        }

        followHead = orbiting[0].Root.localPosition;
        Vector2 radial = followHead.sqrMagnitude > .001f ? followHead.normalized : Vector2.right;
        followDirection = new Vector2(-radial.y, radial.x);
        float angle = Mathf.Atan2(radial.y, radial.x);
        float radius = Mathf.Max(OrbitRadius, followHead.magnitude);
        for (int i = 32; i > 0; i--)
        {
            float distance = .25f * i;
            followPath.Add((followHead + (OrbitDirection(angle - distance / radius) - radial) * radius,
                8f - distance));
        }
        followPath.Add((followHead, 8f));
    }

    void FollowCursor(Vector2 cursor, float dt, bool entering)
    {
        Vector2 towardCursor = cursor - followHead;
        Vector2 desired = Vector2.SmoothDamp(followHead, cursor, ref followVelocity, FollowSmoothTime, FollowSpeed, dt)
            - followHead;
        if (towardCursor.sqrMagnitude > .0025f && desired.sqrMagnitude > .0000001f)
        {
            float angle = Vector2.SignedAngle(followDirection, towardCursor);
            float turn = Mathf.MoveTowards(0, angle, 200f * dt);
            followDirection = Quaternion.Euler(0, 0, turn) * followDirection;
            float speedFactor = Mathf.Lerp(.45f, 1, Mathf.Clamp01(Vector2.Dot(followDirection, towardCursor.normalized)));
            followHead += followDirection * (desired.magnitude * speedFactor);
        }

        var last = followPath[followPath.Count - 1];
        float step = Vector2.Distance(last.point, followHead);
        if (step >= FollowSampleSpacing)
        {
            float distance = last.distance + step;
            followPath.Add((followHead, distance));
            int expired = 0;
            while (expired + 1 < followPath.Count && followPath[expired + 1].distance < distance - FollowPathLength)
                expired++;
            if (expired > 0)
                followPath.RemoveRange(0, expired);
        }

        float depth = 0;
        int first = 0;
        while (first < orbiting.Count)
        {
            Sword lead = orbiting[first];
            Sword partner = lead.FusionSize == 0 && first + 1 < orbiting.Count &&
                orbiting[first + 1].FusionSize == 0 ? orbiting[first + 1] : null;
            SampleFollowPath(depth, out Vector2 position, out Vector2 direction);
            Vector2 side = new Vector2(direction.y, -direction.x);
            float lane = (.3f + FollowGap) * .5f;
            PlaceFollowingSword(lead, position - side * (partner != null ? lane : 0), direction, entering);
            if (partner != null)
                PlaceFollowingSword(partner, position + side * lane, direction, entering);

            depth += .625f * SwordScale(lead) + FollowGap;
            first += partner != null ? 2 : 1;
        }
    }

    void SampleFollowPath(float depth, out Vector2 position, out Vector2 direction)
    {
        if (depth <= 0)
        {
            position = followHead;
            direction = followDirection;
            return;
        }

        var last = followPath[followPath.Count - 1];
        float distance = last.distance + Vector2.Distance(last.point, followHead) - depth;
        Vector2 next = followHead;
        float nextDistance = last.distance + Vector2.Distance(last.point, followHead);
        for (int i = followPath.Count - 1; i >= 0; i--)
        {
            var sample = followPath[i];
            float length = nextDistance - sample.distance;
            if (length > .0001f && (distance >= sample.distance || i == 0))
            {
                direction = (next - sample.point).normalized;
                position = sample.point + direction * (distance - sample.distance);
                return;
            }

            next = sample.point;
            nextDistance = sample.distance;
        }

        position = followHead - followDirection * depth;
        direction = followDirection;
    }

    static void PlaceFollowingSword(Sword sword, Vector2 position, Vector2 direction, bool entering)
    {
        bool moved = ((Vector2)sword.Root.localPosition - position).sqrMagnitude > .000025f;
        sword.Root.localPosition = position;
        SetFusionSize(sword, orbitVisual: false);
        PointSword(sword, direction);

        if (entering)
        {
            sword.PreviousPosition = position;
            sword.Trail.emitting = false;
            sword.Trail.Clear();
        }
        else if (moved && !sword.Trail.emitting)
        {
            sword.Trail.Clear();
            sword.Trail.emitting = true;
        }
    }

    // Only the visual size is capped; damage and owned weapon count remain unrestricted.
    static float SwordScale(Sword sword) => Mathf.Min(3.5f, 1 + .22f * sword.FusionSize);

    void BeginMerge()
    {
        if (mergeSword == null && orbiting.Count > 0)
        {
            mergeSword = CreateSword("Fusion sword");
            mergeSword.Kind = WeaponKind.TemporaryFusion;
            ApplyWeaponAppearance(mergeSword);
            mergeSword.Root.localPosition = Vector2.up * OrbitRadius;
            mergeSword.Blade.color = FusionBlade;
            PointSword(mergeSword, Vector2.up);
        }

        foreach (Sword sword in orbiting)
        {
            Vector2 position = sword.Root.localPosition;
            sword.MergeOrbitOffset = Mathf.Atan2(position.y, position.x) - orbitAngle;
        }
    }

    static void SetFusionSize(Sword sword, bool orbitVisual)
    {
        if (sword.FusionSize == 0)
            return;

        float scale = SwordScale(sword);
        sword.Visual.localScale = Vector3.one * scale;
        // Keep the grip on the inner edge of the orbit as the blade grows outward.
        sword.Visual.localPosition = orbitVisual ? Vector2.down * (.625f * (scale - 1)) : Vector2.zero;
    }

    void FinishMerge()
    {
        if (mergeSword == null) return;
        if (mergeSword.FusionSize > 0)
        {
            swords.Add(mergeSword);
            orbiting.Add(mergeSword);
        }
        else
        {
            mergeSword.Root.gameObject.SetActive(false);
            Destroy(mergeSword.Root.gameObject);
        }

        mergeSword = null;
    }

    bool TouchesMergeSword(Vector2 from, Vector2 to)
    {
        Vector2 center = mergeSword.Root.localPosition;
        Vector2 travel = to - from;
        float progress = travel.sqrMagnitude > 0
            ? Mathf.Clamp01(Vector2.Dot(center - from, travel) / travel.sqrMagnitude) : 0;
        float radius = MergeContactRadius + mergeSword.FusionSize * .018f;
        return (from + travel * progress - center).sqrMagnitude <= radius * radius;
    }

    void LateUpdate()
    {
        if (IsChoosingReward || view == null || shakeTimer <= 0)
            return;

        shakeTimer = Mathf.Max(0, shakeTimer - Time.deltaTime);
        float strength = .045f * shakeTimer / ShakeDuration;
        view.transform.position = cameraRestPosition + (Vector3)(Random.insideUnitCircle * strength);
    }

    void OnDisable()
    {
        RestoreRewardTime();
        if (view != null)
            view.transform.position = cameraRestPosition;
    }

    void LaunchSword(Vector2 cursor)
    {
        Vector2 target = ClampTarget(cursor);
        Sword sword = orbiting[0];
        float nearest = ((Vector2)sword.Root.localPosition - target).sqrMagnitude;
        foreach (Sword candidate in orbiting)
        {
            float distance = ((Vector2)candidate.Root.localPosition - target).sqrMagnitude;
            if (distance > nearest || (distance == nearest && candidate.FusionSize <= sword.FusionSize))
                continue;

            sword = candidate;
            nearest = distance;
        }

        orbiting.Remove(sword);
        target += (target - (Vector2)sword.Root.localPosition).normalized * launchOvershoot;
        BeginFlight(sword, target);
    }

    static Vector2 ClampTarget(Vector2 point)
    {
        float edge = GridSize.x * .5f - .65f;
        return new Vector2(Mathf.Clamp(point.x, -edge, edge), Mathf.Clamp(point.y, -edge, edge));
    }

    Vector2 ClampSwordPosition(Sword sword, Vector2 point)
    {
        // Leave room for the whole sword (including its grip) and camera shake.
        float margin = .7f * SwordScale(sword) + sword.Visual.localPosition.magnitude;
        Vector2 center = transform.InverseTransformPoint(cameraRestPosition);
        float halfY = Mathf.Max(0, view.orthographicSize - margin);
        float halfX = Mathf.Max(0, view.orthographicSize * view.aspect - margin);
        point = new Vector2(Mathf.Clamp(point.x, center.x - halfX, center.x + halfX),
            Mathf.Clamp(point.y, center.y - halfY, center.y + halfY));
        return ClampTarget(point);
    }

    void ConstrainSwordToView(Sword sword)
    {
        Vector2 position = sword.Root.localPosition;
        Vector2 inside = ClampSwordPosition(sword, position);
        if (inside == position)
            return;
        sword.Root.localPosition = inside;
        StopSwordAtBoundary(sword);
    }

    void StopSwordAtBoundary(Sword sword)
    {
        if (sword.InterceptTarget != null)
            sword.InterceptTarget.Reserved = false;
        sword.InterceptTarget = null;
        PlantSword(sword);
    }

    bool IsInRecallRange(Sword sword, Vector2 center) =>
        sword.State == SwordState.Planted &&
        ((Vector2)sword.Root.localPosition - center).sqrMagnitude <= RecallRadius * RecallRadius;

    void CrossSlash(Vector2 cursor)
    {
        ShowSkillArea(cursor, new Color32(255, 137, 95, 230));
        Vector2 aim = ClampTarget(cursor);
        foreach (Sword sword in swords)
        {
            if (!IsInRecallRange(sword, cursor))
                continue;

            BeginFlight(sword, ClampTarget(2 * aim - (Vector2)sword.Root.localPosition));
            sword.Blade.color = ActiveWeaponColor(sword);
            sword.GroundMark.enabled = false;
        }
    }

    void ShowSkillArea(Vector2 cursor, Color color)
    {
        recallArea.color = color;
        recallArea.transform.localPosition = cursor;
        recallArea.enabled = true;
        recallTimer = RecallDisplayTime;
    }

    void RecallSwords(Vector2 cursor)
    {
        ShowSkillArea(cursor, new Color32(96, 237, 255, 230));
        foreach (Sword sword in swords)
        {
            if (!IsInRecallRange(sword, cursor))
                continue;

            BeginReturn(sword);
        }
    }

    void UpdateBullets(float dt)
    {
        foreach (Bullet bullet in bullets)
        {
            bullet.Previous = bullet.Root.localPosition;
            bullet.Root.localPosition = Vector2.MoveTowards(bullet.Previous, Vector2.zero, projectileSpeed * dt);
        }
    }

    void SpawnBullet(Vector2 origin)
    {
        var marker = StageManager.Draw(transform, "Enemy bullet", square,
            new Color32(255, 102, 82, 255), origin, Vector2.one * .19f, 5);
        marker.transform.localRotation = Quaternion.Euler(0, 0, 45);
        bullets.Add(new Bullet { Root = marker.transform });
    }

    void RemoveBullet(int index)
    {
        var bullet = bullets[index].Root.gameObject;
        bullet.SetActive(false);
        Destroy(bullet);
        bullets.RemoveAt(index);
    }

    void TryStartAutoIntercept(Sword sword)
    {
        Vector2 position = sword.Root.localPosition;
        float nearest = InterceptRange * InterceptRange;
        Bullet target = null;
        foreach (Bullet bullet in bullets)
        {
            if (bullet.Reserved)
                continue;

            float distance = ((Vector2)bullet.Root.localPosition - position).sqrMagnitude;
            if (distance > nearest)
                continue;

            nearest = distance;
            target = bullet;
        }

        if (target == null)
            return;

        target.Reserved = true;
        sword.InterceptTarget = target;
        sword.State = SwordState.Intercepting;
        sword.Blade.color = ActiveWeaponColor(sword);
        sword.GroundMark.enabled = false;
    }

    void AdvanceAutoIntercept(Sword sword, float dt)
    {
        Bullet target = sword.InterceptTarget;
        int index = bullets.IndexOf(target);
        if (index < 0)
        {
            sword.InterceptTarget = null;
            PlantSword(sword);
            return;
        }

        Vector2 position = sword.Root.localPosition;
        Vector2 targetPosition = target.Root.localPosition;
        PointSword(sword, targetPosition - position);
        Vector2 next = Vector2.MoveTowards(position, targetPosition, LaunchSpeed * dt);
        Vector2 inside = ClampSwordPosition(sword, next);
        bool reachedBoundary = inside != next;
        next = inside;
        sword.Root.localPosition = next;
        if (DistanceToSegmentSquared(Vector2.zero, position - target.Previous, next - targetPosition)
            > InterceptContact * InterceptContact)
        {
            if (reachedBoundary)
                StopSwordAtBoundary(sword);
            return;
        }

        InterceptBullet(index);
        sword.InterceptTarget = null;
        BeginReturn(sword);
    }

    void BeginReturn(Sword sword)
    {
        ClearFragments(sword);
        sword.ReturnHits.Clear();
        sword.State = SwordState.Returning;
        sword.Blade.color = ActiveWeaponColor(sword);
        sword.GroundMark.enabled = false;
    }

    void AdvancePlanted(Sword sword, float dt)
    {
        sword.AutoRecallRemaining = Mathf.Max(0, sword.AutoRecallRemaining - dt);
        if (sword.AutoRecallRemaining <= 0)
            BeginReturn(sword);
    }

    void BeginFlight(Sword sword, Vector2 target)
    {
        target = ClampSwordPosition(sword, target);
        sword.State = SwordState.Flying;
        sword.Trail.emitting = false;
        sword.Target = target;
        Vector2 travel = target - (Vector2)sword.Root.localPosition;
        sword.FlightDirection = sword.LaunchDirection = travel.normalized;
        sword.FlightDistanceRemaining = travel.magnitude;
        sword.InterceptsRemaining = 1 + sword.FusionSize;
        sword.Recovery = 0;
        sword.FlightHits.Clear();
        sword.GuardHits.Clear();
        sword.ChainUsed = sword.IsChainFlight = false;
        sword.ChainTarget = null;
        SetFusionSize(sword, orbitVisual: false);
    }

    void AdvanceFlight(Sword sword, float dt)
    {
        Vector2 position = sword.Root.localPosition;
        Vector2 destination = sword.Target;
        if (sword.IsChainFlight)
        {
            sword.ChainRemaining -= dt;
            if (sword.ChainRemaining <= 0 || !enemies.Contains(sword.ChainTarget))
            {
                PlantSword(sword);
                return;
            }
            destination = sword.ChainTarget.Root.localPosition;
        }
        float nearest = InterceptRange * InterceptRange;
        int targetIndex = -1;
        for (int i = 0; i < bullets.Count; i++)
        {
            Vector2 bulletPosition = bullets[i].Root.localPosition;
            float distance = (bulletPosition - position).sqrMagnitude;
            if (distance > nearest)
                continue;

            nearest = distance;
            destination = bulletPosition;
            targetIndex = i;
        }

        float speed = WeaponSpeed(sword) * Mathf.Lerp(1, .18f, sword.Recovery / InterceptRecovery);
        sword.Recovery = Mathf.Max(0, sword.Recovery - dt);
        Vector2 next;
        if (targetIndex >= 0)
        {
            // Preserve immediate bullet interception priority and its existing steering.
            next = Vector2.MoveTowards(position, destination, speed * dt);
            sword.FlightDirection = (destination - position).normalized;
        }
        else
        {
            if (!sword.IsChainFlight) destination = EnemyHomingTarget(sword, position);
            Vector2 desired = (destination - position).normalized;
            float turnSpeed = enemyHomingTurnSpeed + 10 * homingUpgrades;
            float turn = Mathf.Clamp(Vector2.SignedAngle(sword.FlightDirection, desired), -turnSpeed * dt, turnSpeed * dt);
            sword.FlightDirection = sword.IsChainFlight ? desired : (Vector2)(Quaternion.Euler(0, 0, turn) * sword.FlightDirection);
            float step = Mathf.Min(speed * dt, sword.FlightDistanceRemaining, Vector2.Distance(position, destination));
            next = ClampTarget(position + sword.FlightDirection * step);
        }
        Vector2 inside = ClampSwordPosition(sword, next);
        bool reachedBoundary = inside != next;
        next = inside;
        sword.FlightDistanceRemaining = Mathf.Max(0, sword.FlightDistanceRemaining - Vector2.Distance(position, next));
        PointSword(sword, next - position);
        sword.Root.localPosition = next;
        if (TryReboundFromEnemy(sword, position, next))
            return;

        if (targetIndex >= 0)
        {
            Bullet bullet = bullets[targetIndex];
            Vector2 start = position - bullet.Previous;
            Vector2 end = next - (Vector2)bullet.Root.localPosition;
            Vector2 travel = end - start;
            float progress = travel.sqrMagnitude > 0
                ? Mathf.Clamp01(-Vector2.Dot(start, travel) / travel.sqrMagnitude) : 0;
            if ((start + travel * progress).sqrMagnitude <= InterceptContact * InterceptContact)
            {
                InterceptBullet(targetIndex);
                Vector2 impact = Vector2.Lerp(position, next, progress);
                sword.InterceptsRemaining--;
                if (sword.InterceptsRemaining == 0)
                {
                    sword.Root.localPosition = impact;
                    PlantSword(sword);
                }
                else
                {
                    Vector2 forward = sword.Root.localRotation * Vector2.down;
                    sword.Root.localPosition = impact - forward * InterceptRecoil;
                    sword.Recovery = InterceptRecovery;
                }
            }
            if (sword.State == SwordState.Flying && (reachedBoundary || sword.FlightDistanceRemaining <= .001f))
                StopSwordAtBoundary(sword);
            return;
        }

        if (!reachedBoundary && next != sword.Target && sword.FlightDistanceRemaining > .001f && next != position)
            return;

        PlantSword(sword);
    }

    Vector2 EnemyHomingTarget(Sword sword, Vector2 position)
    {
        float range = enemyHomingRange + .25f * homingUpgrades;
        float nearest = range * range;
        float minimumDot = Mathf.Cos((enemyHomingHalfAngle + 5 * homingUpgrades) * Mathf.Deg2Rad);
        Vector2 target = sword.Target;
        foreach (Enemy enemy in enemies)
        {
            if (sword.FlightHits.Contains(enemy)) continue;
            Vector2 offset = (Vector2)enemy.Root.localPosition - position;
            float distance = offset.sqrMagnitude;
            if (distance > nearest || distance < .000001f)
                continue;
            Vector2 direction = offset.normalized;
            if (Vector2.Dot(sword.FlightDirection, direction) < minimumDot ||
                Vector2.Dot(sword.LaunchDirection, direction) < minimumDot)
                continue;
            nearest = distance;
            target = enemy.Root.localPosition;
        }
        return target;
    }

    bool TryReboundFromEnemy(Sword sword, Vector2 from, Vector2 to)
    {
        Vector2 travel = to - from;
        if (travel.sqrMagnitude <= .000001f) return false;
        Vector2 direction = travel.normalized;
        while (true)
        {
            Enemy enemy = FindFirstEnemyHit(sword, from, to, out float distance);
            if (enemy == null) return false;
            Vector2 enemyPosition = enemy.Root.localPosition;
            Vector2 impact = from + direction * distance;
            sword.FlightHits.Add(enemy);
            SpawnHitBurst(impact, new Color(1, .77f, .34f));
            DamageEnemy(enemy, SwordDamage(sword), direction, WeaponKnockback(sword));
            ForwardSpecialEffects(sword, enemy, impact);
            if (StartChain(sword, enemy, impact))
            {
                sword.Root.localPosition = impact;
                return true;
            }
            if ((Abilities(sword) & Ability.Pierce) != 0) continue;
            Vector2 normal = (impact - enemyPosition).normalized;
            Vector2 rebound = normal.sqrMagnitude > 0 && Vector2.Dot(direction, normal) < 0
                ? Vector2.Reflect(direction, normal) : -direction;
            sword.Root.localPosition = impact;
            sword.Target = ChooseReboundTarget(impact, rebound);
            sword.State = SwordState.Rebounding;
            sword.IsChainFlight = false;
            PointSword(sword, sword.Target - impact);
            return true;
        }
    }
    static Vector2 ChooseReboundTarget(Vector2 impact, Vector2 rebound)
    {
        float distance = Random.Range(ReboundMinDistance, ReboundMaxDistance);
        for (int attempt = 0; attempt < 8; attempt++)
        {
            Vector2 direction = Quaternion.Euler(0, 0, Random.Range(-75f, 75f)) * rebound;
            Vector2 target = ClampTarget(impact + direction * distance);
            if ((target - impact).sqrMagnitude >= ReboundMinDistance * ReboundMinDistance)
                return target;
        }

        // Aim back into the arena if the reflected directions are clipped by a wall.
        Vector2 inward = Quaternion.Euler(0, 0, Random.Range(-35f, 35f)) * (-impact).normalized;
        return ClampTarget(impact + inward * distance);
    }

    void AdvanceRebound(Sword sword, float dt)
    {
        Vector2 position = sword.Root.localPosition;
        Vector2 next = Vector2.MoveTowards(position, sword.Target, ReboundSpeed * dt);
        Vector2 inside = ClampSwordPosition(sword, next);
        sword.Root.localPosition = inside;
        if (inside != next || next == sword.Target)
            PlantSword(sword);
    }

    void InterceptBullet(int index)
    {
        SpawnHitBurst(bullets[index].Root.localPosition, new Color(.65f, 1, .96f));
        RemoveBullet(index);
    }

    void SpawnHitBurst(Vector2 position, Color color)
    {
        if (!availableHits.TryPop(out HitBurst burst))
        {
            burst = new HitBurst();
            for (int i = 0; i < burst.Sparks.Length; i++)
            {
                SpriteRenderer spark = StageManager.Draw(transform, "Hit spark", square, color,
                    position, new Vector2(.18f, .055f), 7);
                spark.transform.localRotation = Quaternion.Euler(0, 0, 360f * i / burst.Sparks.Length);
                burst.Sparks[i] = spark;
            }
        }

        burst.Origin = position;
        burst.Color = color;
        burst.Remaining = HitEffectTime;
        activeHits.Add(burst);
        DrawHitBurst(burst);
    }

    void UpdateHitEffects(float dt)
    {
        UpdateHitBursts(dt);
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

    void PlantSword(Sword sword)
    {
        bool newlyPlanted = sword.State != SwordState.Planted;
        sword.State = SwordState.Planted;
        sword.AutoRecallRemaining = AutomaticRecallDelay;
        sword.Trail.emitting = false;
        SetFusionSize(sword, orbitVisual: false);
        sword.Root.localRotation = Quaternion.identity;
        sword.Blade.color = PlantedBlade;
        sword.GroundMark.enabled = true;
        if (newlyPlanted) OnWeaponPlanted(sword);
    }

    static Vector2 ReturnTarget(Vector2 position) =>
        position.sqrMagnitude > 0 ? position.normalized * OrbitRadius : Vector2.right * OrbitRadius;

    void AdvanceReturn(Sword sword, float dt)
    {
        Vector2 position = sword.Root.localPosition;
        Vector2 target = ReturnTarget(position);
        PointSword(sword, target - position);
        Vector2 next = Vector2.MoveTowards(position, target, ReturnSpeed * dt);
        sword.Root.localPosition = next;
        DamageEnemiesOnReturn(sword, position, next);
        if (next != target)
            return;

        sword.ReturnHits.Clear();
        if (sword.Components.Count > 0)
        {
            if (!pendingSplits.Contains(sword)) pendingSplits.Add(sword);
            return;
        }
        sword.State = SwordState.Orbiting;
        sword.PreviousPosition = next;
        SetFusionSize(sword, orbitVisual: true);
        sword.MergeOrbitOffset = Mathf.Atan2(next.y, next.x) - orbitAngle;
        orbiting.Add(sword);
    }

    static Vector2 OrbitDirection(float angle) => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

    static void PointSword(Sword sword, Vector2 direction)
    {
        if (direction.sqrMagnitude > 0)
            sword.Root.localRotation = Quaternion.Euler(0, 0, Vector2.SignedAngle(Vector2.down, direction));
    }

    static Sprite MakeRecallSprite()
    {
        const int size = 128;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            var point = new Vector2(x + .5f - size * .5f, y + .5f - size * .5f);
            float distance = point.magnitude;
            float alpha = distance > size * .5f ? 0 : distance > size * .5f - 2 ? 1 : .13f;
            texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
        }

        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, size);
    }

    public void DrawFusionCounts()
    {
        if (Event.current.type != EventType.Repaint)
            return;

        fusionLabel ??= new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 15,
            normal = { textColor = FusionBlade }
        };
        foreach (Sword sword in swords)
            DrawFusionCount(sword);
        if (mergeSword != null)
            DrawFusionCount(mergeSword);
    }

    GUIStyle fusionLabel;

    void DrawFusionCount(Sword sword)
    {
        if (sword.FusionSize == 0)
            return;

        Vector3 world = sword.Visual.TransformPoint(new Vector3(.19f, .35f));
        Vector3 pixel = view.WorldToScreenPoint(world);
        GUI.Label(new Rect(pixel.x - 16, Screen.height - pixel.y - 11, 32, 22),
            sword.FusionSize.ToString(), fusionLabel);
    }

    void DrawGrid()
    {
        Vector2 size = GridSize;
        var line = new Color32(52, 73, 92, 255);
        for (int i = 1; i < Cells; i++)
        {
            float offset = i * CellSize - size.x * .5f;
            StageManager.Draw(transform, "Grid column", square, line,
                new Vector2(offset, 0), new Vector2(.018f, size.y), 1);
            StageManager.Draw(transform, "Grid row", square, line,
                new Vector2(0, offset), new Vector2(size.x, .018f), 1);
        }
    }

    static Sprite MakeBladeSprite()
    {
        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float width = Mathf.Min(size * .5f, (y + .5f) * 1.5f);
            texture.SetPixel(x, y, Mathf.Abs(x + .5f - size * .5f) <= width ? Color.white : Color.clear);
        }
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, size);
    }
}
