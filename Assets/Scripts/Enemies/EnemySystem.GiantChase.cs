using UnityEngine;

public sealed partial class EnemySystem
{
    const int GiantChaseHits = 3;
    const int GiantChallengeHits = 20;
    const float GiantChallengeRecoilDistanceScale = .045f;
    const float GiantChallengeSlowTime = .2f;
    const float GiantChaseSlowTime = .85f;
    const float GiantChaseAccelerationTime = .28f;
    const float GiantChaseSpeedMultiplier = 1.32f;
    const float GiantRecoilSpeed = 44;
    const float GiantRecoilDeceleration = 176;
    EnemyBullet chasingGiantSword;
    public EnemyBullet ChasingGiantSword => chasingGiantSword;
    BulletView plantedGiantSword;
    BulletView capturedGiantSword;
    EnemyState giantSwordBoss;
    bool giantReturning;

    public EnemyState GiantSwordBoss => giantSwordBoss;
    public bool HasGiantSwordBoss => giantSwordBoss != null && giantSwordBoss.Health > 0;
    int giantChaseHits;
    float giantChaseSlow;
    Vector2 giantRecoilVelocity;
    bool giantAdvancesDuringRecoil;
    float giantFallSpeed;
    bool giantFalling;
    SpriteRenderer giantBlade;
    Color giantActiveColor;

    void StartGiantChase(EnemyState boss)
    {
        giantSwordBoss = boss;
        foreach (EnemyBullet basicSword in boss.BasicSwords)
        {
            if (basicSword.State == BossSwordState.Flying && bullets.Remove(basicSword))
                StartBossSwordRecall(basicSword);
        }
        BulletView sword = Instantiate(swordProjectilePrefab, transform);
        Vector2 start = boss.Position + Vector2.up * 2;
        sword.transform.position = start;
        sword.transform.localScale = Vector3.one * 8;
        giantBlade = sword.transform.Find("Visual/Blade").GetComponent<SpriteRenderer>();
        giantActiveColor = giantBlade.color;
        sword.ResetTrail(true);
        if (sword.trail != null) sword.trail.startWidth = .65f;
        chasingGiantSword = new EnemyBullet { View = sword, Previous = start, GiantChaseOwner = boss };
        giantChaseHits = 0;
        giantChaseSlow = 0;
        giantRecoilVelocity = Vector2.zero;
        giantAdvancesDuringRecoil = false;
        bullets.Add(chasingGiantSword);
    }

    void AdvanceGiantChase(int index, float dt)
    {
        EnemyBullet sword = bullets[index];
        if (sword.GiantChaseOwner.Health <= 0)
        {
            RemoveBullet(index);
            return;
        }
        giantChaseSlow = Mathf.Max(0, giantChaseSlow - dt);
        float recovery = 1 - Mathf.Clamp01(giantChaseSlow / GiantChaseAccelerationTime);
        float speed = player.MoveSpeed * Mathf.Lerp(.55f, GiantChaseSpeedMultiplier, recovery * recovery);
        Vector2 position = sword.Root.position;
        Vector2 direction = (player.Position - position).normalized;
        sword.Velocity = direction * speed;
        if (giantRecoilVelocity.sqrMagnitude > 0)
        {
            Vector2 next = giantAdvancesDuringRecoil
                ? Vector2.MoveTowards(position, player.Position, speed * dt)
                    + giantRecoilVelocity * (dt * GiantChallengeRecoilDistanceScale)
                : position + giantRecoilVelocity * dt;
            geometry.ClipTerrain(position, next, Vector2.zero, out next);
            sword.Root.position = next;
            giantRecoilVelocity = Vector2.MoveTowards(giantRecoilVelocity, Vector2.zero, GiantRecoilDeceleration * dt);
        }
        else
            sword.Root.position = Vector2.MoveTowards(position, player.Position, speed * dt);
        if (direction.sqrMagnitude > 0)
            sword.Root.rotation = Quaternion.Euler(0, 0, Vector2.SignedAngle(Vector2.down, direction));
    }

    public void RepelGiantSwordAfterPlayerHit()
    {
        if (chasingGiantSword == null) return;
        Vector2 away = chasingGiantSword.Previous - player.Position;
        if (away.sqrMagnitude < .001f) away = chasingGiantSword.Root.up;
        giantAdvancesDuringRecoil = false;
        giantRecoilVelocity = away.normalized * 54;
        giantChaseSlow = 1.5f;
    }

    public void InterceptProjectile(int index)
    {
        if (bullets[index].Owner is { IsPractice: true } instructor)
            instructor.PracticeSwordIntercepted = true;
        if (bullets[index] != chasingGiantSword)
        {
            RemoveBullet(index);
            return;
        }

        bool bossChallenge = StoryProgress.IsBossChallenge;
        giantChaseSlow = bossChallenge ? GiantChallengeSlowTime : GiantChaseSlowTime;
        Vector2 away = -chasingGiantSword.Velocity.normalized;
        if (away.sqrMagnitude == 0)
            away = chasingGiantSword.Root.up;
        giantAdvancesDuringRecoil = bossChallenge;
        giantRecoilVelocity = away * GiantRecoilSpeed;
        int hitsToDrop = bossChallenge ? GiantChallengeHits : GiantChaseHits;
        if (++giantChaseHits < hitsToDrop) return;
        plantedGiantSword = chasingGiantSword.View;
        chasingGiantSword = null;
        bullets.RemoveAt(index);
        giantFalling = true;
        giantFallSpeed = 3;
        plantedGiantSword.transform.rotation = Quaternion.identity;
        plantedGiantSword.ResetTrail(false);
        plantedGiantSword.SetDroppedIndicator(true);
        giantBlade.color = SwordSystem.PlantedBlade;
    }

    float PlantedGiantDistanceSquared(Vector2 cursor)
    {
        Vector2 tip = plantedGiantSword.transform.position;
        Vector2 hilt = plantedGiantSword.transform.TransformPoint(new Vector3(0, .62f));
        Vector2 blade = hilt - tip;
        float along = Mathf.Clamp01(Vector2.Dot(cursor - tip, blade) / blade.sqrMagnitude);
        return (cursor - (tip + blade * along)).sqrMagnitude;
    }

    public void ReturnGiantSword(Vector2 position)
    {
        if (capturedGiantSword == null || !HasGiantSwordBoss || giantReturning) return;
        giantReturning = true;
        capturedGiantSword.transform.position = position;
        capturedGiantSword.gameObject.SetActive(true);
        capturedGiantSword.ResetTrail(true);
        if (capturedGiantSword.trail != null) capturedGiantSword.trail.startWidth = .65f;
    }

    void ResetGiantReward()
    {
        if (plantedGiantSword != null) Destroy(plantedGiantSword.gameObject);
        if (capturedGiantSword != null)
        {
            capturedGiantSword.gameObject.SetActive(false);
            Destroy(capturedGiantSword.gameObject);
        }
        plantedGiantSword = capturedGiantSword = null;
        giantSwordBoss = null;
        giantReturning = false;
        giantFalling = false;
    }

    void TickGiantReward(float dt)
    {
        if (giantSwordBoss == null) return;
        if (!HasGiantSwordBoss || (giantReturning &&
            AdvanceBossRecallFlight(capturedGiantSword, BossSwordHome(giantSwordBoss), 24, dt)))
        {
            if (HasGiantSwordBoss) giantSwordBoss.AreaAttackCooldown = 0;
            ResetGiantReward();
            return;
        }
        TickGiantFall(dt);
    }

    void TickGiantFall(float dt)
    {
        if (!giantFalling || plantedGiantSword == null) return;
        Vector2 position = plantedGiantSword.transform.position;
        giantFallSpeed += 22 * dt;
        float floor = geometry.transform.position.y - geometry.Size.y * .5f;
        Vector2 next = new(position.x, Mathf.Max(floor, position.y - giantFallSpeed * dt));
        bool landed = geometry.ClipTerrain(position, next, Vector2.zero, out next);
        plantedGiantSword.transform.position = next;
        giantFalling = !landed && next.y > floor;
    }
}
