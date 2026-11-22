# Eunomia — Build Plan: the toy

Nov 21, 2026

The full spec (`reference-spec-v2.md`) is a quarry, not a blueprint. Reviewing prose stopped converging, so we build the smallest version that runs, look at what it does, and add realism only where a run shows it is missing.

**Rule:** nothing is added because a reviewer can imagine it matters. It is added when a run shows it matters.

## What the toy is

Portugal as one national economy, governed for 40 years in yearly steps (a generation: born, educated, working), by a government that sets taxes, the deficit and the budget split. Every run is compared with another run in the same world.

| In the toy | Left out until a run asks for it |
| --- | --- |
| One national economy, five income groups | Regions, sectors (except a farming share) |
| Yearly steps, 40 years | Monthly ticks, quarterly and emergency meetings |
| Three taxes, planned deficit, seven budget shares | Allowances, tax-change limits beyond simple bounds |
| A seeded world: demand, energy, food, interest rate, stress, a few events | Partner countries, the world map |
| Occupants: frozen, status quo, technocrat, then one AI, then three branches | Replay, human player |
| CSV output, a returns table, simple charts | The Godot viewer (comes right after the AIs) |
| Debt floor, income-group floor, end-state checks | Hunger floor, validation against history |

## The model, in plain words

Everything is in constant 2024 euros. Every stock is an index that starts at 1.

1. **Output.** Productivity × capital × workers made better by skills and health × infrastructure, with a farming share boosted by farm productivity. Some output is lost to instability. World demand pushes it up or down, and so does a change in public purchases (a short-run multiplier, one number).
2. **Trend.** Productivity grows 1% a year by itself, so a government that does nothing still sees slow growth.
3. **Stocks.** Each budget line fills a stock after a delay, and the stock drains slowly if neglected. Spending at the starting level holds the stock at exactly 1.
4. **Incomes.** Output splits into wages and profits. Firms pay corporate tax, invest part of what is left, and pay the rest as dividends, mostly to the top fifth. Households pay income tax, receive transfers (mostly the bottom fifths), and spend a share of what they have, paying consumption tax.
5. **Taxes shrink their base.** A higher rate collects less than proportionally, because more is avoided. State capacity improves collection.
6. **Leakage.** Part of every budget line is lost before delivery. State capacity reduces it.
7. **Prices.** Energy, food and the consumption tax raise the household price index, which lowers real income.
8. **Debt.** Interest is charged at a market rate (world rate + spread that rises with debt + financial stress). Inflation shrinks real debt each year. *This fixes the units bug found in spec v2.*
9. **People.** Working-age population and household count follow fixed yearly rates from the pack.

**Score.** Real disposable income of the median household (the middle fifth), in the final year and in the worst year. Floors first, then income, as in the design notes.

## Occupants

| Occupant | Behaviour | Purpose |
| --- | --- | --- |
| Frozen | Keeps the starting policy | Sanity check |
| Status quo | Inertia, deficit limit, crisis reaction, election tilt | The typical government |
| Technocrat | A short script: steady shift toward long-term investment, debt kept on a declining path | Does a language model add anything over a sensible rule? |
| Single AI | One model, one mandate | First AI run |
| Three branches | The protocol from the design notes | The show |

## Tools

- `eunomia run` — one run, writes a folder with `years.csv` and `summary.json`.
- `eunomia pair` — two occupants over a list of seeds, writes the comparison as CSV.
- `eunomia returns` — **the returns table**: for each budget line, the effect on output and median income at years 5, 10 and 20 of spending an extra 1% of GDP, under each assumption set. This is how a non-economist judges the model.

## Steps

Each step ends green (builds, tests pass) and is committed.

1. Solution skeleton, configuration loading, seeded random generator.
2. World generator: series and events.
3. Economy model with the frozen occupant; one run writes CSV. Steady-state and direction tests.
4. Status quo and technocrat; `pair` and the returns table. **First look: does it feel plausible?**
5. Single AI through a vendor CLI, then three branches. **Second look: is it fun to read?**
6. A crude map viewer.
7. Realism, only where steps 4 and 5 showed it was missing.

## First look (step 4, Nov 21, 2026, unvalidated)

`eunomia returns`: an extra 1% of output a year, borrowed, on each line, against frozen policy in a calm world (central set):

| Line | Median income, year 5 | Year 10 | Year 20 |
| --- | --- | --- | --- |
| Social transfers | +1.57% | +1.50% | +1.36% |
| Health | 0.00% | +0.28% | +0.71% |
| Infrastructure | -0.06% | +0.14% | +0.62% |
| Agriculture | +0.08% | +0.30% | +0.57% |
| Education | -0.06% | -0.14% | +0.54% |
| Administration | +0.06% | +0.04% | +0.13% |
| Security | +0.06% | +0.04% | -0.11% |

`eunomia pair --seeds 1-50`: the technocrat loses to the status quo in all 150 worlds (median about -1.8% at year 20), because it moves money out of transfers into investment that has not paid back by year 20. The status quo beats frozen in 38 of 50 worlds, through its crisis and election transfers.

What this says: under this model and this score, **handing out borrowed money beats every investment for the median household within 20 years, even under high returns.** The debt it leaves (about +14% of output) breaches no floor. Agriculture, a tiny line, pays about as well as education: the stock-size artefact predicted from spec v2.

## Second look: rating agency and EU fiscal rules (Nov 22, 2026, unvalidated)

Added a composite rating agency (stepped ratings, outlook warnings, a two-notch crisis cut, a spread cliff below investment grade, calibrated so Portugal today scores about A- and Portugal 2011 about BB) and the EU fiscal rules as a switchable floor (deficit under 3%, debt above 60% falling on average over 4 years, escape clause in crisis years). The rating replaces the old debt-level spread curve.

Borrowing for transfers, against frozen (central set):

| Extra per year | Median, year 20 | Rating, year 20 | Floors |
| --- | --- | --- | --- |
| 0 (frozen) | — | AA- | none |
| 1% of output | +1.23% | A+ | none |
| 2% | +2.43% | A- | EU rules breached in year 4 |
| 3% | +3.62% | BBB+ | EU rules breached in year 4 |
| 5% | +4.98% | BB+ (junk) | EU rules breached in year 1 |

Pairs over 50 worlds per set: the technocrat still loses to the status quo in 47 to 49 of 50 worlds. The status quo itself breaks the EU rules in 2 of 50 worlds.

What changed: the EU rules now cap borrowed redistribution at roughly 1 to 2% of output a year, which is about the room Portugal really has. What did not change: inside that room, transfers still beat every investment for the median household within 20 years. The question of what the score rewards (debt and stocks handed on, or output alongside the median) is still open.

## Third look: a 40-year horizon (Nov 22, 2026, unvalidated)

The horizon is now 40 years: the time from birth through school into a full working life. `--horizon` overrides it.

Extra 1% of output a year, borrowed, against frozen (central set), median household income:

| Line | Year 20 | Year 30 | Year 40 |
| --- | --- | --- | --- |
| Education | +0.41% | +1.45% | **+2.52%** |
| Infrastructure | +0.50% | +0.83% | +1.09% |
| Administration | 0.00% | +0.69% | +1.08% |
| Health | +0.59% | +0.84% | +0.99% |
| Social transfers | **+1.23%** | +1.02% | +0.80% |

Transfers fade as interest on the borrowed money accumulates; education keeps compounding. Education overtakes transfers around year 25.

Technocrat against status quo, 50 worlds per set:

| Horizon | Central | Low returns | High returns |
| --- | --- | --- | --- |
| 20 years | loses 48 | loses 47 | loses 49 |
| 30 years | loses 47 | loses 47 | trade-off 45 |
| 40 years | trade-off 43 (final median +1.2%) | loses 47 | trade-off 48 (final median +4.1%) |

At 40 years the technocrat ends richer under central and high returns, but its worst year is worse (it cut transfers early), so the win rule calls it a trade-off: pain now, gain later. Under low returns it still loses. The answer now depends on the return assumption, which is what the named sets are for.

## Known questions to answer by running, not by writing

- Is a line's return driven by its size today rather than by evidence? (Spec v2 finding: stocks assume today's spending has held forever.)
- ~~Can education ever pay back inside 20 years?~~ No; inside 40, yes (third look).
- What do EU funds and rising pension costs change?
- The population path is fixed. Over 40 years, Portugal's real working-age decline is likely steeper than 0.4% a year.
