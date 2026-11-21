# Eunomia — Build Plan: the toy

Nov 21, 2026

The full spec (`reference-spec-v2.md`) is a quarry, not a blueprint. Reviewing prose stopped converging, so we build the smallest version that runs, look at what it does, and add realism only where a run shows it is missing.

**Rule:** nothing is added because a reviewer can imagine it matters. It is added when a run shows it matters.

## What the toy is

Portugal as one national economy, governed for 20 years in yearly steps, by a government that sets taxes, the deficit and the budget split. Every run is compared with another run in the same world.

| In the toy | Left out until a run asks for it |
| --- | --- |
| One national economy, five income groups | Regions, sectors (except a farming share) |
| Yearly steps, 20 years | Monthly ticks, quarterly and emergency meetings |
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

**Score.** Real disposable income of the median household (the middle fifth), in year 20 and in the worst year. Floors first, then income, as in the design notes.

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

## Known questions to answer by running, not by writing

- Is a line's return driven by its size today rather than by evidence? (Spec v2 finding: stocks assume today's spending has held forever.)
- Can education ever pay back inside 20 years?
- What do EU funds, fiscal rules and rising pension costs change?
