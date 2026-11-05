using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

public static partial class SurvivalSmokeCheck
{
    static object Result(object target, string name, params object[] arguments)
    {
        var method = target.GetType().GetMethod(name, Private | BindingFlags.Static);
        var parameters = method.GetParameters();
        int count = arguments.Length;
        Array.Resize(ref arguments, parameters.Length);
        for (int i = count; i < parameters.Length; i++) arguments[i] = parameters[i].DefaultValue;
        return method.Invoke(target, arguments);
    }
    static object Read(object target, string name) => target.GetType().GetField(name).GetValue(target);
    static void Write(object target, string name, object value) => target.GetType().GetField(name).SetValue(target, value);
    static Transform Root(object target) => (Transform)Read(target, "Root");
    static object TestEnemy(TurnActors actors, Vector2 position, int health = 20)
    {
        Set(actors, "rangedEnemyChance", 0f);
        Call(actors, "SpawnEnemy");
        var enemies = (IList)Field(actors, "enemies");
        object enemy = enemies[enemies.Count - 1];
        Root(enemy).localPosition = position;
        Write(enemy, "Health", health); Write(enemy, "MaxHealth", health);
        return enemy;
    }
    static object TestWeapon(TurnActors actors, TurnActors.WeaponKind kind, Vector2 position)
    {
        object weapon = Result(actors, "AddWeapon", kind);
        Root(weapon).localPosition = position;
        ((IList)Field(actors, "orbiting")).Remove(weapon);
        return weapon;
    }
    static void Grant(TurnActors actors, TurnActors.RewardEffect effect)
    {
        if (!actors.IsChoosingReward) Call(actors, "QueueLevelReward");
        Check(ChooseKnownReward(actors, effect), "Reward applies: " + effect);
    }
    static void DrainRewards(TurnActors actors)
    {
        while (actors.IsChoosingReward) Check(ChooseKnownReward(actors, TurnActors.RewardEffect.AddSword), "Drain test level reward");
    }
    static void CheckHealthDrops(TurnActors actors)
    {
        actors.ResetArena();
        Set(actors, "healthDropChance", 0f);
        Call(actors, "DamageEnemy", TestEnemy(actors, new Vector2(4, 0)), 100, Vector2.right);
        var drops = (IList)Field(actors, "healthDrops");
        Check(drops.Count == 0, "Zero chance does not drop health");
        Set(actors, "healthDropChance", 1f);
        Call(actors, "DamageEnemy", TestEnemy(actors, new Vector2(4, 0)), 100, Vector2.right);
        Check(drops.Count == 1, "Kill drops health at guaranteed chance");
        Call(actors, "DamagePlayer");
        Call(actors, "LaunchSword", new Vector2(6, 0));
        object flying = null;
        foreach (object weapon in (IList)Field(actors, "swords"))
            if (Read(weapon, "State").ToString() == "Flying") flying = weapon;
        Root(flying).localPosition = new Vector2(2, 0);
        Call(actors, "BeginFlight", flying, new Vector2(6, 0));
        Call(actors, "AdvanceFlight", flying, .2f);
        Check(drops.Count == 0 && actors.Health == 10, "Forward throw heals once while passing through pickup");
        Call(actors, "TryDropHealth", new Vector2(3, 0));
        Root(flying).localPosition = new Vector2(5, 0);
        Call(actors, "AdvanceReturn", flying, .2f);
        Check(drops.Count == 0 && actors.Health == 10, "Return consumes pickup even at full health and respects HP cap");
        Call(actors, "TryDropHealth", new Vector2(4, 0));
        Call(actors, "UpdateHealthDrops", 0f);
        Check(drops.Count == 1, "Paused pickup lifetime does not advance");
        Call(actors, "UpdateHealthDrops", 20f);
        Check(drops.Count == 0, "Health pickup expires after twenty combat seconds");
        Call(actors, "TryDropHealth", new Vector2(4, 0));
        actors.ResetArena();
        Check(drops.Count == 0, "Reset clears health drops");
        Set(actors, "healthDropChance", .1f);
    }
    static void CheckIntegratedWeapons(TurnActors actors)
    {
        foreach (TurnActors.WeaponKind kind in new[] { TurnActors.WeaponKind.Shard, TurnActors.WeaponKind.Trash,
            TurnActors.WeaponKind.Table, TurnActors.WeaponKind.Monitor, TurnActors.WeaponKind.Fencing, TurnActors.WeaponKind.Washer })
        {
            actors.ResetArena();
            object weapon = TestWeapon(actors, kind, new Vector2(2, 0));
            var blade = (SpriteRenderer)Read(weapon, "Blade");
            if (GameArt.Weapon(kind) != null)
                Check(Mathf.Abs(Mathf.Max(blade.sprite.bounds.size.x, blade.sprite.bounds.size.y) * blade.transform.localScale.x - 1.5f) < .001f,
                    kind + " art is normalized to 1.5 world units independent of source pixels");
            object first = TestEnemy(actors, new Vector2(4, 0));
            object second = TestEnemy(actors, kind == TurnActors.WeaponKind.Monitor ? new Vector2(4, 1) : new Vector2(5.5f, 0));
            Call(actors, "BeginFlight", weapon, new Vector2(7, 0));
            Call(actors, "AdvanceFlight", weapon, 1f);
            Check((int)Read(first, "Health") < 20, kind + " forward attack deals damage");
            if (kind == TurnActors.WeaponKind.Trash)
            {
                Call(actors, "AdvanceFlight", weapon, .5f);
                Check((int)Read(first, "Health") == 19 && (int)Read(second, "Health") == 19 && (bool)Read(weapon, "ChainUsed"),
                    "Trash chains once to a different enemy");
            }
            if (kind == TurnActors.WeaponKind.Fencing)
            {
                Check((int)Read(second, "Health") == 19, "Fencing pierces multiple enemies in one step");
                Call(actors, "BeginReturn", weapon);
                Check((int)Read(first, "Health") == 19, "No repeat damage from forward overlap");
            }
            if (kind == TurnActors.WeaponKind.Table)
                Check((float)Read(first, "KnockbackSpeed") > 6 && (float)Result(actors, "WeaponSpeed", weapon) < 16,
                    "Table has slower flight and stronger knockback");
            if (kind == TurnActors.WeaponKind.Monitor)
            {
                Check((int)Read(second, "Health") == 19, "Monitor damages nearby enemy with electric explosion");
                int effectCount = ((IList)Field(actors, "weaponEffects")).Count;
                Root(weapon).localPosition = new Vector2(6, 0);
                Call(actors, "BeginReturn", weapon); Call(actors, "AdvanceReturn", weapon, 1f);
                Check((int)Read(second, "Health") == 19 && ((IList)Field(actors, "weaponEffects")).Count == effectCount,
                    "Return attack does not trigger electric explosion");
            }
            if (kind == TurnActors.WeaponKind.Shard)
            {
                Check(((IList)Field(actors, "fragments")).Count == 2, "Shard forward hit creates temporary fragments");
                Call(actors, "UpdateWeaponEffects", .5f);
                Check((int)Read(first, "Health") + (int)Read(second, "Health") == 37 && ((IList)Field(actors, "fragments")).Count == 2,
                    "Both clones attack independently and persist after hitting");
                Call(actors, "UpdateWeaponEffects", .1f);
                Check((int)Read(first, "Health") + (int)Read(second, "Health") == 37,
                    "Clone attack interval prevents damage on every frame");
                Call(actors, "UpdateWeaponEffects", .7f);
                Check((int)Read(first, "Health") + (int)Read(second, "Health") < 37 && ((IList)Field(actors, "fragments")).Count == 2,
                    "Clones continue independent attacks without expiring or recursively cloning");
                Call(actors, "ForwardSpecialEffects", weapon, first, new Vector2(4, 0));
                Check(((IList)Field(actors, "fragments")).Count == 2, "Repeated source hits maintain exactly two clones");
                Call(actors, "BeginReturn", weapon);
                Check(((IList)Field(actors, "fragments")).Count == 0, "Owner return clears surviving fragments");
            }
            if (kind == TurnActors.WeaponKind.Washer)
            {
                Root(weapon).localPosition = new Vector2(4, 0);
                Call(actors, "PlantSword", weapon);
                object installed = TestWeapon(actors, TurnActors.WeaponKind.Table, new Vector2(5, 0));
                Call(actors, "PlantSword", installed);
                Vector3 before = Root(installed).localPosition;
                float timer = (float)Read(installed, "AutoRecallRemaining");
                int health = (int)Read(second, "Health");
                Call(actors, "UpdateWeaponEffects", .2f);
                Check(Root(installed).localPosition == before && (float)Read(installed, "AutoRecallRemaining") == timer &&
                    Read(installed, "State").ToString() == "Planted" && (int)Read(second, "Health") == health,
                    "Washer leaves installed weapons, timers, and enemy HP unchanged");
                Vector3 enemyBefore = Root(second).localPosition;
                Call(actors, "UpdateSurvival", .1f);
                Check(Root(second).localPosition.x > enemyBefore.x && (int)Read(second, "Health") == health,
                    "Washer installation pushes nearby enemies outward without extra damage");
            }
        }
        CheckSynergyAndFusion(actors);
        CheckCloneVariants(actors);
        actors.ResetArena();
        var orbiting = (IList)Field(actors, "orbiting");
        object guardWeapon = orbiting[0];
        object guardEnemy = TestEnemy(actors, Root(guardWeapon).localPosition);
        Call(actors, "UpdateGuardDamage"); Call(actors, "UpdateGuardDamage");
        Check((int)Read(guardEnemy, "Health") == 19, "Guard repeat contact obeys per-weapon cooldown");
        Set(actors, "combatTime", 1f);
        Call(actors, "UpdateGuardDamage");
        Check((int)Read(guardEnemy, "Health") == 18, "Guard can hit again after cooldown");
    }
    static void CheckCloneVariants(TurnActors actors)
    {
        foreach (TurnActors.WeaponKind kind in new[] { TurnActors.WeaponKind.Shard, TurnActors.WeaponKind.CrystalRapier, TurnActors.WeaponKind.TemporaryFusion })
        {
            actors.ResetArena();
            object weapon;
            if (kind == TurnActors.WeaponKind.TemporaryFusion)
            {
                object shard = Result(actors, "AddWeapon", TurnActors.WeaponKind.Shard);
                Call(actors, "BeginMerge"); Call(actors, "AbsorbIntoMerge", shard); Call(actors, "FinishMerge");
                var list = (IList)Field(actors, "swords");
                weapon = list[list.Count - 1];
                ((IList)Field(actors, "orbiting")).Remove(weapon);
                Root(weapon).localPosition = new Vector2(2, 0);
            }
            else weapon = TestWeapon(actors, kind, new Vector2(2, 0));
            TestEnemy(actors, new Vector2(4, 0));
            Call(actors, "BeginFlight", weapon, new Vector2(7, 0));
            Call(actors, "AdvanceFlight", weapon, 1f);
            Check(((IList)Field(actors, "fragments")).Count == 2, kind + " summons two clones on hit");
            Call(actors, "BeginReturn", weapon);
            Check(((IList)Field(actors, "fragments")).Count == 0, kind + " clears clones when recall starts");
        }
        actors.ResetArena();
        object washer = Result(actors, "AddWeapon", TurnActors.WeaponKind.Washer);
        Call(actors, "BeginMerge"); Call(actors, "AbsorbIntoMerge", washer); Call(actors, "FinishMerge");
        var active = (IList)Field(actors, "swords");
        object mergedWasher = active[active.Count - 1];
        Root(mergedWasher).localPosition = new Vector2(4, 0);
        object enemy = TestEnemy(actors, new Vector2(5, 0));
        Call(actors, "PlantSword", mergedWasher);
        Check((float)Read(enemy, "KnockbackSpeed") == 20 && (int)Read(enemy, "Health") == 20,
            "Temporary merge inherits washer's damage-free knockback burst");
    }

    static void CheckSynergyAndFusion(TurnActors actors)
    {
        actors.ResetArena();
        object strong = Result(actors, "AddWeapon", TurnActors.WeaponKind.Trash);
        Write(strong, "DamageBonus", 2);
        object weak = Result(actors, "AddWeapon", TurnActors.WeaponKind.Trash);
        object table = Result(actors, "AddWeapon", TurnActors.WeaponKind.Table);
        object monitor = Result(actors, "AddWeapon", TurnActors.WeaponKind.Monitor);
        var state = weak.GetType().GetField("State");
        state.SetValue(weak, Enum.Parse(state.FieldType, "Flying"));
        state.SetValue(table, Enum.Parse(state.FieldType, "Planted"));
        state.SetValue(monitor, Enum.Parse(state.FieldType, "Returning"));
        Call(actors, "CheckSynergies");
        var weapons = (IList)Field(actors, "swords");
        Check(weapons.Contains(strong) && !weapons.Contains(weak) && !Root(weak).gameObject.activeSelf && actors.OwnedSwordCount == 5,
            "Workshop consumes one weakest material per kind across execution states");
        object workshop = weapons[weapons.Count - 1];
        Check(Read(workshop, "Kind").ToString() == "Workshop" && (int)Read(workshop, "DamageBonus") == 0 &&
            actors.HomingUpgradeLevel == 0, "Synergy output starts with its own base stats");
        Check((float)Result(actors, "WeaponSpeed", workshop) == 20f, "Workshop throw speed is doubled");
        Call(actors, "CheckSynergies");
        Check(actors.OwnedSwordCount == 5, "Synergy is not duplicated");

        actors.ResetArena();
        weapons = (IList)Field(actors, "swords");
        object original = weapons[0];
        Write(original, "DamageBonus", 2); Set(actors, "homingUpgrades", 2);
        monitor = Result(actors, "AddWeapon", TurnActors.WeaponKind.Monitor);
        table = Result(actors, "AddWeapon", TurnActors.WeaponKind.Table);
        Call(actors, "BeginMerge");
        Call(actors, "AbsorbIntoMerge", original); Call(actors, "AbsorbIntoMerge", monitor); Call(actors, "AbsorbIntoMerge", table);
        Call(actors, "FinishMerge");
        object compound = weapons[weapons.Count - 1];
        Check(actors.OwnedSwordCount == 5 && (int)Result(actors, "SwordDamage", compound) == 6 &&
            actors.HomingUpgradeLevel == 2, "Temporary merge sums damage and receives global homing");
        Check((float)Result(actors, "WeaponSpeed", compound) == 16f,
            "Temporary merge doubles the previous slowest-component speed once");
        Call(actors, "QueueLevelReward");
        Check(ChooseKnownReward(actors, TurnActors.RewardEffect.AttackUp) &&
            actors.ChooseWeapon((int)Read(original, "Id"), actors.RewardSelectionId), "Merged component can be upgraded");
        Check((int)Result(actors, "SwordDamage", compound) == 7, "Merged damage reflects component upgrade");
        Call(actors, "QueueLevelReward");
        Check(ChooseKnownReward(actors, TurnActors.RewardEffect.CopyWeapon) &&
            actors.ChooseWeapon((int)Read(original, "Id"), actors.RewardSelectionId) && actors.OwnedSwordCount == 6,
            "Merged component copies into independent orbiting weapon");
        Call(actors, "QueueLevelReward");
        Check(ChooseKnownReward(actors, TurnActors.RewardEffect.DeleteWeapon) &&
            actors.ChooseWeapon((int)Read(monitor, "Id"), actors.RewardSelectionId) && actors.OwnedSwordCount == 5,
            "Deleting one merged component preserves remaining group");
        Root(compound).localPosition = new Vector2(5, 0);
        Call(actors, "BeginReturn", compound); Call(actors, "AdvanceReturn", compound, 1f); Call(actors, "FlushPendingSwords");
        Check(weapons.Contains(original) && Root(original).gameObject.activeSelf && (int)Read(original, "DamageBonus") == 3 &&
            actors.HomingUpgradeLevel == 2 && !weapons.Contains(compound), "Split restores original IDs and preserves global homing");

        actors.ResetArena();
        object fencing = Result(actors, "AddWeapon", TurnActors.WeaponKind.Fencing);
        Call(actors, "BeginMerge"); Call(actors, "AbsorbIntoMerge", fencing); Call(actors, "FinishMerge");
        compound = ((IList)Field(actors, "swords"))[3];
        Result(actors, "AddWeapon", TurnActors.WeaponKind.Shard);
        Call(actors, "CheckSynergies");
        Check(actors.OwnedSwordCount == 5, "Temporarily merged material is excluded from automatic synergy");
        Root(compound).localPosition = new Vector2(5, 0);
        Call(actors, "BeginReturn", compound); Call(actors, "AdvanceReturn", compound, 1f); Call(actors, "FlushPendingSwords");
        bool found = false;
        foreach (object weapon in (IList)Field(actors, "swords"))
            if (Read(weapon, "Kind").ToString() == "CrystalRapier") found = true;
        Check(found && actors.OwnedSwordCount == 3, "Split immediately reevaluates Crystal Rapier synergy");
        foreach (object weapon in (IList)Field(actors, "swords"))
            if (Read(weapon, "Kind").ToString() == "CrystalRapier")
                Check((float)Result(actors, "WeaponSpeed", weapon) == 36f, "Crystal Rapier throw speed is doubled");
    }
    static void CheckIntegratedGrowth(TurnActors actors)
    {
        actors.ResetArena();
        foreach (var pair in new[] {
            (TurnActors.RewardEffect.UnlockFusion, TurnActors.Skill.Fusion),
            (TurnActors.RewardEffect.UnlockIntercept, TurnActors.Skill.AutoIntercept),
            (TurnActors.RewardEffect.UnlockGuard, TurnActors.Skill.Guard) })
        {
            Grant(actors, pair.Item1);
            Check(actors.IsSkillUnlocked(pair.Item2) && !PoolContains(actors, pair.Item1), "Unlock disappears after acquisition");
        }
        Check(!actors.IsSkillUnlocked(TurnActors.Skill.CrossSlash) && !actors.IsSkillUnlocked(TurnActors.Skill.CursorFollow),
            "Skills 3 and 5 stay locked");
        Grant(actors, TurnActors.RewardEffect.CounterPassive);
        object enemy = TestEnemy(actors, new Vector2(.3f, 0), 3);
        Call(actors, "DamagePlayer", enemy); Call(actors, "DamagePlayer", enemy);
        Check(actors.Health == 9 && (int)Read(enemy, "Health") == 2, "Contact counter and invulnerability apply once");
        Set(actors, "invulnerability", 0f); Call(actors, "DamagePlayer", enemy);
        Check(actors.Health == 8 && (int)Read(enemy, "Health") == 2, "Counter cooldown prevents repeat retaliation");
        Set(actors, "invulnerability", 0f); Set(actors, "counterRemaining", 0f); Call(actors, "DamagePlayer");
        Check((int)Read(enemy, "Health") == 2, "Bullet damage never triggers contact counter");

        actors.ResetArena();
        for (int i = 0; i < 3; i++) Call(actors, "DamageEnemy", TestEnemy(actors, new Vector2(5, 0)), 100, Vector2.right);
        Set(actors, "<Health>k__BackingField", 5);
        Grant(actors, TurnActors.RewardEffect.HealPassive);
        for (int i = 0; i < 9; i++)
        {
            Call(actors, "DamageEnemy", TestEnemy(actors, new Vector2(5, 0)), 100, Vector2.right);
            DrainRewards(actors);
        }
        Check(actors.Health == 5, "Healing does not retroactively count pre-acquisition kills");
        Call(actors, "DamageEnemy", TestEnemy(actors, new Vector2(5, 0)), 100, Vector2.right); DrainRewards(actors);
        Check(actors.Health == 6, "Tenth acquired-passive kill heals one HP");

        actors.ResetArena();
        enemy = TestEnemy(actors, new Vector2(5, 0), 2);
        Call(actors, "DamageEnemy", enemy, 1, Vector2.right);
        Call(actors, "GainExperience", 8d);
        Check(actors.Level == 3 && (int)Read(enemy, "Health") == 1 && (int)Read(enemy, "MaxHealth") == 2,
            "Level three preserves current and maximum HP of existing enemies");
        Check(actors.CurrentSpawnInterval == 3, "Leveling does not accelerate the three-second spawn interval");
        Check(actors.CurrentSpawnCount == 1, "Level three does not increase spawn batch");
        Call(actors, "GainExperience", 27d);
        Check(actors.Level == 6 && (int)Read(enemy, "Health") == 1 && (int)Read(enemy, "MaxHealth") == 2,
            "Level six also leaves enemy HP unchanged");
        Call(actors, "SpawnEnemy");
        var spawned = (IList)Field(actors, "enemies");
        Check((int)Read(spawned[spawned.Count - 1], "MaxHealth") == 2,
            "New melee enemies still have two HP at higher levels");
        Set(actors, "rangedEnemyChance", 1f);
        Call(actors, "SpawnEnemy");
        Check((int)Read(spawned[spawned.Count - 1], "MaxHealth") == 1,
            "New ranged enemies still have one HP at higher levels");
        DrainRewards(actors);
        Check(actors.CurrentSpawnCount == 1, "Level six also leaves spawn batch unchanged");
        Set(actors, "combatTime", 119.9f);
        Check(actors.CurrentSpawnCount == 1, "First two minutes keep one enemy per spawn");
        Set(actors, "combatTime", 120f);
        Check(actors.CurrentSpawnCount == 2, "Two minutes add one enemy to each spawn");
        Set(actors, "combatTime", 240f);
        Check(actors.CurrentSpawnCount == 3, "Four minutes produce three enemies per spawn");
        int beforeSpawn = actors.EnemyCount;
        Set(actors, "spawnTimer", 0f);
        Call(actors, "UpdateSurvival", .01f);
        Check(actors.EnemyCount == beforeSpawn + 3, "Spawn tick creates the time-scaled batch");
        actors.ResetArena();
    }

    static void CheckIntegratedArtAndUI(TurnActors actors)
    {
        foreach (string name in new[] { "sword", "shard", "trash", "table", "monitor", "fencing", "washer", "synergy_a", "synergy_b",
            "add", "delete", "copy", "attack", "recall", "homing", "fusion", "intercept", "guard", "counter", "heal", "reroll",
            "electric", "fragment_fx", "synergy_fx" })
        {
            Sprite sprite = GameArt.Get(name);
            Check(sprite != null && sprite.texture.filterMode == FilterMode.Point && sprite.pixelsPerUnit == 64,
                "Generated art loads as Point-filtered Sprite: " + name);
        }
        var uiObject = new GameObject("Native reward UI test");
        try
        {
            actors.ResetArena();
            var ui = uiObject.AddComponent<UIManager>();
            Call(actors, "QueueLevelReward");
            OfferRewardForTest(actors, TurnActors.RewardEffect.AddSword);
            Call(ui, "UpdateRewardCanvas", actors);
            var canvas = (Canvas)Field(ui, "rewardCanvas");
            var buttons = canvas.GetComponentsInChildren<UnityEngine.UI.Button>();
            Check(buttons.Length == 4 && Time.timeScale == 0, "Native UI creates reroll plus three clickable cards while paused");
            buttons[1].onClick.Invoke();
            Check(actors.OwnedSwordCount == 4 && !actors.IsChoosingReward, "Native card callback grants exactly one weapon");
            buttons[1].onClick.Invoke();
            Check(actors.OwnedSwordCount == 4, "Old native card callback cannot grant twice");
            Call(ui, "UpdateRewardCanvas", actors);
            Check(!canvas.gameObject.activeSelf, "Native reward canvas closes after selection");
        }
        finally { UnityEngine.Object.DestroyImmediate(uiObject); }
        actors.ResetArena();
    }
}
