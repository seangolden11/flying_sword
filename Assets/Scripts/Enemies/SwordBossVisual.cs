using UnityEngine;

public sealed class SwordBossVisual : MonoBehaviour
{
    public Transform[] blades;
    float elapsed;
    float facing = -1;
    Vector2 velocity;
    bool swordAway;
    BulletView displayedSword;

    void OnEnable()
    {
        elapsed = 0;
        velocity = Vector2.zero;
        displayedSword = null;
        swordAway = false;
        foreach (Transform blade in blades)
            blade.gameObject.SetActive(false);
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

    public void BindSword(BulletView sword) => displayedSword = sword;

    public void SetSwordAway(bool away)
    {
        swordAway = away;
        if (!away && displayedSword != null)
        {
            UpdateSwordPose();
            displayedSword.gameObject.SetActive(true);
        }
    }

    void UpdateSwordPose()
    {
        if (displayedSword != null && blades.Length > 0)
            displayedSword.transform.SetPositionAndRotation(blades[0].position, blades[0].rotation);
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
        UpdateSwordPose();
    }
}
