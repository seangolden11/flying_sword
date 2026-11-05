using UnityEngine;

public sealed class EnemyTarget : MonoBehaviour
{
    public int Health { get; private set; }

    public void Initialize(int health, PhysicsMaterial2D material)
    {
        Health = health;
        var collider = gameObject.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;
        collider.sharedMaterial = material;
    }

    public void TakeHit()
    {
        if (Health == 0)
            return;

        Health--;
        if (Health == 0)
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
