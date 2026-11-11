using UnityEngine;

public sealed partial class SwordSystem
{
    const float OrderDuration = 5f;
    const float OrderPeriod = 1.8f;
    const float OrderSpeed = 14f;
    const float OrderReach = 2.65f;
    const float OrderCrossWidth = .85f;
    static readonly Color OrderBladeColor = new Color32(255, 196, 92, 255);

    void AddOrderSwords(int count)
    {
        if (count == 0) return;
        Sprite blade = Resources.Load<Sprite>("Swords/OrderBlade");
        for (int i = 0; i < count; i++)
        {
            SwordInstance sword = CreateSword("금린검");
            sword.OrderSlot = i;
            sword.Root.localScale = Vector3.one * 1.5f;
            if (blade != null) sword.Blade.sprite = blade;
            sword.Blade.color = OrderBladeColor;
            swords.Add(sword);
            orbiting.Add(sword);
        }
    }

    void StartOrderAttack(SwordInstance sword)
    {
        EnemyState boss = null;
        float nearest = float.PositiveInfinity;
        foreach (EnemyState enemy in enemySystem.Enemies)
        {
            if (!enemy.IsBoss || enemy.Health <= 0) continue;
            float distance = (enemy.Position - (Vector2)sword.Root.position).sqrMagnitude;
            if (distance > nearest) continue;
            nearest = distance;
            boss = enemy;
        }
        if (boss == null) return;

        if (sword.State == SwordState.Planted)
            sword.Root.localPosition += (Vector3)(sword.PlantedNormal * .06f);
        sword.State = SwordState.OrderAttack;
        sword.LaunchEnemy = boss;
        sword.OrderElapsed = 0;
        sword.OrderContact = false;
        sword.GroundMark.enabled = false;
        sword.Blade.color = OrderBladeColor;
        sword.Trail.Clear();
        sword.Trail.startColor = new Color(OrderBladeColor.r, OrderBladeColor.g, OrderBladeColor.b, .8f);
        sword.Trail.endColor = new Color(OrderBladeColor.r, OrderBladeColor.g, OrderBladeColor.b, 0f);
        sword.Trail.emitting = true;
    }

    void AdvanceOrderSword(SwordInstance sword, float dt)
    {
        EnemyState boss = sword.LaunchEnemy;
        sword.OrderElapsed += dt;
        if (boss == null || boss.Health <= 0 || sword.OrderElapsed >= OrderDuration)
        {
            BeginFall(sword);
            return;
        }

        Vector2 center = transform.InverseTransformPoint(boss.Position);
        float axisAngle = sword.OrderSlot * 137.5f * Mathf.Deg2Rad;
        Vector2 axis = new(Mathf.Cos(axisAngle), Mathf.Sin(axisAngle));
        float phase = (sword.OrderElapsed / OrderPeriod + sword.OrderSlot * .20f) * 2f * Mathf.PI;
        Vector2 waypoint = center + axis * (OrderReach * Mathf.Sin(phase))
            + Vector2.Perpendicular(axis) * (OrderCrossWidth * Mathf.Sin(2f * phase));
        Vector2 position = sword.Root.localPosition;
        Vector2 next = Vector2.MoveTowards(position, waypoint, OrderSpeed * dt);
        if (stage.ClipTerrain(position, next, Vector2.zero, out Vector2 impact, out Vector2 normal))
        {
            next = impact + normal * .02f;
        }
        sword.Root.localPosition = next;
        PointSword(sword, next - position);

        float radius = EnemyHitRadius * Mathf.Abs(boss.View.transform.lossyScale.x);
        if (!sword.OrderContact && DistanceToSegmentSquared(center, position, next) <= radius * radius)
        {
            sword.OrderContact = true;
            if (!enemySystem.IsInvulnerable(boss))
            {
                enemySystem.Damage(boss, SwordDamage(sword));
                SpawnHitBurst(center, OrderBladeColor);
            }
        }
        if ((next - center).sqrMagnitude > (radius + .12f) * (radius + .12f))
            sword.OrderContact = false;
    }
}
