using UnityEngine;

public sealed partial class SwordSystem
{
    void CopyCapturedAppearance(SwordInstance sword, BulletView source)
    {
        CopySwordRenderers(source.transform,sword.Root);
        Vector3 scale = source.transform.lossyScale;
        Vector3 parentScale = transform.lossyScale;
        sword.Root.localScale = new Vector3(scale.x / parentScale.x,scale.y / parentScale.y,scale.z / parentScale.z);
        sword.CapturedBladeColor = sword.Blade.color;
        if (source.trail == null) return;
        sword.Trail.sharedMaterial = source.trail.sharedMaterial;
        sword.Trail.time = source.trail.time;
        sword.Trail.startWidth = source.trail.startWidth;
        sword.Trail.startColor = source.trail.startColor;
        sword.Trail.endColor = source.trail.endColor;
    }

    static void CopySwordRenderers(Transform source, Transform target)
    {
        foreach (SpriteRenderer renderer in source.GetComponentsInChildren<SpriteRenderer>(true))
        {
            string path = renderer.name;
            for (Transform parent = renderer.transform.parent; parent != source && parent != null; parent = parent.parent)
                path = parent.name + "/" + path;
            Transform part = target.Find(path);
            if (part == null || !part.TryGetComponent(out SpriteRenderer destination)) continue;
            destination.sprite = renderer.sprite;
            destination.color = renderer.color;
            part.localPosition = renderer.transform.localPosition;
            part.localRotation = renderer.transform.localRotation;
            part.localScale = renderer.transform.localScale;
        }
    }

    void ReturnBorrowedSword(SwordInstance sword)
    {
        if (sword.PendingRemoval) return;
        if (sword.CapturedGiantSword)
            enemySystem.ReturnGiantSword(sword.Root.position);
        else enemySystem.ReturnStolenSword(sword.BorrowedFrom,sword.Root.position);
        orbiting.Remove(sword);
        sword.State = SwordState.Stored;
        sword.PendingRemoval = true;
        sword.Root.gameObject.SetActive(false);
    }

    void UpdateCapturedHoldTimers(float dt)
    {
        foreach (SwordInstance sword in swords)
        {
            if ((sword.BorrowedFrom == null && !sword.CapturedGiantSword) || sword.PendingRemoval) continue;
            sword.Visual.localPosition = Vector3.zero;
            sword.Visual.localRotation = Quaternion.identity;
            bool ownerDefeated = sword.CapturedGiantSword ? !enemySystem.HasGiantSwordBoss : sword.BorrowedFrom.Health <= 0;
            if (!sword.PermanentLoot && ownerDefeated)
            {
                ReturnBorrowedSword(sword);
                continue;
            }
            if (sword.PermanentLoot || sword.BorrowedUsed) continue;
            sword.CapturedShakeTime += dt;
            sword.CapturedTimeRemaining = Mathf.Max(0,sword.CapturedTimeRemaining-dt);
            if (sword.CapturedTimeRemaining <= 0) ReturnBorrowedSword(sword);
        }
    }

    void ApplyCapturedShake()
    {
        foreach (SwordInstance sword in swords)
        {
            if ((sword.BorrowedFrom == null && !sword.CapturedGiantSword) ||
                sword.PermanentLoot || sword.BorrowedUsed || sword.PendingRemoval) continue;
            float progress = 1-sword.CapturedTimeRemaining/3;
            float strength = progress * progress;
            float phase = sword.CapturedShakeTime * 90;
            sword.Visual.localPosition = new Vector2(Mathf.Sin(phase),Mathf.Sin(phase*1.37f)) * (.09f*strength);
            sword.Visual.localRotation = Quaternion.Euler(0,0,Mathf.Sin(phase*.81f)*12*strength);
        }
    }
}
