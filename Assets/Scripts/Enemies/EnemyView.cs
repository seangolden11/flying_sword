using UnityEngine;
using UnityEngine.UI;

public sealed class EnemyView : MonoBehaviour
{
    public SpriteRenderer body;
    public SpriteRenderer core;
    [SerializeField] Text healthText;
    [SerializeField] Image healthFill;
    [SerializeField] Image burstFill;
    [SerializeField] Text statusText;
    [SerializeField] GameObject lockMarker;
    [SerializeField] LineRenderer detectionCircle;
    [SerializeField] LineRenderer sightLine;
    float drawnRadius = -1;

    public void Refresh(EnemyState enemy, EnemySystem system, Vector2 player, bool showRange,
        float range, Camera camera)
    {
        healthText.gameObject.SetActive(!enemy.IsBoss);
        healthFill.transform.parent.gameObject.SetActive(!enemy.IsBoss);
        burstFill.transform.parent.gameObject.SetActive(!enemy.IsBoss);
        statusText.gameObject.SetActive(!enemy.IsBoss);
        float health = (float)enemy.Health / enemy.MaxHealth;
        float flash = enemy.HitTimer / .22f;
        Color damaged = Color.Lerp(new Color32(77, 52, 64, 255), new Color32(225, 77, 109, 255), health);
        body.color = Color.Lerp(damaged, Color.white, flash);
        core.color = Color.Lerp(damaged, new Color32(255, 184, 174, 255), health);
        core.transform.localScale = Vector3.one * (.15f + .14f * health + .15f * flash);
        healthText.text = $"{(enemy.IsBoss ? "BOSS" : "HP")} {enemy.Health}/{enemy.MaxHealth}";
        healthFill.fillAmount = health;
        healthFill.color = Color.Lerp(new Color32(255, 99, 89, 255), new Color32(113, 248, 225, 255), health);
        burstFill.fillAmount = enemy.BurstCharge(system.BurstInterval);
        burstFill.color = enemy.IsFiring ? new Color32(255, 105, 86, 255) : new Color32(240, 169, 97, 255);
        statusText.text = enemy.AlertedByDamage ? "피격 인지 · 교전 중" :
            !enemy.IsInRange ? "범위 밖" : !enemy.HasLineOfSight ? "시야 차단" :
            enemy.IsFiring ? "특공 발사 중" : $"특공 {Mathf.Max(0, enemy.BurstTimer):0.0}초";
        if (enemy.IsBoss && enemy.BossSword != null)
        {
            EnemyBullet sword = enemy.BossSword;
            burstFill.fillAmount = sword.State == BossSwordState.Ready || sword.State == BossSwordState.Returning
                ? 1 : sword.State == BossSwordState.Flying ? 0 : 1 - sword.RecallTime / 3;
            statusText.text = sword.State switch
            {
                BossSwordState.Flying => "검 발사 중",
                BossSwordState.Falling or BossSwordState.Planted => "검 회수 대기",
                BossSwordState.Returning => "검 회수 중",
                _ => enemy.IsAware ? "검 발사 준비" : "대기 중"
            };
        }
        detectionCircle.enabled = sightLine.enabled = showRange && !enemy.IsBoss;
        if (!showRange || enemy.IsBoss)
            return;
        float localRange = range / Mathf.Max(.001f, Mathf.Abs(transform.lossyScale.x));
        if (!Mathf.Approximately(drawnRadius, localRange))
        {
            drawnRadius = localRange;
            const int count = 96;
            detectionCircle.positionCount = count;
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2 / count;
                detectionCircle.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * localRange);
            }
        }
        Color color = enemy.AlertedByDamage ? new Color(1, .39f, .39f, .25f) :
            !enemy.IsInRange ? new Color(.55f, .6f, .66f, .25f) :
            enemy.HasLineOfSight ? new Color(1, .39f, .39f, .25f) : new Color(1, .74f, .31f, .25f);
        detectionCircle.startColor = detectionCircle.endColor = color;
        sightLine.startColor = sightLine.endColor = color;
        sightLine.SetPosition(0, transform.position);
        sightLine.SetPosition(1, player);
    }

    public void SetLocked(bool locked)
    {
        if (lockMarker.activeSelf != locked)
            lockMarker.SetActive(locked);
    }
}
