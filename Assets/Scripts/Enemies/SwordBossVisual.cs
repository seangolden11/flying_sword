using UnityEngine;

public sealed class SwordBossVisual : MonoBehaviour
{
    public Transform[] blades;
    float elapsed;
    float facing;

    public Quaternion SwordRotation => Quaternion.Euler(0, 0, 90 * facing);
    public Vector2 RecallAnchor => transform.TransformPoint(new Vector3(-facing * .24f, .3f, 0));

    void OnEnable()
    {
        elapsed = 0;
        facing = -1;
        foreach (Transform blade in blades)
            blade.gameObject.SetActive(false);
    }

    public void UpdateFormation(Vector2 target, float dt)
    {
        elapsed += dt;
        float direction = target.x - transform.position.x;
        if (Mathf.Abs(direction) > .3f)
            facing = Mathf.Sign(direction);
    }

    public Vector2 SwordPosition(int slot, int count) =>
        (Vector2)transform.position + EllipticSwordFormation.Offset(slot, count, elapsed, facing);
}
