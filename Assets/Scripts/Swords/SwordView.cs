using UnityEngine;
using UnityEngine.UI;

public sealed class SwordView : MonoBehaviour
{
    public Transform visual;
    public SpriteRenderer blade;
    public SpriteRenderer groundMark;
    public TrailRenderer trail;
    public Text fusionCount;

    public void Refresh(int count)
    {
        bool fused = count > 0;
        if (fusionCount.gameObject.activeSelf != fused)
            fusionCount.gameObject.SetActive(fused);
        if (fused)
            fusionCount.text = count.ToString();
    }
}
