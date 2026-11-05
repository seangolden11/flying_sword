using System.Collections.Generic;
using UnityEngine;

public sealed partial class SwordSystem
{
    void StartFollowPath(Vector2 cursor)
    {
        followPath.Clear();
        followVelocity = Vector2.zero;
        if (orbiting.Count == 0)
        {
            followHead = Vector2.zero;
            followDirection = cursor.sqrMagnitude > .001f ? cursor.normalized : Vector2.up;
            followPath.Add((followHead - followDirection * FollowPathLength, 0));
            followPath.Add((followHead, FollowPathLength));
            return;
        }

        followHead = orbiting[0].Root.localPosition;
        Vector2 radial = followHead.sqrMagnitude > .001f ? followHead.normalized : Vector2.right;
        followDirection = new Vector2(-radial.y, radial.x);
        float angle = Mathf.Atan2(radial.y, radial.x);
        float radius = Mathf.Max(OrbitRadius, followHead.magnitude);
        for (int i = 32; i > 0; i--)
        {
            float distance = .25f * i;
            followPath.Add((followHead + (OrbitDirection(angle - distance / radius) - radial) * radius,
                8f - distance));
        }
        followPath.Add((followHead, 8f));
    }

    void FollowCursor(Vector2 cursor, float dt, bool entering)
    {
        Vector2 towardCursor = cursor - followHead;
        Vector2 desired = Vector2.SmoothDamp(followHead, cursor, ref followVelocity, FollowSmoothTime / SwordSpeedMultiplier, FollowSpeed * SwordSpeedMultiplier, dt)
            - followHead;
        if (towardCursor.sqrMagnitude > .0025f && desired.sqrMagnitude > .0000001f)
        {
            float angle = Vector2.SignedAngle(followDirection, towardCursor);
            float turn = Mathf.MoveTowards(0, angle, 200f * dt);
            followDirection = Quaternion.Euler(0, 0, turn) * followDirection;
            float speedFactor = Mathf.Lerp(.45f, 1, Mathf.Clamp01(Vector2.Dot(followDirection, towardCursor.normalized)));
            followHead += followDirection * (desired.magnitude * speedFactor);
        }

        var last = followPath[followPath.Count - 1];
        float step = Vector2.Distance(last.point, followHead);
        if (step >= FollowSampleSpacing)
        {
            float distance = last.distance + step;
            followPath.Add((followHead, distance));
            int expired = 0;
            while (expired + 1 < followPath.Count && followPath[expired + 1].distance < distance - FollowPathLength)
                expired++;
            if (expired > 0)
                followPath.RemoveRange(0, expired);
        }

        float depth = 0;
        int first = 0;
        while (first < orbiting.Count)
        {
            SwordInstance lead = orbiting[first];
            SwordInstance partner = lead.FusionSize == 0 && first + 1 < orbiting.Count &&
                orbiting[first + 1].FusionSize == 0 ? orbiting[first + 1] : null;
            SampleFollowPath(depth, out Vector2 position, out Vector2 direction);
            Vector2 side = new Vector2(direction.y, -direction.x);
            float lane = (.3f + FollowGap) * .5f;
            PlaceFollowingSword(lead, position - side * (partner != null ? lane : 0), direction, entering);
            if (partner != null)
                PlaceFollowingSword(partner, position + side * lane, direction, entering);

            depth += .625f * SwordScale(lead) + FollowGap;
            first += partner != null ? 2 : 1;
        }
    }

    void SampleFollowPath(float depth, out Vector2 position, out Vector2 direction)
    {
        if (depth <= 0)
        {
            position = followHead;
            direction = followDirection;
            return;
        }

        var last = followPath[followPath.Count - 1];
        float distance = last.distance + Vector2.Distance(last.point, followHead) - depth;
        Vector2 next = followHead;
        float nextDistance = last.distance + Vector2.Distance(last.point, followHead);
        for (int i = followPath.Count - 1; i >= 0; i--)
        {
            var sample = followPath[i];
            float length = nextDistance - sample.distance;
            if (length > .0001f && (distance >= sample.distance || i == 0))
            {
                direction = (next - sample.point).normalized;
                position = sample.point + direction * (distance - sample.distance);
                return;
            }

            next = sample.point;
            nextDistance = sample.distance;
        }

        position = followHead - followDirection * depth;
        direction = followDirection;
    }

    static void PlaceFollowingSword(SwordInstance sword, Vector2 position, Vector2 direction, bool entering)
    {
        bool moved = ((Vector2)sword.Root.localPosition - position).sqrMagnitude > .000025f;
        sword.Root.localPosition = position;
        SetFusionSize(sword, orbitVisual: false);
        PointSword(sword, direction);

        if (entering)
        {
            sword.PreviousPosition = position;
            sword.Trail.emitting = false;
            sword.Trail.Clear();
        }
        else if (moved && !sword.Trail.emitting)
        {
            sword.Trail.Clear();
            sword.Trail.emitting = true;
        }
    }

    void BeginMerge()
    {
        if (mergeSword == null && orbiting.Count > 0)
        {
            mergeSword = CreateSword("Fusion sword");
            mergeSword.Root.localPosition = RiderPosition + Vector2.up * OrbitRadius;
            mergeSword.Blade.color = FusionBlade;
            PointSword(mergeSword, Vector2.up);
        }

        foreach (SwordInstance sword in orbiting)
        {
            Vector2 position = (Vector2)sword.Root.localPosition - RiderPosition;
            sword.MergeOrbitOffset = Mathf.Atan2(position.y, position.x) - orbitAngle;
        }
    }

    static void SetFusionSize(SwordInstance sword, bool orbitVisual)
    {
        if (sword.FusionSize == 0)
            return;

        float scale = SwordScale(sword);
        sword.Visual.localScale = Vector3.one * scale;
        // Keep the grip on the inner edge of the orbit as the blade grows outward.
        sword.Visual.localPosition = orbitVisual ? Vector2.down * (.625f * (scale - 1)) : Vector2.zero;
    }

    void FinishMerge()
    {
        if (mergeSword.FusionSize > 0)
        {
            mergeSword.BlocksRemaining = mergeSword.FusionSize;
            swords.Add(mergeSword);
            orbiting.Add(mergeSword);
        }
        else
        {
            ReleaseSword(mergeSword);
        }

        mergeSword = null;
        equipment.NotifyChanged();
    }

    bool TouchesMergeSword(Vector2 from, Vector2 to)
    {
        Vector2 center = mergeSword.Root.localPosition;
        Vector2 travel = to - from;
        float progress = travel.sqrMagnitude > 0
            ? Mathf.Clamp01(Vector2.Dot(center - from, travel) / travel.sqrMagnitude) : 0;
        float radius = MergeContactRadius + mergeSword.FusionSize * .018f;
        return (from + travel * progress - center).sqrMagnitude <= radius * radius;
    }
}
