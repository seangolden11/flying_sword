using UnityEngine;

public sealed partial class UIManager : MonoBehaviour
{
    static readonly Color Background = new Color32(14, 21, 34, 235);
    static readonly Color Text = new Color32(226, 236, 247, 255);
    static readonly Color KeyColor = new Color32(113, 248, 225, 255);
    GUIStyle keyLabel, controlLabel, burstLabel;
    Font hudFont;

    void OnGUI()
    {
        var actors = GetComponent<StageManager>()?.Actors;
        if (actors == null)
            return;

        if (hudFont == null)
            hudFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR" }, 14);

        if (actors.IsChoosingReward)
            return;
        actors.DrawFusionCounts();
        DrawControls(actors);
        DrawSurvival(actors);
        DrawInventory(actors);
    }

    void DrawControls(TurnActors actors)
    {
        keyLabel ??= Label(14, KeyColor, FontStyle.Bold);
        controlLabel ??= Label(13, Text);
        controlLabel.alignment = TextAnchor.MiddleLeft;
        float width = Mathf.Min(262, Screen.width - 24);
        Fill(new Rect(12, 12, width, 177), Background);
        DrawControl(0, "1", actors.IsSkillUnlocked(TurnActors.Skill.Fusion) ? "꾹 누르기 · 검 합체" : "검 합체 · 잠김");
        DrawControl(1, "2", actors.IsSkillUnlocked(TurnActors.Skill.AutoIntercept) ? $"자동 요격 · {(actors.IsAutoInterceptOn ? "켜짐" : "꺼짐")}" : "자동 요격 · 잠김");
        DrawControl(2, "3", actors.IsSkillUnlocked(TurnActors.Skill.CrossSlash) ? "커서 주변 교차 참격" : "교차 참격 · 잠김");
        DrawControl(3, "4", actors.IsSkillUnlocked(TurnActors.Skill.Guard) ? "꾹 누르기 · 검막" : "검막 · 잠김");
        DrawControl(4, "5", actors.IsSkillUnlocked(TurnActors.Skill.CursorFollow) ? "이기어검" : "이기어검 · 잠김");
        GUI.Label(new Rect(22, 132, width - 20, 22), "좌클릭 발사 · 우클릭 회수 (꾹 가능)", controlLabel);
        GUI.Label(new Rect(22, 155, width - 20, 22),
            $"공전 무기 {actors.ActiveSwordCount} · 보유 {actors.OwnedSwordCount}", controlLabel);
    }

    void DrawControl(int row, string key, string action)
    {
        float y = 21 + row * 22;
        Fill(new Rect(22, y + 2, 21, 18), new Color32(35, 58, 72, 255));
        GUI.Label(new Rect(22, y, 21, 22), key, keyLabel);
        GUI.Label(new Rect(50, y, 194, 22), action, controlLabel);
    }

    void DrawSurvival(TurnActors actors)
    {
        burstLabel ??= Label(14, Text, FontStyle.Bold);
        float width = Mathf.Min(280, Screen.width - 24);
        float x = (Screen.width - width) * .5f;
        float y = Screen.height - 28;
        float charge = (float)(actors.Experience / actors.RequiredExperience);
        var color = new Color32(113, 248, 225, 255);

        Fill(new Rect(x, y, width, 12), Background);
        Fill(new Rect(x + 2, y + 2, (width - 4) * charge, 8), color);
        GUI.Label(new Rect(x, y - 24, width, 22),
            $"Lv {actors.Level}   EXP {actors.Experience:0}/{actors.RequiredExperience:0}", burstLabel);
        GUI.Label(new Rect(x, y - 48, width, 22), $"HP {actors.Health}/10   적 {actors.EnemyCount}", burstLabel);
        if (actors.IsGameOver)
        {
            var panel = new Rect((Screen.width - 300) * .5f, (Screen.height - 100) * .5f, 300, 100);
            Fill(panel, Background);
            GUI.Label(new Rect(panel.x, panel.y + 15, panel.width, 30), "게임 오버", burstLabel);
        }
    }

    void DrawInventory(TurnActors actors)
    {
        if (Screen.width < 650) return;
        float height = Mathf.Min(260, Screen.height * .4f);
        Rect panel = new Rect(Screen.width - 222, 12, 210, height);
        Fill(panel, Background);
        GUILayout.BeginArea(new Rect(panel.x + 8, panel.y + 6, panel.width - 16, panel.height - 12));
        GUILayout.Label($"보유 무기 {actors.OwnedSwordCount}", Label(14, KeyColor, FontStyle.Bold));

        for (int i = 0; i < Mathf.Min(3, actors.WeaponOptionCount); i++)
            GUILayout.Label(actors.GetWeaponDescription(i), Label(12, Text));
        if (actors.WeaponOptionCount > 3) GUILayout.Label($"외 {actors.WeaponOptionCount - 3}개 · 보상 대상 선택에서 전체 목록", Label(11, Text));
        GUILayout.EndArea();
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
