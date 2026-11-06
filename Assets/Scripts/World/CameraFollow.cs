using UnityEngine;

public sealed class CameraFollow : MonoBehaviour
{
    [SerializeField] Camera view;
    [SerializeField] Transform target;
    [SerializeField] Vector2 arenaSize = new(44, 13.6f);
    float shakeTimer;
    Vector3 restPosition;
    Vector2 lootFocusPosition;
    float lootFocusProgress;
    float lootFocusBlend;
    int lootFocusFrame = -10;

    public Camera View => view;

    void LateUpdate()
    {
        float normalSize = (arenaSize.y * .5f + .36f) / Mathf.Min(1, view.aspect);
        float requested = Time.frameCount-lootFocusFrame <= 1 && Time.timeScale > 0 ? lootFocusProgress : 0;
        lootFocusBlend = Mathf.MoveTowards(lootFocusBlend,requested,Time.unscaledDeltaTime*2);
        view.orthographicSize = Mathf.Lerp(normalSize,2.2f,lootFocusBlend);
        float limit = Mathf.Max(0, arenaSize.x * .5f - view.orthographicSize * view.aspect);
        restPosition = new Vector3(Mathf.Clamp(target.position.x, -limit, limit), 0, -10);
        shakeTimer = Mathf.Max(0, shakeTimer - Time.deltaTime);
        Vector2 shake = Random.insideUnitCircle * (.045f * shakeTimer / .12f);
        Vector3 focus = new(lootFocusPosition.x,lootFocusPosition.y,-10);
        transform.position = Vector3.Lerp(restPosition,focus,lootFocusBlend) + (Vector3)shake;
    }

    public void FocusLoot(Vector2 position, float progress)
    {
        lootFocusPosition = position;
        lootFocusProgress = Mathf.SmoothStep(0,1,Mathf.Clamp01(progress));
        lootFocusFrame = Time.frameCount;
    }

    public void Shake() => shakeTimer = .12f;

    public void ResetView()
    {
        shakeTimer = 0;
        lootFocusFrame = -10;
        lootFocusBlend = 0;
        LateUpdate();
    }
}
