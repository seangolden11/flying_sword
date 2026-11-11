using System.Collections.Generic;
using UnityEngine;

public sealed partial class SwordSystem
{
    sealed class EnemyTheft
    {
        public EnemyState Enemy;
        public SwordInstance Sword;
        public GameObject Marker;
        public bool Claimed;
        public EnemyBullet Projectile;
        public float ShootDelay = .8f;
    }

    readonly List<EnemyTheft> enemyThefts = new();
    readonly Stack<GameObject> theftMarkerPool = new();
    GameObject theftMarkerPrefab;
    const float TheftApproachSpeed = 5;
    const float TheftMarkerDiameter = 2 * RecallRadius * .9f;

    void ResetEnemyThefts()
    {
        foreach (EnemyTheft attempt in enemyThefts)
        {
            if (attempt.Projectile != null) enemySystem.StopStolenPlayerSword(attempt.Projectile);
            ReleaseTheftMarker(attempt.Marker);
        }
        enemyThefts.Clear();
    }

    void ReleaseTheftMarker(GameObject marker)
    {
        marker.SetActive(false);
        theftMarkerPool.Push(marker);
    }

    void TickEnemyThefts(float dt)
    {
        if (theftMarkerPrefab == null) return;
        for (int i = enemyThefts.Count - 1; i >= 0; i--)
        {
            EnemyTheft attempt = enemyThefts[i];
            SwordInstance sword = attempt.Sword;
            bool valid = attempt.Enemy.Health > 0 && !enemySystem.IsSwordPortalActive &&
                sword.State == (attempt.Projectile != null ? SwordState.EnemyFlying : attempt.Claimed ? SwordState.EnemyHeld : SwordState.Planted);
            if (!valid || (attempt.Projectile != null && attempt.Projectile.Spent))
            {
                if (attempt.Projectile != null) enemySystem.StopStolenPlayerSword(attempt.Projectile);
                if (sword.State == SwordState.EnemyHeld || sword.State == SwordState.EnemyFlying)
                {
                    BeginFall(sword);
                    equipment.NotifyChanged();
                }
                attempt.Enemy.TheftCooldown = 2;
                ReleaseTheftMarker(attempt.Marker);
                enemyThefts.RemoveAt(i);
                continue;
            }
            if (attempt.Projectile != null) continue;
            Vector2 target = attempt.Claimed ? attempt.Enemy.Position + Vector2.up * 1.2f : (Vector2)sword.Root.position;
            Vector2 origin = attempt.Claimed ? sword.Root.position : attempt.Marker.transform.position;
            float approachSpeed = attempt.Enemy.IsBoss ? EnemySystem.BossRecallCircleSpeed * .5f : TheftApproachSpeed;
            float speed = attempt.Claimed ? 15 : approachSpeed;
            Vector2 next = Vector2.MoveTowards(origin, target, speed * dt);
            if (!attempt.Claimed) attempt.Marker.transform.position = next;
            if (attempt.Claimed)
            {
                sword.Root.position = next;
                PointSword(sword,next-origin);
                if (Vector2.Distance(next,target) <= .1f)
                {
                    sword.Trail.emitting = false;
                    attempt.ShootDelay -= dt;
                    if (attempt.ShootDelay <= 0 && attempt.Enemy.IsAware)
                        attempt.Projectile = enemySystem.FireStolenPlayerSword(sword);
                }
            }
            else if (Vector2.Distance(next,target) <= .1f)
            {
                attempt.Claimed = true;
                attempt.Marker.SetActive(false);
                sword.State = SwordState.EnemyHeld;
                sword.Trail.Clear();
                sword.Trail.emitting = true;
                sword.GroundMark.enabled = false;
                sword.Blade.color = new Color32(255,105,105,255);
                equipment.NotifyChanged();
                SpawnHitBurst(target,new Color(1,.15f,.2f));
            }
        }

        foreach (EnemyState enemy in enemySystem.Enemies)
        {
            enemy.TheftCooldown = Mathf.Max(0,enemy.TheftCooldown-dt);
            if (enemySystem.IsSwordPortalActive || !enemy.IsAware || enemy.TheftCooldown > 0 ||
                enemyThefts.Exists(attempt => attempt.Enemy == enemy))
                continue;
            SwordInstance target = null;
            float nearest = 14 * 14;
            foreach (SwordInstance sword in swords)
            {
                if (sword.IsOrderSword || (sword.IsEnemyWeapon && !sword.PermanentLoot) || sword.State != SwordState.Planted ||
                    enemyThefts.Exists(attempt => attempt.Sword == sword)) continue;
                float distance = ((Vector2)sword.Root.position-enemy.Position).sqrMagnitude;
                if (distance > nearest) continue;
                nearest = distance;
                target = sword;
            }
            if (target == null) continue;
            GameObject marker = theftMarkerPool.Count > 0 ? theftMarkerPool.Pop() : Instantiate(theftMarkerPrefab,transform);
            marker.transform.position = enemy.Position;
            marker.transform.Find("Visual").localScale = new Vector3(TheftMarkerDiameter, TheftMarkerDiameter, 1);
            marker.SetActive(true);
            enemyThefts.Add(new EnemyTheft { Enemy = enemy, Sword = target, Marker = marker });
        }
    }

}
