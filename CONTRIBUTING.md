# Contributing to SurfaceQ

Thanks for your interest in improving SurfaceQ. This guide covers the development loop, conventions, and how to open a useful pull request.

By participating you agree to abide by the [Code of Conduct](./CODE_OF_CONDUCT.md).

## Prerequisites

- .NET 8 SDK
- Node.js 22 (for the sidecar's TypeScript compiler)
- Git

## Project layout

```
src/
  SurfaceQ.Cli/            .NET CLI (generate / check / diff)
  SurfaceQ.Core/           file walker, manifest reader, renderer
  SurfaceQ.Sidecar/        .NET client for the Node sidecar
  SurfaceQ.Sidecar.Node/   Node sidecar script + TypeScript dependency
test/
  SurfaceQ.Cli.Tests/      CLI acceptance and unit tests
  SurfaceQ.Core.Tests/     core library tests
  SurfaceQ.Integration.Tests/
                           end-to-end sidecar and perf tests
docs/
  tasks/                   the vertical-slice roadmap
```

## Setup

```sh
git clone https://github.com/QuinntyneBrown/SurfaceQ.git
cd SurfaceQ
npm ci --prefix src/SurfaceQ.Sidecar.Node
dotnet build
dotnet test
```

The `npm ci` step seeds the pinned `typescript` tree under `src/SurfaceQ.Sidecar.Node/node_modules`. The CLI csproj also runs this automatically before pack if the tree is missing.

## Running tests

```sh
# Everything
dotnet test

# Exclude the slower perf benchmarks
dotnet test --filter "Category!=Performance"

# Only the perf benchmarks (50-file cold, 200-file warm, 500-file memory)
dotnet test --filter "Category=Performance"
```

## Development loop (ATDD)

SurfaceQ was built as a sequence of 50 small, test-first vertical slices tracked in [`docs/tasks/README.md`](./docs/tasks/README.md). We ask contributions to follow the same loop:

1. **Write a failing acceptance test first.** Every test file carries a header comment linking it to an L2 requirement:
   ```csharp
   // Acceptance Test
   // Traces to: L2-00X
   // Description: ...
   ```
2. **Run the test and confirm it fails (red).** If it passes on the first run, the test is too weak; tighten it.
3. **Write the minimum code to make it pass (green).** Do not refactor beyond what the test requires.
4. **One task = one small PR.** Do not bundle unrelated changes.

## Code style

- **Optimize for readers who are new to C# or to this codebase.** Prefer plain classes and methods over abstractions, generics, reflection, or clever LINQ.
- **Short methods** (≤ 20 lines). **Cyclomatic complexity per method** target ≤ 5.
- **No premature interfaces.** Add one only when a second implementation is introduced.
- **Minimal comments.** Comment only the *why* behind non-obvious behavior. Do not explain what the code does — names do that.
- **No trailing whitespace, LF line endings, UTF-8 without BOM.**

The renderer's output is itself a format contract (see `test/SurfaceQ.Core.Tests/PublicApiRendererFormattingTests.cs`). Do not change line endings, indentation, or header wording without updating the tests.

## Commit messages

One-line summary (≤ 72 chars), imperative mood, optionally followed by a short body explaining *why*:

```
Task 27: Parse errors surface to stderr and exit 2

Sidecar discover now includes an errors array populated from
sourceFile.parseDiagnostics; when a file has parse diagnostics its
exports are not collected...
```

When working from the roadmap, prefix the commit with `Task NN:`. Otherwise use plain imperative titles.

## Pull request checklist

Before opening a PR:

- [ ] `dotnet test` is green locally (include `Category=Performance` on a capable laptop).
- [ ] New behavior has an acceptance test; the test was red first, then green.
- [ ] Output contracts (renderer formatting, determinism) are preserved — or the relevant tests are updated with a clear rationale.
- [ ] `docs/tasks/README.md` is ticked for any roadmap task you completed.
- [ ] The PR description links to the L2 requirement(s) it traces to.

Smaller PRs are reviewed faster. One vertical slice per PR is the goal.

## Reporting bugs

Please open a GitHub issue with:

1. SurfaceQ version (`surfaceq --version`).
2. .NET SDK version (`dotnet --version`).
3. Node version (`node --version`).
4. A minimal reproducer: the smallest `ng-package.json` plus a handful of `.ts` files that exhibit the problem.
5. What you expected to see versus what SurfaceQ produced.

For security issues, do not use the public issue tracker. See [SECURITY.md](./SECURITY.md).

## Questions

Open a GitHub Discussion or a `question` issue. If you are unsure whether a change is in scope for v1, asking before coding is welcome.
