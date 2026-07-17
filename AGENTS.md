# Siege of Sabrevois — AGENTS.md

## Project identity

- **Unity 6000.3.0f1** (URP) — Windows standalone.
- Visual Studio / Rider solution is auto-generated; `.csproj` and `.sln` are gitignored.
- **Zenject** throughout — prefer constructor injection over field injection. Installers follow `Installer<T>` pattern (see below).
- Unity MCP by Coplay is available. Code review step MUST include unity mcp running.

## Assembly definitions

Custom asmdef assemblies under `Assets/Sabrevois/`:

| Assembly | Role | Key dependencies |
|---|---|---|
| `AI` | Utility AI framework — actions, considerations, data sources, decision-making | Zenject, Gameplay |
| `Gameplay` | Game systems — Health, Energy, Hunger, Weapon/Attack, Dialogue, Input, Resources (Food/Wood/Water) | Zenject, AI, Utils, Level |
| `UI` | UI Toolkit — damage numbers, vignette, weapon bob, world map | Zenject, Gameplay, Level |
| `Utils` | Shared utilities — Scribe logger, Billboard, GameObjectExtensions | *(none)* |
| `ProceduralWounds2D` | Billboard-sprite wound system, limb severing, GPU wound slices | Zenject, Gameplay, Utils, Level |

Also: `Dreamteck.Splines`, `Dreamteck.Utilities`, `Zenject`, `Zenject-Editor`, `ArtificeToolkit` (external).

## Namespace root

Everything is under `Sabrevois.*`. Sub-namespaces match folder structure:
`Sabrevois.AI`, `Sabrevois.Gameplay`, `Sabrevois.UI`, `Sabrevois.Utils`, `Sabrevois.ProceduralWounds2D`, `Sabrevois.Level`.

## Zenject wiring — how to add new bindings

Three installer levels, all in `Assets/Sabrevois/`:

1. **Root** — `SabrevoisInstaller` (MonoInstaller, scene-attached). Calls sub-installers and owns cross-assembly bindings.
2. **AIInstaller** — scans all assemblies for `IAction` types (reflection), binds them all. Binds `AgentWorldService`. Chooses `SequentialDecisionMakingService` or `ParallelDecisionMakingService` from `Resources/AISettings`.
3. **GameplayInstaller** — binds `ConversationService` and `AttackService`.

**To add a new binding**: create an installer in the target assembly (or add to an existing one). Prefer constructor injection:

```csharp
// GOOD — primary constructor, Zenject resolves automatically
public record MyAction(MyService Service) : IAction<MyConfig, MyState> { ... }

// Also acceptable — [Inject] field injection
public class Agent : MonoBehaviour
{
    [Inject] private IDecisionMakingService _decisionMakingService;
}

// Also acceptable — [Inject] method
public class AttackController : MonoBehaviour
{
    [Inject] public void Construct(AttackService attackService) { ... }
}
```

`Container.BindInterfacesTo<T>().AsSingle()` and `Container.BindInstance()` are both in use.

## Key architectural patterns

- **Utility AI**: `Archetype` (ScriptableObject) defines `ActionCandidate[]` — each has `Precondition[]` and `Desirability[]` (Considerations). Actions (`IAction<,>`) are `record` types with primary constructors for DI. Decision-making is `IDecisionMakingService` (sequential or parallel).
- **Procedural wounds**: Billboarding sprites (GPU) with `WoundsComponent` — raycast against rotated hitbox, wound applied as UV splat on GPU texture + optional limb severing via connectivity graph.
- **Character movement**: Rigidbody-based (not CharacterController). Input via `InputSystem_Actions` (auto-generated), wrapped by `InputRouter`.
- **UI**: UI Toolkit (`UIDocument`). Inspector-serialized references to `UIDocument` and `VisualElement` names. Polling in `Update`, not data-binding.
- **Logging**: `AppointScribe` replaces `Debug.unityLogger.logHandler` with `Scribe` — colored, class-prefixed output (editor/dev only). Stripped in release builds.
- **Singletons** (not DI-bound): `WorldObjectRegistry` (MonoBehaviour singleton), `GlobalWoundManager` (MonoBehaviour singleton).

## File layout

```
Assets/
  Sabrevois/                        # Source code
    AI/                             # Utility AI framework
    Gameplay/                       # Game systems
      AI/Actions/                   # Agent action implementations (12+)
      Dialogue/                     # ConversationService
      Input/                        # Player input handling
      Food/, Wood/, Water/, Housing/, Tree/  # Resource types
    UI/                             # UI Toolkit elements
    Utils/                          # Shared utilities
    Level/                          # Level systems (water, reflections)
    ProceduralWounds2D/             # Wound/slicing system
  _SabrevoisAssets/                 # Content (prefabs, config, scenes, shaders)
    Config/AI/                      # AI Archetype assets
    Resources/AISettings.asset      # Loaded by AIInstaller via Resources.Load
    Scenes/Sabrevois.unity          # Main scene
    Prefabs/                        # NPCs, enemies, trees, houses, terrain
    Settings/                       # Input, Renderer settings
```

## Testing

No tests exist in the project. No CI workflows.

## Notable gotchas

- **`WorldObjectRegistry`** is a scene singleton, not DI-bound. Agent actions look it up via `WorldObjectRegistry.Instance`.
- **`AISettings`** is loaded via `Resources.Load("AISettings")` — it must live in a `Resources/` folder. Currently at `_SabrevoisAssets/Resources/AISettings.asset`.
- **`Scribe`** only logs in editor/dev builds — use `Debug.Log` normally for runtime debugging.
- **Action types are auto-discovered** via reflection at startup — new actions are automatically registered.
- **Assembly references in asmdefs use GUIDs** — adding a new assembly reference requires the correct GUID. Check existing asmdefs for the pattern.
- **DamageNumber** uses a Zenject `MonoMemoryPool` — spawned via `_pool.Spawn(position, amount)`.
- **Agent billboarding** uses a GPU shader parameter (`_EnableBillboard`) and a dynamically rotated BoxCollider hitbox — the hitbox faces the camera in `Update()` to match the visual sprite.
