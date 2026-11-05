# Spawn balance review — 2026-11-05

## Scope and evidence
Read-only balance review. No gameplay values changed.
Unity Play Mode directly observed: three starting swords, enemies appearing, and later a level-up weapon-target selection screen. User input was detected during attempted test inputs; this was not an isolated controlled playthrough. No survival-time, kill-rate, frame-rate, or successful full-run claim is made.

## Current configured defaults
Batch count = 1 + floor(active combat seconds / 60).
Spawn interval = max(0.25, 2 - (level - 1) * 0.06) seconds.
Approximate rate = batch count * 60 / interval; frame scheduling can slightly lower actual rates.
Enemy mix: 65% melee (2 HP), 35% ranged (1 HP). Mean base damage required per kill: 1.65 before special effects, piercing, or overkill.

| Active time | Batch | Level 1 enemies/min | Level 10 enemies/min | Level 20 enemies/min |
| --- | ---: | ---: | ---: | ---: |
| 0–59 s | 1 | 30 | 41 | 70 |
| 60–119 s | 2 | 60 | 82 | 140 |
| 120–179 s | 3 | 90 | 123 | 209 |
| 300–359 s | 6 | 180 | 247 | 419 |
| 600–659 s | 11 | 330 | 452 | 767 |

These are independent time/level scenarios, not predicted levels reached at those times.

## Findings
1. The first timed increase doubles the batch (1 to 2), an abrupt 100% increase. Subsequent steps add 50%, 33%, etc.
2. Level still accelerates spawn frequency. At level 31 the 0.25-second minimum means eight times the initial spawn frequency. Combined with five minutes elapsed, this is about 1,440 enemies/minute. Reaching that level by five minutes is not assumed.
3. With only automatic five-second retrieval and three basic swords, a deliberately conservative launch-only estimate is below 0.6 hits/sec before travel time. Initial arrivals average 0.5 enemies/sec requiring 0.825 base damage/sec. Return piercing and manual retrieval materially improve this, so this is a limitation of waiting for automatic retrieval, not a proof of impossibility.
4. Rewards are choices rather than guaranteed weapon growth. Damage capacity is not determined by level, while enemy frequency still is. Thus player builds with similar levels may face substantially different pressure.

## Recommendation, not applied
First isolate time-based scaling by setting level-based interval reduction to zero and retaining a two-second interval. Assess 60/90/120-second batch growth periods using controlled runs before selecting the final period. Keep enemy HP unchanged. Record active survival time, HP, level, build, kills, current enemy count and projectile count at 30-second checkpoints. Include basic-weapon/manual-recall and special-weapon builds; do not infer full balance from an idle run.

## Limits
No controlled multi-minute playtest, reward-build comparison, performance stress measurement, or final balance approval completed. Editor left in its observed ongoing state because concurrent user input was present.
