# Walking skeleton: set-up guide and lessons

This guide goes with the walking-skeleton code in `IndoorNavigation/Assets/IndoorNavigation/Runtime`, `Tests/EditMode` and `Assets/StreamingAssets/SamplePackage`. The code follows the architecture doc (Ports and Adapters, one composition root, the session statechart, passive views) and the screen index in the UI blueprint.

The code arrives complete. Your part is the work only Unity can do (scene, assets, Inspector wiring, device build), the commits, and six short lessons that each end with a small change you write yourself.

| Part | You do | Time |
|---|---|---|
| A | Set up the App scene and run the tests | 1.5 h |
| B | Click through the scenario in the Editor | 0.5 h |
| C | Commit in dependency order | 0.5 h |
| D | Build to the iPhone and record the test | 1 h |
| Lessons 1–6 | Read, try, and one exercise each | about 6 h |

That is about 9.5 hours instead of the 17 planned for building the skeleton, which leaves time in week 2 for the scanning work.

## What is in the folder

```
Docs/decisions/                 six decision records (Nygard format)
Docs/walking-skeleton-guide.md  this guide
.gitattributes                  keeps the sample package's bytes unchanged
IndoorNavigation/Assets/
  IndoorNavigation/Runtime/
    Core/        IndoorNavigation.Core       no Unity at all (No Engine References)
      Session/     AppSession, the statechart, view mode, tilt switch, screen codes
      Ready/       package version checks
      Venue/       places, route graph, manifest, validator
      Routing/     Dijkstra route planner, route progress
      Ports/       the six ports plus IClock
    Adapters/    IndoorNavigation.Adapters   -> Core
      Placeholders/  one placeholder per port
      Data/          JSON reader for the package, the session log file
      AR/            the real phone attitude from the AR camera
    UI/          IndoorNavigation.UI         -> Core
      Tokens.uss, AppShell.uxml, ScreenHost, ScreenPresenter, RouteView, Wording
      Screens/     ten screens: UXML plus one presenter each
      Developer/   developer panel (W)
    App/         IndoorNavigation.App        -> Core, Adapters, UI, Input System
      CompositionRoot, RunConfig, DeveloperActions, DeveloperKeys
  IndoorNavigation/Tests/EditMode/   IndoorNavigation.Core.Tests -> Core (215 tests)
  StreamingAssets/SamplePackage/     manifest.json, catalogue.json, graph.json
```

The sample building is the one in the route-graph diagram: two floors, 13 places, 26 nodes, stairs and a lift. The studio door has two steps, so "Avoid stairs" to the colour printer has no route (screen 5b).

## Part A: set up the App scene (1.5 h)

### A1. Before you start

1. Make a branch: `git switch -c feat/walking-skeleton`
2. `git status` lists `IndoorNavigation/Assets/_Recovery/`. Unity wrote it after a crash; it holds backup scenes. Never stage it. Delete it in Unity once you are sure you don't need those backups.
3. Open the project in Unity 6000.3.24f1. Unity imports the new files, writes a `.meta` file next to each one, and compiles. The Console must show no red errors.

### A2. Panel Settings and Run Config

1. In the Project window, open `Assets/IndoorNavigation/Runtime/UI`. Right-click > **Create > UI Toolkit > Panel Settings Asset**. Name it `AppPanelSettings`.
   - Unity also creates `Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss` the first time, and sets it as the Theme Style Sheet. Keep it: without a theme, buttons and text fields have no look. If it is not there, create it with **Create > UI Toolkit > Default Runtime Theme File** and set it yourself.
   - In the Inspector set **Scale Mode** to *Scale With Screen Size*, **Reference Resolution** to 390 × 844, **Screen Match Mode** to *Match Width Or Height* and **Match** to 0 (width). The USS sizes are points on a 390 × 844 screen, like the blueprint.
2. Open `Assets/IndoorNavigation/Runtime/App`. Right-click > **Create > Indoor Navigation > Run Config**. Name it `RunConfig` and keep every port on *Placeholder*.

### A3. The App scene

1. **File > New Scene**, choose the **Empty** template.
2. **GameObject > XR > AR Session** and **GameObject > XR > XR Origin (Mobile AR)**. The XR Origin brings its own camera.
3. Select **AR Session** and untick the checkbox next to its name, so it starts inactive. CompositionRoot switches it on at screen 1, so iOS asks for the camera after the download, as the V3 screen says.
4. **GameObject > Create Empty**, name it `App`. Add two components:
   - **UI Document**: Panel Settings = `AppPanelSettings`, Source Asset = `Runtime/UI/AppShell.uxml`.
   - **Composition Root**: Config = `RunConfig`, Document = the UI Document above, Ar Camera = *XR Origin > Camera Offset > Main Camera*, Ar Session = *AR Session*.
5. Save as `Assets/IndoorNavigation/Scenes/App.unity`.
6. **File > Build Profiles > iOS**, in the scene list: add `App`, drag it to the top, and untick `LocalisationLab` (keep it in the list for the world-map recording in week 3).

### A4. Run the tests

**Window > General > Test Runner > EditMode > Run All.** Expect 215 passed. They run in seconds because the core needs no phone, no AR and no UI.

### A5. Press Play

The Console shows `[IndoorNavigation] Composition root started` and the path of the session log. Screen V1 appears. Use **Window > General > Device Simulator** with an iPhone so the layout looks as it will on the phone.

If something is wrong, see "When something goes wrong" at the end.

## Part B: click through the scenario (0.5 h)

The developer panel (W) plays the world around the app: the download, the position, the phone pitch and the search answers. Your own taps stay on the real screens. Open it with a triple tap on the top-left corner, or use the keys (click the Game or Simulator view first so it has keyboard focus).

| Key | Panel button | Does |
|---|---|---|
| W | | open or close the panel |
| D | Download fails | the next download stops halfway (V3b) |
| F | Position found | the room is recognised |
| L | Position lost | tracking is lost (banner on B) |
| Up / Down | Raise · AR / Lower · 2D | phone pitch 80° or 10°; the switch needs 0.5 s |
| Q / S / X | Ask question / Results / No match | forces the next search answer |
| N | Next step | walks to the next route node |
| G | Arrived | walks on until the destination or a floor change |

Walk through it once:

1. V1: **Download and continue** → V3 (progress) → 1.
2. Press **F** → 2. Type `I need a charger`, **Search** → 3b briefly → 3. Tap **Borrow a cable** → 4 with two suggestions.
3. Tap **Cable drawer** → 5: the route takes the stairs. Tick **Avoid stairs**: it moves to the lift.
4. **Start** → B. Press **G**: you stop at the lift with "Are you on floor 2?". Tap **I'm on floor 2**: the banner shows while the new floor is found. Press **G** again → G. **Found it** → 2.
5. Hold **Up** during a route → A; **Down** → B. Change RunConfig's condition to *TwoDOnly* and try again: raising keeps B.
6. Search `print a poster` → 4 with two printers. Pick **Printer, colour** with Avoid stairs ticked → 5b.
7. Press **D** (or the panel button **Download fails**), then **Change** on screen 2 and download again → V3b → **Try again**. Arming the failure makes the placeholder forget what it installed in this run, so the next download really runs and can fail.

Open the session log (the Console printed its path; on Windows it is under `%USERPROFILE%\AppData\LocalLow\<company>\<product>\SessionLogs`). Every line has the time, participant `dev`, condition, event and detail.

Edit > **Clear All PlayerPrefs** makes the app forget the remembered building, so it starts at V1 again.

## Part C: commit in dependency order (0.5 h)

The files arrived together, so commit them by layer, inner layers first. Each commit then compiles on its own: the core needs nothing, adapters and UI need only the core, and the app needs everything. Stage explicit paths, never `git add .`, and check with `git diff --cached --stat` before each commit. Commit each folder together with its `.meta` file.

```
git add .gitattributes Docs/decisions Docs/walking-skeleton-guide.md
git commit -m "docs: record the first six architecture decisions"

git add IndoorNavigation/Assets/IndoorNavigation/Runtime/Core IndoorNavigation/Assets/IndoorNavigation/Runtime/Core.meta IndoorNavigation/Assets/IndoorNavigation/Tests IndoorNavigation/Assets/IndoorNavigation/Tests.meta
git commit -m "feat(core): add session statechart, venue model, routing and ports with tests"

git add IndoorNavigation/Assets/IndoorNavigation/Runtime/Adapters IndoorNavigation/Assets/IndoorNavigation/Runtime/Adapters.meta IndoorNavigation/Assets/StreamingAssets IndoorNavigation/Assets/StreamingAssets.meta
git commit -m "feat(adapters): add placeholder adapters, package reader, session log and sample package"

git add IndoorNavigation/Assets/IndoorNavigation/Runtime/UI IndoorNavigation/Assets/IndoorNavigation/Runtime/UI.meta "IndoorNavigation/Assets/UI Toolkit" "IndoorNavigation/Assets/UI Toolkit.meta"
git commit -m "feat(ui): add UI Toolkit shell, ten screens and developer panel"

git add IndoorNavigation/Assets/IndoorNavigation/Runtime/App IndoorNavigation/Assets/IndoorNavigation/Runtime/App.meta IndoorNavigation/Assets/IndoorNavigation/Scenes/App.unity IndoorNavigation/Assets/IndoorNavigation/Scenes/App.unity.meta "IndoorNavigation/Assets/Settings/Build Profiles/iOS.asset"
git commit -m "feat(app): add composition root, run config and App scene"
```

Then `git status`. If Unity changed something else (for example a file in `ProjectSettings/`), read its diff and commit it only if it came from these steps.

## Part D: build to the iPhone (1 h)

Use the same pipeline as before: Unity build to an Xcode folder → `Tools/Package-Xcode.ps1` → release `export-00N` → run **iOS unsigned build** on GitHub → install with Sideloadly.

On the phone, check:

- The scenario from Part B, with the panel (triple tap top-left).
- The camera permission appears when screen 1 opens, not at launch.
- The session log is in the Files app under the app's folder, `SessionLogs`.

Add an entry to `Docs/device-tests.md` in the usual format and commit it: `docs: record walking skeleton device test`.

## Lessons

Each lesson has the same shape: the idea, the files to read in order, something to try (undo it afterwards), and one exercise you write and commit yourself. Write the test first where the exercise touches the core: watch it fail, make it pass, then tidy (Freeman and Pryce 2009).

Unity compiles C# 9: no file-scoped namespaces, no global usings, no records.

### Lesson 1: assemblies and the composition root (1 h)

**Idea.** Dependencies point inward: UI and adapters know the core, the core knows neither (Martin 2017). Assembly definitions turn this into a compiler rule: an assembly can only use the assemblies it references (Unity Manual: Referencing assemblies). The core's **No Engine References** means Unity adds no `UnityEngine` or `UnityEditor` reference when compiling it (Unity Manual: Assembly Definition Inspector window reference). One composition root creates every adapter, close to the app's entry point (Seemann 2011).

**Read.** The five `.asmdef` files, `App/CompositionRoot.cs`, `App/RunConfig.cs`, decision records 1 and 3.

**Try.**
- Add `using UnityEngine;` to `Core/Session/AppSession.cs`. The compile error is the Dependency Rule at work.
- Set RunConfig's Position to *Real* and press Play. The app stops at once and names the week the real adapter arrives (fail fast).

**Exercise.** Answer in two lines each, in your notes: Why does `UI` not reference `Adapters`, and how does the developer panel reach the placeholders anyway? In which assembly would a new port `IPhotoStore` go, and where would its placeholder go?

### Lesson 2: the session statechart (1 h)

**Idea.** One state machine owns the app state; screens show it and send requests, and never open each other (Harel 1987; decision record 2). The table in `SessionMachine` is the screen index as code, and the tests check every state and trigger pair.

**Read.** `SessionState`, `SessionTrigger`, `SessionMachine`, `ScreenCodes`, `SessionMachineTests`, then the request methods in `AppSession`.

**Try.** Delete one row of the transition table and run the tests: see which fail and what they say. Undo.

**Exercise: `PickNamedPlace` for screen 2b.** The statechart has `Asking + PickNamedPlace → Previewing` (tap a common place on screen 2), but `AppSession` has no request for it yet. Add the test first, in `AppSessionTests`:

```csharp
[Test]
public void PickingACommonPlace_GoesStraightToThePreview()
{
    GoToAsking();

    session.PickNamedPlace("meeting-room");

    Assert.AreEqual(SessionState.Previewing, session.State);
    Assert.AreEqual("d-meeting", session.Route.End.Id);
}
```

It does not compile yet: that is the first red. Add `PickNamedPlace(string placeId)` next to `PickPlace`, firing `SessionTrigger.PickNamedPlace`. When it passes, remove the copy you made: both requests can share one private method. Also check that **Back** on screen 5 then returns to screen 2. Commit: `feat(core): add PickNamedPlace for common places`.

### Lesson 3: ports, adapters and the log (1 h)

**Idea.** The core asks the world for six things through ports; each has a real version, a placeholder and a test fake, which are interchangeable strategies (Cockburn 2005; Gamma et al. 1994). The test is logged by design: every state change, search and route event, and the position at most once a second.

**Read.** `Core/Ports/*`, `Adapters/Placeholders/*`, `Adapters/Data/JsonLinesSessionLog.cs`, `Tests/EditMode/Fakes.cs`, `App/DeveloperActions.cs`, decision record 1.

**Try.** Run the scenario and read the log file from top to bottom. Find the `state` lines, the `pose` lines (never two within a second) and the `developer` lines the panel buttons wrote. Keys write none yet; that is part 3 of the exercise.

**Exercise: log the screen.** For the study you will want to know how long people stay on each screen, for example A against B. In `UI/ScreenHost.cs`, when the screen changes (`next != current`), call `session.LogExternal("screen", code)`. Run the scenario and check the new lines. Commit: `feat(ui): log every screen change`.

**Exercise, part 2: say whether a route is step-free.** The route line logs `stairs` and `lift`, but a route can avoid both and still cross steps: the colour printer route goes through the studio door, which has two steps, and logs `stairs False, lift True`. For RQ3 the log must say what the participant actually met. Write the test first in `RoutePlannerTests`: the route from `j-vr-lab` to `d-studio` (the studio door, where the colour printer is) is not step-free; copy the shape of the test that uses `d-studio`. Add `bool StepFree` to `Route`, set it in `RoutePlanner.BuildRoute` (false if any edge on the route is not `stepFree`), and add `step-free {4}` to the route line in `AppSession.PlanRoute`. Commit: `feat(core): log whether a route is step-free`.

**Exercise, part 3: make the keys leave a trace.** A panel button writes a `developer` line before it acts (`Wire` in `DeveloperPanelPresenter`), but a key calls the same action without logging, so the log cannot show that an event was forced by hand, and you cannot tell whether a key arrived. Move the `LogExternal("developer", …)` call out of `Wire` and into each method of `App/DeveloperActions.cs`, so buttons and keys share one place that logs. Press D and F, then check that each wrote exactly one line. Commit: `fix(app): log developer actions from keys as well as buttons`.

### Lesson 4: passive views and one-way flow (1 h)

**Idea.** UXML and USS hold layout and style; a presenter only fills in text, shows or hides parts, and forwards taps (Fowler 2006). Requests go view → presenter → core; changes come back as the `Changed` event (Observer; Gamma et al. 1994). `ScreenHost` is the only place that switches screens.

**Read.** `ScreenPresenter`, `ScreenHost`, `Screens/Ask.uxml` with `AskPresenter`, `Tokens.uss`, `Developer/DeveloperPanelPresenter.cs`.

**Try.** Open `Ask.uxml` in **Window > UI Toolkit > UI Builder** and change a text. Then rename the `search` button to `go` and press Play: the error names the missing element and the presenter. Undo.

**Exercise: common places on screen 2.** Add a row of three buttons under the search button: Meeting room, Kitchen, Printer. In `Ask.uxml` add `<ui:VisualElement name="common" class="row" />`. In `AskPresenter`, build the buttons in `Render` once a venue is loaded, and only once per venue (remember the venue you built them for, as `BuildingSearchPresenter` remembers its list). Each button calls `Session.PickNamedPlace(id)` from Lesson 2. Commit: `feat(ui): add common places on screen 2`.

### Lesson 5: venue data and Dijkstra (1 h)

**Idea.** The package is data with a versioned schema (decision record 4). Routes come from Dijkstra's shortest path on the walkable graph (Dijkstra 1959). A floor change costs 5 extra metres, so the planner avoids needless floor changes, but the reported length counts only metres walked on the plans. The validator checks every package before the app uses it.

**Read.** `graph.json` and `catalogue.json`, `Adapters/Data/VenueJsonReader.cs`, `Core/Venue/RouteGraph.cs`, `Core/Routing/RoutePlanner.cs`, `Core/Venue/VenueValidator.cs`, `RoutePlannerTests`.

**Try.** Sketch floor 1 from `graph.json` on paper and run Dijkstra by hand from `j-vr-lab` to `stairs-1`. Compare with `FromTheVrLab_ToCabinetB_TakesTheStairs`.

**Exercise: a new validator rule.** A lift edge should join two lift nodes, and a stairs edge two stairs nodes. Today the validator does not check this, and in week 6 you will draw the real stairs and lift by hand. Write a test first in `VenueValidatorTests` (copy the shape of `StairsOnOneFloor_AreReported`, with a lift edge between a lift node and a junction), then add the rule to `CheckEdges`. Commit: `feat(core): check that floor links join matching nodes`.

### Lesson 6: phone attitude (1.5 h, with the device)

**Idea.** Raising the phone shows A, lowering shows B, only in the 2D + AR condition. `TiltSwitch` uses two thresholds (raise above 60°, lower below 35°) and a 0.5 s hold, so the view does not flicker near one angle. `ArCameraAttitude` reads the pitch from the AR camera: flat is 0°, upright is 90°.

**Read.** `TiltSwitch` with `TiltSwitchTests`, `ViewMode`, `ScreenCodes.Resolve`, `Adapters/AR/ArCameraAttitude.cs`.

**Try.** In the Editor, tap **Up** then **Down** quickly, within half a second: nothing changes. Hold **Up**: A appears.

**Exercise: the real attitude (lesson 6 "done when").** Set RunConfig's Attitude to *Real*. Build to the iPhone, start a route, and tilt the phone: raised shows A, lowered shows B. Look at the `view` lines in the log. Record the test in `Docs/device-tests.md`. Commit the RunConfig change and the record: `feat(app): use the real phone attitude`.

## When something goes wrong

| You see | Likely cause |
|---|---|
| A blank Game view | The UI Document has no Panel Settings or Source Asset, or the theme file is missing. |
| Buttons without a look | Panel Settings has no Theme Style Sheet. Set it to `UnityDefaultRuntimeTheme`. |
| `'xyz' is missing in the screen of …Presenter` | An element was renamed in UXML. Use the name the presenter asks for. |
| `Screen C is not built yet` | Expected: screens C and D come later. |
| The app starts at V3 or screen 1 | It remembered the building. **Edit > Clear All PlayerPrefs**. |
| Keys do nothing | Click the Game or Simulator view first. Keys are ignored while a text field has focus. |
| No tests in the Test Runner | Choose the EditMode tab, then check that `IndoorNavigation.Core.Tests.asmdef` imported without errors. |

## Sources

- Cockburn, A. (2005). The Hexagonal (Ports & Adapters) Architecture. HaT Technical Report 2005.02.
- Dijkstra, E. W. (1959). A note on two problems in connexion with graphs. Numerische Mathematik, 1(1), 269–271. doi:10.1007/BF01386390
- Fowler, M. (2006, July 18). GUI Architectures. martinfowler.com
- Freeman, S., & Pryce, N. (2009). Growing Object-Oriented Software, Guided by Tests. Addison-Wesley.
- Gamma, E., Helm, R., Johnson, R., & Vlissides, J. (1994). Design Patterns: Elements of Reusable Object-Oriented Software. Addison-Wesley.
- Harel, D. (1987). Statecharts: A visual formalism for complex systems. Science of Computer Programming, 8(3), 231–274. doi:10.1016/0167-6423(87)90035-9
- Martin, R. C. (2017). Clean Architecture: A Craftsman's Guide to Software Structure and Design. Pearson.
- Nygard, M. (2011, November 15). Documenting Architecture Decisions. Cognitect blog.
- Seemann, M. (2011, July 28). Composition Root. blog.ploeh.dk
- Unity Technologies. (n.d.). Organize scripts into assemblies; Referencing assemblies. Unity 6.3 Manual.
- Unity Technologies. (n.d.). Assembly Definition Inspector window reference. Unity 6.3 Manual. https://docs.unity.com/en-us/engine/6000.3/manual/programming-environment/script-compilation/assembly-definition-files/class-assembly-definition-importer
