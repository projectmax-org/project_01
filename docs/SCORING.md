# How the readiness score works

One number per destination, 0 to 100, from four parts that are always shown beside it. The arithmetic is
deliberately simple enough to explain in a sentence each.

| Part | Role | What it measures | How |
| --- | --- | --- | --- |
| Coverage | multiplies everything | Is there a copy at all? | Files found (verified, outdated or corrupt) ÷ files sampled |
| Freshness | 20 of the remaining 40 points | How far behind is the backup? | 1 − lag ÷ 30 days, floored at 0; 1 when nothing is behind |
| Integrity | 10 points | Are the copies the right bytes? | (found − corrupt) ÷ found |
| Reachability | 10 points | Is the destination usually there? | 0.7 × share of the last 10 drills that reached it + 0.3 × this drill |

**Total** = 100 × coverage × (0.60 + 0.20 freshness + 0.10 integrity + 0.10 reachability), rounded.
Coverage comes first because a copy that does not exist cannot be fresh or intact: a backup holding every
sampled file, up to date, scores 100; one holding 90% of them scores at most 90; one holding half scores
at most 50. Two weeks behind costs 10 points, a month 20. Any corrupt file caps the total at 69, because a
backup with corruption in it must never read as green.

| Total | Verdict |
| --- | --- |
| 95 to 100 | would restore |
| 70 to 94 | would mostly restore |
| 40 to 69 | needs attention |
| 0 to 39 | would not restore |

An unreachable destination scores 0 and reads "unreachable"; a drill that sampled nothing reads "nothing
to check".

## What each file can be

| Outcome | Meaning |
| --- | --- |
| Verified | A copy exists and its SHA-256 matches the original |
| Outdated | A copy exists but was written more than 2 seconds before the original and its bytes differ: the backup has not caught up |
| Corrupt | A copy exists, is not older than the original, yet its bytes differ |
| Missing | No copy where the layout says it should be |
| Placeholder | A cloud placeholder: the name is on the disk, the bytes are not. Never downloaded |
| Error | The copy or the original could not be read |

## Lag

Freshness looks only at changes newer than anything verified. The oldest sampled file that is missing,
outdated or a placeholder *and* was written after the newest verified file marks "behind since"; lag is
the time since then. An old file that was never copied is a coverage hole, not a lag, so it does not
count here. If nothing is behind, freshness is 1.

## Margin of error

Coverage is estimated from a sample of *n* files out of *N*. The half-width of the 95% interval is
1.96 × √(p(1 − p) ÷ n × (N − n) ÷ (N − 1)); 200 files out of 10,000 at 95% coverage gives about ±3%. The
app shows it beside every coverage figure, and the sample size is a setting.

## Full-restore estimate

Bytes in all sources ÷ the read-back speed this drill measured on this destination. It is an estimate of
reading, not of writing to a replacement disk, and it is shown only when something was read.

## Sampling

One pass over each source with weighted reservoir sampling: every file gets the key u^(1/w) for a uniform
random u and a weight w of 4 (changed in the last 7 days), 2 (30 days), 1 (a year) or 0.5 (older); the
files with the largest keys are the sample. A small reservoir per top-level folder guarantees every folder
is represented. Files written in the last 2 hours are left out (they may still be changing), files over
256 MB are left out (one could eat the whole byte cap), and the sample is trimmed largest-first to the
byte cap (512 MB by default).
