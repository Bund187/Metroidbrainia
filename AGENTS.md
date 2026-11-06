# Metroidbrainia - Codex Instructions

## Project

- Engine: Unity 6000.6.3f1
- Render pipeline: Universal Render Pipeline (URP) 17.6.0
- Language: C#
- Input: Unity Input System
- Target: PC first
- Project type: First-person exploration, investigation and deduction game.

The project is inspired by knowledge-based progression games such as Outer Wilds
and Tunic.

The player should primarily progress by learning, observing, experimenting and
understanding the world rather than by acquiring conventional upgrades.

## Visual Direction

The game uses a deliberately simple retro visual style inspired by classic
first-person games such as Doom.

The world itself is real 3D, but the presentation should favor:

- Pixel-art / low-resolution textures.
- Simple 3D geometry.
- Low-poly props where appropriate.
- Sprites or billboards when they are a better fit than 3D models.
- Simple, readable lighting.
- A deliberately retro appearance rather than realistic rendering.

Do not introduce expensive or complex graphical systems unless they provide a
clear benefit to the project.

## Development Priorities

This is a small indie project.

Prefer:

- Simple implementations.
- Small, focused components.
- Code that is easy to understand and modify.
- Fast iteration.
- Clear dependencies between systems.
- Solutions appropriate to the actual size of the project.

Avoid:

- Overengineering.
- Large frameworks for simple problems.
- Premature optimization.
- Unnecessary abstraction.
- Excessive use of inheritance.
- Global managers unless there is a clear reason for them.
- Singleton patterns by default.
- Creating systems for hypothetical future requirements.

Implement what the current feature actually needs.

## Unity Guidelines

Use standard Unity patterns unless there is a good reason not to.

Prefer private serialized fields for Inspector configuration, for example:

`[SerializeField] private float moveSpeed;`

instead of public fields used only for Inspector access.

Use Unity's Input System rather than the legacy Input Manager.

Prefer composition through MonoBehaviour components over deep inheritance
hierarchies.

Keep runtime code separate from Editor-only code.

Be careful when modifying scenes, prefabs, ScriptableObjects and serialized
assets.

Do not manually edit `.meta` files unless absolutely necessary.

Do not deliberately edit generated directories such as:

- Library
- Temp
- Logs
- obj
- .vs
- UserSettings

It is acceptable for Unity or development tools to generate or update these
directories automatically during normal operation.

## Packages and Project Configuration

The following actions require explicit user authorization:

- Installing or removing Unity packages.
- Adding third-party libraries or assets.
- Changing the Unity version.
- Changing the render pipeline.
- Making significant changes to `ProjectSettings`.

Do not modify `Packages/manifest.json` unless required by an explicitly
authorized package-related task.

If one of these changes appears useful but has not been requested, explain why
it would be useful and wait for authorization before making it.

Small configuration changes that are clearly necessary to complete an
explicitly requested task may be made when their purpose and impact are clear.
When uncertain, ask first.

## Architecture

Before introducing a new architectural pattern, check whether the same result
can be achieved with a simpler Unity component.

Do not create managers, service locators, event buses, dependency injection
systems, generic frameworks or similar infrastructure unless the current
problem clearly requires them.

A significant architectural change is one that affects multiple systems,
introduces a project-wide pattern, creates shared infrastructure, or makes
future systems depend on a new architectural decision.

Explain significant architectural changes before implementing them.

For small, clearly scoped features, implementation can proceed directly.

## Code Style

Write code in English.

Use clear and descriptive names.

Keep classes and methods focused on one responsibility.

Avoid very large MonoBehaviours when functionality can naturally be separated
into components.

Avoid comments that merely repeat what the code says.

Add comments when they explain:

- Why something is implemented in a non-obvious way.
- Unity-specific behavior.
- Important constraints.
- Decisions that would otherwise be difficult to understand later.

Use namespaces for project code.

Use the `Metroidbrainia` namespace unless an existing project structure
indicates otherwise.

## Existing Code

Before implementing a feature:

1. Inspect the relevant existing files.
2. Understand the current implementation.
3. Reuse existing systems where appropriate.
4. Avoid duplicating functionality.
5. Limit changes to files relevant to the requested task.

Do not perform unrelated refactors while implementing another feature.

If unrelated code appears problematic, mention it separately without modifying
it unless explicitly requested.

## Git

The repository is managed by the user through Git and Fork.

Do not perform any of the following unless explicitly requested:

- Commit changes.
- Push changes.
- Rewrite Git history.
- Change remotes.
- Force push.
- Delete branches.

It is acceptable to inspect Git status, history and diffs.

After making meaningful changes, report which files were created, modified or
deleted.

## Safety

Do not delete user-created assets or code unless explicitly requested.

When uncertain whether an existing asset, script or configuration is still
needed, preserve it and ask rather than deleting it.

Prefer reversible changes.

## Communication

Communicate with the user in Spanish unless asked otherwise.

Code, identifiers, filenames and code comments should normally be in English.

When explaining implementation decisions, be concise and practical.

If there are several reasonable approaches with meaningful trade-offs, explain
them before choosing one.

If a request is simple and unambiguous, do not add unnecessary architecture or
planning before implementing it.