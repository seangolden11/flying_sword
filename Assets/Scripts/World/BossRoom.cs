using UnityEngine;

public sealed class BossRoom : MonoBehaviour
{
    [SerializeField] PlayerController player;
    [SerializeField] EnemySystem enemies;
    [SerializeField] EnemyPlacement bossPlacement;
    [SerializeField] GameObject entranceDoor;
    [SerializeField] float entryX = 45;
    EnemyState boss;
    public bool Entered { get; private set; }
    public bool Cleared { get; private set; }

    void Start() => entranceDoor.SetActive(false);
    void Update() => Tick();

    public void Tick()
    {
        if (Time.timeScale <= 0 || player.IsDead || Cleared)
            return;
        if (!Entered && player.Position.x >= entryX)
        {
            Entered = true;
            entranceDoor.SetActive(true);
            Physics2D.SyncTransforms();
            enemies.ClearBullets();
            boss = enemies.SpawnPlaced(bossPlacement);
        }
        if (Entered && boss != null && boss.Health <= 0)
        {
            Cleared = true;
            entranceDoor.SetActive(false);
            enemies.ClearBullets();
        }
    }
}
