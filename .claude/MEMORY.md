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
  is to exist just doesn't override it. The empty body trips CS1998, and so does any override that
  awaits nothing — that warning is now suppressed compiler-wide, see [csc.rsp](#cscrsp--cs1998-suppressed-everywhere).
- **`LoadManager` is the one exception** to all of this: it calls `RegisterInstance()` from its own
  `Awake`, because it is the thing that loads everyone else and nobody is there to load it.

### SequenceRunner

The entity-loading half of `LoadManager` lives in `SequenceRunner`, a plain class (not a
MonoBehaviour) constructed with the MonoBehaviour that owns it. `LoadManager` and `SceneLoader` both
hold one, so neither duplicates the loading switch or the validation loop.

It is **composition, not a base class**, because C# allows one base and `LoadManager` already has
one: `Singleton<LoadManager>`. `SceneLoader` cannot join that hierarchy — every loaded scene has its
own, and `RegisterInstance` throws on the second instance. The owner is passed in for the three
things the loading actually needs from a component: `transform` as the fallback parent,
`destroyCancellationToken` for the waits, and nothing else.

The runner is reached through a lazy `Runner => _sequenceRunner ??= new(this)` property rather than
built in `Awake`, keeping `Awake` free the way the rest of the kit does. The property is named
`Runner`, not `SequenceRunner`: a nested `Configurations` class calls the **static**
`SequenceRunner.ValidateSequences`, and an instance property of the enclosing type sharing that name
would shadow the type and not compile.

`ValidateSequences` is static and takes the log context, so `OnValidate` on either host is one call
per list. `LoadManager` validates its `TestSequence` and `Sequences`, `SceneLoader` its `Sequences`.

### The loading screen has one owner

`SceneLoader.Load()` runs its sequences' entities **without touching a `LoaderUI`**. It is called
from inside a `LoadManager` sequence, which means the screen is already covered; a nested
`ToggleLoader(open: true)` at the end of the scene's own load would uncover the screen while the
outer sequence still had entities to load. `LoadManager.LoadSequence` is the only thing that drives
the loader, and it wraps everything below it.

The consequence: `UseDefaultLoader` / `LoaderUI` on a `SceneLoader`'s sequences are inert. They mean
something only on the sequences `LoadManager` itself runs.

### Loading an entity

`LoadEntities` walks a sequence in order and awaits each entry before starting the next — the list
*is* the load order, so nothing here runs in parallel. Each `Entity.Type` has its own method rather
than a body inside the switch, so a case stays one line.

- **GameObject** is a prefab, so it is instantiated first. `GameObjectParent` is where it lands;
  when it is empty the manager parents it to itself, which keeps a manager alive across the scene
  unloads its own loading later performs.
- **Object** is already an instance (a ScriptableObject, or a scene object on a non-asset sequence),
  so it is only asked for its `ILoadableEntity` and loaded.
- **Scene** loads additively and is then asked for its `SceneLoader`. Every scene the kit loads is
  expected to have exactly one; it is the scene's own entry point, the way `LoadManager` is the
  game's.
- The `null` checks are not redundant with `OnValidate`. Validation runs at authoring time on the
  asset; a prefab can lose its component afterwards, and a scene path can point at a scene missing
  from the build settings. Both throw with the sequence name, since a silent skip would leave a
  half-loaded game with no clue why.
- Scene lookup is `GetSceneByPath`, not `GetSceneByName` — `ScenePath` is stored as the asset path,
  and two scenes in different folders may share a name.
- The `SceneLoader` is found by sweeping the loaded scene's root objects with
  `GetComponentInChildren(includeInactive: true)`. `FindObjectOfType` searches every loaded scene
  and would happily return another scene's loader.
- Loaders are kept in `_sceneLoaders`, keyed by scene path, so the scene can later be addressed
  (unloaded, or re-sequenced) without searching for it again.

### Unloading the boot leftovers

`LoadManager.Start` begins with `UnloadActiveScenes()`, which unloads every loaded scene except the
one the manager's GameObject lives in. Working in the editor, whatever scenes happened to be open in
the Hierarchy are also loaded on Play, and the kit's own boot scene must be the only survivor.

- The scene to keep is `gameObject.scene`, not the active scene. Which scene is *active* on boot is
  whatever the editor left set; where this component actually sits is not. It then makes that scene
  the active one, so anything instantiated afterwards lands in it rather than in a scene about to
  disappear.
- The scenes are collected into a list **before** any unload. `SceneManager.GetSceneAt` indexes a
  list that unloading mutates, so unloading inside the loop skips scenes.
- `UnloadSceneAsync` returns `null` when Unity refuses the unload (the last loaded scene, an
  already-unloading one). Awaiting that would be a null ref, so a null operation is skipped.
- Awaiting is the same `while (!isDone) await Awaitable.NextFrameAsync(destroyCancellationToken)`
  poll used by [[LoaderUI]] — `AsyncOperation` is not awaitable on its own, and the token means
  tearing the manager down stops the wait.
- `Start` awaits it, so the first sequence never loads into a scene that is still being torn down.

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

The three stages are modelled as a state machine, not as flags. `LoaderUI` is a `partial` class and
each state is a private nested class deriving from `BaseState`, in its own
`Loader/FiniteStates/LoaderUI.{State}.cs` file, one per member of the `LoaderUI.State` enum:
`Open`, `Closed`, `TransitionToOpen`, `TransitionToClose`.

- **Nested and private** because a state is meaningless outside the loader and needs its serialized
  fields. `BaseState.Start(parent)` hands it the `LoaderUI`, so the state reaches them through
  `_parent` rather than through a duplicated set of its own.
- **One file per state, all `partial LoaderUI`** so a state can grow without turning `LoaderUI.cs`
  into a scroll, while the nesting keeps them off the public API.
- The nested class names deliberately shadow nothing: the enum members are only ever reached as
  `State.Open`, so `Open` the class and `State.Open` the value coexist.

The four states are one loop: `Open -> TransitionToClose -> Closed -> TransitionToOpen -> Open`.
`Open` means the screen is *exposed* (the loading image is parked off in an opened position and the
container is off); `Closed` means the image covers the screen. Only the two transitions tick.

- Every `OnEnter` **snaps** the image to where that state says it belongs, even when it is already
  there. Arriving from a transition makes the snap a no-op, so the same line also serves the case
  where the state is entered cold as the initial state. That is what removes the boot-time special
  casing from three of the four states.
- `LastOpenedPosition` is the memory of where the image sits while open. It is only null on a fresh
  boot, so `EnsureLastOpenedPosition` (in `BaseState`) rolls a random one, and both `Open` and
  `TransitionToClose` can be entered first without knowing which of them went first.
- `TransitionToOpen` rolls a **new** random position every time, so each loading screen leaves in a
  different direction. `TransitionToClose` deliberately does not roll: the image is already
  somewhere, and moving it before the transition would be a visible jump.
- Movement is `anchoredPosition`, not world `position`, because everything lives in one Canvas.
  `TransitionSpeed` is then in canvas reference-resolution units, so a CanvasScaler keeps the
  transition the same duration at any resolution. **This requires the loading image and every
  position marker to share a parent RectTransform** — anchoredPosition is meaningless across
  different parents or anchors.
- Arrival is `Vector2.MoveTowards` reaching the target exactly, then `==` (which is Unity's
  approximate compare). No epsilon of our own, and no overshoot to clamp.
- `LoaderUI` ticks the current state from its own `Update`, **not** through [[UpdateManager]]. The
  loader has to run while the managers are still loading, which is precisely when `UpdateManager`
  cannot be relied on to exist.

`TransitionToClose` does not tick. Its `OnEnter` plays `AnimationClosing` and then polls
`Animator.IsPlayingAnimation(AnimationClosed)` frame by frame, moving to `Closed` once the controller
has arrived there by itself. The animator owns the duration, so the closing animation can be
retimed in Unity without touching a speed or a timer in code, and the state machine never disagrees
with what is on screen. `Container.SetActive(true)` comes **first**: `PlayAnimation` is a no-op on an
inactive object, so playing before activating would leave the poll waiting forever.

The poll uses `_parent.destroyCancellationToken`, the same token as the waits in `LoaderUI` itself.

`Open()` is the public verb `LoadManager` calls; it returns `Awaitable` and finishes only once the
screen is actually exposed, so the caller never has to poll the state itself.

- It waits out a transition **before** deciding what to do, rather than assuming `Closed`. Called
  mid-`TransitionToClose` it lets the close finish and then reopens; called when already `Open` it
  returns without a frame of work. Reading the current state and acting on it in the same breath
  would hang on two of the four states.
- Waiting is a `while (!IsState(...)) await Awaitable.NextFrameAsync(destroyCancellationToken)`
  poll. The states already publish everything through `IsState`, so an event or a completion source
  per transition would be a second source of truth to keep in sync for no gain at this size.
- The token is the MonoBehaviour's own `destroyCancellationToken`, so tearing the loader down
  cancels the await instead of leaving it resuming against a dead object.

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

## Extensions

### AnimatorExtension

Lives in `Vkaike2.StarterKit.Base.Extensions` like every other extension — the namespace mirrors
the folder. Both helpers are null- and inactive-safe, because an animator on a disabled container is
the normal case for the loader, not an error.

`IsPlayingAnimation` reads `GetCurrentAnimatorStateInfo(layer).IsName(name)`, so the string is the
**Animator state name**, the same string `Play` takes — not the clip name. The two differ the moment
a state is renamed in the controller.

It returns `false` while `IsInTransition(layer)`. During a blend, `GetCurrentAnimatorStateInfo` still
reports the state being left, so a caller waiting for the *next* state would otherwise see it arrive
one blend early. Waiting for the transition to settle is the honest answer to "is it playing this".

### RandomExtensions

`List<T>.GetRandom()` picks one element with `UnityEngine.Random`, not `System.Random`, so a game
that seeds Unity's generator gets a reproducible pick out of it for free.

It **throws** on null and on empty rather than returning `default`. A `default` here is silent: it
is a real element for a value type and a null the caller blames on the list's contents, so the bad
call site stays hidden. `ArgumentNullException` for null and `InvalidOperationException` for empty
follow the BCL split — an empty list is a valid argument, just not one you can pick from.

Deliberately typed on `List<T>` and not `IReadOnlyList<T>`: widening it later is a source-compatible
change, narrowing it is not.

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

[SerializeField, HideIf(nameof(_shouldLoad), false), ShowIf(nameof(_type), Type.Scene)]
private Scene _scene;
```

`Header` and `SpaceBefore` are optional named arguments that draw a bold label and/or blank space
above the field, and **disappear with it**:

```csharp
[SerializeField, HideIf(nameof(_isActive), false, Header = "Configurations", SpaceBefore = 8f)]
private bool _useDefaultLoader = true;
```

They exist because Unity's `[Header]` and `[Space]` are `DecoratorDrawer`s, and a decorator is never
handed the `SerializedProperty` — it cannot read the condition, and Unity draws it *before* the
property drawer gets its rect. So a `[Header]` above a conditionally hidden field stays on screen,
titling nothing. Moving the header into the attribute puts it back in the one place that can see the
condition. Attributes on a bare `[Header(...)]` line also bind to the next field anyway, so
`[Header("X"), HideIf(...)]` on its own line was never anything but a duplicate of the field below.

The header block is `singleLineHeight * 1.5` with the label in its lower line, matching Unity's own
`HeaderDrawer` so the spacing is indistinguishable from a real `[Header]`.

Both attributes are `AllowMultiple = true`, and stacking them **ANDs** the conditions: the field
shows only when every one of them is satisfied. `ShowIf` and `HideIf` mix freely in a stack, since
each one contributes a satisfied/not-satisfied answer and nothing more. AND was chosen over OR
because "this field belongs to this state" is what the inspector is almost always expressing; an OR
is still reachable by pointing all the stacked attributes at one derived bool field.

### ShowIfDrawer

Registered with `useForChildren: true`, so the one drawer serves both attributes on any type.

Unity hands a drawer only **one** `attribute`, so the stack is read off `fieldInfo` instead and
cached on the drawer instance. `attribute` is still the fallback for the case where `fieldInfo` is
missing. Unity builds a single handler per field even with several `PropertyAttribute`s on it, so
the drawer runs once and draws the field once.

A stack that is not satisfied hides the field if any unsatisfied attribute is `Mode.Hide`, and only
greys it out when every unsatisfied one is `Mode.Disable` — hiding wins, because a field the author
declared invisible for a state must not appear in it.

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
- The header is drawn through a **cached `GUIContent`**, never through the `string` overload, and
  `label` is **copied** before anything is drawn above the field. Unity's implicit `string` →
  `GUIContent` conversion hands back the shared temp content, which is the very instance Unity passed
  in as `label` — drawing the header by string retitles the field with the header text. The copy
  covers the help-box path for the same reason.
- The `Header` / `SpaceBefore` decoration is taken from the **first** attribute in the stack that
  declares either, so a field with several stacked conditions carries its header only once. Its
  height is added in both `GetPropertyHeight` and `OnGUI`, and contributed by neither when the field
  is hidden. It is still drawn above the missing-condition help box, since that path draws the field.
- `FindConditionProperty` resolves siblings via the property path, so the attribute also works inside
  a nested serializable class. A list element path ends in `.Array.data[i]`, whose parent is the list
  itself and holds no sibling fields, so that case falls back to the root object.

### Button

`[Button]` on a method exposes it as a clickable button under the default inspector. It is a plain
`System.Attribute`, not a `PropertyAttribute`: `PropertyDrawer` only ever sees serialized fields, so
methods have to be found by reflection from a `CustomEditor` instead.

`InspectorButtons.Draw(editor)` does that work, and two near-empty editors — one for
`typeof(MonoBehaviour)` and one for `typeof(ScriptableObject)`, both with `editorForChildClasses:
true` — are what put it on every user script. Unity picks the **most specific** editor for a type,
so a game's own `CustomEditor` still wins over these; when it does, its author calls
`InspectorButtons.Draw(this)` to keep the buttons. Unity's own components are not `MonoBehaviour`
(they derive straight from `Behaviour`), so none of their inspectors are shadowed by this.

- Methods are collected walking the type hierarchy with `DeclaredOnly`, deduped by name, derived
  first. `FlattenHierarchy` would miss a private method on a base class, and without the dedupe an
  `override` of a decorated method would draw two buttons. The result is cached per `Type`, since
  `OnInspectorGUI` runs every repaint.
- Only parameterless methods work; anything else draws a warning box instead of a button. Drawing
  argument fields would mean serializing state the inspector has nowhere to keep.
- `ApplyModifiedProperties` runs before the call and `Update` after, so the method reads the values
  the user just typed and the inspector shows whatever the method changed.
- Each target gets an `Undo.RecordObject` + `SetDirty`, so a button is undoable and its edits are
  saved. Static methods are called once, with no target.
- The call is wrapped so a throwing method logs through `Debug.LogException` with the object as
  context instead of taking the whole inspector down with it. A non-null return value is logged;
  an `Awaitable` is not, since it is the normal shape of an async method here and its value is
  meaningless.
- `ButtonMode` greys the button out rather than hiding it, so a play-mode-only action is visible
  (and explains itself) while the editor is stopped.

### HierarchyStyle

A `MonoBehaviour` in `Runtime/Hierarchy` carrying two colour pairs and an `ApplyToChildren` toggle.
It is the whole authoring surface for hierarchy colouring — put it on a GameObject and
[[HierarchyPainter]] paints that row.

The colours are wrapped in a nested `[Serializable] class Colors` (background + text) rather than
sitting loose on the component, so the same pair can appear twice: `Style` for the object's own row
and `ChildrenStyle` for the rows it lends its colour to. Without the wrapper the second pair would
be two more fields with prefixed names, and adding an icon or a font style later would double again.
Nesting it inside `HierarchyStyle` follows the kit's `Components` / `Configurations` habit — the type
is meaningless on its own, so it stays off the namespace.

`ChildrenStyle` is drawn `[HideIf(nameof(_applyToChildren), false)]`, so it appears only once the
toggle is on. The [[ShowIfDrawer]] passes `includeChildren: true`, which is what lets it hide and
size a nested serializable class rather than just a primitive.

The colours live on a **component**, not in an editor-side asset keyed by object id, because Unity
then serializes them for free: they ride along with prefabs and prefab variants, survive renames,
reparenting and duplication, and diff in git next to the object they describe. The alternative —
a `GlobalObjectId` → colour map in a ScriptableObject — has to be keyed, and every key rots
(prefab instantiation mints a new id, deleted objects leave entries behind, one shared asset is a
merge magnet). It also puts the "later" features within reach: an icon override or a font style is
one more serialized field here, drawn by the default inspector with no editor code.

The price, taken deliberately: the class ships in builds. With no `Awake` and no `Update` that is a
few serialized bytes per marked object and zero frames of work. `#if UNITY_EDITOR` around the fields
is not an option — the data has to deserialize in a build or the component comes back broken.

- `Colors.BackgroundColor` / `Colors.TextColor` have **public setters**, which exist for
  [[HierarchyStyleMenu]]. The editor assembly is separate, so `internal` would need an
  `InternalsVisibleTo`, and the alternative is `SerializedObject` with `"_backgroundColor"` as a
  magic string. A settable property is the cheapest of the three and the only one a rename cannot
  silently break. `Style` and `ChildrenStyle` themselves are **getters only** — the pair is mutated
  in place, and a settable reference would let a caller swap in an instance Unity is not serializing.
- Both pairs are initialised with `new()` at the field, so a fresh component and a component whose
  toggle was just turned on both start from a real colour rather than transparent black.
- `[DisallowMultipleComponent]`, since a second style on one object has no meaning.
- A **background alpha of 0 disables the paint entirely** rather than meaning "text colour only".
  Recolouring the label requires repainting the row first, so with nothing drawn underneath the new
  label would blend over Unity's. Transparent therefore means off, which is at least a rule that can
  be stated.

### HierarchyPainter

Colours rows in the **Hierarchy** window from the [[HierarchyStyle]] on the object. It hangs off the
hierarchy's per-item GUI callback via `[InitializeOnLoad]`, so it needs no asset and no scene
object; the callback is unsubscribed before subscribing because a domain reload re-runs the static
constructor while the old delegate may still be registered.

It used to match `GameObject.name` against a hardcoded keyword list (`Manager` → dark blue,
`Canvas` → red) held in a `HierarchyPaintRule` struct. Both are gone: a colour is now a decision made
per object, not a consequence of what it happens to be called.

`TryResolveColors` answers with a `HierarchyStyle.Colors`, not with the component, so the caller
never has to know which of the two pairs it got.

**An object's own style always wins.** If it has a `HierarchyStyle`, its `Style` is used and the
search stops — a child that states its own colour is never overruled by the section it sits in.
Only when it has none does the search walk **up** the parents, taking the `ChildrenStyle` of the
first ancestor with `ApplyToChildren`. So a parent tinting three children still lets one of them
paint itself differently, which is the point of the split pair.

An ancestor whose style is *not* marked `ApplyToChildren` is skipped rather than ending the search,
so a plain style only ever paints its own row and cannot mask a section colour set higher up.

- The callback only runs on `EventType.Repaint`. Drawing on the layout/mouse events would fight the
  hierarchy's own hit-testing and swallow clicks.
- Unity draws the row **before** this callback, so the row is repainted over and everything the
  fill covered — icon and name — has to be re-drawn on top. The fill is exactly `selectionRect` and
  is **never widened leftwards**: `selectionRect` begins to the right of the foldout arrow, so any
  negative x offset paints over the expand arrow and the object can no longer be unfolded.
- The icon is re-drawn from `EditorGUIUtility.ObjectContent(gameObject, typeof(GameObject)).image`,
  which is the same icon Unity used (custom icon, prefab variant icon, and so on) rather than a
  guessed one. The label is then offset by `IconSize + LabelLeftPadding` to land where Unity had it.
- Painting over the row also erases Unity's selection tint, so a selected row lerps its background
  toward white to keep the selection legible. Inactive objects lerp their *text* toward the
  background and draw their icon at a lower alpha, standing in for the greying-out that was painted
  over.
- `GetComponent` runs per visible row per repaint. At the few dozen rows a hierarchy shows that is
  not measurable, and a cache would need invalidating on every add, remove and reparent.
- `GUIStyle` is built lazily, not in a static field initialiser. `EditorStyles` is not populated
  when `[InitializeOnLoad]` runs during a domain reload, and touching it there throws.
- 6000.5 made `EditorApplication.hierarchyWindowItemOnGUI` obsolete-as-**error** (CS0619) in favour
  of `hierarchyWindowItemByEntityIdOnGUI`, whose callback takes a `UnityEngine.EntityId` instead of
  an int instance id. `package.json` still declares `6000.0`, so both are kept behind
  `#if UNITY_6000_5_OR_NEWER` — the new symbol does not exist on 6000.0–6000.4. Each branch does
  nothing but resolve the id to a `GameObject` and hand it to the shared `Paint`.
- Selection is tested with `Selection.Contains(gameObject)` rather than searching
  `Selection.instanceIDs` / `Selection.entityIds`. The `Object` overload exists on both versions,
  so the version split stays confined to the two callbacks.

### HierarchyStyleMenu

`[MenuItem]` entries under `GameObject/Hierarchy Style/`, which is what the hierarchy's right-click
menu shows. Six colour presets plus `Clear`, all operating on the whole selection.

The presets are pure convenience: they add the component if it is missing and stamp a colour, so the
common case is two clicks and anything else is the colour picker in the inspector. The
[[HierarchyStyle]] component remains the source of truth — the menu writes to it and stores nothing
of its own.

A preset writes `Style` only, never `ChildrenStyle`. Inheritance is a deliberate act — it needs the
toggle turned on anyway — and a menu that silently recoloured a whole subtree would be a surprising
thing for a right-click to do.

- The methods take **no `MenuCommand` parameter**. A `GameObject/` item that declares one is invoked
  once per selected object; without it Unity calls the method once and the loop over
  `Selection.gameObjects` is the only iteration.
- `Undo.AddComponent` for a new component and `Undo.RecordObject` before mutating an existing one,
  so a preset is a single undo step either way. `Clear` uses `Undo.DestroyObjectImmediate`.
- Validation methods grey the presets out with an empty selection and `Clear` out when nothing in the
  selection carries a style. One validate method serves all six presets by stacking `[MenuItem]`
  attributes, which allow multiples.
- `EditorApplication.RepaintHierarchyWindow()` after every change: the hierarchy does not repaint on
  a component being added, so the colour would otherwise appear on the next unrelated redraw.

---

## Inspector validation

### ValidatableFields

A base class for the nested `Components` / `Configurations` classes. The nested class lists its
required references in one `Validate()` override; the host MonoBehaviour's `OnValidate` is a single
call, `_components.ValidateFields(this)`.

- The owner is handed over **once** through `ValidateFields(context)` and stashed as `Context`, so
  `ValidateNull(field, nameof(field))` stays two arguments. Passing the context to every call was
  the duplication worth removing — there is one line per field and nothing else. `Context` is
  `protected` because a derived `Validate()` sometimes needs it for a check of its own, the way
  `LoadManager.Configurations` passes it to `SequenceRunner.ValidateSequences`.
- `Validate()` is **virtual with an empty body**, not abstract. Every `Components` and
  `Configurations` in the project extends the base, and several have nothing to check yet; an
  abstract method would make each of those carry an empty override. Extending costs one word, and
  the class is then ready the moment a field is added.
- Every MonoBehaviour with these nested classes calls both in `OnValidate` —
  `_configurations.ValidateFields(this)` then `_components.ValidateFields(this)`. `LoadManager` and
  `SceneLoader` used to have a bespoke `ValidateSequences(context)` on their `Configurations`; that
  is now the `Validate()` override, so there is one entry point per nested class rather than one per
  kind of check.
- Not every serialized reference is validated. `LoaderUI.Components.Image` is left out because
  nothing reads it — validating a field the code never uses would demand an assignment for no
  reason. Optional-by-design references are the general case of this, and the base has no way to
  express one; leaving the `ValidateNull` line out is how a field is declared optional.
- The parameter is `UnityEngine.Object`, not `object`, and that is the whole point: Unity overloads
  `==` so a *missing* or *destroyed* reference compares equal to null. A plain `object` check would
  call the wrong equality and pass a broken reference as valid.
- `Debug.LogError` is given the context object, so clicking the console entry selects the offending
  GameObject. The message names the owning type, the object, and the field, because a bare
  "unassigned reference" in a scene of many objects is not actionable.
- Validation is **not** a load-time guard. `OnValidate` is editor-only, so this catches an
  unassigned field while authoring; a missing reference still throws a plain
  `NullReferenceException` at runtime. That is the trade taken deliberately — the runtime path stays
  free of null checks, see [Loading an entity](#loading-an-entity) for the same reasoning about what
  is worth checking twice.
- The class lives in the package because it knows nothing about any game; only the fields it is
  pointed at are game-specific.

---

## Utils

### RayCastUtils

`GetComponentsAtPosition<T>(Vector2 worldPosition)` returns every `T` sitting at a world point.
Written for picking whatever is under the cursor without the caller knowing about colliders.

- `where T : class`, so `T` can be an interface — the whole point is asking for a *contract*
  (`IInteractableEntity`) rather than a concrete component.
- It searches **parents and children** of each hit collider, not the collider's own GameObject. A
  collider is usually a child of the thing that owns the behaviour, or a parent of the art; either
  way the component answering for the object is rarely on the collider itself.
- Results are de-duplicated through a `HashSet`. Overlapping colliders on one hierarchy, and the
  parent/children sweeps overlapping each other, both return the same component twice.
- **2D only** (`Physics2D.RaycastAll` with a zero direction, which is how the Input System-era
  point query is spelled). A 3D sibling is the obvious extension but is deliberately not written
  until a 3D game needs it.
- Position is a **world** position. Screen-to-world is the caller's job, since only the caller knows
  which camera it means.

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

Keep the `.meta` files inside `Samples~`, even though Unity never reads them there. Import copies
them along with the assets, so the GUIDs survive; without them Unity mints fresh GUIDs on import and
every reference *inside* the sample breaks — the demo scene loses its prefabs, the prefabs lose their
mixer and ScriptableObjects.

Every new sample folder needs its own entry in the `samples` array, or it ships invisibly with no
way to import it. The path in that entry is a folder under `Samples~`, never `Samples~` itself.

The folder drifted back to a plain `Samples/` at some point, which is how the demo scene ended up
shipping as a read-only asset in consuming projects — and `Samples/Resources/` with it, force-including
a test audio clip in every player build. If the scene is ever openable from the Project window in this
repo, the tilde has been lost again.

### Dependencies

`package.json` declares `com.unity.ugui`, because `LoaderUI` and `CustomAnimator2d` use
`UnityEngine.UI`, which is a package rather than a built-in module. Nothing else needs declaring:
`UnityEngine.Audio`, `Animator` and `SceneManagement` are built-in modules, always present.

The rule is that anything outside a built-in module has to be listed here, or the kit compiles in
this repo — which has the package in its project manifest — and fails in a consuming project that
does not.

### csc.rsp — CS1998 and CS8632 suppressed everywhere

`-nowarn:1998` ("this async method lacks await") is set through a `csc.rsp` next to every assembly:
`Assets/csc.rsp` for the predefined assemblies, and one beside each of the four `.asmdef`s. The kit's
loading contract is `Awaitable`-returning (`ILoadableEntity.Load()`, `MySingleton.OnLoad()`), so an
implementation that has nothing to await is the normal case, not a mistake — and `#pragma warning
disable CS1998` around each one was noise the no-comments rule already rejects. Those pragmas were
removed when the rsp files landed.

There is no project-wide switch: Unity regenerates the `.csproj` on every reimport, so
`Directory.Build.props` and `.editorconfig` severities are discarded.

`-nowarn:8632` sits beside it, for the same files. It silences "the annotation for nullable
reference types should only be used in code within a `#nullable` annotations context", which is what
C# says about a `BoardTile?` return type while nullable reference types are switched off — as they
are here, and as they stay.

**`?` on a reference type is documentation in this project, not a compiler contract.** It marks a
return or field that is legitimately null (`Board.GetTile`, `MapManager.GetTile`) so a caller reads
the signature and knows to branch. Nothing enforces it: there is no flow analysis, no CS8600 family,
and an unchecked dereference compiles silently. Turning nullable on for real would flag every Unity
serialized field — they are all null to the compiler and assigned by the inspector — so the
annotation stays advisory.

**A new `.asmdef` needs its own `csc.rsp` in the same folder**, or both warnings come back for that
assembly alone.

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

## Game scripts (Assets/Scripts)

**Nothing outside `Packages/` belongs to the package.** `Assets/` is a throwaway game prototype
whose only purpose is to exercise the kit before it is consumed by a real project. Code there may
hold game logic freely — the no-game-logic rule applies to the package alone. It is documented here
anyway, under the same no-comments rule.

### InputManager

Wraps `PlayerInput.inputactions` and republishes each action as a plain C# event, so nothing else in
the game touches the Input System directly. Currently one action: `LMB` -> `OnLeftMouseButton`.

**PC only, deliberately.** The `LMB` action was briefly bound to `<Mouse>/leftButton` *and*
`<Touchscreen>/primaryTouch/tap`, and that was undone. One action cannot honestly serve both:
`tap` is a *completed gesture* that pulses after the finger lifts, so it has no press edge to
report, while `leftButton` is a held state with a real press and release. A drag also needs a
position, which the mouse takes from one shared cursor and touch carries per-finger. Mobile, if it
ever comes, gets its own action and its own event — not a second binding on this one.

- The press comes from `started` / `canceled`, not `performed`. A Button action fires `performed`
  the moment the press crosses the threshold and never again until release, so `performed` alone
  cannot tell press from release. `started` is the press, `canceled` the release.
- Actions arrive as inspector-assigned `InputActionReference`s, not as an `InputActionAsset` plus
  map/action name strings. The reference serializes the asset GUID and the action's **id**, so
  renaming the action in the asset cannot break the lookup, and there is nothing to keep in sync by
  hand. `Components` keeps the reference private and exposes `InputAction` directly, so callers
  never unwrap `.action`.
- Events are named after the physical input (`OnLeftMouseButton`), not after a device-neutral
  abstraction. With the platform scope narrowed to PC, a name like `OnPrimaryTap` would be claiming
  a portability the class does not have. No underscores in public members either — an `_` prefix
  means "private field" everywhere else here.
- Subscribe and unsubscribe are one method taking an `activating` flag, so the pairs cannot drift
  apart as more actions are added. Enabling happens per **action**, not per map: the manager owns
  the actions it exposes and has no business switching on the rest of `PlayerActions`.
- Actions are enabled in `OnLoad` and disabled in `OnDestroy`, since `Awake` does nothing anywhere
  in this project — see [Singleton](#singleton).

### Board, BoardTile and MapManager

**The board is scene objects, not tilemap data.** `Board` holds one `BoardTile` GameObject per cell;
`MapManager` reaches the board through `Components.Board` and exposes it to the rest of the game.
The `Tilemap` is gone from `MapManager` entirely.

Why the tilemap lost the board:

- The logical grid is now independent of the art. Tile coordinates came from the paint's `Grid`
  origin, so `(0,0)` was wherever the artist started — `(-4,-4)..(1,1)` in the 6x6 scene, with no
  corner at the origin. Own objects mean own origin.
- The tiles are isometric **boxes**, so their sprites overlap their neighbours. The overlap is only
  in the art: the top face is a diamond and diamonds tile the plane exactly, so a per-cell collider
  on the *top-face diamond* resolves a point to exactly one cell. A `TilemapCollider2D` is one
  collider for the whole map and cannot do that.
- Those boxes need per-tile draw order (`sortingOrder` from the coordinate, front-most being the
  largest `x + y`), which is a `SpriteRenderer` per cell.

A `Tilemap` is still the right tool for static decorative map art. It just is not the board.

- `Board.Initialize()` **generates** the board: it destroys every child, then instantiates
  `Components.BoardTile` (a prefab) once per cell. Nothing is authored in the scene, so there are no
  36 transforms to keep in step and no coordinates to type in — the misnumbering the tilemap origin
  caused cannot come back. It is called from `MapManager.OnLoad`, keeping the manager the one entry
  point for map questions.
- `BoardTile.Coordinate` is `Vector2Int` behind `Initialize(coordinate)`, not a serialized field.
  `Vector2Int` and not `Vector2` because coordinates are looked up by equality, and integer
  equality is exact.
- `BoardTile.Initialize` also paints the tile: `(x + y) % 2` picks between the `Normal` and
  `Alternated` sprites, which is what gives the board its checkerboard. The parity of the *sum* is
  the whole trick — it flips on every step in either direction, so no neighbour ever shares a
  colour. The same `x + y` will later give the draw order, since both are asking the same question
  about a diagonal.
- The tile paints itself rather than the board painting it. `Board` decides *where* a tile goes and
  *what* its coordinate is; everything that follows from the coordinate belongs to the tile.
- **The centre cell sits on the board's own origin.** `center` is `(Size - one) / 2`, so a 6x6 board
  centres on `(2, 2)` (there is no true middle cell on an even board; this picks the lower-left of
  the four) and an odd board centres exactly. Every tile is then placed at the offset from that
  cell, which means moving the `Board` GameObject moves the whole board and the coordinates never
  change.
- Placement is the isometric formula spelled out — `x' = (dx - dy) * cellWidth / 2`,
  `y' = (dx + dy) * cellHeight / 2` — not a `Grid` component. A `Grid` would do the same maths, but
  it is another required reference whose `Cell Layout` has to be set to Isometric or the board comes
  out silently rectangular. The board deliberately owns its own geometry now.
- `CellSize` defaults to `(1, 0.5)`: the tile's **top face** is 32x16 px at 32 pixels per unit. It
  is the top face that matters, not the sprite — the sprites are isometric boxes and overlap their
  neighbours, while the top-face diamonds tile the plane exactly.
- `Size` and `CellSize` are serialized with initializers rather than being consts, so the board can
  be resized without touching code. Both are visible in the inspector, which is what makes that
  safe: a field added to an already-serialized component can deserialize to zero, and a `(0,0)`
  board is obvious on sight.

**Open, deliberately unwritten:** the top-face `PolygonCollider2D` on the prefab, and
`sortingOrder` from the coordinate (front-most is the largest `x + y`) so the boxes layer correctly.
`BoardTile.Components` is empty until those are decided. `Initialize` uses `Destroy`, so it is a
play-mode operation; previewing the board from an editor `[Button]` would need `DestroyImmediate`.

---

### EntityManager

`MySingleton<EntityManager>` next to `MapManager`. It owns the runtime population of the board:
`Components.PlayerEntity` is a **prefab**, `Configurations.InitialCoordinate` is a board coordinate,
and `OnLoad` spawns one player there.

- **`InitialCoordinate` is `Vector2Int`, not `Vector2`.** Board coordinates are integers everywhere
  (`BoardTile.Coordinate`, `Board.GetTile`), and a float coordinate would only invite an
  equality comparison that never matches.
- **Tile lookup returns the tile, not a `bool` with an `out`.** `GetTile` gives back the `BoardTile`
  or `null`; null *is* the "not found" answer, so the `Try`/`out` pair only added ceremony. The
  pattern earns its keep when the result is a struct (no null to return) or when the failure is
  expected and cheap to branch on — neither holds for a `MonoBehaviour` reference.
- **Two lookups, exact inverses of each other.** `GetTile(Vector2Int)` answers "where is this
  coordinate"; `GetTileAtWorldPosition(Vector2)` answers "which coordinate is this point", by
  inverting the placement formula: `dx = y/h + x/w`, `dy = y/h - x/w`, then `RoundToInt`. Rounding
  those two independently is *exactly* the diamond cell — the square that rounds to an integer pair
  in tile space maps to a diamond in world space — so the point query needs **no collider at all**,
  and the arbitrary hit order of a zero-length `Physics2D.RaycastAll` (see
  [Known issues](#known-issues)) never enters into picking a tile.
- `CenterOffset` is subtracted before that inverse. Tiles are *placed* by their root but entities
  stand on `BoardTile.CenterPosition`, which the prefab may offset (a box sprite pivots at its base,
  its top face does not). Without the correction, feeding a tile's own `CenterPosition` back into
  `GetTileAtWorldPosition` could return the neighbour — an off-by-one that only shows up as a piece
  snapping one cell away. It is read from a live tile rather than assumed, cached, and cleared in
  `DestroyChildren`.
- `Center` is a property, used by both placement and the inverse. It was a local in `BuildTiles`
  passed down to `BuildTile`; the two directions have to agree on where the origin is, so it can
  only be defined once.
- The spawn goes through `MapManager.GetTile`, never through arithmetic on the board size. The
  manager asks the board where a coordinate is; only `Board` knows the isometric formula. A missing
  coordinate is a `Debug.LogError` and no player, not an exception — a bad inspector value should
  not take the load sequence down.
- **`EntityManager` must load after `MapManager`** in the `LoadManager` sequence: `GetTile` is
  answered from the list `Board.Initialize()` fills, and that runs in `MapManager.OnLoad`.
- The spawned player is `await player.Load()`-ed by hand. `PlayerEntity` is an `ILoadableEntity`,
  but a prefab instantiated at runtime is not in the scene's load sequence, so nothing else would
  ever call it. Anything spawning a loadable entity owns its `Load()`.
- The player is placed on `BoardTile.CenterPosition`, a child transform the prefab authors, not on
  the tile's own transform — the tile root sits wherever the sprite pivot needs it, and the point an
  entity stands on is a separate, authorable thing. It is the tile that exposes where to stand; the
  entity side is still root-positioned, so a prefab whose art is offset from its root will float.

---

### IInteractableEntity

The contract the `MouseManager` states will speak to: `Priority`, `CanInteract()`,
`OnInteraction(InteractionType, InteractionState)`. `PlayerEntity` is the first implementer.

- `Priority` is typed **`InteractionPriority`**, not `int`. `UpdateManager.Register` takes a raw
  `int` because it lives in the package and cannot know the game's order enum
  ([Update dispatch](#update-dispatch)); this interface is game-side, so the enum is right there and
  the weaker type buys nothing. Every interactable is ranked on one scale by design — two entities
  under the cursor have to be comparable. Leave gaps between values.
- `CanInteract()` is a **method, not a property**, because the answer depends on the entity's state
  at the moment it is asked, not on stored data. A property reads like a cached flag.
- `OnDrag(Vector2 worldPosition)` is a **second channel, not an `InteractionState`**. The pointer
  moving is a continuous stream, not one of the two edges `OnInteraction` reports, and folding it in
  would have meant an `InteractionState.Moving` that carries no position. `MouseManager.Dragging`
  pumps it from `Update` at whoever it is holding; what the entity does with a world position — snap
  to a tile, follow freely, ignore it — is the entity's business, so the manager sends the raw point
  and no instruction.
- `InteractionState` lives in `Scripts.Enums`, not nested in `InputManager` as `ClickState`. Both
  the input side and the interaction side name the same two edges, and a nested enum would have made
  every interactable depend on the input manager to describe itself.

### PlayerEntity

A finite state machine over `Idle` / `Dragging`, built exactly like [MouseManager](#mousemanager) —
`partial class`, `Base/` for the shared state, `FiniteStates/` one file per state — and the first
`IInteractableEntity`.

- Its tick is `OnFixedUpdate`, not `Update`: the entity registers for
  `UpdateType.FixedUpdate` / `UpdateOrder.Entities`, so its states move a body on the physics clock
  while `MouseManager`'s read input on the frame clock. The two machines are deliberately not on the
  same channel, and the base method is named after the channel it is driven by so no state is
  confused about which one it is on.
- `OnEnter` / `OnExit` are abstract, `OnFixedUpdate` virtual and empty — same split as
  `MouseManager.BaseState`, same reason.
- `Components.DraggingPosition` is exposed as `{ get; private set; }` over a
  `[field: SerializeField]` backing field. A true get-only auto-property makes that field
  `readonly`, which the Unity serializer should not be asked to write into.
- `_initialArtLocalPosition` is captured in `BaseState.Start`, before any `OnEnter` has run, and is
  what `Idle` returns the art to. It lives on `BaseState` rather than on the entity because it is
  only ever read by states. **It is a `localPosition`, deliberately.** It used to be a world
  position, which was correct only while the entity itself never moved; once dragging moves the root,
  restoring a world position would drop the art back on the tile the drag started from and leave it
  detached from its own root. The local offset is the thing that is actually invariant.
- The entity does not listen for input. `MouseManager` decides who was picked and calls
  `OnInteraction`, which is the only thing that drives this machine. That keeps the priority
  arbitration in one place instead of every interactable racing to claim the same click.
- `Components` carries three transforms, each answering a different question: `ArtPosition` is what
  moves, `DraggingPosition` where it goes while dragged, `GroundPosition` the point that actually
  touches the tile. The last one exists because the art's own origin is not on the ground on an
  isometric map — a sprite pivots somewhere up its body, and asking the tilemap about *that* point
  returns the wrong cell.
- `Load` dereferences `MapManager.Instance`, so **`MapManager` has to load before `PlayerEntity`**
  in the `LoadManager` sequence, and its tiles have to be built before the entity asks for one.

**Dragging: the root moves, and it is always on a tile.**

The entity is four separable things — the root (the whole piece), `ArtPosition` (the sprite only),
the shadow (another child of the root), and `DraggingPosition` (how far up the art lifts). Dragging
uses two of them at once, and that split is the whole design:

- **The root snaps, the art lifts.** `Dragging.OnEnter` raises `ArtPosition` to `DraggingPosition`,
  and from then on `OnDrag` moves the *root* to `BoardTile.CenterPosition`. Because art and shadow
  are children, the lift rides along and the shadow stays on the board — the piece reads as held
  above the tile it is over.
- **The root is never set to the pointer.** It is set to a tile, and only ever to a tile. That is
  what makes "can't leave the board" fall out for free instead of needing a clamp: when
  `GetTileAtWorldPosition` returns null the drag simply does nothing that frame, and the piece stays
  on the last tile it was over. There is no state in which the entity is between tiles or off the
  edge, so nothing downstream has to handle one.
- **Releasing needs no move.** "Move to the tile below it" is already true — the root was snapped
  every frame of the drag, so `Idle.OnEnter` only drops the art back and calls `SnapToCurrentTile`
  to settle any sub-pixel drift. `_currentTile` is the answer to "where is this piece", maintained
  during the drag rather than recomputed at the end.
- `_currentTile` is `BoardTile?` and resolves in `Load` from `GroundPosition`, not from the root:
  the root sits at the tile *center*, but the point that touches the board is the ground point, and
  on an isometric map those differ.
- `MoveToTile` / `SnapToCurrentTile` live on the entity, not on the state. Where the piece is, is a
  fact about the entity; the states only decide *when* to move it.

---

### MouseManager

A finite state machine over `Idle` / `Dragging`, built as a `partial class` split the same way
`LoaderUI` is — `MouseManager.cs` holds the machine, `Base/` the shared state, `FiniteStates/` one
file per state. States are `private` nested classes, so they reach `_components` and
`_configurations` without either being exposed.

- `OnEnter` / `OnExit` / `Update` / `OnLeftMouseButton` are **`void`**, not `Awaitable` as on
  `LoaderUI.BaseState`. This machine is driven per frame, and an `async void`-shaped `OnEnter` would
  keep running detached after the state had already been exited. `LoaderUI` needs awaitables because
  its transitions wait on animations; nothing here waits on anything.
- `OnEnter` / `OnExit` are **abstract**; `Update` and `OnLeftMouseButton` are **virtual with empty
  bodies**. Every state has to say what entering and leaving it means, but a state that ignores the
  frame tick or the mouse should stay silent rather than carry an empty override — the states that
  do override are then the interesting ones.
- Input reaches the states through `MouseManager`, never directly: it subscribes once to
  `InputManager.OnLeftMouseButton` and forwards to `_currentState`. A state subscribing on `OnEnter`
  would have to remember to unsubscribe on `OnExit`, and a missed pair leaves a dead state reacting
  to clicks.
- Picking lives on `BaseState.TryGetInteractableUnderMouse`, not in the states: `Idle` needs it to
  find a drag target and any later state that needs it gets the same one method. It filters on
  `CanInteract()` and takes the **highest** `InteractionPriority` — larger enum value wins, the way
  a sorting order does, so "on top" and "higher number" agree.
- `_currentInteractable` is held by the manager, not by `Dragging`, and cleared in `Dragging.OnExit`
  rather than by whoever changes state. Every exit from the drag frees it, including one that does
  not go through the mouse.
- `GetMouseWorldPosition` converts through `_camera`, which is `Camera.main` **resolved once in
  `OnLoad`** and cached. `Camera.main` is a tag search, so calling it per click would be wasteful;
  caching it also means the manager needs nothing wired in the inspector. The cost is that the
  camera is whatever carries the `MainCamera` tag at load time — a camera swapped in later, or a
  scene loaded afterwards with its own, will not be picked up.
- **Load order matters.** `OnLoad` dereferences `UpdateManager.Instance` and `InputManager.Instance`,
  so both must appear before `MouseManager` in the `LoadManager` sequence. `OnDestroy` guards both
  with `HasInstance` instead, since teardown order is not ours to choose.
- `_allStates` is an instance field, not `static` as on `LoaderUI`. Each state caches its `_parent`
  in `Start`, so a static list would hand every instance the last one's parent. Harmless for a
  singleton, wrong in general.
- `Update` reaches the states through `UpdateManager` (`UpdateOrder.Managers`), not a Unity `Update`
  — see [Update dispatch](#update-dispatch). `UpdateOrder.Managers` is `0`, ahead of
  `Entities`, so a manager's state has settled before entities read it in the same frame.
- The folder is nested (`Managers/MouseManager/…`) but the namespace stays `Scripts.Managers`, the
  way `Runtime/Managers/LoadManager/` stays in `Vkaike2.StarterKit.Managers`. A
  `Scripts.Managers.MouseManager` namespace would collide with the class name.

---

## Components

### CustomAnimator

A sprite-swapping animator: a list of clips, each a list of sprites plus a one-line duration string.
It exists because the Unity `Animator` brings a state machine and a per-object controller asset with
it, and a pile of enemies sharing the same animation shapes wants neither. The whole configuration
lives on the component, so it copy/pastes onto another prefab and only the sprite lists change.

- Folder and namespace are `Components/Animations`, **not** `Components/Animator`. A namespace
  segment named `Animator` shadows `UnityEngine.Animator` for every type declared inside it, which
  would make [[AnimatorExtension]] unreachable from here.
- The nested clip class is `Clip`, not `Animation`, for the same reason: `UnityEngine.Animation` is a
  real component type, and a nested class silently winning that lookup is a trap.
- The inspector label field must literally be called `name` (`[HideInInspector] public string name`).
  Unity reads that one field to title a list element; any other name just adds a text box. Same
  convention as `LoadSequence.Entity`.

#### The configuration string

`t:{total seconds} f{index}:{seconds}`. `t` is the duration of one full pass; any frame without an
explicit `f` splits what is left of the total between them. `t:1 f0:0.4` over four frames is a held
first frame and three quick ones.

- Parsed with `CultureInfo.InvariantCulture`. `float.Parse` on a machine with a comma decimal
  separator reads `0.25` as `25`, which is a hundredfold timing bug that only appears on someone
  else's computer.
- Durations are `float`, not `int`. Single frames are routinely well under a second.
- `TryBuildFrames` returns a message rather than logging, so `OnValidate` and the runtime lazy build
  share one parser and one wording. Every rejection names the offending order.

#### Layers

A clip does not hold sprites, it holds `Layer`s — a name plus a sprite list. Every layer of a clip
holds the *same frames seen differently*: same count, same timing, same indices, same events. The
motivating case is a character animated front-facing and back-facing; `walk` is one clip either way,
and turning around must not restart it.

- **The layer is animator state, not clip state.** `SetLayer("Back")` sets it once and it survives
  every later `Play`. A clip resolves the name to an index when it starts; `SetLayer` re-resolves it
  for the clip already running and repaints the frame on screen, so the swap happens mid-animation
  with the frame index and elapsed time untouched.
- **An unknown layer name falls back to index 0.** Most clips have a single layer, and a character
  facing away still has to be able to play a clip that was only ever drawn one way. To keep that
  from swallowing typos, the component collects every layer name declared by any clip and `SetLayer`
  errors on a name outside that set.
- **A single layer needs no name.** That is the old shape of the component, unchanged in behaviour.
  Two or more layers must all be named and named uniquely, checked in `OnValidate`.
- **Layers must agree on frame count**, or the shared configuration string means nothing. Rejected by
  the same `TryBuildFrames` path as every other configuration error.
- `Frame` holds `Sprite[] SpritesByLayer`, built once per clip, so switching layer is an array index
  rather than a name lookup per swap.
- **`Stop` deliberately does not clear `_currentFrame`.** It is what is *on screen*, not what is
  playing. A static idle clip ends its playback immediately, and a later `SetLayer` still has to be
  able to repaint it — clearing it there left a character facing the wrong way after turning around.
- `Layer` is nested inside `Clip` with public members, and `Frame` is `public` for the same reason: a
  containing type gets no access to its nested type's private members, only the other way round.
- Restructuring `_spriteFrames` into `_layers` **breaks existing serialized data**. There is no
  `FormerlySerializedAs` across that shape change; sprite lists had to be dragged in again.

#### Single frame clips

A clip with **one sprite and an empty configuration is static**: it swaps the sprite and stops.
`Clip.IsStatic` says so, and everything else follows from two properties, so `PlayClips` needed no
change at all:

- `ItLoops => _itLoops && !IsStatic`, so a static clip never re-enters the frame loop. Without that,
  ticking *it loops* on a held sprite spins one `NextFrameAsync` per frame forever, doing nothing.
- `TryBuildFrames` short-circuits to a single zero-duration frame before the configuration is even
  looked at. The zero duration costs one `NextFrameAsync` before playback ends — the sprite is
  already on screen by then, since it is set before the await.
- `_configuration` no longer defaults to `ConfigurationExample`. A pre-filled `t:1 f0:0.4` would make
  every new single-sprite clip fail validation until the field was cleared by hand. The example
  survives in the tooltip and in the empty-configuration error, which now names the frame count so
  the message reads as *this clip has more than one frame, it needs timing*.
- `OnValidate` rejects a static clip with a `NextClip`: it has no duration, so the chain would be
  invisible. Give it a `t:` and it is an ordinary one-frame clip again.
- The inspector label reads `idle (static)`, ahead of `(loops)` and `-> next`.

#### Playback

- Cancellation is a `CancellationTokenSource` linked to `destroyCancellationToken`, **not**
  `Awaitable.Cancel()` on the stored awaitable. `Awaitable` is pooled and recycled the moment it is
  awaited, so holding one past its completion and cancelling it later can cancel an unrelated
  operation somewhere else in the game.
- The clip chain (`NextClip`) is a `while` loop inside one playback method, not `Play()` calling
  itself from the end of the previous clip. The recursive version cancelled the very awaitable it
  was still running inside.
- A looping clip re-enters through a `do/while` over the frame list. The bug worth remembering: the
  original reset `i = 0` at the last frame and let `i++` carry it to `1`, so every pass after the
  first skipped frame 0.
- A frame with a zero duration awaits `NextFrameAsync` instead of `WaitForSecondsAsync(0)`. Without
  that, a looping clip whose durations all resolve to zero is an infinite loop that never yields and
  hangs the editor. An empty frame list ends playback for the same reason.
- `OnDisable` cancels and `OnEnable` replays `CurrentClipName`. An `Awaitable` is driven by the
  player loop, not by the component, so a deactivated object would otherwise keep swapping sprites.
  Together they make the component pool-friendly for free.
- `EnsureReady` resolves the renderer and builds the name lookup on first use, so `Play` works even
  when whoever owns the object forgot to call `Initialize`. `Initialize` is still the entry point
  that honours `StartPlaying`.
- `_useUnscaledTime` is polled per frame rather than handed to `WaitForSecondsAsync`, which only
  knows scaled time. UI animations that have to keep moving during a pause need it.

#### Frame events

`Initialize(params ClipEvents[])` is how the owner wires callbacks: a clip name, a
`Dictionary<int, Action>` keyed by frame index, and an optional `Finished`. Nothing about events is
serialized, and the animator never decides what an event means — it only knows *when*.

- **The owner declares them, not the inspector.** A `CustomAnimator2d` has exactly one owner (the
  entity that calls `Initialize`), so changing what frame 3 of `attack` does is a code edit in that
  entity, not a prefab edit. That is the whole reason this is not a serialized `UnityEvent` list.
- **A second `Initialize` replaces the table wholesale**, so there is no `On`/`Off` pair and no way
  to leak a subscriber. `Initialize()` with no arguments clears it.
- **Events live on the component, not on `Clip.Frame`.** Baking an `Action` into a frame was the
  first idea and it is wrong twice: `_frames` is a cache `OnValidate` nulls at will, so callbacks
  registered after the first build would silently vanish; and a pure sprite/duration table that
  never references a game entity is what keeps a pooled owner from leaking.
- **Index-keying cannot survive reordering.** Deleting a sprite is caught (`Initialize` checks every
  index against the clip's real frame count); reordering two sprites is not, and frame 3 quietly
  becomes a different pose. Accepted on purpose — the alternative was markers in the configuration
  string, which puts the wiring back in the inspector.
- **Every callback is invoked inside its own `try/catch`.** The dispatch sits in the same `try` as
  the playback loop, which only catches `OperationCanceledException`; without the inner catch a
  throwing callback unwinds `PlayClips` and freezes the sprite mid-clip.
- **Calling `Play` from inside a callback works** — it cancels the token the loop is about to await
  on, so the next `await` throws and unwinds cleanly. `Finished` is followed by an explicit
  `ThrowIfCancellationRequested`, because a `Play` from there has no await left to notice it and
  would otherwise let the old chain advance into `NextClip` over the new playback.
- `Finished` fires when a clip ends, per clip in a chain. A looping clip never reaches it. It exists
  because the last frame *enters* before its duration elapses, so a frame event on the last index is
  early by that frame's duration.
- `Initialize` forces the clip parse (it needs frame counts to validate indices), so a broken
  configuration string now surfaces at load rather than on the first `Play`.

#### Validation

`OnValidate` checks what cannot be checked at runtime without a cost: the renderer matching `_isUI`,
at least one clip, unique clip names, every `NextClip` resolving to a real clip, no null sprites, and
the configuration parsing. Cross-clip checks (duplicates, chain targets) live on the component
because a `Clip` cannot see its siblings; everything else lives on the `Clip`.

It deliberately does **not** use [[ValidatableFields]] — that base is for the `Components` /
`Configurations` reference groups, and none of these are null checks on references.

---

## Known issues

- `SceneLoader.LoadSequence()` — the `ILoadableEntity.Load()` a scene's own loader exposes — was
  briefly named `LoadSequence`; the interface member is `Load()`. Nothing else carries the old name.

Resolved: the `[field: SerializeField]` auto-properties on `Configurations` / `Components` do
serialize — the attribute targets the compiler-generated backing field, which is what Unity picks
up. `SceneLoader.Configurations` was the real bug: it was missing `[Serializable]` entirely.
`LoadSequence.IsActive` was the same shape of bug — an auto-property with no link to the serialized
`_isActive` field, so every sequence filtered out as inactive. It is now `=> _isActive`.
