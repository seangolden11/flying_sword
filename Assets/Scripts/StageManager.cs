using UnityEngine;

public sealed class StageManager : MonoBehaviour
{
    [SerializeField] StageGeometry geometry;
    [SerializeField] CameraFollow cameraFollow;
    [SerializeField] PlayerController player;
    [SerializeField] SwordSystem swordSystem;
    [SerializeField] EnemySystem enemySystem;
    [SerializeField] EquipmentSystem equipment;
    [SerializeField] UIManager ui;
    [SerializeField] bool tutorial;
    public TutorialLesson Tutorial { get; private set; }

    public PlayerController Player => player;
    public SwordSystem Swords => swordSystem;
    public EnemySystem Enemies => enemySystem;
    public EquipmentSystem Equipment => equipment;

    void Start()
    {
        StoryProgress.RecordStageEntry();
        Time.timeScale = 1;
        player.Initialize(geometry, cameraFollow);
        equipment.Initialize(swordSystem);
        enemySystem.Initialize(geometry, player, cameraFollow.View);
        swordSystem.Initialize(geometry, player, enemySystem, equipment, cameraFollow);
        player.ResetPlayer();
        enemySystem.ResetEnemies();
        swordSystem.ResetSwords();
        if (tutorial) Tutorial = new TutorialLesson(this, geometry);
        ui.Initialize(this);
    }

    void Update()
    {
        if (Time.deltaTime <= 0 || player.IsDead)
            return;
        float dt = Time.deltaTime;
        player.Tick(dt);
        enemySystem.Tick(dt);
        swordSystem.Tick(dt);
        Tutorial?.Tick(dt);
        foreach (EnemyState enemy in enemySystem.Enemies)
            enemy.View.SetLocked(enemy == swordSystem.LockedEnemy);
    }
}
