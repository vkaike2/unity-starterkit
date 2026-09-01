# StarterKit — Memory

Documentation lives here, not in the code. Code carries no comments; anything worth explaining
gets an entry below. Keep entries short and say *why*, since the *what* is readable in the class.

---

## The project

`com.vkaike2.starterkit` is a personal base package, reused by every future game. It holds only
auxiliary components — infrastructure with no knowledge of the game on top of it. No gameplay
concepts ever land here.

- Unity 6000.0+ (developed on 6000.5).
- No third-party dependencies. Worth keeping that way.
- Runtime namespaces mirror the folder path: `Runtime/Base/Interfaces` is
  `Vkaike2.StarterKit.Base.Interfaces`.

---

## Loading

### The single entry point

`LoadManager` is the one thing that starts the game. Nothing else does meaningful work in `Awake`
or `Start`, because Unity's ordering between them is not something you can reason about. The
manager calls `ILoadableEntity.Load()` on everything in the order it lists them, and that list is the
load order.

### ILoadableEntity

`ILoadableEntity.Load()` returns `UnityEngine.Awaitable`, not `void` and not `IEnumerator`.

- **Not `IEnumerator`** — this project does not use coroutines anywhere.
- **Not `void`** — the intended loading flow is *play the cover animation → wait for the screen to
  be black → load everything → play it back out*. Waiting on those animations makes the manager
  async regardless, so an awaitable contract costs nothing and buys the case where a load really
  does take time (a scene, an asset bundle, a save file).
- `Awaitable` is built into Unity 6, pooled, and resumes on the main thread. No UniTask needed.

An `Awaitable` is recycled once awaited, so a single instance can never be handed out twice. That
rules out a shared "already completed" instance.

### Singleton

`Singleton<T>` implements `ILoadableEntity` and **has no `Awake`**. `Instance` is assigned inside
`Load()`, so registration happens in the manager's order rather than Unity's.

- `Load()` is deliberately **not virtual**: it registers the instance and then calls `OnLoad()`,
  so a derived manager cannot forget to register itself. Work goes in `OnLoad`.
- `OnLoad()` is **virtual with an empty async body**, not abstract, so a singleton whose only job
  is to exist just doesn't override it. The empty body is what the `#pragma warning disable CS1998`
  in that file is for — it keeps the warning in one place instead of on every manager.
- A synchronous `OnLoad` that does real work but awaits nothing still trips CS1998. If that ever
  becomes noise, add `-nowarn:1998` to an `Assets/csc.rsp` rather than putting a helper type in
  the public API.
- **`LoadManager` is the one exception** to all of this: it calls `RegisterInstance()` from its own
  `Awake`, because it is the thing that loads everyone else and nobody is there to load it.

### LoadSequence

A `ScriptableObject` holding an ordered group of objects to load behind one loading screen.

Entries are a private `[Serializable]` wrapper class, `Entity`, holding a `UnityEngine.Object` plus
a `ShouldLoad` toggle. The wrapper exists because **Unity cannot serialize an interface field**.
`UnityEngine.Object` is used rather than `GameObject` on purpose: a GameObject with two `ILoadableEntity`
components would make `GetComponent` pick one arbitrarily, and that choice would silently move when
someone reorders components.

`OnValidate` asks each entry's `IsValid(index, context)` to judge itself, so a bad reference is a
console error naming the slot at authoring time instead of a null at runtime. Passing the asset as
the log context makes the message click back to it.

**Open problem:** a ScriptableObject asset cannot reference scene objects — Unity nulls them on
scene reload. So a `LoadSequence` asset can only hold prefabs. Either sequences instantiate prefabs,
or `LoadSequence` becomes a plain `[Serializable]` class on the `LoadManager` component instead of
an asset. Not decided yet.

### LoaderUI

Planned, not built. Three stages: an animation covering the screen, then a held black (or a picture),
then it moves off once loading is done. `LoadManager` drives it — start the cover, load while the
screen is black, then call it again to finish.

Note that if every manager loads synchronously the whole sequence finishes inside one frame. That is
fine; the cover and uncover animations are what give the screen its dwell time, not the load.

---

## Update dispatch

`UpdateManager` replaces per-MonoBehaviour `Update` with registered callbacks, dispatched in
ascending order value, and in registration order within the same value.

- `Register` is generic over `TOrder : struct, Enum` and stores the order as an `int`. C# enums
  cannot be extended, so the kit ships no order enum at all: the consuming game declares its own
  (`MyGameOrder`) and passes it directly — no cast, and no edit to the kit when a game adds an
  order. A game that never needs ordering can pass any single-value enum.
- The conversion is `Convert.ToInt32`, which boxes. Registration is not a per-frame path, and it is
  correct for every underlying integral type, unlike a reinterpreting cast.
- Order values are plain integers, so unrelated enums still sort against each other.
  Leave gaps between values to slot things in without renumbering, the same idea as Unity's
  `DefaultExecutionOrder`.
- Registering during dispatch would shift the indices the loop is walking, so mid-dispatch
  registrations are parked in `_pending` and merged after the loop.
- Unregistering only flags a subscription; the channel sweeps flagged ones after the loop for the
  same reason.
- A subscription also counts as dead when its owning `UnityEngine.Object` was destroyed without
  unregistering. That owner is only known for a method group — a lambda's target is its closure,
  not the MonoBehaviour.
- One subscriber throwing must not kill the rest of the frame, so each invoke is caught and logged.

---

## Editor

### ShowIf / HideIf

`ShowIfAttribute` / `HideIfAttribute` show or grey out a serialized field based on another
serialized field on the same object. `HideIf` is the same drawer inverted, and reads better when
the condition is an opt-out.

The condition **must be a serialized field** — the drawer reads it through the same
`SerializedObject`, so a plain property or a method will not be found. Always pass it with
`nameof` so a rename cannot silently break the link. Supported condition types: `bool`, enum,
integer, float, string, and object reference (where the expected value is a `bool` meaning
"is assigned").

`ShowIfMode.Disable` greys the field out instead of hiding it, which stops the inspector layout
from jumping around.

```csharp
[SerializeField] private bool _useDefaultLoader;
[SerializeField, HideIf(nameof(_useDefaultLoader))] private LoaderUI _loaderUI;

[SerializeField] private LoadMode _mode;
[SerializeField, ShowIf(nameof(_mode), LoadMode.Async)] private float _timeout;
```

### ShowIfDrawer

Registered with `useForChildren: true`, so the one drawer serves both attributes on any type.

- When the condition cannot be resolved the field is drawn **anyway**, above a warning help box
  naming the missing field. A typo must be loud, never a silently invisible field.
- A hidden field returns `-EditorGUIUtility.standardVerticalSpacing` as its height, cancelling the
  spacing the inspector adds after every field, so it leaves no gap behind.
- With several objects selected and the condition differing between them (`hasMultipleDifferentValues`)
  the field stays reachable, since there is no single right answer to guess at.
- An unsupported condition type also falls through to visible, for the same reason.
- Enum matching by name is supported (`ShowIf(nameof(_mode), "Async")`) and is the only option when
  the enum type is not visible from the declaring assembly. Matching by value reads `longValue`, not
  `enumValueIndex` — the index is a position in the name list and is wrong for any enum whose members
  are not numbered `0..n`.
- `FindConditionProperty` resolves siblings via the property path, so the attribute also works inside
  a nested serializable class. A list element path ends in `.Array.data[i]`, whose parent is the list
  itself and holds no sibling fields, so that case falls back to the root object.

---

## Packaging

### Samples~

Samples live in `Samples~/`, with the tilde. The tilde hides the folder from Unity's asset database,
so the examples ship inside the package but are **not** compiled into every game that installs the
kit. Package Manager reads the `samples` array in `package.json` and offers an Import button, which
copies the chosen sample into the consuming project's `Assets/Samples/`.

The trade: a `~` folder is invisible to the editor *in this repo too*, so the example scene cannot be
opened from the Project window while working on the kit. Open it by file path, or import it the way a
consumer would.

A `~` folder gets no `.meta` files — Unity never sees it. The old ones were deleted in the rename;
do not let them come back.

Every new sample folder needs its own entry in the `samples` array, or it ships invisibly with no
way to import it.

### .gitattributes

Unity YAML (`.unity`, `.prefab`, `.asset`, `.meta`, and the rest) is routed to `merge=unityyamlmerge`.
Git's line-based merge produces textually clean but structurally broken YAML — duplicated object ids,
`fileID` references pointing at nothing — with no conflict markers to warn you. UnityYAMLMerge
understands the object graph instead.

The attribute only names the driver. The driver itself lives in **local git config**, which is not
committed, so it has to be set again on every machine and after every Unity upgrade (the path is
version-pinned):

```
git config merge.unityyamlmerge.driver '"C:/Program Files/Unity/Hub/Editor/<version>/Editor/Data/Tools/UnityYAMLMerge.exe" merge -p --force --fallback none %O %B %A %A'
git config merge.unityyamlmerge.recursive binary
```

Git LFS is deliberately **not** used. This package is consumed as a UPM git dependency, and LFS in
that path requires git-lfs on every consuming machine and is a known source of failed package
resolution. Binaries are declared `binary` instead — no diff, no merge, but stored in Git normally.
Revisit only if a genuinely large asset ever needs to live here.

---

## Known issues

- `LoadManager.Configurations` and `LoadManager.Components` declare their members as auto-properties
  (`public List<LoadSequence> Sequences { get; set; }`). **Unity serializes fields, not properties**,
  so these are null at runtime and invisible in the inspector. They need to become fields.
