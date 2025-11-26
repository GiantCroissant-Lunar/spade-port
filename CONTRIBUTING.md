# Contributing to Spade for .NET

Thanks for your interest in contributing to **Spade for .NET**!

This project is an MIT-licensed port of the Rust [`spade`](https://github.com/Stoeoef/spade) crate and related geometry libraries. Contributions are welcome in the form of bug fixes, improvements to robustness, documentation, and new tests.

## Project layout

The C# code for this port lives under:

- `dotnet/src/Spade` – core triangulation / Voronoi library
- `dotnet/src/Spade.Advanced` – higher-level Voronoi / power-diagram helpers
- `dotnet/tests/Spade.Tests` – test project
- `dotnet/Spade.sln` – solution for the C# projects

Reference projects used for validation live under `ref-projects/` and are ignored from version control in this repository.

## Development workflow

1. **Clone** the parent repository and open the solution:
   - Open `dotnet/Spade.sln` in your preferred IDE (Rider, Visual Studio, VS Code + C#).

2. **Build** the solution:
   - Use your IDE's build command, or from the `dotnet` directory run:
     - `dotnet build Spade.sln`

3. **Run tests** before submitting changes:
   - From the `dotnet` directory:
     - `dotnet test Spade.sln`

4. **Add tests**:
   - For any non-trivial change, please add or update tests in `dotnet/tests/Spade.Tests`.
   - Where possible, keep tests close to the APIs they validate.

## Pull requests

- Keep PRs **small and focused** when possible.
- Describe the **motivation**, **approach**, and **any breaking changes** in the PR description.
- If you are changing core algorithms or robustness behavior, please mention any relevant RFCs (under `docs/rfcs/`) or add/update one.

## Code style

- Follow existing C# style in the repository (PascalCase types, camelCase locals, etc.).
- Prefer explicit types over `var` in public APIs; usage inside methods may use `var` where it improves readability.
- Avoid introducing new dependencies unless they are clearly justified.

## Licensing

By submitting a contribution, you agree that your work will be licensed under the
same license as this project, the **MIT License** (see `LICENSE`).
