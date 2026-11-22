# Eunomia

A simulation, later a game in the look of EU4 and Victoria 3, in which Portugal is governed for 40 years by AI while the rest of the world carries on. It asks whether a competent government lifts a country faster than a status-quo government facing the same world. Every result compares two governments in the same seeded world. Version one is headless C#; the map comes later.

## Read first

| Document | What it is |
| --- | --- |
| `docs/design-notes.md` | The vision and decisions. The owner's document: edit only to record decisions the owner has agreed |
| `docs/build-plan.md` | What the toy is, the tools, and the numbered "looks": the history of results, newest last |
| `docs/sectors-and-levers.md` | The current design for sector depth, its order of work (steps 1 to 3 of 7 done) and its "Decided" list |
| `docs/reference-spec-v2.md` | An old full spec. A quarry for ideas only; do not build from it |

## Working with the owner

- Plain language, short sentences, tables. The owner is not an economist: explain every mechanism in words.
- Push back with reasons and recommend; the owner decides value questions (what the score rewards, rights, the constitution). Record agreed decisions in the docs.
- After every green step (builds, tests pass): commit and push to `origin main`.
- Run, look, then add realism only where a run shows it is missing. Every mechanism is an assumption with a range; where evidence is mixed, the value varies across the named sets in `config/parameter-sets.json`.
- AI runs use the owner's subscription. Say how many calls and how long before a batch. Use `replay` and `compare` to rescore recorded runs without model calls.

## Build, test, run (Windows, Git Bash or PowerShell)

```
dotnet build
dotnet test
dotnet run --project src/Eunomia.Cli -- returns
dotnet run --project src/Eunomia.Cli -- pair --a technocrat --b statusquo --seeds 1-50
dotnet run --project src/Eunomia.Cli -- regimes --seeds 1-10
dotnet run --project src/Eunomia.Cli -- run --occupant singleai --seed 1            # Haiku by default
dotnet run --project src/Eunomia.Cli -- run --occupant threebranch --seed 1
dotnet run --project src/Eunomia.Cli -- run --occupant replay --from runs/<folder> --seed <n>
dotnet run --project src/Eunomia.Cli -- compare --runs "runs/singleai-haiku-s{seed}" --seeds 1-10 --b technocrat
```

- `dotnet test` does not rebuild the CLI: run `dotnet build` before `dotnet run --no-build`.
- For long AI runs, `dotnet publish src/Eunomia.Cli -c Release -o <scratchpad>/cli`, copy `config/` beside it, and run from that copy with `--config`, so rebuilds do not lock the exe.
- There is no working Python (the Windows Store stub hangs). Read CSVs with PowerShell `Import-Csv`: some fields contain commas, so `awk -F,` misaligns.

## Layout

- `src/Eunomia.Core`: the simulation. `Model/Simulation.cs` (the yearly loop, budget checks, floors), `Sector.cs` (management and absorption), `EducationSystem.cs` (coverage, quality, generations), `EducationRegimeState.cs` (regimes, phase-in, earmark, fees and grants), `RatingAgency.cs`, `Stock.cs`, `Scoring.cs`; `World/Feed.cs`; `Config/`; `Government.cs` (the slot contract); `Policy.cs`; `EducationRegime.cs`.
- `src/Eunomia.Governments`: Frozen, StatusQuo, Technocrat, ReplayDecisions, ReturnsProbe, RegimeFixed, Occupants. `Ai/`: `Models.cs` (CliModel, ScriptedModel), `StateReport.cs` (the only prompt builder), `ReplyParser.cs`, `SingleAi.cs`, `ThreeBranch.cs`.
- `src/Eunomia.Cli`: commands and writers (`years.csv`, `deliberations.md`, `transcripts.jsonl`).
- `tests/Eunomia.Tests`: 113 tests, all passing.
- `config/`: economy, world, government (floors, constitution, resistance switch), parameter sets, baseline scripts, models, `mandates/`.
- `packs/portugal/`: `country.json` (data and starting policy), `ministries.json` (display only).
- `runs/`: output, ignored by git. The only copies of the AI runs live here: `singleai-haiku-s1` to `s10` (before the new levers), `singleai-sonnet-s1` to `s10` (eleventh look), `singleai-sonnet-guard-s1` to `s10` (twelfth look), `threebranch-haiku-s1`, `smoke-*`, `smoke2-*`.

## Conventions that matter

- Economic numbers live in config, never in code. Regime and sector effects are measured against the starting state, so the starting policy changes nothing; the steady-state test guards this.
- Governments never see parameters or the future. `StateReport` builds every prompt. Derived gauges are allowed (committed spending, sectors, absorption, education by fifth).
- Model calls: `claude -p` in an empty temporary folder, tools off, no session persistence, `--setting-sources project`, system prompt from a file. Never `--bare`: it bypasses the subscription login.
- Rates a government sets are shown to two decimals (a 0.75% plan once read as 0.8% and caused a false dispute).

## Where things stand (Nov 22, 2026)

Done: toy steps 1 to 5; rating agency and EU fiscal rules; 40-year horizon; single AI and three branches; committed-spending gauge; ministry mapping; sectors-and-levers steps 1 to 3 (management quality and absorption; education by income fifth across generations; education regimes, the constitution, the right-to-education floor, the resistance switch, the regime search).

The Haiku runs in `runs/` predate steps 1 to 3. Sonnet with the new levers (eleventh look) used management shares, never an education regime, and lost every world to the poorest-fifth guard through steady income tax cuts. Told what the guard compares against and shown a gauge (twelfth look), it won 2 worlds against the technocrat, traded off 8, lost none.

## Open decisions for the owner

1. Next after the AI reruns with education regimes: step 4 (health on the sector template).

Decided Nov 22, 2026: the poorest fifth guards the win rule (build plan, tenth look); rerun the AI governments before health.

## Backlog (known gaps, not built)

The map viewer (Godot, reusing the Oathfall import pipeline); regions (NUTS II, health regions); EU funds and the Ministry of Economy's levers; AI ministers and the organisation experiment (design notes, Delegation); the justice system as an eighth line; pensions rising with ageing; unemployment; an anonymised-country experiment; approval with effects; checkpoints, resume and an adapter isolation check for AI runs; other vendor adapters; the two-of-three signature rule; a budget-office projection; validation against Portugal 2000 to 2025. Known model quirks: absolute household incomes are too high for Portugal, and net trade is a residual that nothing uses.
