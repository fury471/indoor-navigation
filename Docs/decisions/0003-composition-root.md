# 3. One composition root and a RunConfig asset

Date: 2026-10-20
Status: Accepted

## Context

Placeholders and real adapters are mixed during development, for example real phone attitude with placeholder search. The choice must live in one place.

## Decision

`CompositionRoot` is the only class that creates adapters and hands them to the core, as close as possible to the app's entry point (Seemann 2011). Which version each port uses is set in a `RunConfig` ScriptableObject, edited in the Inspector. There is no dependency-injection container, no singleton and no `FindObjectOfType`.

## Consequences

- Switching a port between placeholder and real needs no code change.
- A real version that does not exist yet fails loudly at start-up with the week it arrives in.
- The developer panel and the Editor keys reach placeholders through `DeveloperActions`, which the composition root also creates.
