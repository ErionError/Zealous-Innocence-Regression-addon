# Zealous Innocence: Regression

> *A horror-flavoured addon for Zealous Innocence — psychological unravelling, bodily dependency, and the slow loss of self.*

**RimWorld 1.6 · Requires Zealous Innocence · Dubs Bad Hygiene · Biotech · Harmony**

---

## What is this mod?

**Zealous Innocence: Regression** extends the base ZI experience with a new `Regression` need that tracks how far a pawn has mentally and physically unravelled. Every pawn carries this bar. Accidents, environmental exposure, and helplessness push it down. Patience, routine, and recovery push it back up slowly.

---

## Requirements

| Mod | Where to get it |
|-----|----------------|
| [Zealous Innocence](https://steamcommunity.com/sharedfiles/filedetails/?id=XXXXXXX) | Steam Workshop |
| [Dubs Bad Hygiene](https://steamcommunity.com/sharedfiles/filedetails/?id=836318027) | Steam Workshop |
| Biotech DLC | Steam |
| [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077) | Steam Workshop |

Load order: **Harmony → Core → Biotech → Dubs Bad Hygiene → Zealous Innocence → Zealous Innocence: Regression**

---

## Core Feature — The Regression Need

A 0–1 bar on every pawn, split into five psychological stages. The bar never drifts on its own; it only moves when events happen.

| Stage | Bar range | What it means |
|-------|-----------|---------------|
| **Adult** | 1.00 – 0.75 | Baseline; full autonomy |
| **Pre-Teen** | 0.75 – 0.50 | Early regression; minor dependence, small skill erosion |
| **Young Child** | 0.50 – 0.30 | Marked regression; visible behavioural changes |
| **Toddler** | 0.30 – 0.10 | Heavy dependence; continence mostly lost |
| **Infant** | 0.10 – 0.00 | Full regression; helpless, needs constant care |

### What pushes the bar down
- **Accidents** — scaled by training progress and stage
- **Bedwetting** — softer penalty, half the accident weight
- **Diaper state** — wearing one hourly contributes; diaper ruination is a sharper hit
- **Nursery environment** — passive contextual pressure
- Stage-crossing is not linear: each boundary crossed strips XP, downgrades passions, removes traits, and carries growth points forward at ×0.5

### Stage-Crossing Effects
**Crossing down:** skills lose XP, passions downgrade, one trait removed, growth points halved, potty training progress scaled down (×0.75 per boundary; resets to 0 at Infant). Mood memory fires, plus a social memory on loved ones.

**Crossing up (recovery):** bar +0.25, passion and a trait are restored. Player chooses which ones via an interactive recovery letter. Growth points reset to 0.

---

## Recovery System

Recovery is intentionally slow and gated.

| Stage | Growth floor (pts/day) | Threshold to next stage |
|-------|------------------------|------------------------|
| Infant | 0 (event-driven only) | 160 pts |
| Toddler | 0 (event-driven only) | 110 pts |
| Young Child | 0.2 pts/day | 70 pts |
| Pre-Teen | 1.0 pts/day | 40 pts |

The actual daily rate is `lerp(floor, 4.0, learningLevel)` — pawns who can still learn progress faster. When growth points hit the threshold a **choice letter** fires: the player picks a passion to restore and a trait to return, the bar jumps up one stage, and the counter resets.

---

## Potty Training System

One of the most granular systems in the mod. Every regressed pawn tracks a `pottyProgress` value (0–1) that represents their current continence training. It is independent of the regression bar but intimately linked to it.

### How progress moves

| Event | pottyProgress | Growth pts | Learning |
|-------|:-------------:|:----------:|:--------:|
| Self toilet — Pre-Teen | +0.12 × trait | — | +0.06 |
| Self toilet — Young Child | +0.08 × trait | — | +0.04 |
| Self toilet — Toddler | +0.04 × trait | — | — |
| Self toilet — Infant | no change | — | — |
| Caretaker session — Pre-Teen | +0.10 × trait | — | +0.10 |
| Caretaker session — Young Child | +0.10 × trait | — | +0.08 |
| Caretaker session — Toddler | +0.10 × trait | +5 pts | — |
| Caretaker session — Infant | +0.10 × trait | +2 pts | — |
| Accident (awake) | −0.10 (cap 2/day) | — | — |
| Bedwetting | −0.05 (cap 2/day) | — | — |

Progress floors at 0.05 regardless of accidents, so a completely broken pawn can still be trained back up.

### Trait multipliers

Every progress gain and regression-bar reversal is scaled by the pawn's personality:

| Trait | Multiplier |
|-------|:----------:|
| *(default)* | ×1.0 |
| **Big Boy/Girl** | ×1.3 |
| **Potty Rebel** — self-directed | ×0.0 |
| **Potty Rebel** — caretaker session | ×0.5 + mood debuff |
| **Diaper Lover** | ×0.25 |

### Bar reversal from toilet success
Successful self-directed toilet use slightly pushes the regression bar back up (capped at the current stage ceiling):
- Pre-Teen: +0.015 × trait mult, cap 0.75
- Young Child: +0.012 × trait mult, cap 0.50
- Toddler / Infant: no reversal

### Accident damage scales with training
Pawns who are already well-trained take a smaller regression hit from accidents; those with near-zero training take the maximum hit.

| Stage | Range |
|-------|-------|
| Pre-Teen / Young Child | lerp(×1.4 → ×0.6, progress) |
| Toddler | lerp(×1.2 → ×0.7, progress) |
| Infant | lerp(×1.0 → ×0.8, progress) — no spiral risk |

---

## BladderControl Integration

The mod replaces ZI's bladder capacity calculation entirely for regressed pawns. Instead of a fixed value, bladder control is a **stage-envelope interpolated by pottyProgress**:

| Stage | Min capacity | Max capacity |
|-------|:------------:|:------------:|
| Infant | 2 % | 20 % |
| Toddler | 10 % | 45 % |
| Young Child | 30 % | 70 % |
| Pre-Teen | 55 % | 90 % |

Training your pawn raises their effective bladder capacity within their current stage. Bedwetting is handled separately (intentional — sleep is its own factor).

---

## Caretaker Job — Potty Training

A new `ZIR_PottyTraining` job in the **Childcare** work type. Any colonist assigned to Childcare will periodically take regressed pawns to the toilet for a guided session.

The job is also **directly orderable** — right-click a regressed pawn to schedule a session immediately.

---

## Potty Chair

A new buildable: **ZIR Potty Chair**

| Property | Value |
|----------|-------|
| Size | 1×1 |
| Cost | 50 Wood |
| Mass | 3 kg |
| Category | Furniture |
| DBH plumbing | None required |

---

## Gizmos (Pawn Inspector Bar)

Regressed pawns show a custom gizmo panel below their portrait:

**Toddler / Infant** (2 rows):
- 🟦 **Recovery** — current growth points vs. threshold to next stage
- 🟧 **Potty Training** — current pottyProgress bar

**Pre-Teen / Young Child** (3 rows, Biotech learning shown):
- 🟦 Recovery
- 🩵 **Learning** — Need_Learning level (un-frozen for these stages)
- 🟧 Potty Training

The **Schedule Training** button on the pawn's command bar lets you queue a caretaker session manually, with tooltip feedback explaining why it might be unavailable (cooldown / no free caretaker / no fixture in range).

---

## Learning Need Integration

Biotech's Need_Learning is frozen by ZI for adult pawns. This mod un-freezes it for **Pre-Teen** and **Young Child** stages, so those pawns can still accumulate learning and use it as a recovery accelerant. Toddler/Infant remain frozen — they recover via direct growth-point events instead.

---

## Planned / In Progress

- **Infant care positives** — mood bonuses from diaper changes, feeding, crib sleep
- **Low hygiene trigger** (DBH) — prolonged filth as a regression vector
- **Social reactions** — colony members reacting to a regressed colonist
- **Psychological/ritual/drug vectors** — additional ways regression can be induced
- **Potty chair direct interaction** — pawn-initiated use without a caretaker

---

## Compatibility Notes

- **Save-safe to add** on an existing save; the Regression need initialises at 1.0 (Adult) for all pawns.
- **Not save-safe to remove** mid-save.
- Dubs Bad Hygiene plumbing is **not required** for the potty chair; the fallback to a DBH toilet only happens when one exists on the map.

---

## License

Standart MIT License
