using UnityEngine;
using UnityEngine.UI;

public sealed class BossHealthBar : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] Image healthFill;
    [SerializeField] Image damageFill;
    EnemySystem enemies;
    EnemyState boss;
    int previousHealth;
    float damageDelay;
    float defeatTime;

    void Awake()
    {
        panel.SetActive(false);
    }

    public void Initialize(EnemySystem system) => enemies = system;

    void LateUpdate()
    {
        if (enemies == null) return;
        if (boss == null || boss.Health <= 0)
        {
            foreach (EnemyState enemy in enemies.Enemies)
            {
                if (!enemy.IsBoss || enemy == boss) continue;
                boss = enemy;
                previousHealth = boss.Health;
                damageFill.fillAmount = 1;
                defeatTime = 0;
                break;
            }
        }
        if (boss == null) return;
        if (boss.Health <= 0) defeatTime += Time.deltaTime;
        panel.SetActive(defeatTime < 2);
        float ratio = (float)boss.Health / boss.MaxHealth;
        healthFill.fillAmount = ratio;
        if (boss.Health < previousHealth) damageDelay = .4f;
        previousHealth = boss.Health;
        damageDelay = Mathf.Max(0,damageDelay-Time.deltaTime);
        if (damageDelay == 0)
            damageFill.fillAmount = Mathf.MoveTowards(damageFill.fillAmount,ratio,Time.deltaTime*.7f);
    }
}
