# 5. Venue service and download rules

Date: 2026-10-20
Status: Accepted

## Context

Building data lives on a server. The visitor chooses a building, checks it is supported and available, downloads its data, and only then localises with the camera. The LLM API key must never ship in the app.

## Decision

A small venue service over HTTPS. Everything except `/search` is static files: `GET /venues.json`, `GET /venues/{id}/versions/{v}/manifest.json` and `GET /venues/{id}/versions/{v}/{file}`. `POST /search` holds the LLM key. Published versions are never changed, only replaced by a newer one. The app downloads only when its installed version is older, checks every file against its SHA-256, writes to a temporary folder and swaps it in only when every file has passed, and keeps one cached version. The course deployment uses a read-only access token copied onto the test phone as a file, never built into the app or committed.

## Consequences

- Until week 4 the placeholder directory and repository read the bundled sample package.
- Adding a building is new data on the server, not a new app.
