using UnityEngine;
using UnityEngine.InputSystem;

public sealed class TutorialLesson
{
    readonly StageManager stage;
    readonly StageGeometry geometry;
    readonly Vector2 start;
    int step;
    float shotTimer;

    public TutorialLesson(StageManager stage, StageGeometry geometry)
    {
        this.stage = stage;
        this.geometry = geometry;
        start = stage.Player.Position;
    }

    public string Hint => step switch
    {
        0 => "1 / 4   WASD로 검을 타고 이동하세요",
        1 => "2 / 4   마우스로 조준하고 좌클릭으로 검을 발사하세요",
        2 => "3 / 4   꽂힌 검에 커서를 대고 우클릭으로 회수하세요",
        3 => "4 / 4   날아오는 탄을 향해 검을 쏴서 요격하세요",
        _ => "훈련 완료   Enter 보스전   ·   Esc 메뉴"
    };

    public void Tick(float dt)
    {
        switch (step)
        {
            case 0:
                if ((stage.Player.Position - start).sqrMagnitude >= 2.25f) step++;
                break;
            case 1:
                if (stage.Swords.HasPlantedSword) step++;
                break;
            case 2:
                if (stage.Swords.ActiveSwordCount > 0) step++;
                break;
            case 3:
                if (stage.Enemies.PracticeShotIntercepted)
                {
                    stage.Enemies.ClearBullets();
                    step++;
                    break;
                }
                shotTimer -= dt;
                if (shotTimer > 0) break;
                Vector2 rider = stage.Player.Position;
                float direction = rider.x < geometry.transform.position.x ? 1 : -1;
                stage.Enemies.FirePracticeShot(rider + Vector2.right * (6 * direction));
                shotTimer = 2;
                break;
            default:
                if (Keyboard.current?.enterKey.wasPressedThisFrame == true)
                    SceneRouter.Boss();
                break;
        }
    }
}
