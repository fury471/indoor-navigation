# Indoor Navigation

An iPhone app that guides people indoors, where GPS doesn't reach. A visitor picks a building, says what they need ("I need a charger"), answers a short clarifying question if the request is ambiguous, previews the route on a 2D plan or a 3D miniature, and follows it. With the phone raised they see AR guidance and a small 2D map; with the phone lowered they see the 2D map.

It is the project of the course MAMN41 (UX/XR) at Lund University, built in Unity for iOS. The demo covers part of two floors of the VR lab building, connected by stairs and a lift. The work-in-progress seminar is on **5 Nov 2026**, the final seminar on **11 Dec**, and user tests start on **14 Dec**.

Owner: Fury (fury471). Licence: MIT.

## Status on 30 Oct 2026

| Part | State |
|---|---|
| Walking skeleton: ten visitor screens, the session statechart, route planner, sample two-floor package, developer panel | Done; device-tested 26 Oct |
| Real phone attitude (raise for AR, lower for 2D) | Done; device-tested 26 Oct |
| Real position from ARKit world maps, aligned to the plan by door corners (decision 7) | Works, but too slow and unreliable on its own (lab visit 1, builds 4–6) |
| **Start at a door** when the map is slow: mark a named door's two corners (decision 11); Fix position at any time; route ahead drawn in AR | Build 7 code done and tested in the Editor; device test pending |
| World-map library on the phone: name, test, continue and delete maps, name doors (developer only) | Builds 4–7 |
| Floor plan and miniature of the real building | Converter works on practice scans. RoomPlan failed in the lab on 29 Oct, so the plan will come from the evacuation plans (not built yet) |
| Low-phone prompt (decision 9), minimap in AR | Next |
| Venue service, natural-language search with an LLM, floors 2 and 3, study set-up | After the seminar |

`Docs/device-tests.md` has every device test, and `Docs/lab-visit-1.md` what we learnt in the lab.

## Getting started

You need:
- Windows with PowerShell (the scripts are PowerShell; macOS works for the Editor but the build steps below assume Windows);
- **Unity 6000.3.24f1** with iOS Build Support (don't upgrade the Editor or the packages: AR Foundation and the Apple ARKit XR Plug-in are pinned at 6.3.5);
- Python 3.9 or newer for `Tools/scan-to-plan` (standard library only);
- for device work: an iPhone with iOS 15 or newer (tested on an iPhone 16 Pro), Sideloadly, and write access to the GitHub repo to make releases and run the build workflow.

Then:
1. Clone the repo and open the folder `IndoorNavigation/` in Unity Hub. Let the packages resolve.
2. **Window → General → Test Runner → EditMode → Run All.** Expect 282 tests to pass.
3. Open `Assets/IndoorNavigation/Scenes/App.unity` and press Play. In the Editor, position and phone attitude are placeholders that you drive from the keyboard (on screen 1, **Start at a door instead** works with sample doors):

   | Key | Does |
   |---|---|
   | W | open or close the developer panel |
   | F / L | position found / lost |
   | ↑ / ↓ | raise / lower the phone |
   | Q, S, X | a search that needs a question / has results / has no match |
   | N, G | next route step / arrive |
   | D | make the next download fail |

   On the iPhone, the developer panel opens with a **triple tap in the top-left corner**.
4. Read `Docs/walking-skeleton-guide.md` part A and B for how the scene is wired, and the decision records in `Docs/decisions/` for why.

The Editor can't record or load world maps; that needs the iPhone. Everything in `Runtime/Core` runs and is tested without a phone.

## Building for the iPhone

There is no Mac in the loop: Unity exports an Xcode project on Windows, GitHub Actions builds an unsigned IPA on a macOS runner, and Sideloadly signs and installs it.

1. Unity → **File → Build Profiles → iOS → Build** into a new, empty folder outside the repo, for example `E:\IndoorNavigationBuilds\position-007\Xcode`. The iOS profile builds `App.unity` only.
2. From the repo root:
   ```powershell
   .\Tools\Package-Xcode.ps1 -ExportDirectory E:\IndoorNavigationBuilds\position-007\Xcode -ZipPath E:\IndoorNavigationBuilds\position-007\xcode-export.zip
   ```
   It checks the export, refuses to overwrite a ZIP and prints its SHA-256.
3. GitHub → Releases → new release with a new tag (`export-position-007`) on the commit you pushed, attach `xcode-export.zip`.
4. GitHub → Actions → **iOS unsigned build** → Run workflow with `export_tag` = that tag.
5. Download the `IndoorNavigation-unsigned.ipa` artifact (kept 3 days) and install it with Sideloadly over the old app, so the maps and logs on the phone stay.
6. Record the test in `Docs/device-tests.md`: date, build tag, source commit, what passed, what didn't.

The workflow builds the uploaded export, not the current source: a push alone changes nothing on the phone.

## How the code is organised

Ports and Adapters, with one composition root (decisions 1–3). Each folder under `IndoorNavigation/Assets/IndoorNavigation/Runtime/` is its own assembly, and the arrows are the only allowed references:

```
Core      IndoorNavigation.Core       pure C#, no UnityEngine (session statechart, venue package,
                                      route planner, positioning maths, the ports)
Adapters  IndoorNavigation.Adapters → Core      AR Foundation, files, placeholders: one per port
UI        IndoorNavigation.UI       → Core      UI Toolkit screens (UXML + one presenter each), panel W
App       IndoorNavigation.App      → all       CompositionRoot, RunConfig, developer actions
Tests/EditMode  IndoorNavigation.Core.Tests → Core
```

`RunConfig.asset` chooses real or placeholder adapters. The older folders `Diagnostics`, `Platform` and `Presentation` belong to the first experiments (the `ARBootstrap` and `LocalisationLab` scenes). They are kept as a reference lab: `LocalisationLab` is where world-map relocalisation first worked, and it is the comparison when the app's own recorder misbehaves. Don't add to them.

Where to look:

| You want to … | Look at |
|---|---|
| Change what a screen shows | `UI/Screens/<Screen>.uxml` and its presenter; shared styles in `UI/Tokens.uss` |
| Change the flow between screens | `Core/Session` (statechart, tests in `SessionMachineTests`) |
| Change routing | `Core/Routing`, `Core/Venue/RouteGraph` |
| Work on positioning | `Core/Positioning` (fit, coverage, recording rules) and `Adapters/AR` (ARKit) |
| Change the building data | `Assets/StreamingAssets/SamplePackage` (format: decision 4) |
| Turn scans into a plan and a miniature | `Tools/scan-to-plan` |

## Documents

| Document | What it is for |
|---|---|
| `Docs/walking-skeleton-guide.md` | Unity set-up of the App scene, the commit order of the skeleton, six lessons |
| `Docs/real-position-guide.md` | The idea behind world maps and door-corner alignment (part A); parts C and D are history |
| `Docs/build-7-guide.md` | **Current:** build 7 (start at a door, arrows in AR) and the floor-by-floor lab visit 2 |
| `Docs/world-map-guide.md` | How to record, test, continue and delete world maps (builds 4–6) |
| `Docs/lab-visit-1.md` | What happened in the lab on 29 Oct, the numbers, and what changed because of it |
| `Docs/localisation-experiment.md` | The localisation method and how it is measured |
| `Docs/device-tests.md` | Every device test, newest last |
| `Docs/decisions/` | Decision records 1–11 (context, decision, consequences, sources) |
| `Tools/scan-to-plan/README.md` | The scan converter, its checks and its limits |

The architecture document, the UI blueprint (38 boards) and the reference log live outside the repo; ask Fury for access. Where they and the repo disagree about the code, the repo wins.

## Rules for working here

- **Data stays out of git.** World maps, session logs, scans, photos of the building, Xcode exports and IPAs live outside the repo (Fury keeps them under `E:\Lund\S3P1P2\MAMN41_UXXR\` and `E:\IndoorNavigationBuilds\`). Building scans show private spaces: share them only when needed, and don't upload them to cloud AI tools without checking the university's rules.
- **No secrets in the app or the repo.** The LLM key will live in the venue service, never in a Unity build. No signing certificates or service keys in commits.
- **Minimum iOS stays 15.0** (decision 6). Features that need newer iOS check availability.
- **Stage explicit paths**, not `git add .`, and check `git diff --cached --stat` before committing. Unity often rewrites `ProjectSettings/ProjectSettings.asset` (`preloadedAssets`): leave it out unless you changed it on purpose.
- **Commit `.meta` files** with their assets; let Unity create them first. Rename assets inside Unity.
- **Commit messages** use `feat:`, `fix:`, `docs:`, `build:`, `chore:`, `test:`, with a scope where useful (`feat(position): …`). Don't rewrite published history.
- **Cite what you use.** Every technique or number taken from a paper or Apple's documentation is cited where it is used (decision records, guides) and in the reference log.
- **Keep names as they are:** British `Localisation`, `Docs/`, `Plugins/iOS/`, exact casing.
- **New behaviour gets a test** when it is logic in `Core`; device behaviour gets a line in `device-tests.md`.
- A big choice (a format, a frame, a dependency) gets a decision record in `Docs/decisions/`.

## What is open

Short list; the architecture document has the full plan.

1. Device test of build 7 (`Docs/build-7-guide.md` part D).
2. Lab visit 2, floor by floor: a map per floor with every start door marked and named; automatic start, door fix and drift measured (part E).
3. The plan from the evacuation-plan photos, fitted onto each floor's door corners; the route graph between the named doors; the catalogue; package 1.1.0.
4. The "raise the phone" prompt (decision 9), the minimap in AR, floor height along the ramp.
5. After the seminar: the venue service, LLM search, the measured localisation test (world map versus door fix), the study.
