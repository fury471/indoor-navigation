# 1. Ports and Adapters, with a pure C# core

Date: 2026-10-20
Status: Accepted

## Context

Most parts of the app start as placeholders and are replaced week by week: position (week 3), venue data (week 4), search (week 5). Routing, search flow and state transitions must be testable on Windows without the phone or AR. One person builds the app at about 14 hours a week.

## Decision

Use Ports and Adapters (Cockburn 2005). The core is plain C# in its own assembly, `IndoorNavigation.Core`, with **No Engine References** ticked, so it cannot use any UnityEngine type. The core asks the world for six things through ports: `IVenueDirectory`, `IVenueRepository`, `IPositionSource`, `IPlaceSearch`, `IDeviceAttitude` and `ISessionLog`, plus `IClock` for time. Each port has a real version and a test fake; all except the session log also have a placeholder. Dependencies point inward: Adapters, UI and App reference Core; Core references nothing (Martin 2017, the Dependency Rule).

## Consequences

- Swapping a placeholder for the real part changes one adapter and one RunConfig setting, never a screen or the core.
- The core's EditMode tests run in the Unity Editor without a device, and the core can be compiled with plain `dotnet`.
- The core keeps its own small types (for example `PlanPose`) instead of Unity's `Vector3`. Adapters convert at the edge.
- This tightens the architecture doc, which allowed Unity math types in the core.
