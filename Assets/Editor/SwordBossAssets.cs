using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Imports the new prefabs once without editing or replacing any open scene.
[InitializeOnLoad]
public static class SwordBossAssets
{
    const string Folder = "Assets/Resources/Enemies";

    static SwordBossAssets() => EditorApplication.delayCall += BuildWhenReady;

    static void BuildWhenReady()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.playModeStateChanged += AfterStop;
            return;
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/SwordBoss.prefab") &&
            AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/HomingSword.prefab"))
            return;
        Build();
    }

    static void AfterStop(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.playModeStateChanged -= AfterStop;
        EditorApplication.delayCall += BuildWhenReady;
    }

    [MenuItem("Flying Sword/Rebuild Sword Boss Prefabs")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var enemy = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Enemy.prefab");
        var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Player.prefab");
        var sword = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Sword.prefab");
        if (!enemy || !player || !sword) return;
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(enemy, preview);
            root.name = "Sword Boss";
            var view = root.GetComponent<EnemyView>();
            view.body.gameObject.SetActive(false);
            view.core.gameObject.SetActive(false);
            var rider = (GameObject)PrefabUtility.InstantiatePrefab(player, preview);
            rider.name = "Sword Rider";
            rider.transform.SetParent(root.transform, false);
            rider.transform.localPosition = Vector3.zero;
            var controller = rider.GetComponent<PlayerController>();
            var settings = new SerializedObject(controller);
            view.body = (SpriteRenderer)settings.FindProperty("hero").objectReferenceValue;
            Object.DestroyImmediate(controller);
            foreach (TrailRenderer trail in rider.GetComponentsInChildren<TrailRenderer>(true))
                trail.emitting = false;
            foreach (SwordView weapon in rider.GetComponentsInChildren<SwordView>(true))
            {
                weapon.blade.color = new Color32(255,130,107,255);
                Object.DestroyImmediate(weapon);
            }
            var orbit = root.AddComponent<SwordBossVisual>();
            orbit.blades = new Transform[1];
            for (int i = 0; i < orbit.blades.Length; i++)
            {
                var weapon = (GameObject)PrefabUtility.InstantiatePrefab(sword, preview);
                weapon.name = "Orbit sword " + (i + 1);
                weapon.transform.SetParent(root.transform, false);
                var weaponView = weapon.GetComponent<SwordView>();
                weaponView.blade.color = new Color32(255,130,107,255);
                weaponView.groundMark.enabled = false;
                weaponView.trail.emitting = false;
                float phase = i * Mathf.PI * 2 / orbit.blades.Length;
                Vector2 direction = new(Mathf.Cos(phase), Mathf.Sin(phase));
                weapon.transform.localPosition = direction * .95f;
                weapon.transform.localRotation = Quaternion.Euler(0,0,
                    Vector2.SignedAngle(Vector2.down,direction));
                orbit.blades[i] = weapon.transform;
                Object.DestroyImmediate(weaponView);
            }
            PrefabUtility.SaveAsPrefabAsset(root, Folder + "/SwordBoss.prefab");

            var projectile = (GameObject)PrefabUtility.InstantiatePrefab(sword, preview);
            projectile.name = "Homing Sword";
            projectile.transform.localScale = Vector3.one * 1.35f;
            var visual = projectile.GetComponent<SwordView>();
            visual.blade.color = new Color32(255,130,107,255);
            visual.groundMark.gameObject.SetActive(false);
            visual.fusionCount.transform.parent.gameObject.SetActive(false);
            var bullet = projectile.AddComponent<BulletView>();
            bullet.trail = visual.trail;
            bullet.trail.time = .23f;
            bullet.trail.startWidth = .075f;
            bullet.trail.startColor = new Color(1,.08f,.12f,.8f);
            bullet.trail.endColor = new Color(.65f,.015f,.04f,0);
            bullet.trail.emitting = false;
            Object.DestroyImmediate(visual);
            PrefabUtility.SaveAsPrefabAsset(projectile, Folder + "/HomingSword.prefab");
            AssetDatabase.SaveAssets();
            Debug.Log("Sword boss and homing sword prefabs saved. Open scenes were preserved.");
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }
}
