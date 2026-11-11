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
        else enemySystem.ReturnStolenSword(sword.StolenBasicSword, sword.Root.position);
        orbiting.Remove(sword);
        sword.State = SwordState.Stored;
        sword.PendingRemoval = true;
        sword.Root.gameObject.SetActive(false);
    }

    void ReleaseCapturedSwordsFromDefeatedOwners()
    {
        foreach (SwordInstance sword in swords)
        {
            if (sword.PermanentLoot || sword.PendingRemoval) continue;
            bool ownerDefeated = sword.CapturedGiantSword
                ? !enemySystem.HasGiantSwordBoss : sword.BorrowedFrom is { Health: <= 0 };
            if (ownerDefeated)
                ReturnBorrowedSword(sword);
        }
    }
}
