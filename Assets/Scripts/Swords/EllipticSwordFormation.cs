using System;
using UnityEngine;

internal static class EllipticSwordFormation
{
    const int SwordsPerOrbit = 8;
    const int ArcSamples = 96;
    const float HorizontalAspect = .30f;
    const float TurnsPerSecond = .054f;
    static readonly float[] ArcLengths = BuildArcLengths();

    public static Vector2 Offset(int slot, int swordCount, float elapsed, float facing)
    {
        int orbit = slot / SwordsPerOrbit;
        int count = Mathf.Min(SwordsPerOrbit, swordCount - orbit * SwordsPerOrbit);
        float phase = Mathf.Repeat(elapsed * TurnsPerSecond + (slot % SwordsPerOrbit) / (float)count, 1f);
        float center = .76f + orbit * .76f;
        float radius = .82f + orbit * .38f;
        Vector2 point = UnitPoint(AngleAtPhase(phase));
        return new Vector2(-facing * (center + point.x * radius), .38f + point.y * radius);
    }

    static float AngleAtPhase(float phase)
    {
        float distance = phase * ArcLengths[ArcSamples];
        int sample = Array.BinarySearch(ArcLengths, distance);
        if (sample < 0)
        {
            int upper = ~sample;
            float fraction = Mathf.InverseLerp(ArcLengths[upper - 1], ArcLengths[upper], distance);
            return SampleAngle(upper - 1 + fraction);
        }
        return SampleAngle(sample);
    }

    static float[] BuildArcLengths()
    {
        var lengths = new float[ArcSamples + 1];
        Vector2 previous = UnitPoint(SampleAngle(0));
        for (int i = 1; i <= ArcSamples; i++)
        {
            Vector2 point = UnitPoint(SampleAngle(i));
            lengths[i] = lengths[i - 1] + Vector2.Distance(previous, point);
            previous = point;
        }
        return lengths;
    }

    static float SampleAngle(float sample) => Mathf.PI * .5f + sample * (2 * Mathf.PI / ArcSamples);
    static Vector2 UnitPoint(float angle) => new(HorizontalAspect * Mathf.Cos(angle), Mathf.Sin(angle));
}
