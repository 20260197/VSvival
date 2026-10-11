# Codex Agent Instructions — CookieBreaker

## Product intent

Build a **finished, playable Unity 2D mobile game** inspired by the core game loop of 「쿠키 대량 파괴」. The user prioritizes *finishing a game quickly* over hand-writing every line of C#. Start with a small prototype, verify it, then expand. See `GAME_DESIGN.md` for scope and `TASKS.md` for acceptance criteria.

## Before editing

1. Inspect the existing Unity project structure, Unity editor version, installed packages, target platform, active scenes, and Git status.
2. If the project does not exist, explain what setup is possible in the current environment. Never claim to have created or run a Unity project without actually doing so.
3. Read `GAME_DESIGN.md` and `TASKS.md`; identify the current milestone.
4. Avoid changing unrelated files or overwriting hand-authored scenes/assets.

## Implementation standards

- Use readable, straightforward C# MonoBehaviour-based Unity 2D code unless complexity justifies otherwise.
- Keep gameplay logic (spawning, targeting, combat, economy, upgrades) modular and avoid an oversized manager class.
- Expose important tuning values in Inspector or configuration assets, with reasonable temporary defaults clearly identified.
- Prefer minimal placeholder sprites and UI over spending time on art before the core loop works.
- Keep controls and UI suitable for portrait mobile, while maintaining easy Unity Editor testing.
- Avoid unnecessary packages, speculative abstractions, and premature optimization.
- Prevent double rewards, negative currency balances, destroyed target references, and uncontrolled object counts.
- Use original or properly licensed assets; never copy third-party copyrighted assets without permission.

## Workflow

- Implement the smallest coherent slice from `TASKS.md`.
- State which files were changed and why.
- Run available compile/tests and report **what was actually run** versus **what requires the user's local Unity Editor**.
- Include concise reproduction and test instructions (scene to open, Play actions, expected result).
- When a design detail is missing, prefer a simple reversible assumption, label it as such, and avoid inventing facts about the original game.
- Maintain a running task checklist in `TASKS.md` based on verified work, not code creation alone.
- Do **not** commit, push, merge, publish, or deploy unless the user explicitly requests it.

## Prototype v0.1 acceptance

A cookie spawns; one turret detects, aims at and shoots it; damage destroys the cookie; destruction grants currency exactly once; a UI button spends sufficient currency to upgrade turret damage; the loop repeats in Unity Play mode without Console errors.

## Communication

Respond in Korean unless asked otherwise. Keep updates concise, identify blockers precisely, and recommend the next smallest step toward a playable build.
