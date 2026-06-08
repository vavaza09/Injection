# Game Instruction: 2D Sidescroller Action-Adventure (Unity C#)

## Game Goal
This game is a 2D sidescroller where the player explores stages, encounters monsters, and defeats enemies through skill-based movement combat. The core loop is "the longer you run, the faster you become," and that speed must influence every movement action the character performs.

---

## Core Pillars
1. Exploration + Combat: Traverse stages, hazards, and monsters.
2. Momentum-Based Movement: Speed built from running is the primary resource.
3. Movement-As-Weapon: Dashes and movement skills can directly damage enemies.

---

## Player Core Systems

### 1) Momentum Speed System (Game Heart)
- The player starts at `baseSpeed`.
- While running continuously, speed gradually increases up to `maxSpeed`.
- If the player stops, collides, or gets hit, speed decreases using `deceleration`.
- Use 3 speed tiers to simplify balancing:
1. Tier 1: Normal (easy control)
2. Tier 2: Fast (longer jumps, faster climbing)
3. Tier 3: Overdrive (strong movement, stronger dash impact, higher attack potential)

Recommended variables:
- `baseSpeed`, `maxSpeed`
- `accelerationRate`, `decelerationRate`
- `speedTierThresholds`
- `speedLossOnHit`, `speedLossOnWallCrash`

---

### 2) Speed Affects All Movement
All movement actions must scale with `currentSpeed`:
- Run: Horizontal speed increases with momentum.
- Jump: Jump height or distance scales by speed factor.
- Climb: Climb speed increases, but should have a cap for balance.
- Wall Slide: Higher speed can reduce friction (faster slide) or require tighter timing control.
- Dash: Dash range and impact force depend on pre-dash speed.

Balancing principles:
- No single movement option should dominate and replace all others.
- Each movement type should benefit from speed in different dimensions (distance, time, risk, damage).

---

### 3) Movement-Based Combat
Primary attacks are movement-driven:
- Dash Attack: Ram enemies to deal damage.
- Aerial Strike: Attack while jumping/falling.
- Wall Kick Attack: Rebound from walls for counterattacks.
- Skill Movement: Skills that temporarily turn movement into active hitboxes.

Recommended combat rules:
- Damage scales with speed, for example `damage = baseDamage * speedMultiplier`.
- Use a short `impactWindow` to reward timing.
- Add `cooldown` or `resource cost` to prevent spamming.
- Some enemy types should be vulnerable to dash attacks but resistant to normal hits.

---

## Enemies and Level Design

### Enemy Design
- Light enemies: Low HP, used to teach dash combat.
- Armored enemies: Require a speed threshold to break through.
- Ranged enemies: Force players to keep momentum and close distance quickly.

### Level Design
- Stages should encourage maintaining continuous speed.
- Use slopes, walls, and platform layouts to create movement flow.
- Include risk-reward routing:
1. Safe but slower route.
2. Faster route with heavier enemy pressure or harder platforming.

---

## Architecture Guidelines (Unity)

### Separation of Systems
- `PlayerInputHandler`: Handles input only.
- `PlayerMovementController`: Calculates movement and momentum.
- `PlayerCombatController`: Manages hitboxes, damage, and cooldowns.
- `PlayerStateMachine`: Idle/Run/Jump/Climb/WallSlide/Dash/Attack.
- `EnemyController`: AI behavior and damage handling via interfaces.

### Recommended Interfaces
```csharp
public interface IDamageable
{
    void TakeDamage(int amount, Vector2 hitDirection, float impactForce);
}

public interface IMomentumConsumer
{
    void OnMomentumChanged(float currentSpeedNormalized);
}
```

### Data-Driven with ScriptableObjects
Store frequently tuned values in ScriptableObjects:
- `PlayerMovementConfig`
- `PlayerCombatConfig`
- `EnemyConfig`
- `LevelFlowConfig`

---

## Performance and Stability
- Avoid allocations in `Update`.
- Cache references in `Awake`/`Start`.
- Use object pooling for hit effects and projectiles.
- Keep `Update` for input/logic and `FixedUpdate` for physics.
- Use explicit layer masks and contact filters.

---

## Recommended Folder Structure
```
Assets/
- _Project/
  - Scripts/
    - Core/
    - Player/
      - Input/
      - Movement/
      - Combat/
      - States/
    - Enemy/
    - Level/
    - UI/
  - ScriptableObjects/
    - Player/
    - Enemy/
    - Level/
  - Prefabs/
  - Scenes/
  - VFX/
  - Audio/
```

---

## Definition of Done (Checklist)
1. The player can build speed by running, and the effect is clearly noticeable.
2. Built speed affects Run, Jump, Climb, Wall Slide, and Dash.
3. The player can damage monsters via movement (at minimum Dash Attack).
4. At least 2 enemy archetypes react differently to player speed.
5. A test level exists that validates momentum and movement combat systems.
6. Responsibilities are cleanly separated, and balancing is editable through ScriptableObjects.

---

## Summary
This game must make players "run to build power" and "use movement as a weapon." Movement, combat, enemy behavior, and level design should all reinforce this core loop consistently.