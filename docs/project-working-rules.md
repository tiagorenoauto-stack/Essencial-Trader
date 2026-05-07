# Project Working Rules

This document is the operational standard for working on Essencial ChartGuard.
It captures **how** we work in this repository: where the source of truth
lives, what to do at the start and end of every task, what counts as Done,
how commits are written, and which safety lines must never be crossed without
an explicit phase that authorizes it.

These rules apply to humans and to AI assistants working on the repository.
If a rule below conflicts with a one-off request, prefer the rule and ask
before proceeding.

For deeper context, see:

- `CLAUDE.md` — project mission, architecture constraints, NinjaTrader rules.
- `docs/safe-core-contract.md` — the contract the Safe Core must obey.
- `docs/panel-command-map.md` — UI ↔ command mapping.
- `docs/chartguard-product-map.md` / `docs/plano-integrado-chartguard-pt.md`
  — phased product plan.
- `docs/manual-test-checklist.md` — manual NinjaTrader validation gates.

## Source of truth

- `src/` and `docs/` are the **only** source of truth, versioned in this Git
  repository.
- `Documents/NinjaTrader 8/bin/Custom/AddOns/EssencialChartGuard/` and
  `Documents/NinjaTrader 8/bin/Custom/Indicators/EssencialChartGuard/` are
  **local sync targets only**. They exist so NinjaTrader can compile our
  `.cs` files. They are **never** the source of truth and must **never** be
  versioned.
- If a file diverges between `src/` and `Documents/NinjaTrader 8/...`, the
  `src/` copy wins. Re-sync the runtime folder from `src/`, not the other
  way around.
- The runtime sync target is excluded from Git automatically because it
  lives outside the repository worktree (`c:/Users/tiago/Documents/...`),
  not because of `.gitignore`. Do not relocate the worktree under
  `Documents/`.

## Start of every task

1. Run `git status --short`.
2. Identify any pre-existing modifications, untracked files, or stale syncs.
   Do not start new work on top of unexplained changes — surface them to the
   user first.
3. If the user just provided new instructions that overlap with in-flight
   changes, confirm what should be preserved. Do not silently overwrite
   prior work.
4. Read the docs that govern the area you are about to touch (Safe Core
   contract, panel command map, manual test checklist, product map). The
   rules in those docs override convenience.

## During a task

- Keep scope small. One intent per task: fix, feature, refactor, or docs —
  not a mix unless the user explicitly asked for one.
- Do not refactor unrelated code "while you're there."
- Preserve what already worked unless the task explicitly asks to change it:
  - **Safe Core** stays free of `using NinjaTrader.Cbi` / `NinjaTrader.Data`.
  - **EventBridge** stays read-only.
  - **Side panel** stays read-only with every actionable control
    `IsEnabled=false` and **no** event handlers attached (Phase 1/2
    contract).
  - **Resize** of the panel via `Thumb` keeps working.
  - **Snapshot-then-subscribe** order in the host is not reordered.
- WPF UI mutations always run through the chart dispatcher.
- Logs go to the Output Window with the stable `[Essencial*]` prefixes.

## End of every task

Before declaring a task done, run and report:

1. `git status --short` — what changed in the worktree.
2. `git diff --stat` (or `git diff --cached --stat` if already staged) — the
   diff size summary.
3. The list of files added/modified/removed under `src/` and `docs/`.
4. The list of `.cs` files that were synced to
   `Documents/NinjaTrader 8/bin/Custom/AddOns/EssencialChartGuard/` and
   `Documents/NinjaTrader 8/bin/Custom/Indicators/EssencialChartGuard/`.
5. The relevant safety greps for the area touched (see **Safety** below).
   Report each as "0 matches" or list every match with file/line so the
   user can audit.

## Definition of Done

A task is Done only when **all** of the following are true:

- The code change exists in `src/` and compiles in the NinjaScript Editor
  (or the user has been told it is pending validation, with the reason).
- `docs/` is updated when the change touches behavior described there
  — at minimum: `safe-core-contract.md`, `panel-command-map.md`,
  `ninjatrader-bridge-notes.md`, `manual-test-checklist.md`,
  `chartguard-product-map.md`, `plano-integrado-chartguard-pt.md`,
  `mapa-funcional-chartguard-pt.md`, when relevant.
- New or changed manual-test items are added to
  `docs/manual-test-checklist.md`. **No checkbox is marked off** unless the
  user manually validated it in NinjaTrader.
- Every `.cs` file consumed by NinjaTrader has been synced to
  `Documents/NinjaTrader 8/bin/Custom/...` at the matching path.
- All applicable safety greps return 0 matches (or every match is justified
  in a comment that says it is intentional).
- A commit has been created or, if one is pending, the reason is stated
  explicitly to the user (e.g. "waiting for Replay validation").

## Commit pattern

Commits are small, intentional, and prefixed:

| Prefix | Use for |
|---|---|
| `chore:` | Repo plumbing, scaffolding, baselines, sync of generated files. |
| `feat:` | New behavior or new visible/structural functionality. |
| `fix:` | Bug fix that does not change intended behavior. |
| `docs:` | Documentation only. No `.cs` changes. |
| `test:` | Manual-test checklist or automated test changes only. |
| `refactor:` | Internal restructure with no behavior change. |

Rules:

- One intent per commit. Do not bundle a feature, a refactor, and doc
  reorganization in the same commit.
- Commit messages are short on the subject line and explain the **why** in
  the body when the change is not obvious.
- Never start a new phase with accumulated, uncommitted changes from the
  previous one. If the worktree is dirty before starting, propose a commit
  first and wait for confirmation.
- Never amend a published commit. Create a new one.
- Never skip hooks (`--no-verify`) and never bypass signing without an
  explicit user instruction.

## Safety lines (never cross without an explicit phase)

The following lines are off limits unless the active phase in
`docs/chartguard-product-map.md` / `docs/plano-integrado-chartguard-pt.md`
explicitly authorizes them and the matching items in
`docs/manual-test-checklist.md` exist:

- Adding a trading route outside the explicit phase.
- Calling `Account.Submit`, `Account.CreateOrder`, or `AtmStrategyCreate`
  anywhere outside `NinjaTraderAccountAdapter.cs` (and even there only when
  the gated `EnableForControlledTest(...)` path has been authorized).
- Adding `Cancel`, `Modify`, or `Flatten` order routes.
- Calling `EnableForControlledTest(...)` at all unless the user explicitly
  asked for that exact session.
- Attaching `Click`, `SelectionChanged`, `TextChanged`, `MouseDown`,
  `MouseUp`, `PreviewMouse*`, `ContextMenu`, `KeyBinding`, or
  `InputBindings` handlers anywhere under `src/EssencialChartGuard/AddOns/Panel/`
  while the panel is read-only (Phase 1/2).
- Adding hotkeys, chart-click interception, or context-menu overrides on
  the chart.
- Importing `NinjaTrader.Cbi` or `NinjaTrader.Data` from
  `src/EssencialChartGuard/AddOns/SafeCore/` or
  `src/EssencialChartGuard/AddOns/Panel/`.
- Versioning binaries, logs, `obj/`, `bin/`, IDE caches (`.vs/`, `.idea/`,
  `.vscode/`), backups, temporary files, or anything from
  `Documents/NinjaTrader 8/...`.

If a task requires crossing one of these lines, stop and confirm with the
user the phase authorization, the manual-test items it must satisfy, and
the reversibility of the change before proceeding.

## Safety greps to run at end of task

Adapt the path to the area you touched. Each grep must return 0 matches
(or every match must be a comment that explicitly says it is intentional):

```text
Account.Submit | Account.CreateOrder | AtmStrategyCreate | EnableForControlledTest
   → src/
.Click | Click += | SelectionChanged | TextChanged | MouseDown | MouseUp
   | PreviewMouse | ContextMenu | InputBindings | KeyBinding
   → src/EssencialChartGuard/AddOns/Panel/
   → src/EssencialChartGuard/Indicators/
using NinjaTrader.Cbi | using NinjaTrader.Data
   → src/EssencialChartGuard/AddOns/SafeCore/
   → src/EssencialChartGuard/AddOns/Panel/
```

Report results in the task summary.
