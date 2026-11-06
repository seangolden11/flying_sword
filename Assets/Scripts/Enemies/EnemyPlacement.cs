using UnityEngine;

// Scene-authored enemy position. Bosses are activated by their room.
public sealed class EnemyPlacement : MonoBehaviour
{
    public bool boss;
    public bool spawnOnStart;
    [Min(1)] public int health = 30;
    [Min(.1f)] public float scale = 1;
}
