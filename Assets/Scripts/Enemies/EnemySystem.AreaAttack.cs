using System.Collections.Generic;
using UnityEngine;

public sealed partial class EnemySystem
{
    const float AreaWarningTime = 1.2f;
    const float AreaAttackInterval = 1.1f;
    const int AreaVolleySize = 5;
    const float AreaVolleyCooldown = 10;
    const int MaxConcurrentAreaAttacks = 3;
    const float GiantSwordSpeed = 32;
    const float GiantSwordLength = 5.2f;
    const float AreaWidth = 2.2f;

    sealed class AreaAttack
    {
        public EnemyState Owner;
        public GameObject Warning;
        public SpriteRenderer WarningFill;
        public BulletView Sword;
        public Vector2 Anchor;
        public Vector2 Direction;
        public float Time;
        public bool Flying;
        public bool HitPlayer;
    }

    GameObject areaWarningPrefab;
    readonly List<AreaAttack> areaAttacks = new();
    readonly Stack<AreaAttack> areaAttackPool = new();

    void TickBossAreaPattern(EnemyState owner, float dt)
    {
        if (owner.AreaAttacksInVolley >= AreaVolleySize)
        {
            if (areaAttacks.Exists(attack => attack.Owner == owner)) return;
            owner.AreaAttacksInVolley = 0;
            owner.AreaAttackCooldown = AreaVolleyCooldown;
            return;
        }
        owner.AreaAttackCooldown = Mathf.Max(0,owner.AreaAttackCooldown-dt);
        if (owner.AreaAttackCooldown > 0 || areaWarningPrefab == null || swordProjectilePrefab == null)
            return;
        int activeCount = 0;
        foreach (AreaAttack active in areaAttacks)
            if (active.Owner == owner) activeCount++;
        if (activeCount >= MaxConcurrentAreaAttacks) return;
        AreaAttack attack = areaAttackPool.Count > 0 ? areaAttackPool.Pop() : new AreaAttack
        {
            Warning = Instantiate(areaWarningPrefab,transform),
            Sword = Instantiate(swordProjectilePrefab,transform)
        };
        attack.WarningFill = attack.Warning.GetComponentInChildren<SpriteRenderer>(true);
        attack.Owner = owner;
        attack.Anchor = player.Position;
        float[] angles = { 0,180,90,-90,30,150 };
        float angle = angles[Random.Range(0,angles.Length)] * Mathf.Deg2Rad;
        attack.Direction = new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
        attack.Time = 0;
        attack.Flying = attack.HitPlayer = false;
        attack.Warning.transform.position = attack.Anchor;
        attack.Warning.transform.rotation = Quaternion.Euler(0,0,angle*Mathf.Rad2Deg);
        attack.Warning.transform.localScale = new Vector3(geometry.Size.magnitude+40,AreaWidth,1);
        attack.Warning.SetActive(true);
        attack.Sword.gameObject.SetActive(false);
        areaAttacks.Add(attack);
        owner.AreaAttacksInVolley++;
        owner.AreaAttackCooldown = AreaAttackInterval;
    }

    void AdvanceAreaAttacks(float dt)
    {
        for (int i = areaAttacks.Count-1; i >= 0; i--)
        {
            AreaAttack attack = areaAttacks[i];
            if (attack.Owner.Health <= 0)
            {
                FinishAreaAttack(i);
                continue;
            }
            attack.Time += dt;
            if (!attack.Flying)
            {
                float pulse = .5f + .5f*Mathf.Sin(attack.Time*18);
                attack.WarningFill.color = new Color(1,.04f,.10f,.18f+.15f*pulse);
                if (attack.Time < AreaWarningTime) continue;
                LaunchAreaSword(attack);
            }
            Vector2 previous = attack.Sword.transform.position;
            Vector2 next = previous + attack.Direction*(GiantSwordSpeed*dt);
            attack.Sword.transform.position = next;
            if (!attack.HitPlayer && SweptGiantSwordHitsPlayer(attack,previous,next))
            {
                attack.HitPlayer = true;
                player.TakeHit();
            }
            Vector2 tail = next-attack.Direction*GiantSwordLength;
            if ((Vector2.Dot(tail-attack.Anchor,attack.Direction) > 0 && OutsideSameScreenEdge(next,tail)) || attack.Time > 6)
                FinishAreaAttack(i);
        }
    }

    void LaunchAreaSword(AreaAttack attack)
    {
        attack.Flying = true;
        attack.Time = 0;
        Vector2 cameraCenter = view.transform.position;
        float halfHeight = view.orthographicSize;
        float halfWidth = halfHeight*view.aspect;
        float screenRadius = Mathf.Abs(attack.Direction.x)*halfWidth+Mathf.Abs(attack.Direction.y)*halfHeight;
        float centerOffset = Vector2.Dot(cameraCenter-attack.Anchor,attack.Direction);
        Vector2 start = attack.Anchor+attack.Direction*(centerOffset-screenRadius-GiantSwordLength-4);
        attack.Sword.transform.position = start;
        attack.Sword.transform.rotation = Quaternion.Euler(0,0,Vector2.SignedAngle(Vector2.down,attack.Direction));
        attack.Sword.transform.localScale = Vector3.one*8;
        attack.Sword.gameObject.SetActive(true);
        attack.Sword.ResetTrail(true);
        if (attack.Sword.trail != null) attack.Sword.trail.startWidth = .65f;
        attack.WarningFill.color = new Color(1,.04f,.10f,.10f);
    }

    bool SweptGiantSwordHitsPlayer(AreaAttack attack, Vector2 previous, Vector2 next)
    {
        Vector2 side = new(-attack.Direction.y,attack.Direction.x);
        Vector2 from = player.PreviousPosition-previous;
        Vector2 to = player.Position-next;
        Vector3 start = new(Vector2.Dot(from,attack.Direction),Vector2.Dot(from,side),0);
        Vector3 end = new(Vector2.Dot(to,attack.Direction),Vector2.Dot(to,side),0);
        var box = new Bounds(new Vector3(-GiantSwordLength*.5f,0,0),new Vector3(GiantSwordLength+.6f,AreaWidth+.6f,2));
        if (box.Contains(start)) return true;
        Vector3 travel = end-start;
        return travel.sqrMagnitude > .000001f && box.IntersectRay(new Ray(start,travel.normalized),out float distance)
            && distance <= travel.magnitude;
    }

    bool OutsideSameScreenEdge(Vector2 tip, Vector2 tail)
    {
        Vector3 a = view.WorldToViewportPoint(tip);
        Vector3 b = view.WorldToViewportPoint(tail);
        return a.x < -.15f && b.x < -.15f || a.x > 1.15f && b.x > 1.15f ||
            a.y < -.15f && b.y < -.15f || a.y > 1.15f && b.y > 1.15f;
    }

    void FinishAreaAttack(int index)
    {
        AreaAttack attack = areaAttacks[index];
        attack.Warning.SetActive(false);
        attack.Sword.ResetTrail(false);
        attack.Sword.gameObject.SetActive(false);
        areaAttacks.RemoveAt(index);
        areaAttackPool.Push(attack);
    }

    void ResetAreaAttacks()
    {
        for (int i = areaAttacks.Count-1; i >= 0; i--) FinishAreaAttack(i);
    }
}
