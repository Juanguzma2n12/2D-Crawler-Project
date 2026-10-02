# Project Overview & Technical Documentation

## 1. Project Description
This project is a 2D Pixel Art Platformer modular asset toolkit and framework built in Unity 6 (6000.5.10f1). Designed for indie developers, technical designers, and gameplay programmers creating 2D action-adventure and platforming games, the project delivers modular 2D environments, dynamic interactive platforming mechanisms, physics-ready props, animated interactables, and custom inspector tooling.

### Core Pillars
- **Dynamic Physics Traversal**: Interactive platform mechanisms including elevators, bounding bounce-pads, physics rope bridges, and velocity-compensated moving platforms.
- **Procedural Motion Smoothing**: Implementation of second-order dynamics differential equations for natural, fluid mechanical oscillation and bounce effects.
- **Modular Environment & Props**: 2D tilemaps, parallax biome backgrounds, physics-enabled interactables (barrels, crates, carts), and particle-lit torches/campfires.
- **Editor Tooling via Lucid Editor**: Inspector-driven debugging and procedural property controls enabling runtime testing directly from the Unity Inspector.

---

## 2. Gameplay Flow / User Loop
The runtime experience operates across demo scenes that showcase environment interaction, physics-based object simulation, and platform mechanics:

1. **Boot & Scene Initialization**: The engine initializes 2D Physics subsystems, tilemap renderers, and sprite sheets. Dynamic props (barrels, pots, hay bales) instantiate with `Rigidbody2D` and collider setups.
2. **Environment Traversal Loop**: 
   - Moving platforms continuously compute relative frame velocity in `FixedUpdate` and translate registered dynamic actors to prevent precision drift.
   - Elevators cycle between defined vertical bounds, pausing at endpoints before reversing direction with second-order spring dynamics.
   - Bounding platforms charge on contact and impart vertical velocity boosts to launch objects.
   - One-way platforms utilizing `PlatformEffector2D` allow vertical ascension while maintaining solid ground from above.
3. **Actor & Prop Interaction Loop**:
   - Dynamic bodies interact with chain-linked bridges (`HingeJoint2D`), tilting carts, and breakable/pushable items.
   - Interactive chests transition between discrete visual states (`Closed` -> `Open` -> `Opened` / `Close` -> `Closed`) driven by animator parameters and script state logic.

---

## 3. Architecture
The project follows a component-based architecture where gameplay mechanics are decoupled into modular MonoBehaviours governed by mathematical smoothing models and 2D physics triggers.

### Key Entry Points & Data Flow
- **Physics-Driven Motion**: Fixed-timestep updates (`FixedUpdate`) drive kinematics and impulse forces. Platform actors track entering and exiting `Collider2D` instances to manipulate actor positions and inherit platform momentum.
- **Mathematical Simulation Model**: `SecondOrderDynamics` acts as a pure mathematical solver computing spring-damper displacement curves for mechanical systems without relying on rigid physics joints.
- **Input Architecture**: Configured via the New Input System with `InputSystem_Actions.inputactions`, mapping standard platformer controls (`Move`, `Look`, `Attack`, `Interact`, `Crouch`, `Jump`).
- **Editor-to-Runtime Bridge**: Components expose public controls and properties annotated with `Cainos.LucidEditor` attributes, allowing inspectors to execute runtime methods without custom GUI boilerplate.

---

## 4. Game Systems & Domain Concepts

### Dynamic Motion & Smoothing System
The dynamic motion system calculates physical spring-damper behaviors analytically to drive fluid mechanical motion.

- `SecondOrderDynamics`: A mathematical struct implementing second-order differential equations with frequency, damping, and response parameters to produce spring-damper oscillations.

**How to extend:**
Instantiate a `SecondOrderDynamics` instance inside any MonoBehaviour, initialize it via `Reset(initialValue)`, and feed target positions into `Update(target, deltaTime)` within `FixedUpdate` or `Update` to smooth rotations, scales, UI transforms, or camera followers.

**Design Pattern:**
Mathematical Solver / Value Object pattern. The struct maintains its own internal previous states and coefficient constants (`k1`, `k2`, `k3`), operating purely on numerical inputs and outputs independent of Unity engine state.

`Location:` `Assets/Cainos/Common/Script/Utils`

---

### Platforming & Traversal System
Handles automated vertical transport, jump-boosting pads, and physics tracking for standing entities.

- `Elevator`: Manages vertical transport between configurable length limits, updating chain sprite renderer dimensions and platform heights via `SecondOrderDynamics`.
- `MovingPlatform`: Tracks dynamic rigidbodies entering its trigger area and translates them by platform delta-velocity to eliminate physical jitter and drift.
- `BoundingPlatform`: A timed spring-pad mechanism that accumulates entities on top and periodically exerts an upward impulse force (`pushSpeed`).

**How to extend:**
To create new platform types (such as horizontal oscillating shuttles or pressure-triggered elevators), inherit or build alongside `MovingPlatform` by overriding trigger logic and subscribing to external activation events.

**Design Pattern:**
Observer / Area Trigger pattern combined with State pattern. Platforms detect actors via 2D trigger volumes (`OnTriggerEnter2D`/`OnTriggerExit2D`) and track active occupants in an internal collection while transitioning through operational states (`State.Up`, `State.Down`).

`Location:` `Assets/Cainos/Pixel Art Platformer - Village Props/Script`

---

### Interactive World Objects System
Controls interactive chests and animated world props that respond to state changes.

- `Chest`: Controls chest animation states (`IsOpened`) by interfacing with an `Animator` component and exposing inspector buttons for debugging.
- `ChestEditor`: Custom inspector binding linking the `Chest` component into the Lucid Editor rendering pipeline.
- `ElevatorEditor`: Custom inspector binding linking the `Elevator` component into the Lucid Editor rendering pipeline.

**How to extend:**
Implement new interactables (e.g., doors, levers, destructibles) following the pattern in `Chest`: define runtime boolean properties that drive Animator parameters, and annotate parameterless methods with `[Button]` for editor inspection.

**Design Pattern:**
State Machine / Animator Proxy pattern. The C# script acts as a state wrapper that synchronizes high-level programmatic states (`IsOpened`) directly with Unity Mecanim animator state machines.

`Location:` `Assets/Cainos/Pixel Art Platformer - Village Props/Script`

---

### Lucid Editor Framework
A reflection-driven editor extension system providing declarative attribute-based inspector layouts.

- `LucidEditor`: Custom `Editor` base class that parses properties and attributes to render organized inspectors.
- `PropertyGroupAttribute`: Base attribute for organizing serialized fields into logical UI containers (`FoldoutGroupAttribute`, `BoxGroupAttribute`, `HorizontalGroupAttribute`).
- `ButtonAttribute`: Decorates methods to render clickable invocation buttons directly in the Inspector.
- `ShowInInspector`: Exposes non-serialized C# properties to the inspector with live read/write access.
- `PropertyProcessor`: Base reflection processor pipeline mapping runtime attributes to their respective GUI layout calls.

**How to extend:**
Create new custom attributes deriving from `PropertyGroupAttribute` or `PropertyProcessor` to add bespoke rendering behaviors (such as custom color pickers, progress bars, or conditional field validators).

**Design Pattern:**
Decorator and Chain of Responsibility patterns. Attributes decorate metadata onto fields and methods, and processor classes evaluate whether to display, disable, format, or execute elements during inspector rendering.

`Location:` `Assets/Cainos/Third Party/Lucid Editor`

---

## 5. Scene Overview
The project organizes its scenes into demonstration levels and starter scenes:

- `SC Demo Scene - Village Props` (`Assets/Cainos/Pixel Art Platformer - Village Props/Scene/SC Demo Scene - Village Props.unity`):
  - **Purpose**: The primary showcase scene displaying all village props, modular ground tilemaps, multi-segment `HingeJoint2D` suspension bridges, active `Elevator` mechanisms, `BoundingPlatform` bounce pads, interactive chests of multiple tiers (Wooden, Iron, Silver, Golden), dynamic pushable objects (barrels, pots, crates), and particle-lit props (torches, campfires, stone of recall).
  - **Hierarchy Structure**: Root `SCENE` object subdividing `Background`, `Tilemap` (Grid with Ground `TilemapCollider2D`), `Grass`, and `Props`.
- `Demo` (`Assets/2D Pixel Art Platformer Biome - Plains/Demo.unity`):
  - **Purpose**: Showcases the plains biome tilemap collection, layered parallax background planes, environmental hazard spikes, and modular fence arrangements.
- `SampleScene` (`Assets/Scenes/SampleScene.unity`):
  - **Purpose**: Minimal default scene setup containing the camera and a standalone anvil prefab for testing.

---

## 6. UI System
- **Framework**: Built with support for Unity's UI Toolkit and UGUI (`com.unity.ugui`), while utilizing Lucid Editor for developer-facing interactive debugging UI in the Inspector.
- **Inspector Debugging Controls**: Interactive components expose horizontal button groups (`[Button("Open")]`, `[Button("Close")]`) and property toggles rendered directly in play mode or edit mode.
- **Input System Integration**: Uses `InputSystem_Actions.inputactions` which includes action bindings for UI navigation (`Navigate`, `Submit`, `Cancel`, `Point`, `Click`, `ScrollWheel`), enabling straightforward hookup to Canvas or UI Toolkit interfaces.
- **Extending UI**: To introduce an in-game HUD or pause menu, create a standard Canvas with TextMeshPro components, connect a new menu controller to the `UI` action map, and trigger gameplay state pauses.

---

## 7. Asset & Data Model
The project organizes assets into dedicated folders structured by domain and asset type:

### Visual Assets & Atlases
- **Sprites & Atlases**: Sprites are packed using `Atlas.spriteatlas` and organized sheets (`Sprites.png`, `TX Village Props.png`, `TX Tileset Ground.png`).
- **Tile Palettes**: Tile assets (`TP Ground.prefab`, individual `Tile` ScriptableObjects) configured with 2D tilemap colliders for rapid level design.

### Prefab Catalog
- **Interactive Prefabs**: `PF Village Props - Elevator`, `PF Village Props - Bounding Platform 01`, `PF Village Props - Chest Wooden/Iron/Silver/Golden`.
- **Physics Objects**: `PF Village Props - Barrel`, `PF Village Props - Wheelbarrow` (combining `HingeJoint2D` with `CircleCollider2D`), `PF Village Props - Wooden Bridge X6` (multi-node joint chain).
- **Environment & Decor**: Fences, signboards, stair colliders (`Stairs X24` through `X64`), platforms with `PlatformEffector2D`, and market stalls.

### Animation & Particles
- **Animator Controllers**: `AC Chest Wooden`, `AC Chest Iron`, `AC Chest Silver`, `AC Chest Golden` handling opening/closing sprite animations driven by `IsOpened` bool parameters.
- **Particle Systems**: Fire and spark materials (`MT FX Flame Glow`, `MT FX Flame Spark`, `MT FX Torch Flame`) applied to torch and campfire prefabs.

---

## 8. Notes, Caveats & Gotchas

- **Unity 6 Physics API (`linearVelocity`)**: `MovingPlatform.cs` and `BoundingPlatform.cs` use `Rigidbody2D.linearVelocity` (the Unity 6 standard) instead of the legacy `velocity` property. Upgrading or downgrading Unity versions requires keeping this API distinction in mind.
- **Moving Platform Dynamic Rigidbody Filter**: `MovingPlatform` only registers objects whose attached `Rigidbody2D` has `bodyType == RigidbodyType2D.Dynamic`. Kinematic or static colliders will not be moved by the platform.
- **Elevator Chain 9-Slice Math**: The `Elevator` script procedurally modifies chain sprite renderer dimensions using hardcoded pixel ratios (`chainL.size = new Vector2(0.09375f, value - 8 * 0.03125f)`). If changing the chain sprite resolution or pixels-per-unit, this formula must be updated.
- **Lucid Editor Reflection Overhead**: Lucid Editor extensively reflects on type properties and methods for inspector rendering. While performant in Edit Mode, heavily nested group attributes should be used thoughtfully in production projects.
- **Joint-Chain Bridge Stability**: The wooden bridge prefab relies on 13 linked `Rigidbody2D` segments with `HingeJoint2D`. Placing excessively heavy dynamic objects on it can cause joint stretching unless solver iterations in 2D Physics Project Settings are configured appropriately.