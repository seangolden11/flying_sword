using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(GameStateManager), typeof(AimController), typeof(UIManager))]
public sealed class StageManager : MonoBehaviour
{
    readonly List<BallController> balls = new();
    public IReadOnlyList<BallController> Balls => balls;
    public Camera View { get; private set; }
    public TurnActors Actors { get; private set; }
    [SerializeField] Material swordTrailMaterial;
    Sprite square;

    void Awake()
    {
        View = Camera.main;
        View.transform.SetPositionAndRotation(new Vector3(0, 0, -10), Quaternion.identity);
        View.orthographic = true;
        View.backgroundColor = new Color32(14, 21, 34, 255);
        FitCamera();
        square = MakeSquare();

        GetComponent<AimController>().enabled = false;
        GetComponent<GameStateManager>().enabled = false;
        Vector2 size = TurnActors.GridSize;
        const float wallThickness = .24f;
        float wallX = (size.x + wallThickness) * .5f;
        float wallY = (size.y + wallThickness) * .5f;
        var horizontalWall = new Vector2(size.x + 2 * wallThickness, wallThickness);
        var verticalWall = new Vector2(wallThickness, size.y);
        Draw(transform, "Court", square, new Color32(24, 39, 55, 255), Vector2.zero, size, 0);
        Wall("North", new Vector2(0, wallY), horizontalWall);
        Wall("South", new Vector2(0, -wallY), horizontalWall);
        Wall("West", new Vector2(-wallX, 0), verticalWall);
        Wall("East", new Vector2(wallX, 0), verticalWall);
        Actors = GetComponent<TurnActors>();
        Actors.Initialize(square, View, swordTrailMaterial);
        Actors.ResetArena();
    }

    void Update() => FitCamera();

    void FitCamera() => View.orthographicSize = (TurnActors.GridSize.y * .5f + .36f) / Mathf.Min(1, View.aspect);

    void Wall(string label, Vector2 center, Vector2 size)
    {
        Draw(transform, label, square, new Color32(88, 119, 144, 255), center, size, 1);
    }

    internal static SpriteRenderer Draw(Transform parent, string label, Sprite sprite, Color color,
        Vector2 position, Vector2 size, int order)
    {
        var renderer = new GameObject(label).AddComponent<SpriteRenderer>();
        renderer.transform.SetParent(parent, false);
        renderer.transform.localPosition = position;
        renderer.transform.localScale = new Vector3(size.x, size.y, 1);
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = order;
        return renderer;
    }

    static Sprite MakeSquare()
    {
        var texture = Texture2D.whiteTexture;
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
            Vector2.one * .5f, texture.width);
    }
}
