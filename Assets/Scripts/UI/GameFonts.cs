using UnityEngine;
using UnityEngine.UI;

public static class GameFonts
{
    static Font body;
    public static Font Body
    {
        get
        {
            if (body == null)
                body = Font.CreateDynamicFontFromOSFont(
                    new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 18);
            return body;
        }
    }

    public static void Apply(Transform root)
    {
        foreach (Text text in root.GetComponentsInChildren<Text>(true))
            text.font = Body;
    }
}
