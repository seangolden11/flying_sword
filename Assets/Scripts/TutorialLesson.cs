using UnityEngine;
using UnityEngine.InputSystem;

public sealed class TutorialLesson
{
    readonly StageManager stage;
    readonly StageGeometry geometry;
    readonly Vector2 start;
    EnemyState instructor;
    int step;

    public TutorialLesson(StageManager stage, StageGeometry geometry)
    {
        this.stage = stage;
        this.geometry = geometry;
        start = stage.Player.Position;
    }

    public string Hint => step switch
    {
        0 => "1 / 4   WASD로 검을 타고 이동하세요",
        1 => "2 / 4   날아오는 적 검을 내 검으로 요격하세요",
        2 => "3 / 4   떨어진 적 검에 커서를 가져가 빼앗으세요",
        3 => "4 / 4   빼앗은 검이 적을 향해 자동 발사됩니다",
        _ => "훈련 완료   Enter 보스전   ·   Esc 메뉴"
    };

    public void Tick()
    {
        switch (step)
        {
            case 0:
                Vector2 rider = stage.Player.Position;
                if ((rider - start).sqrMagnitude < 2.25f) break;
                float direction = rider.x < geometry.transform.position.x ? 1f : -1f;
                instructor = stage.Enemies.SpawnPracticeSwordsman(rider + Vector2.right * (6f * direction));
                step++;
                break;
            case 1:
                if (instructor.PracticeSwordIntercepted) step++;
                break;
            case 2:
                if (instructor.BossSword?.State == BossSwordState.Stolen) step++;
                break;
            case 3:
                if (instructor.PracticeSwordRelaunched) step++;
                break;
            default:
                if (Keyboard.current?.enterKey.wasPressedThisFrame == true)
                    SceneRouter.Boss();
                break;
        }
    }
}
