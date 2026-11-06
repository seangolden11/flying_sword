using UnityEngine;

public sealed class SwordBossVisual : MonoBehaviour
{
    public Transform[] blades;
    float elapsed;
    float facing = -1;
    Vector2 velocity;
    bool swordAway;

    void OnEnable()
    {
        elapsed = 0;
        velocity = Vector2.zero;
        SetSwordAway(false);
        if (blades.Length > 0)
        {
            blades[0].localPosition = new Vector2(-facing*.24f,.3f);
            blades[0].localRotation = RestAngle();
        }
    }

    public void Face(Vector2 target)
    {
        float direction = target.x-transform.position.x;
        if (Mathf.Abs(direction) > .3f) facing = Mathf.Sign(direction);
    }

    public void SetSwordAway(bool away)
    {
        swordAway = away;
        for (int i = 0; i < blades.Length; i++)
            blades[i].gameObject.SetActive(i == 0 && !away);
    }

    Quaternion RestAngle() => Quaternion.Euler(0,0,
        Vector2.SignedAngle(Vector2.down,Vector2.right*facing));

    void Update()
    {
        if (swordAway || blades.Length == 0) return;
        elapsed += Time.deltaTime;
        Vector2 target = new(-facing*.24f+Mathf.Sin(elapsed*1.1f)*.012f,
            .3f+Mathf.Sin(elapsed*1.6f)*.02f);
        blades[0].localPosition = Vector2.SmoothDamp(blades[0].localPosition,target,
            ref velocity,.14f,10,Time.deltaTime);
        blades[0].localRotation = Quaternion.Slerp(blades[0].localRotation,RestAngle(),1-Mathf.Exp(-10*Time.deltaTime));
    }
}
