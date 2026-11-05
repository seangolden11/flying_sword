using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class SurvivalSmokeCheck
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void RunBatch()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
        EditorApplication.isPlaying = true;
    }

    static void OnPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode) return;
        EditorApplication.playModeStateChanged -= OnPlayMode;
        try { Run(); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    [MenuItem("Flying Sword/Run Survival Smoke Check")]
    public static void Run()
    {
        if (!Application.isPlaying)
            throw new InvalidOperationException("Enter Play Mode before running the smoke check.");
        var arena = new GameObject("Smoke check arena");
        var cameraObject = new GameObject("Smoke check camera");
        Sprite sprite = null;
        try
        {
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 8;
            camera.transform.position = new Vector3(0, 0, -10);
            sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f);
            var actors = arena.AddComponent<TurnActors>();
            actors.Initialize(sprite, camera, null);
            actors.ResetArena();
            Check(actors.ActiveSwordCount == 3 && actors.OwnedSwordCount == 3, "Start with three swords");
            CheckWeakHoming(actors);
            actors.ResetArena();
            CheckScreenBoundary(actors, camera);
            actors.ResetArena();
            CheckReturnDamage(actors);
            actors.ResetArena();
            CheckThrowForgiveness(actors);
            actors.ResetArena();
            CheckTimedRecall(actors);
            actors.ResetArena();
            CheckRewardChoices(actors);
            actors.ResetArena();

            Call(actors, "LaunchSword", new Vector2(5, 0));
            Check(actors.ActiveSwordCount == 2, "Launch removes one orbiting sword");
            var swords = (IList)Field(actors, "swords");
            object launched = null;
            foreach (object sword in swords)
                if (sword.GetType().GetField("State").GetValue(sword).ToString() == "Flying")
                    launched = sword;
            Check(launched != null, "Flying state exists");
            Call(actors, "AdvanceFlight", launched, 1f);
            Check(launched.GetType().GetField("State").GetValue(launched).ToString() == "Planted", "Flight plants sword");
            Call(actors, "RecallSwords", new Vector2(5, 0));
            Call(actors, "AdvanceReturn", launched, 1f);
            Check(actors.ActiveSwordCount == 3, "Recall restores orbit");

            var enemies = (IList)Field(actors, "enemies");
            Set(actors, "rangedEnemyChance", 1f);
            Call(actors, "SpawnEnemy");
            var rangedRoot = (Transform)enemies[0].GetType().GetField("Root").GetValue(enemies[0]);
            rangedRoot.localPosition = new Vector2(4, 0);
            Call(actors, "LaunchSword", new Vector2(6, 0));
            foreach (object sword in swords)
                if (sword.GetType().GetField("State").GetValue(sword).ToString() == "Flying")
                    launched = sword;
            Call(actors, "AdvanceFlight", launched, .5f);
            Check(actors.EnemyCount == 0 && actors.Experience == 1 &&
                launched.GetType().GetField("State").GetValue(launched).ToString() == "Rebounding",
                "Flying sword kills ranged enemy and rebounds");
            Call(actors, "AdvanceRebound", launched, 2f);
            Check(launched.GetType().GetField("State").GetValue(launched).ToString() == "Planted",
                "Rebound ends in installation");
            actors.ResetArena();

            Call(actors, "SpawnEnemy");
            rangedRoot = (Transform)enemies[0].GetType().GetField("Root").GetValue(enemies[0]);
            rangedRoot.localPosition = new Vector2(6, 0);
            Call(actors, "UpdateSurvival", .1f);
            var bullets = (IList)Field(actors, "bullets");
            Check(bullets.Count == 1, "Ranged enemy fires a single shot in range");
            Vector3 beforeMove = rangedRoot.localPosition;
            Call(actors, "UpdateSurvival", .1f);
            Check(rangedRoot.localPosition != beforeMove && bullets.Count == 1,
                "Ranged enemy moves between shots");
            Call(actors, "UpdateBullets", 3f);
            Call(actors, "RemoveArrivedBullets");
            Check(actors.Health == 9 && bullets.Count == 0, "Arriving bullet deals one damage");
            actors.ResetArena();

            Set(actors, "rangedEnemyChance", 0f);
            Call(actors, "SpawnEnemy");
            object melee = enemies[0];
            Call(actors, "DamageEnemy", melee, 1, Vector2.up);
            Check(enemies.Count == 1 && actors.Experience == 0, "Melee survives one hit");
            Call(actors, "DamageEnemy", melee, 1, Vector2.up);
            Check(enemies.Count == 0 && actors.Experience == 1, "Melee dies on second hit and gives one XP");
            Set(actors, "rangedEnemyChance", 1f);
            for (int i = 0; i < 4; i++)
            {
                Call(actors, "SpawnEnemy");
                Call(actors, "DamageEnemy", enemies[0], 1, Vector2.up);
            }
            Call(actors, "FlushPendingSwords");
            Check(actors.Level == 2 && actors.Experience == 0 && actors.RequiredExperience == 10,
                "Five kills level up; next threshold increases by five");
            Check(actors.IsChoosingReward && actors.ActiveSwordCount == 3 && actors.OwnedSwordCount == 3,
                "Level waits for a reward instead of automatically adding a sword");
            Check(actors.ChooseReward(0, actors.RewardSelectionId), "First level reward can be chosen");
            Check(actors.ActiveSwordCount == 4 && actors.OwnedSwordCount == 4, "Selected reward adds one sword");
            for (int i = 0; i < 10; i++)
            {
                Call(actors, "SpawnEnemy");
                Call(actors, "DamageEnemy", enemies[0], 1, Vector2.up);
            }
            Call(actors, "FlushPendingSwords");
            Check(actors.ChooseReward(2, actors.RewardSelectionId), "Later level also offers a reward");
            Check(actors.Level == 3 && actors.RequiredExperience == 15 && actors.ActiveSwordCount == 5,
                "Second level increases threshold by five and adds exactly one sword");

            Call(actors, "DamagePlayer");
            Call(actors, "DamagePlayer");
            Check(actors.Health == 9, "Invulnerability blocks repeated damage");
            for (int i = 0; i < 9; i++)
            {
                Set(actors, "invulnerability", 0f);
                Call(actors, "DamagePlayer");
            }
            Check(actors.IsGameOver, "Ten damage triggers game over");
            actors.ResetArena();
            Check(actors.Health == 10 && actors.Level == 1 && actors.Experience == 0 &&
                actors.RequiredExperience == 5 && actors.ActiveSwordCount == 3 && actors.EnemyCount == 0,
                "Reset restores survival state");
            Debug.Log("Flying Sword survival smoke check PASSED");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(arena);
            UnityEngine.Object.DestroyImmediate(cameraObject);
            if (sprite != null) UnityEngine.Object.DestroyImmediate(sprite);
        }
    }

    static void CheckWeakHoming(TurnActors actors)
    {
        Call(actors, "LaunchSword", new Vector2(6, 0));
        object flying = null;
        foreach (object sword in (IList)Field(actors, "swords"))
            if (sword.GetType().GetField("State").GetValue(sword).ToString() == "Flying") flying = sword;
        var root = (Transform)flying.GetType().GetField("Root").GetValue(flying);
        Vector2 origin = root.localPosition;
        Call(actors, "SpawnEnemy");
        object enemy = ((IList)Field(actors, "enemies"))[0];
        var enemyRoot = (Transform)enemy.GetType().GetField("Root").GetValue(enemy);
        var select = actors.GetType().GetMethod("EnemyHomingTarget", Private);
        Vector2 aim = (Vector2)flying.GetType().GetField("Target").GetValue(flying);
        foreach (Vector2 offset in new[] { Vector2.left, new Vector2(3, 0), new Vector2(1, 1) })
        {
            enemyRoot.localPosition = origin + offset;
            Check((Vector2)select.Invoke(actors, new[] { flying, (object)origin }) == aim,
                "Homing ignores rear, distant, and side enemies");
        }
        enemyRoot.localPosition = origin + new Vector2(1.5f, .5f);
        Check((Vector2)select.Invoke(actors, new[] { flying, (object)origin }) == (Vector2)enemyRoot.localPosition,
            "Homing selects nearby forward enemy");
        Call(actors, "AdvanceFlight", flying, .01f);
        float turn = Vector2.Angle(Vector2.right, (Vector2)root.localPosition - origin);
        Check(turn > .1f && turn <= .61f, "Steering is bounded to 60 degrees per second");
        enemyRoot.localPosition = new Vector2(-10, -10);
        for (int i = 0; i < 100; i++)
        {
            if (flying.GetType().GetField("State").GetValue(flying).ToString() != "Flying") break;
            Call(actors, "AdvanceFlight", flying, .02f);
        }
        Check(flying.GetType().GetField("State").GetValue(flying).ToString() == "Planted",
            "A missed homing sword exhausts its original flight distance and plants");

        actors.ResetArena();
        Call(actors, "LaunchSword", new Vector2(6, 0));
        foreach (object sword in (IList)Field(actors, "swords"))
            if (sword.GetType().GetField("State").GetValue(sword).ToString() == "Flying") flying = sword;
        root = (Transform)flying.GetType().GetField("Root").GetValue(flying);
        origin = root.localPosition;
        Call(actors, "SpawnEnemy");
        enemy = ((IList)Field(actors, "enemies"))[0];
        enemyRoot = (Transform)enemy.GetType().GetField("Root").GetValue(enemy);
        enemyRoot.localPosition = origin + new Vector2(1.5f, .5f);
        Call(actors, "SpawnBullet", origin + Vector2.down);
        Call(actors, "UpdateBullets", 0f);
        Call(actors, "AdvanceFlight", flying, .01f);
        Check(root.localPosition.y < origin.y, "Bullet interception takes priority over enemy homing");
    }

    static void CheckScreenBoundary(TurnActors actors, Camera camera)
    {
        float originalAspect = camera.aspect;
        try
        {
            foreach (float aspect in new[] { .6f, 1.8f })
            {
                camera.aspect = aspect;
                actors.ResetArena();
                Call(actors, "LaunchSword", new Vector2(100, 100));
                object flying = null;
                foreach (object sword in (IList)Field(actors, "swords"))
                    if (sword.GetType().GetField("State").GetValue(sword).ToString() == "Flying") flying = sword;
                Vector2 target = (Vector2)flying.GetType().GetField("Target").GetValue(flying);
                Check(target.x <= camera.orthographicSize * aspect - .69f &&
                    target.y <= camera.orthographicSize - .69f, "Launch leaves sword-sized screen margin");
                var root = (Transform)flying.GetType().GetField("Root").GetValue(flying);
                var state = flying.GetType().GetField("State");
                state.SetValue(flying, Enum.Parse(state.FieldType, "Rebounding"));
                flying.GetType().GetField("Target").SetValue(flying, new Vector2(-100, -100));
                Call(actors, "AdvanceRebound", flying, 100f);
                Vector2 position = root.localPosition;
                Check(Mathf.Abs(position.x) <= camera.orthographicSize * aspect - .69f &&
                    Mathf.Abs(position.y) <= camera.orthographicSize - .69f && state.GetValue(flying).ToString() == "Planted",
                    "Rebounding sword plants inside portrait and landscape screen edges");
            }
        }
        finally { camera.aspect = originalAspect; }
    }

    static void CheckReturnDamage(TurnActors actors)
    {
        Call(actors, "LaunchSword", new Vector2(5, 0));
        object returning = null;
        foreach (object sword in (IList)Field(actors, "swords"))
            if (sword.GetType().GetField("State").GetValue(sword).ToString() == "Flying") returning = sword;
        Call(actors, "AdvanceFlight", returning, 1f);
        ((Transform)returning.GetType().GetField("Root").GetValue(returning)).localPosition = new Vector2(5, 0);
        Set(actors, "rangedEnemyChance", 0f);
        var enemies = (IList)Field(actors, "enemies");
        for (int i = 0; i < 2; i++)
        {
            Call(actors, "SpawnEnemy");
            var enemyRoot = (Transform)enemies[i].GetType().GetField("Root").GetValue(enemies[i]);
            enemyRoot.localPosition = new Vector2(4 - i, 0);
        }
        object first = enemies[0], second = enemies[1];
        Call(actors, "RecallSwords", new Vector2(5, 0));
        Call(actors, "AdvanceReturn", returning, .05f);
        Check((int)first.GetType().GetField("Health").GetValue(first) == 1, "Recall deals throw-equivalent damage");
        Call(actors, "AdvanceReturn", returning, .01f);
        Check((int)first.GetType().GetField("Health").GetValue(first) == 1,
            "Overlapping enemy is hit only once per recall");
        Call(actors, "AdvanceReturn", returning, 1f);
        Check((int)second.GetType().GetField("Health").GetValue(second) == 1 && actors.ActiveSwordCount == 3,
            "Recall pierces multiple enemies and reaches orbit");
        var root = (Transform)returning.GetType().GetField("Root").GetValue(returning);
        ((IList)Field(actors, "orbiting")).Remove(returning);
        root.localPosition = new Vector2(5, 0);
        Call(actors, "PlantSword", returning);
        Call(actors, "RecallSwords", new Vector2(5, 0));
        Call(actors, "AdvanceReturn", returning, 1f);
        Check(actors.EnemyCount == 0 && actors.Experience == 2, "Next recall can hit the same enemies again and award XP");

        actors.ResetArena();
        Call(actors, "LaunchSword", new Vector2(5, 0));
        foreach (object sword in (IList)Field(actors, "swords"))
            if (sword.GetType().GetField("State").GetValue(sword).ToString() == "Flying") returning = sword;
        Call(actors, "AdvanceFlight", returning, 1f);
        ((Transform)returning.GetType().GetField("Root").GetValue(returning)).localPosition = new Vector2(5, 0);
        Call(actors, "SpawnEnemy");
        first = enemies[0];
        ((Transform)first.GetType().GetField("Root").GetValue(first)).localPosition = new Vector2(4, 0);
        Call(actors, "SpawnBullet", new Vector2(5.5f, 0));
        Call(actors, "UpdateBullets", 0f);
        Call(actors, "TryStartAutoIntercept", returning);
        Call(actors, "AdvanceAutoIntercept", returning, .1f);
        Call(actors, "AdvanceReturn", returning, 1f);
        Check((int)first.GetType().GetField("Health").GetValue(first) == 1 && actors.ActiveSwordCount == 3,
            "Automatic return after interception also deals damage");
    }

    static void CheckThrowForgiveness(TurnActors actors)
    {
        Call(actors, "LaunchSword", new Vector2(5, 0));
        object flying = null;
        foreach (object sword in (IList)Field(actors, "swords"))
            if (sword.GetType().GetField("State").GetValue(sword).ToString() == "Flying") flying = sword;
        Vector2 target = (Vector2)flying.GetType().GetField("Target").GetValue(flying);
        Check(Vector2.Distance(target, new Vector2(5.8f, 0)) < .001f, "Manual throw extends 0.8 past cursor");
        Set(actors, "rangedEnemyChance", 1f);
        Call(actors, "SpawnEnemy");
        object enemy = ((IList)Field(actors, "enemies"))[0];
        var enemyRoot = (Transform)enemy.GetType().GetField("Root").GetValue(enemy);
        enemyRoot.localPosition = new Vector2(5.65f, 0);
        Call(actors, "AdvanceFlight", flying, 1f);
        Check(actors.EnemyCount == 0, "Throw hits enemy just beyond cursor instead of planting short");

        actors.ResetArena();
        Call(actors, "LaunchSword", new Vector2(6, 0));
        foreach (object sword in (IList)Field(actors, "swords"))
            if (sword.GetType().GetField("State").GetValue(sword).ToString() == "Flying") flying = sword;
        var root = (Transform)flying.GetType().GetField("Root").GetValue(flying);
        root.localPosition = new Vector2(4, 0);
        Call(actors, "SpawnEnemy");
        enemy = ((IList)Field(actors, "enemies"))[0];
        enemyRoot = (Transform)enemy.GetType().GetField("Root").GetValue(enemy);
        enemyRoot.localPosition = new Vector2(3.5f, .4f);
        Call(actors, "AdvanceFlight", flying, .01f);
        Check(actors.EnemyCount == 0, "Visible trailing blade can hit after tip passes enemy");
    }

    static void CheckTimedRecall(TurnActors actors)
    {
        Call(actors, "LaunchSword", new Vector2(5, 0));
        object sword = null;
        foreach (object candidate in (IList)Field(actors, "swords"))
            if (candidate.GetType().GetField("State").GetValue(candidate).ToString() == "Flying") sword = candidate;
        Call(actors, "AdvanceFlight", sword, 1f);
        var state = sword.GetType().GetField("State");
        Check(!actors.IsAutoInterceptOn, "Timed recall test has interception disabled");
        Call(actors, "AdvancePlanted", sword, 4.9f);
        Check(state.GetValue(sword).ToString() == "Planted", "Sword stays planted before five seconds");
        Call(actors, "AdvancePlanted", sword, .11f);
        Check(state.GetValue(sword).ToString() == "Returning", "Five seconds starts return without interception");
        Call(actors, "AdvanceReturn", sword, 1f);
        Check(actors.ActiveSwordCount == 3, "Timed recall rejoins orbit");
        Call(actors, "PlantSword", sword);
        Call(actors, "AdvancePlanted", sword, 3f);
        Call(actors, "PlantSword", sword);
        Call(actors, "AdvancePlanted", sword, 3f);
        Check(state.GetValue(sword).ToString() == "Planted", "Reinstallation resets the five-second timer");
    }

    static void CheckRewardChoices(TurnActors actors)
    {
        float originalTimeScale = Time.timeScale;
        try
        {
            for (int choice = 0; choice < 3; choice++)
            {
                actors.ResetArena();
                foreach (TurnActors.Skill skill in Enum.GetValues(typeof(TurnActors.Skill)))
                    Check(!actors.IsSkillUnlocked(skill), "All five skills start locked");
                Check(actors.RewardOptionCount == 3, "Three reward options are available");
                Call(actors, "LaunchSword", new Vector2(5, 0));
                object planted = null;
                foreach (object sword in (IList)Field(actors, "swords"))
                    if (sword.GetType().GetField("State").GetValue(sword).ToString() == "Flying") planted = sword;
                Call(actors, "AdvanceFlight", planted, 1f);
                var root = (Transform)planted.GetType().GetField("Root").GetValue(planted);
                Vector3 position = root.localPosition;
                float remaining = (float)planted.GetType().GetField("AutoRecallRemaining").GetValue(planted);
                float spawnBefore = (float)Field(actors, "spawnTimer");
                Time.timeScale = .5f;
                Call(actors, "GainExperience", 7d);
                Check(actors.Level == 2 && actors.Experience == 2 && actors.OwnedSwordCount == 3 &&
                    actors.PendingRewardCount == 1 && Time.timeScale == 0, "Level pauses, preserves excess XP, and grants nothing yet");
                Call(actors, "Update");
                Check(root.localPosition == position && (float)Field(actors, "spawnTimer") == spawnBefore &&
                    (float)planted.GetType().GetField("AutoRecallRemaining").GetValue(planted) == remaining,
                    "Combat and recall timers do not advance while choosing");
                int selectionId = actors.RewardSelectionId;
                Check(!actors.ChooseReward(-1, selectionId) && actors.IsChoosingReward, "Invalid choice does not consume reward");
                Check(actors.ChooseReward(choice, selectionId) && actors.OwnedSwordCount == 4 && actors.ActiveSwordCount == 3,
                    "Each option grants exactly one orbiting sword without recalling planted swords");
                Check(!actors.ChooseReward(choice, selectionId) && actors.OwnedSwordCount == 4,
                    "Repeated selection cannot grant twice");
                Check(!actors.IsChoosingReward && Time.timeScale == .5f && root.localPosition == position &&
                    planted.GetType().GetField("State").GetValue(planted).ToString() == "Planted",
                    "Selection restores previous time scale and preserves installed sword");
            }
            actors.ResetArena();
            Time.timeScale = 1;
            Call(actors, "GainExperience", 37d);
            Check(actors.Level == 4 && actors.Experience == 7 && actors.RequiredExperience == 20 &&
                actors.PendingRewardCount == 3 && actors.OwnedSwordCount == 3, "Multiple levels queue one reward each");
            int oldId = actors.RewardSelectionId;
            Check(actors.ChooseReward(1, oldId), "First queued reward applies");
            Check(!actors.ChooseReward(1, oldId) && actors.PendingRewardCount == 2 && Time.timeScale == 0,
                "A stale click cannot select the next queued reward");
            actors.enabled = false;
            Check(Time.timeScale == 1, "Disabling component releases its pause");
            actors.enabled = true;
            Check(Time.timeScale == 0, "Re-enabling preserves pending reward pause");
            Check(actors.ChooseReward(0, actors.RewardSelectionId) && Time.timeScale == 0,
                "Combat stays paused until all queued rewards are chosen");
            Check(actors.ChooseReward(2, actors.RewardSelectionId) && Time.timeScale == 1 && actors.OwnedSwordCount == 6 &&
                actors.ActiveSwordCount == 6 && !actors.IsChoosingReward, "Three choices grant exactly three swords and resume");
            actors.UnlockSkill(TurnActors.Skill.AutoIntercept);
            Check(actors.IsSkillUnlocked(TurnActors.Skill.AutoIntercept), "Skill unlock state can be set for future items");
            actors.ResetArena();
            Check(!actors.IsSkillUnlocked(TurnActors.Skill.AutoIntercept), "Internal initialization resets skill locks");
        }
        finally { Time.timeScale = originalTimeScale; }
    }

    static object Field(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    static void Call(object target, string name, params object[] arguments) =>
        target.GetType().GetMethod(name, Private | BindingFlags.Static).Invoke(target, arguments);
    static void Check(bool success, string message)
    {
        if (!success) throw new Exception("Survival smoke check failed: " + message);
    }
}
