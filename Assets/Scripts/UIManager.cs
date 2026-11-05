using UnityEngine;

public sealed class UIManager : MonoBehaviour
{
    static readonly Color Background = new Color32(14, 21, 34, 235);
    static readonly Color Text = new Color32(226, 236, 247, 255);
    static readonly Color KeyColor = new Color32(113, 248, 225, 255);
    GUIStyle keyLabel, controlLabel, burstLabel;
    Font hudFont;

    void OnGUI()
    {
        var actors = GetComponent<StageManager>().Actors;
        if (actors == null)
            return;

        actors.DrawFusionCounts();
        if (hudFont == null)
            hudFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR" }, 14);

        DrawControls(actors);
        DrawEnemyBurstGauge(actors);
    }

    void DrawControls(TurnActors actors)
    {
        keyLabel ??= Label(14, KeyColor, FontStyle.Bold);
        controlLabel ??= Label(13, Text);
        controlLabel.alignment = TextAnchor.MiddleLeft;
        float width = Mathf.Min(262, Screen.width - 24);
        Fill(new Rect(12, 12, width, 199), Background);
        DrawControl(0, "1", "꾹 누르기 · 검 합체");
        DrawControl(1, "2", $"자동 요격 · {(actors.IsAutoInterceptOn ? "켜짐" : "꺼짐")}");
        DrawControl(2, "3", "커서 주변 교차 참격");
        DrawControl(3, "4", "꾹 누르기 · 검막");
        DrawControl(4, "5", $"이기어검 · {(actors.IsFollowingCursor ? "켜짐" : "꺼짐")}");
        GUI.Label(new Rect(22, 132, width - 20, 22), "좌클릭 발사 · 우클릭 회수 (꾹 가능)", controlLabel);
        GUI.Label(new Rect(22, 155, width - 20, 22),
            $"R 초기화     사용 가능 검 {actors.ActiveSwordCount}/{TurnActors.SwordCount}", controlLabel);
        GUI.Label(new Rect(22, 177, width - 20, 22), "WASD · 검 타고 이동", controlLabel);
    }

    void DrawControl(int row, string key, string action)
    {
        float y = 21 + row * 22;
        Fill(new Rect(22, y + 2, 21, 18), new Color32(35, 58, 72, 255));
        GUI.Label(new Rect(22, y, 21, 22), key, keyLabel);
        GUI.Label(new Rect(50, y, 194, 22), action, controlLabel);
    }

    void DrawEnemyBurstGauge(TurnActors actors)
    {
        burstLabel ??= Label(14, Text, FontStyle.Bold);
        float width = Mathf.Min(280, Screen.width - 24);
        float x = (Screen.width - width) * .5f;
        float y = Screen.height - 28;
        if (actors.EnemyHealth == 0)
        {
            GUI.Label(new Rect(x, y - 24, width, 22), $"적 재등장까지 {actors.EnemyRespawnTime:0.0}초", burstLabel);
            return;
        }

        bool firing = actors.IsEnemyFiringBurst;
        float charge = firing ? 1 : actors.EnemyBurstCharge;
        var color = firing || charge >= .8f
            ? new Color32(255, 105, 86, 255) : new Color32(240, 169, 97, 255);

        Fill(new Rect(x, y, width, 12), Background);
        Fill(new Rect(x + 2, y + 2, (width - 4) * charge, 8), color);
        GUI.Label(new Rect(x, y - 24, width, 22),
            firing ? $"적 HP {actors.EnemyHealth} · 탄막 발사 중" :
                $"적 HP {actors.EnemyHealth} · 탄막까지 {actors.SecondsUntilEnemyBurst:0.0}초", burstLabel);
    }

    static void Fill(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    GUIStyle Label(int size, Color color, FontStyle weight = FontStyle.Normal)
    {
        var style = new GUIStyle(GUI.skin.label)
        {
            font = hudFont, fontSize = size, fontStyle = weight, alignment = TextAnchor.MiddleCenter
        };
        style.normal.textColor = color;
        return style;
    }
}
