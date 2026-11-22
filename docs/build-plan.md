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

## Next realism step: management quality (planned Nov 22, 2026, not built)

**The gap.** The toy is almost all budget allocation: a euro for health does the same work whether the health system is well or badly run. The only management idea is one national state-capacity index, which acts on leakage, tax collection and the reallocation limit. In real public systems, outcomes are often limited by management (scheduling, staffing, procurement, information systems, approvals) more than by money or medical competence.

**Three levels, and what we do with each:**

| Level | What it means | Plan |
| --- | --- | --- |
| 1. National sector efficiency | Health, education and infrastructure each get their own management-quality index | **Build next.** About two formulas per sector |
| 2. Health regions | Each region has its own management quality and needs; the government divides the health budget among regions | **Build with regions and the map** |
| 3. Hospitals and staff | Individual hospitals, doctors, waiting lists | **Never.** No data to tune it, and it would not change the answer |

**Level 1 mechanics:**

- **Effective spending** = money × the sector's management quality. Only effective spending fills the sector's stock.
- **Absorption limit.** When a sector's budget grows faster than its management can handle, most of the extra is wasted. This also fixes the stock-size artefact from the first look, where a tiny line (agriculture) could be tripled for outsized returns.
- **Management quality** improves slowly when part of a sector's money goes to running it, and decays when that is neglected. The government decides that part, so it faces a real choice: more doctors now, or fix how hospitals are run first.

**Level 2 notes.** Portugal's health service was organised in five mainland health regions, and since 2024 runs through about 39 local health units that join hospitals and primary care; the Azores and Madeira run their own systems. The former health regions line up roughly with the nine NUTS II regions planned for the map. With regions, the legislature's "fair spread" mandate becomes concrete, and a map mode can show how well each region's health system is run.

**Caveat.** Management quality is hard to measure. There is regional data on outputs (waiting times, doctors and beds per person, deaths timely care could have prevented), but no reliable figure for how well a region is managed. Its strength will be an assumption, tested across the named parameter sets and recorded as assumed in the calibration ledger.

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

## Fourth look: the first AI government (Nov 22, 2026, unvalidated, one world)

Claude Haiku as the single AI, 40 years, world seed 1: 41 calls, one retry, no abstentions, about 15 to 30 seconds a decision.

Its path: surpluses for twenty years paying debt from 95% to 50% of output; administration raised from 10.6% to 19% of the budget, education from 10% to 18%, transfers cut from 42% to about 33%; then, with debt low, income tax cut from 42% to 34% and deficits near 2.5%.

| Government | Final median | Worst year | Poorest fifth, final | Debt, final |
| --- | --- | --- | --- | --- |
| Single AI (Haiku) | **€60,752** | **€39,559** | €43,281 | 49% |
| Technocrat | €60,013 | €38,855 | €46,929 | 19% |
| Frozen | €59,426 | €39,041 | €48,214 | 17% |
| Status quo | €59,271 | €39,041 | €47,975 | 23% |

The run first showed a breach of the end-state pipeline floor. That was a bug: the report said committed spending must hold each stock "there" (at 0.98 or above), while the code demanded it hold the stock at its current, higher level. The check now matches the stated rule, and replaying the recorded decisions (no model calls) gives a clean run that beats all three scripted governments on both scores, in this one world.

Worth watching: the poorest fifth ends lower than under any scripted government. The median score does not protect it beyond its 80% floor.

## Fifth look: the three branches meet (Nov 22, 2026, three-year trial)

Haiku in all three seats, world seed 1, three years. In 2026 the judiciary blocked the executive's first budget on the EU debt rule for 2027 (naming the floor, the year and the path); the executive cut the deficit, the legislature amended it lower, the executive accepted, the judiciary passed. In 2027 the executive declined an amendment as "internally inconsistent", which turned out to be a display bug: revisions showed rates to one decimal, so 0.75% read as 0.8%. Rates the government sets are now shown to two decimals.

## Sixth look: ten worlds, and the three branches over 40 years (Nov 22, 2026, unvalidated)

**Single AI (Haiku) against the technocrat, worlds 1 to 10 (replayed under the corrected rules):** the AI ends with a higher median in 10 of 10 worlds (by +1% to +10%), a better worst year in 9, and a richer poorest fifth in 8. But it breaks a floor in 6 of 10, so under the win rule it scores 3 wins, 6 losses and 1 trade-off (against the status quo: 2 wins, 6 losses, 2 trade-offs). Of the six breaches, four were on rules it could see (the EU debt rule in worlds 2, 3 and 9; state capacity ending below 0.98 in world 10) and two only on the committed-spending floor, which it could not verify (worlds 6 and 8). The recurring weak spot is administration: once state capacity is high, the AI treats it as expendable in the late years.

**Three branches (Haiku), world 1, 40 years:** 158 calls, 33 of 40 budgets settled in the first round, 6 amendments, 2 judiciary blocks (both on the EU debt rule, with correct four-year-window reasoning), no year without a decision. Highest incomes of any government on world 1 (median €64,051, poorest fifth €50,961, against the single AI's €60,752 and €43,281): the legislature's fair-spread mandate kept transfers at 41–46%. It breached the committed-spending floor for state capacity, which the judiciary could not check.

**Response:** the report now carries a committed-spending gauge for the four checked indices (now, and after this year's budget), and every revision shows it. A test confirms the final-year gauge predicts the floor exactly. Most retries were unit slips (42 for 42%); the reply format should say "0.42 means 42%".

## Seventh look: the sector template (step 1 of sectors and levers, Nov 22, 2026, unvalidated)

Six sectors now split their money into a management share (a new yearly lever, starting at 5%) and front-line money. Management builds a slow index; efficiency (index ^ 0.15, capped at 1.5) multiplies front-line money; front-line money can grow about 10% a year times efficiency over what was absorbed last year, and the excess builds a fifth as much. Social transfers are cash and exempt.

Returns table, extra 1% of output a year on one line, median household income (central set):

| Line | Year 10, before | Year 10, now | Year 40, before | Year 40, now |
| --- | --- | --- | --- | --- |
| Agriculture | +0.23% | **-0.01%** | +0.65% | +0.71% |
| Education | -0.21% | -0.21% | +2.52% | +2.78% |
| Health | +0.21% | +0.20% | +0.99% | +1.20% |
| Infrastructure | +0.07% | +0.04% | +1.09% | +1.29% |
| Administration | -0.04% | -0.03% | +1.08% | +1.27% |
| Social transfers | +1.44% | +1.44% | +0.80% | +0.78% |

The stock-size artefact is gone: tripling a tiny line now takes years to absorb, so agriculture no longer pays early. Long-run returns rise slightly because stocks are calibrated on front-line money.

Technocrat against status quo, 50 worlds: central trade-off in 46 (final median +1.8%, was +1.2%), high returns trade-off in 48 (+5.5%, was +4.1%), low returns still loses 47. Its one-point-a-year shifts stay within the absorption limit.

## Eighth look: education by fifth and generation (step 2 of sectors and levers, Nov 22, 2026, unvalidated)

Education money builds a school stock; quality is school stock per pupil to the power 0.5; coverage at three levels per fifth responds to quality and to the parents' schooling; school leavers join the workforce after nine years and a fortieth of the workforce retires each year; wages by fifth follow each fifth's skills.

**Frozen policy, calm world, 40 years:** workforce skills +24% with no policy change (poorest fifth +48%, richest +9%), because Portugal's young are far better schooled than its older workers. Quality per school year +33% (spending grows with the economy while cohorts shrink). Growth rises from 1.0% to 1.7% a year; median household income +57%; poorest fifth +78%, richest +29%.

**Returns table, extra 1% of output a year, median at year 40 (central):** education fell from +2.78% to **+0.57%**; health +1.31%, infrastructure +1.42%, agriculture +0.87%, administration +0.69%, transfers +0.87%. Education money now works only through quality, at half elasticity, and only through new generations.

**Technocrat against status quo, 50 worlds:** central 14 losses and 36 trade-offs (final median +0.2%, was +1.8%); low returns 49 losses; high returns 50 trade-offs (+2.6%).

**Two things to watch.** The spending-to-learning elasticity (0.5) now largely decides what education money is worth, and it is not yet in the named sets. And with wages following skills one for one, frozen policy compresses inequality strongly over 40 years. Both are assumptions to test, not findings.

## Ninth look: the education regime search (step 3 of sectors and levers, Nov 22, 2026, unvalidated)

All 36 combinations of provision, university fees, student support and standards (the earmark only matters when a government cuts, so it is left out), each held from year 0 on the frozen budget, paired with Portugal's regime today (public, flat fee, tested support, comprehensive) over 10 worlds per set. `eunomia regimes`, no model calls, seconds to run.

The first pass gave selective schools a 10% learning gain for those who stay in every set, and selective topped every list. Studies of early tracking tend to find more inequality without a clear average gain, so the gain now spans the sets (0 low, 5% central, 10% high), like private efficiency.

| Set | Best regime | Final median | Poorest fifth |
| --- | --- | --- | --- |
| Central | public / free / universal / selective | +0.86% | -0.61% |
| Low returns | public / free / universal / comprehensive | +0.45% | -0.13% |
| High returns | private / income-scaled / universal / selective | +5.68% | +1.90% |

**What holds across sets:** universal student support is in every set's best regime; public provision wins under central and low assumptions; fees matter less than support. **What flips:** provision (private wins only when private schools are assumed 15% more efficient) and standards (selective wins when it is assumed to teach more, and then lowers the poorest fifth's income under central assumptions). The worst regime under central and low assumptions is private with flat fees and tested support: the poorest fifth ends 2.8% to 5.0% worse off.

**The median score lets a regime win while the poorest lose.** Central's best regime raises the median and lowers the poorest fifth. The win rule only protects the poorest down to their 80% floor. This is a value question for the score, not a modelling one.
