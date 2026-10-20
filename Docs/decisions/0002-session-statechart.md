# 2. One session statechart, with view mode as its own region

Date: 2026-10-20
Status: Accepted

## Context

The blueprint has 38 boards. If every screen opened the next one, the app state would be spread over 38 places and impossible to test.

## Decision

The core owns one state machine with nine states: ChoosingBuilding, GettingData, Locating, Asking, Clarifying, ChoosingPlace, Previewing, Navigating and Arrived. Its allowed transitions are one table in `SessionMachine`, copied from the screen index. View mode (phone raised or lowered, 2D or 3D, the floor shown, hold view) is a separate region, as in Harel's statecharts (Harel 1987). A screen is a state seen through the view mode (`ScreenCodes.Resolve`). Screens send requests to `AppSession`; they never open each other. A request that is not allowed in the current state is ignored and logged.

## Consequences

- One EditMode test checks every pair of state and trigger against the screen index.
- Dialogs, tips and sheets (B3, F3, and 1c, 6b, H during navigation) are overlays drawn by presenters, not states.
- Adding a screen means a view, a presenter and a mapping in `ScreenCodes`, not a new state.
