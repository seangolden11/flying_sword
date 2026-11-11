using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-100)]
public sealed class UIManager : MonoBehaviour
{
    public const bool InventoryEnabled = false;
    [SerializeField] bool titleScreen;
    [SerializeField] GameObject titlePanel;
    [SerializeField] GameObject hud;
    [SerializeField] GameObject pausePanel;
    [SerializeField] GameObject defeatPanel;
    [SerializeField] InventoryPanel inventory;
    [SerializeField] Text playerHealth;
    [SerializeField] Image playerHealthFill;
    [SerializeField] Text swordCount;
    [SerializeField] Text waveTimer;
    StageManager stage;
    float resumeTimeScale = 1;
    bool paused;

    void Awake()
    {
        Time.timeScale = 1;
        var input = GetComponentInChildren<InputSystemUIInputModule>(true);
        if (input != null)
            input.AssignDefaultActions();
        titlePanel.SetActive(titleScreen);
        hud.SetActive(!titleScreen);
        pausePanel.SetActive(false);
        defeatPanel.SetActive(false);
        inventory.gameObject.SetActive(false);
        var bagButton = hud.transform.Find("Open inventory");
        if (bagButton != null) bagButton.gameObject.SetActive(InventoryEnabled);
    }

    public void Initialize(StageManager owner)
    {
        stage = owner;
        if (!titleScreen && stage.Tutorial == null)
        {
            var prefab = Resources.Load<BossHealthBar>("UI/BossHealthBar");
            if (prefab != null)
                Instantiate(prefab, hud.transform, false).Initialize(stage.Enemies);
        }
        inventory.Initialize(stage.Equipment, stage.Swords);
        if (stage.Tutorial != null)
        {
            waveTimer.rectTransform.anchoredPosition = new Vector2(0, -62);
            waveTimer.rectTransform.sizeDelta = new Vector2(1050, 50);
            waveTimer.fontSize = 30;
        }
    }

    void Update()
    {
        if (titleScreen || stage == null)
            return;
        if (stage.Player.IsDead)
        {
            UpdateHud();
            if (!defeatPanel.activeSelf)
            {
                Pause();
                pausePanel.SetActive(false);
                inventory.gameObject.SetActive(false);
                defeatPanel.SetActive(true);
            }
            return;
        }
        var keyboard = Keyboard.current;
        if (InventoryEnabled && keyboard?.tabKey.wasPressedThisFrame == true)
        {
            if (inventory.gameObject.activeSelf)
                Resume();
            else if (!paused)
                OpenInventory();
        }
        if (keyboard?.escapeKey.wasPressedThisFrame == true)
        {
            if (paused)
                Resume();
            else
                OpenPause();
        }
        UpdateHud();
    }

    void UpdateHud()
    {
        playerHealth.text = $"HP {stage.Player.Health}/{PlayerController.MaxHealth}";
        playerHealthFill.fillAmount = (float)stage.Player.Health / PlayerController.MaxHealth;
        swordCount.text = $"사용 가능 검 {stage.Swords.ActiveSwordCount}/{stage.Equipment.EquippedCount}";
        waveTimer.text = stage.Tutorial?.Hint ?? (!stage.Enemies.UsesFixedEncounters && stage.Enemies.Enemies.Count == 0 ?
            $"다음 적 무리까지 {stage.Enemies.RespawnTime:0.0}초" : "");
    }

    void Pause()
    {
        if (paused)
            return;
        resumeTimeScale = Time.timeScale;
        paused = true;
        Time.timeScale = 0;
    }

    public void OpenInventory()
    {
        if (!InventoryEnabled || titleScreen || stage == null || stage.Player.IsDead || paused)
            return;
        stage.Swords.PrepareInventory();
        Pause();
        inventory.gameObject.SetActive(true);
        inventory.Refresh();
    }

    public void OpenPause()
    {
        Pause();
        pausePanel.SetActive(true);
    }

    public void Resume()
    {
        if (stage != null && stage.Player.IsDead)
            return;
        inventory.gameObject.SetActive(false);
        pausePanel.SetActive(false);
        paused = false;
        Time.timeScale = resumeTimeScale;
    }

    public void StartTutorial() => SceneRouter.Tutorial();
    public void StartCombat() => SceneRouter.Combat();
    public void StartMetroidvania() => SceneRouter.Metroidvania();
    public void StartBoss() => SceneRouter.Boss();
    public void StartBossChallenge() => SceneRouter.BossChallenge();
    public void ReturnToTitle() => SceneRouter.Title();
    public void RestartStage() => SceneRouter.Restart();

    void OnDisable()
    {
        if (paused)
            Time.timeScale = resumeTimeScale;
    }
}
