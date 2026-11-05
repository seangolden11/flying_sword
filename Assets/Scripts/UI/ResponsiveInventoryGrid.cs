using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(GridLayoutGroup))]
public sealed class ResponsiveInventoryGrid : MonoBehaviour
{
    [SerializeField] float minimumWidth = 124;
    [SerializeField] float cellHeight = 132;
    GridLayoutGroup grid;
    float previousWidth = -1;

    void OnEnable() => Rebuild();
    void OnRectTransformDimensionsChange() => Rebuild();

    void Rebuild()
    {
        if (grid == null)
            grid = GetComponent<GridLayoutGroup>();
        float width = ((RectTransform)transform).rect.width;
        if (width <= 0 || Mathf.Approximately(width, previousWidth))
            return;
        previousWidth = width;
        int columns = Mathf.Max(1, Mathf.FloorToInt((width + grid.spacing.x) / (minimumWidth + grid.spacing.x)));
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.cellSize = new Vector2((width - grid.spacing.x * (columns - 1) - grid.padding.horizontal) / columns, cellHeight);
    }
}
