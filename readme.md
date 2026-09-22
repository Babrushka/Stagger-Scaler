# Stagger Scaler

An elegant, lightweight combat calibration mod for Valheim that surgically restores core defensive and offensive stagger mechanics in multiplayer sessions and custom difficulty worlds.

Developed by **Babrushka**.

---

## 🛡️ The Philosophy

Originally, Valheim's combat framework was designed around a finely tuned normal difficulty balance. Shield block thresholds, parry windows, and weapon-stagger values were meticulously balanced around a **Normal Solo Profile (1.0x damage & health)** to reward tactical player combinations and active execution.

Later, the developers added world difficulty sliders and multiplayer scaling. However, these adjustments act as **flat mathematical multipliers** applied directly to raw enemy damage and health matrices. This brute-force scaling fails to fit the game's original design architecture. Under flat multipliers:
* **Defense Shatters:** Because defense relies on flat subtraction (`Damage - Block Armor`), inflated monster strikes instantly blow past your shield's absolute capacities. Shield blocking, buckler parrying, and weapon deflection become entirely useless, forcing groups into endless dodge-rolling.
* **Monsters Become Unyielding Tanks:** Inflated group health pools (+30% HP per nearby player) exponentially expand monster stagger thresholds. Creatures become completely unyielding targets, making it nearly impossible to interrupt their attack strings or break their stance.

**Stagger Scaler fixes this design flaw by introducing a virtual timeline rollback directly into Valheim's stagger engine (`AddStaggerDamage`).** 

Instead of altering baseline enemy values or nerfing AI properties globally, the mod evaluates active combat frames against your chosen target configurations. If your gear, positioning, and parry timing would successfully function under standard parameters, the mod scales the internal posture velocity so that combat reacts fluidly and dynamically—exactly as the core game intended—regardless of world sliders or player counts.

---

## 🛠️ Features

* **Multiplayer Proximity Loops:** Dynamically monitors a 100-meter zone around the player to track group-size scaling metrics for both incoming damage (+4% per player) and monster health pools (+30% per player).
* **0% Reflection Dependency:** Intercepts combat calculations using native public API hooks (`player.IsBlocking()`). It contains zero fragile assembly lookups, making it fully immune to future Valheim game patches.
* **Precise Defensive Calibration:** Automatically scales down incoming multiplayer and high-difficulty posture hits when you have your guard raised, preserving your **40% max health stagger limit** so a successful parry consistently breaks an enemy's stance.
* **Custom Stun-Lock Prevention:** Eliminates the punishing vanilla multiplayer loop where getting hit while stunned infinitely builds up your posture bar. Stun calculations adapt to your config limits, allowing you to recover at solo speed.
* **Offensive Stagger Rebalancing:** Reverses flat multiplayer health matrices and custom world difficulty settings on your forward weapon attacks. Your strikes break through inflated enemy health caps, allowing you to reliably stagger a tough mob (like a Greydwarf Brute) in exactly **2 to 3 well-placed tactical hits** rather than 7 or 8.
* **Fully Modular Controls:** Independent toggle profiles allow you to selectively run active shield scaling, passive armor protection scaling, or offensive weapon scaling based purely on your personal preference.

---

## 📦 Installation

This is a **dual-sided mod**. It must be installed on **both the server and all connecting clients** for network calculation variables to stay synchronized without lag or desynchronization.

1. Ensure **BepInEx Pack for Valheim** is properly installed.
2. Drop the **`StaggerScaler.dll`** file into your `<Valheim>/BepInEx/plugins/` directory on both your local PC and your dedicated server.
3. Start up the game once to automatically generate your personalized configuration file layout.

---

## ⚙️ Configuration

The config file generates automatically at `<Valheim>/BepInEx/config/babrushkas.staggerscaler.cfg`.

### `[General]`

* **`GlobalSwitch`** (Default: `true`): The master control switch. Set to `true` to actively process corrected stagger adjustments. Set to `false` to let the vanilla engine run unhindered (useful for checking logs).
* **`EnableShieldScaling`** (Default: `true`): Enables stagger scaling reduction while you are actively raising your shield or weapon to block.
* **`EnableArmorScaling`** (Default: `false`): Enables passive stagger scaling protection when your shield is NOT up (takes your body armor capacity into calculations).
* **`EnableAttackScaling`** (Default: `true`): Enables offensive stagger scaling, allowing your weapon strikes to cut through inflated monster difficulty filters.

### `[Difficulty]`

* **`TargetDifficultyScale`** (Default: `1.0`): The defensive difficulty tier you WANT your shield blocks and stagger thresholds to calculate against. Set to your world setting to handle player-count scaling only.
  * `0.5` = Very Easy | `1.0` = Normal (*Recommended*) | `2.0` = Very Hard
* **`AttackTargetDifficultyScale`** (Default: `1.0`): The offensive difficulty tier you WANT your attacks to deal stagger damage against. Setting this to `1.0` means your weapons break monster posture at standard Normal Solo speeds.
  * `0.5` = Very Easy (Fills bar faster) | `1.0` = Normal | `2.0` = Very Hard (Fills bar slower)

### `[Paste your world settings here]`

* **`CurrentWorldDifficulty`** (Default: `1.0`): Set this to match the actual difficulty scaling currently running on your world slider menu (`0.5`, `1.0`, `1.5`, `2.0`). Required for precise calibration math.
* **`DetectionRadius`** (Default: `100.0`): The horizontal radius (in meters) to scan for nearby players to track multiplayer group presence.
* **`DamagePerPlayerPercent`** (Default: `4.0`): The percentage of enemy damage scaling added per extra player (Vanilla game default is 4%).
* **`HpBonusPerPlayerPercent`** (Default: `30.0`): The percentage of extra health monsters gain per additional nearby player in vanilla Valheim (Vanilla default is 30%).

### `[Debug]`

* **`EnableLogs`** (Default: `false`): Set to `true` to print live downscaling math ratios, raw `m_staggerDamageFactor` coefficients, and live posture values directly into the F5 game console interface.
* **`SimulatedPlayerCount`** (Default: `0`): Forces a mocked player count for testing in singleplayer. Set to `0` for normal live server operations. Max cap is `4`.
