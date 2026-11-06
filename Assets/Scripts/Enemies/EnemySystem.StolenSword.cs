using UnityEngine;

public sealed partial class EnemySystem
{
    public EnemyBullet FireStolenPlayerSword(SwordInstance sword)
    {
        Vector2 position = sword.Root.position;
        var projectile = new EnemyBullet
        {
            PlayerSword = sword, Previous = position, Target = player.Position,
            Velocity = (player.Position-position).normalized*16, HomingSword = true
        };
        sword.State = SwordState.EnemyFlying;
        sword.Trail.Clear();
        sword.Trail.time = .23f;
        sword.Trail.startColor = new Color(1,.08f,.12f,.8f);
        sword.Trail.endColor = new Color(.65f,.015f,.04f,0);
        sword.Trail.emitting = true;
        bullets.Add(projectile);
        return projectile;
    }

    void AdvanceStolenPlayerSword(int index, float dt)
    {
        EnemyBullet projectile = bullets[index];
        if (projectile.ReachedTarget)
        {
            RemoveBullet(index);
            return;
        }
        Vector2 position = projectile.Previous;
        Vector2 next = Vector2.MoveTowards(position,projectile.Target,16*dt);
        Vector2 travel = next-position;
        if (travel.sqrMagnitude > .000001f)
            projectile.Root.rotation = Quaternion.Euler(0,0,Vector2.SignedAngle(Vector2.down,travel));
        bool terrainHit = geometry.ClipTerrain(position,next,Vector2.zero,out next);
        projectile.Root.position = next;
        if (terrainHit)
            RemoveBullet(index);
        else if (Vector2.Distance(next,projectile.Target) < .01f)
            projectile.ReachedTarget = true;
    }

    public void StopStolenPlayerSword(EnemyBullet projectile)
    {
        int index = bullets.IndexOf(projectile);
        if (index >= 0) RemoveBullet(index);
    }
}
