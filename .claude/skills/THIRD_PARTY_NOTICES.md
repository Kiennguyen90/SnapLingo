# Third-party Claude Code skills vendored in this project

The skills below were downloaded from public repositories and copied into
this project's `.claude/skills/` so Claude Code can use them automatically
when working on SnapLingo. Each is MIT-licensed; the required copyright and
permission notices are reproduced here.

## dotnet-claude-kit

- Source: https://github.com/codewithmukesh/dotnet-claude-kit
- Vendored: 47 skills (`api-versioning`, `arch-check`, `architecture-advisor`,
  `aspire`, `authentication`, `build-fix`, `caching`, `checkpoint`, `ci-cd`,
  `clean-architecture`, `dotnet-code-review` (renamed from `code-review` to
  avoid clashing with Claude Code's built-in `code-review` skill),
  `configuration`, `container-publish`, `convention-learner`, `ddd`,
  `dependency-injection`, `de-sloppify`, `docker`, `dotnet-init`, `ef-core`,
  `error-handling`, `health-check`, `httpclient-factory`, `instinct-system`,
  `logging`, `messaging`, `migrate`, `minimal-api`, `modern-csharp`,
  `openapi`, `opentelemetry`, `outdated`, `plan`, `project-setup`,
  `project-structure`, `resilience`, `scaffold`, `scalar`, `security-scan`,
  `serilog`, `spec`, `tdd`, `testing`, `verify`, `vertical-slice`,
  `workflow-mastery`, `wrap-up`).
- Only the plain-markdown `skills/` content was copied. The kit's agents,
  slash commands, hooks, and Roslyn MCP server were **not** installed — those
  require running `/plugin marketplace add codewithmukesh/dotnet-claude-kit`
  and `/plugin install dotnet-claude-kit` inside Claude Code, plus
  `dotnet tool install -g CWM.RoslynNavigator` for the MCP server. Skills
  that depend on those MCP tools (e.g. `dotnet-code-review`, `arch-check`)
  will fall back to manual review until that's done.
- License: MIT, Copyright (c) 2025 Mukesh Murugan.

## claude-flutter-skill

- Source: https://github.com/Arcturus91/claude-flutter-skill
- Vendored: the `flutter` skill (router `SKILL.md` + `references/*.md`
  covering Dart 3, BLoC/Cubit, Material 3, Firebase, flutter_map, testing).
- License: MIT, Copyright (c) 2026 Arturo Barrantes Vasquez.

## MIT License text (applies to both, per their own copyright line above)

```
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to
deal in the Software without restriction, including without limitation the
rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
sell copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
```
