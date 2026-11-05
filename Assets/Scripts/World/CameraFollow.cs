using UnityEngine;

public sealed class CameraFollow : MonoBehaviour
{
    [SerializeField] Camera view;
    [SerializeField] Transform target;
    [SerializeField] Vector2 arenaSize = new(44, 13.6f);
    float shakeTimer;
    Vector3 restPosition;

    public Camera View => view;

    void LateUpdate()
    {
        view.orthographicSize = (arenaSize.y * .5f + .36f) / Mathf.Min(1, view.aspect);
        float limit = Mathf.Max(0, arenaSize.x * .5f - view.orthographicSize * view.aspect);
        restPosition = new Vector3(Mathf.Clamp(target.position.x, -limit, limit), 0, -10);
        shakeTimer = Mathf.Max(0, shakeTimer - Time.deltaTime);
        Vector2 shake = Random.insideUnitCircle * (.045f * shakeTimer / .12f);
        transform.position = restPosition + (Vector3)shake;
    }

    public void Shake() => shakeTimer = .12f;

    public void ResetView()
    {
        shakeTimer = 0;
        LateUpdate();
    }
}
