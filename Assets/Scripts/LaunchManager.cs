using System.Collections.Generic;
using UnityEngine;

public sealed class LaunchManager : MonoBehaviour
{
    public void Launch(IReadOnlyList<BallController> balls)
    {
        foreach (var ball in balls)
            ball.LaunchShot();
    }
}
