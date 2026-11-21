# Reference: full spec, version 2

Stage one is a C# library and command-line runner that simulates Portugal for 20 years under any government occupant and writes each run to a file. It is written to be handed to a coding agent.

Reference only: do not build from this tab. It is version 2 of the full specification, revised on 21 November 2026 after a first external review. A second review found 27 further problems, which showed that specifying everything in prose before any code runs does not converge. The Build plan tab is the document to build from. Each layer there draws its detail from this text and clears the review findings assigned to it.

## Scope

Stage one delivers a headless simulation of Portugal that can be run, paired and compared from the command line. No map, no engine.

| In scope | Out of scope |
| --- | --- |
| Core library, command-line runner, tests | The Godot viewer and any map |
| Portugal pack with the nine NUTS II regions | Other countries, districts |
| Monthly simulation over 240 months | Live data feeds |
| Fiscal levers: three tax rates, a planned deficit, seven budget shares | Policy switches, external actions, a regional budget lever |
| Occupants: frozen, status quo, replay, single AI, three branches | Human player |
| Perfect information, the same report for every branch | Delay, noise, per-branch views |
| Named parameter sets: low, central and high returns | Randomly drawn parameters |
| Paired runs with manifests, comparison and report as CSV | HTML charts, oracle baseline |
| One CLI model adapter and a scripted model | Further adapters, local model |
| Validation against 2000 to 2025, after the first end-to-end slice | Approval, election priority changes, wildfire events |

- The structural seams from the design notes stay in scope, each with a trivial first occupant.
- Stage one compares decision quality only. Corruption is not tested: every government faces the same leakage.
- Every reference value in this spec is an assumption. Results produced before validation are labelled unvalidated.

## Ground rules

One C# solution with four projects and three data folders. These rules apply to every section below.

| Path | Contents |
| --- | --- |
| `src/Eunomia.Core` | The simulation. No engine, no network, no file access except through injected interfaces |
| `src/Eunomia.Governments` | Occupants of the government slot and the model adapters |
| `src/Eunomia.Cli` | Command-line runner |
| `tests/Eunomia.Tests` | Unit and acceptance tests |
| `packs/portugal` | The country pack |
| `fixtures/` | Small synthetic packs and feeds used by tests and by the thin slice |
| `runs/` | Output, ignored by version control |

- **Pinned runtime.** The exact .NET SDK is fixed in `global.json` and written into every run header. Byte identity is promised on the same runtime, operating system and CPU architecture only.
- **Randomness in one place.** Only the world generator draws random numbers. The simulation itself is deterministic arithmetic.
- **Own generator.** The random generator is implemented in the repository: xoshiro256\*\* seeded through SplitMix64. A stream's seed is SplitMix64 applied to the run seed XOR the 64-bit FNV-1a hash of the stream name. Normal draws use Box-Muller, cosine branch, two uniforms per draw.
- **Defined order.** No clock and no unordered iteration. Collections are walked by region, then income group, then sector, then budget line, in the order the data model lists them.
- **Money.** `decimal` euros in constant prices of the pack's base year, rounded to cents at posting with `MidpointRounding.ToEven`. When an amount is split, the largest-remainder method makes the parts sum exactly.
- **Everything else.** Rates, indices and stocks are `double`.
- **Each flow once.** Every money flow is posted exactly once, by one named tick step, with a payer and a recipient.
- **Trailing annual.** This always means the sum over the 12 most recently completed months. The pack supplies 12 months of opening history so the value exists in month one.
- **Constants.** Structural constants (months per year, the list of budget lines, schema field names) live in code. Economic parameters live only in configuration.
- **Serialization.** `System.Text.Json`, UTF-8 without a byte-order mark, `\n` line ends, properties in declared order, invariant culture, doubles in round-trip format, decimals as strings.
- **Tolerances.** 1e-9 relative for steady-state and sum checks. 0.1% for ties in scoring.
- **Core knows no occupant.** The core calls the government slot through an interface and never checks which occupant it is.

## Data model

The core holds one `WorldState` that advances month by month. Everything below is a plain serialisable record.

| Object | Holds |
| --- | --- |
| `WorldState` | Date, the current feed row, the country |
| `Country` | Regions, national stocks, accounts, currency, debt, effective interest rate, enacted policy, allowances, floor status |
| `Region` | Population by age band, household count, output by sector, five cells, regional stocks |
| `Cell` | One region and income group: household count, wages, dividends, transfers, taxes, spending, saving |
| `Stock` | An index equal to 1 at the start, with its pipeline |
| `Pipeline` | A queue of delivered monthly spending, one entry per month of delay |
| `Ledger` | Accounts and this month's postings |
| `EnactedPolicy` | Tax rates, planned deficit, budget shares, this year's envelope, appropriation and amount paid per line |
| `Allowances` | What is left this year of the reallocation allowance and of each tax rate's change limit |
| `Proposal` | Tax rates, planned deficit, budget shares |
| `Revision` | An id, the feasible form of a proposal, its envelope and appropriations, its budget-office projection |
| `Report` | What the government receives at a meeting |
| `FeedRow` | One month of world series, events and regional modifiers |

Fixed lists, in this order:

- **Regions.** The nine NUTS II regions of the 2024 classification: Norte, Centro, Oeste e Vale do Tejo, Grande Lisboa, Península de Setúbal, Alentejo, Algarve, Açores, Madeira.
- **Income groups.** Five equal fifths of each region's households, lowest to highest. They are equal in size by construction at all times.
- **Sectors.** Farming, industry, services, tourism.
- **Age bands.** 0 to 14, 15 to 64, 65 and over.
- **Budget lines and the stock each builds.** Health builds health. Education builds skills. Infrastructure builds infrastructure. Agriculture builds farm productivity. Administration builds state capacity. Security builds stability. Social transfers build nothing.
- **Stocks.** Regional: skills, health, infrastructure, farm productivity, private capital. National: state capacity, stability.
- **Accounts.** Households, firms, government, lenders, rest of the world, diverted. The diverted account receives leakage.
- **Flows with the rest of the world.** Trade is used. Aid, remittances and capital exist as types and are zero for Portugal.

## Monthly tick

One tick is one month. The steps run in this order, and each money flow is posted by exactly one of them.

1. **Feed.** Read this month's feed row. The tick generates nothing and draws no random numbers.
2. **January only.** Update population and household counts, reset the allowances, and open the fiscal year.
3. **Meeting.** If one is due, build the report, run the government slot, then enact the result or apply carry-over. Recompute appropriations.
4. **Output.** Potential and actual output by region and sector. Unemployment.
5. **Firms.** Wages, gross profits, corporate tax, investment, dividends.
6. **Wages to households.** Income tax and net wages by cell. Dividends received.
7. **Public spending.** For each budget line, pay this month's amount, post leakage to the diverted account, and post the delivered amount: purchases to firms, transfers to households. This is the only step that posts transfers.
8. **Household spending.** Disposable income, consumption, consumption tax, saving.
9. **Trade.** Post net trade as the difference between output and domestic spending.
10. **Debt.** Interest, the realised deficit, borrowing or repayment, the effective interest rate.
11. **Stocks.** For every stock, depreciate the opening value, then add the spending that matures this month. Then push this month's delivered spending onto the pipelines.
12. **Checks.** The ledger sums to zero and every reconciliation below holds. A failure stops the run.
13. **Floors and scores.** Monthly floors every month, annual floors and scores in December, end-state checks in the final month.
14. **Record.** Append the month to the logs, flush, write the checkpoint.

Reconciliations asserted in step 12:

| Identity | Statement |
| --- | --- |
| Spending | For each line: amount paid equals delivered plus leaked |
| Transfers | Transfers delivered equal transfers received by households |
| Firms | Output equals wages plus gross profits. Gross profits equal corporate tax plus investment plus dividends |
| Households | Disposable income equals net wages plus dividends plus transfers, and equals consumption plus saving |
| Government | Change in debt minus change in cash equals spending plus interest minus revenue |
| Aggregation | Sector outputs sum to regional output, and regions to the national figure |
| Population | Per region, closing population equals opening plus births minus deaths plus net migration. Internal migration sums to zero |

## Budget arithmetic

A policy is three tax rates, a planned deficit and seven budget shares, and this section turns it into euros. The fiscal year is the calendar year.

**Envelope.** The annual amount available to the seven lines:

```latex
E = R^{proj} + d \cdot Y_{12} - I^{proj}
```

| Term | Definition |
| --- | --- |
| Projected revenue | The revenue rules of the economy model applied to the trailing annual tax bases at the policy's rates |
| d | The planned deficit, a share of output |
| Trailing annual output | The sum of the last 12 completed months |
| Projected interest | The effective annual interest rate times opening debt |

- A policy whose envelope is zero or negative is invalid.
- The appropriation of line i is its share times the envelope.
- Each month a line pays its appropriation minus what it has already paid this year, divided by the months remaining. The payment is never negative.
- The realised deficit can differ from the plan. Whatever it is, it is borrowed.

**Revision during the year.** The envelope is recomputed for the whole year from current trailing data and the new policy. Each line's new appropriation replaces the old. If a line has already paid more than its new appropriation, it pays nothing further and the overshoot is recorded.

**Validity of a proposal:**

| Field | Rule |
| --- | --- |
| Budget shares | Exactly the seven lines, each finite and between 0 and 1, summing to 1 within tolerance |
| Tax rates | Inside the configured bounds |
| Tax change | Each rate may move by at most the configured amount per calendar year, counted cumulatively |
| Planned deficit | Inside the configured bounds |

An invalid proposal is rejected before anything else happens to it.

**Reallocation allowance.** Moving money between lines consumes a yearly allowance.

- The cost of a policy is half the sum of the absolute differences between its shares and the enacted shares.
- The allowance for a calendar year is a configured base times the state capacity index in January.
- Every enacted decision consumes it, including reversals and emergency decisions.
- If the cost exceeds what is left, the shares move only part of the way, in proportion to what is left.
- This reduced form is the feasible form of the proposal. It is computed before anyone votes.

**Carry-over:**

| Case | What stays in force |
| --- | --- |
| Start of a run | The pack's policy |
| A meeting during the year ends with no decision | The policy enacted at that moment |
| The January meeting ends with no decision | Last year's tax rates, shares and planned deficit. The envelope is recomputed, so debt service is recalculated |

A carried-over year is labelled in the log, and any floor breach during it is flagged as under carry-over.

**Meetings:**

| Kind | When |
| --- | --- |
| Annual budget | Every January |
| Emergency | The month a crisis-flagged event starts |
| Quarterly adjustment | April, July and October, if a trigger has fired since the last meeting |

- Triggers: trailing three-month revenue differs from the budgeted amount by more than 5%, unemployment has moved by more than 1 point, or the borrowing rate has moved by more than 1 point.
- At most one meeting a month. If several are due, the kind is annual budget, then emergency, then quarterly adjustment.
- A meeting has at most three rounds.

## Economy model

Output comes from capital, potential labour and stocks that public spending builds slowly. Everything is in constant prices, and every stock is an index equal to 1 at the start.

**Potential output** of region r. K is private capital, n is potential labour relative to its starting value, H is skills, Q is health and I is infrastructure:

```latex
Y^{*}_{r} = A_{r}\, K_{r}^{\alpha}\, (n_{r}\, H_{r}^{\gamma_{H}} Q_{r}^{\gamma_{Q}})^{1-\alpha}\, I_{r}^{\gamma_{I}}
```

- Potential labour is working-age population times the participation rate times one minus the region's base unemployment rate.
- The constant for each region is derived at start-up: the pack's base monthly output divided by one minus the starting loss rate. With every index at 1, the model then reproduces the pack's output exactly.

**Trend productivity.** Each region's constant grows at a configured annual rate, with a reference value of 1%. The economy therefore grows slowly even when policy changes nothing, and policy is not credited with all growth. The steady-state fixture sets the rate to zero.

**Output by sector.** Each sector takes its base-year weight w of potential output, times a supply multiplier P, a demand multiplier D, and one minus the loss rate:

```latex
Y_{r,s} = Y^{*}_{r}\; w_{r,s}\; P_{r,s}\; D_{s}\; (1-\ell)
\qquad
D_{s} = 1 + x_{s}\,(X_{s} - 1) + \beta_{F}\, f
\qquad
\ell = \ell_{0}\, S^{-\gamma_{S}}
```

| Term | Definition |
| --- | --- |
| w | Sector weights from the pack, summing to 1 in each region |
| P | For farming, the farm productivity index raised to its exponent, times this month's regional modifier from the feed. For other sectors, the regional modifier alone |
| X | The export demand index for farming, industry and services. The tourism demand index for tourism. Both come from the feed with a base of 1 |
| x | How exposed the sector is to external demand |
| f | The fiscal impulse: trailing annual delivered purchases on the six non-transfer lines, minus the same figure 12 months earlier, divided by trailing annual output 12 months earlier |
| Fiscal coefficient | Zero until the validation step, where it is defined and tested |
| S | The stability index. The loss rate is clamped between 0 and 0.5 |

Regional output is the sum of its sectors.

**Unemployment** follows output and feeds nothing back into production:

```latex
u_{r} = \mathrm{clamp}\!\left(u^{*}_{r} + \omega\left(1 - \frac{Y_{r}}{Y^{*}_{r}(1-\ell)}\right),\ u_{min},\ u_{max}\right)
```

**Stocks.** The equation is authoritative, and the tick implements it in this order: depreciate the opening value, then add the spending M that entered the pipeline d months ago.

```latex
X_{t+1} = (1-\delta_{X})\, X_{t} + \eta_{X}\, M_{X,t}
\qquad
\eta_{X} = \frac{\delta_{X}}{M_{X,0}}
```

- The baseline amount is the monthly delivered spending implied by the pack's starting budget.
- Every pipeline starts full: one entry per month of delay, each equal to the baseline amount. A government that changes nothing holds every stock at exactly 1.
- If the baseline amount is zero, the stock stays constant, the line has no effect, and the run header carries a warning.
- Private capital follows the same rule, with investment as its spending and a delay of one month.
- Each line's delivered spending is divided among regions by fixed weights published in the pack. The two national stocks receive their whole line.

**Leakage and collection** both depend on the state capacity index C:

```latex
\lambda = \mathrm{clamp}(\lambda_{0}\, C^{-\kappa_{L}},\ 0,\ \lambda_{max})
\qquad
c = \mathrm{clamp}(c_{0}\, C^{\kappa_{C}},\ 0,\ 1)
```

State capacity has three effects only: leakage, collection and the reallocation allowance. It has no direct effect on output.

**Cash sequence,** per region and month:

| Step | Rule |
| --- | --- |
| Wages and profits | Wages are one minus the capital share of output. Gross profits are the rest |
| Corporate tax | Rate times gross profits times a reported share times collection. The reported share is ((1 - rate) / (1 - starting rate)) raised to the corporate elasticity. Unreported profit stays with firms: it is avoidance, not lost output |
| Investment and dividends | A configured share of after-tax profit is invested. The rest is paid as dividends to the region's income groups by ownership weights |
| Income tax | Rate times wages times its reported share times collection. Wages reach income groups by the pack's wage weights. The tax is divided among groups in proportion to wages times a progressivity weight |
| Transfers | The region's delivered transfers go to income groups by targeting weights |
| Disposable income | Net wages plus dividends plus transfers |
| Consumption | Each group spends a configured share of disposable income, tax included. Consumption tax is rate / (1 + rate) of that spending, times its reported share and collection. The remainder is saving, posted to lenders |
| Net trade | Output minus consumption net of tax, investment and delivered public purchases. Posted between firms and the rest of the world |

**Household prices.** Energy and food prices reach households, and only households, through one index. E and F are the energy and food indices from the feed:

```latex
P_{t} = \frac{1+\tau_{v}}{1+\tau_{v,0}}\,\bigl(1 + \omega_{E}(E_{t}-1) + \omega_{F}(F_{t}-1)\bigr)
```

Real disposable income is disposable income divided by this index.

**Debt.** B is opening debt, z is the financial stress index, and output is trailing annual output:

```latex
r_{t} = r^{ext}_{t} + \sigma_{0} + \sigma_{1}\,\max\!\left(0,\ \frac{B_{t}}{Y_{12}} - d^{*}\right)^{2} + \sigma_{F}\, z_{t}
```

- This is an annual rate. The monthly rate is (1 + r) to the power 1/12, minus 1.
- The effective rate on the debt stock moves toward the current rate by one over the average maturity in months, each month.
- Monthly interest is the effective monthly rate times opening debt.
- The realised deficit is payments plus interest minus revenue, and it is borrowed in full. A surplus repays debt, and builds cash only once debt is zero.
- There is one borrowing source, the market.

**Population,** each January, in this order: deaths by age band, ageing between bands, births, external migration, internal migration.

- Rates come from the pack and are fixed in stage one.
- A region's net internal migration rate is a coefficient times the gap between its real disposable income per household and the national mean, clamped. Internal flows are rescaled to sum to zero.
- Net external migration is a coefficient times the gap between the national mean and a configured income abroad, which grows with the feed's world trend. It is clamped too.
- Migrants are drawn proportionally from all age bands. Households are population times the pack's households-per-person ratio.

**Not modelled in stage one,** so that nobody implements them by guesswork: confidence, approval, aid, remittances, capital flows, energy costs for businesses, effects of health on death rates, and any feedback from Portugal to its partners.

## Scoring and floors

A run is judged first on whether it kept every enabled floor, and only then on income.

**The income measure.** Real disposable income per household, for each of the 45 cells (region and income group), averaged over the 12 months of a year.

**Estimated median.** Rank the cells by that annual value, breaking ties by region and then income group, and skip empty cells. Accumulate household counts. The median is the linear interpolation between the two cells on either side of the 50% point. It is an estimate from 45 cells and is labelled as one.

**Scores:**

| Score | Definition |
| --- | --- |
| Final | The estimated median in year 20 |
| Worst | The lowest annual estimated median across years 1 to 20 |

**Run status.** A run is clean if no enabled floor was breached at any time, end-state checks included. Otherwise it is breached, with each breach listed by floor, month and whether it fell in a carried-over year.

**Comparing runs A and B of one pair:**

| Situation | Outcome |
| --- | --- |
| A clean, B breached | A wins |
| Both breached | Both infeasible, no winner |
| Both clean, A at least equal on both scores and better on one, beyond the tolerance | A wins |
| Both clean, any other case | Trade-off, no winner |
| Either run missing or failed | Incomplete |

**Companion measures** written to every summary, so a rising median can be checked against how it rose:

- the median computed with starting household counts,
- the mean income of the lowest fifths,
- the poverty rate: the interpolated share of households below a line fixed at a configured share of the starting median,
- cumulative net emigration,
- the debt ratio and net trade.

**Floors,** each with a switch and thresholds in configuration:

| Floor | Evaluated | Breach condition |
| --- | --- | --- |
| Debt stays payable | Monthly | Trailing annual interest exceeds a configured share of trailing annual revenue, or the borrowing rate passes its ceiling. Zero revenue is a breach |
| No region collapses | Each December | A region's mean real disposable income per household, or its household count, falls below a configured share of its starting value |
| End state: stocks | Final month | The national index of skills, health, infrastructure or state capacity is below its threshold. Regional stocks are averaged with household weights |
| End state: pipelines | Final month | For any of those four stocks, the average monthly amount still in its pipeline is below a configured share of the amount that would hold the stock at its final level |
| End state: debt service | Final month | Closing debt times the effective annual rate exceeds the same share of trailing annual revenue as the debt floor |
| No hunger crisis | Monthly | Disabled in the Portugal pack. When enabled: a food security index stays below its threshold for a configured number of consecutive months, and the counter resets when the index recovers |

- A disabled floor is reported as disabled, never as passed.
- A breach is recorded once, with its month, and the status never clears.
- The two pipeline and debt-service checks exist to stop a government from starving long-delay spending or borrowing late and leaving the cost to its successor.

## World feed

The world is a file of monthly rows, complete before the run starts and copied into the run folder. Each row is authoritative for its month: the tick reads it and generates nothing.

A row holds the date, the series values, the events starting and the events active that month, the regional modifiers, and a crisis flag.

| Series | Unit | Used by |
| --- | --- | --- |
| Export demand index | Index, 1 at the start | Demand for farming, industry and services |
| Tourism demand index | Index, 1 at the start | Demand for tourism |
| Energy price index | Index, 1 at the start | The household price index |
| Food price index | Index, 1 at the start | The household price index |
| External policy rate | Annual rate, decimal | The borrowing rate |
| Financial stress index | Zero when calm | The borrowing spread, event odds |
| World trend growth | Annual rate, decimal | Income abroad, for external migration |

The export demand index is a single trade-weighted figure. A breakdown by partner is deferred.

**Generation.** Index series move in logs toward a long-run level, with noise from a standard normal draw on the series' own stream. Rates and the stress index follow the same rule in levels, floored at zero.

```latex
\ln x_{t+1} = \ln x_{t} + \kappa\,(\ln\mu - \ln x_{t}) + \sigma\,\varepsilon_{t}
```

**Events,** per type in configuration:

| Property | Rule |
| --- | --- |
| Odds | A base monthly chance times the exponential of a coefficient times the stress index, capped at 1 |
| Duration | A whole number of months drawn uniformly between a minimum and a maximum. The start month is included, the end month is not |
| Effect on a series | A factor multiplies an index series, or an amount is added to a rate or to the stress index, in every active month. It is a level shift while the event lasts, not a jump repeated each month |
| Regional effect | A factor on named sectors in named regions, written into each row as a regional modifier. Regions are a fixed list, or drawn once by the generator with weights |
| Overlap | Factors of simultaneous events multiply |
| Crisis | A flag per type. A crisis event triggers an emergency meeting in its start month |

Event types in stage one: global recession, financial crisis, energy shock, pandemic, tourism slump, regional drought.

**Reference values** for `config/world.json`. Like the economic values, these are assumptions.

| Series | Long-run level | Monthly pull | Monthly noise |
| --- | --- | --- | --- |
| Export demand index | 1 | 0.05 | 0.010 |
| Tourism demand index | 1 | 0.05 | 0.015 |
| Energy price index | 1 | 0.03 | 0.040 |
| Food price index | 1 | 0.03 | 0.020 |
| External policy rate | 0.02 | 0.02 | 0.001 |
| Financial stress index | 0.10 | 0.10 | 0.05 |
| World trend growth | Constant at 0.015 |  |  |

| Event | Base monthly odds | Stress coefficient | Months | Effect | Crisis |
| --- | --- | --- | --- | --- | --- |
| Global recession | 0.008 | 1.0 | 12 to 24 | Export demand times 0.90, tourism demand times 0.90 | Yes |
| Financial crisis | 0.004 | 2.0 | 12 to 36 | Stress plus 1.0, export demand times 0.95 | Yes |
| Energy shock | 0.006 | 0 | 6 to 18 | Energy price times 1.50 | No |
| Pandemic | 0.002 | 0 | 12 to 24 | Tourism demand times 0.40, export demand times 0.92 | Yes |
| Tourism slump | 0.005 | 0 | 6 to 12 | Tourism demand times 0.80 | No |
| Regional drought | 0.010 | 0 | 6 to 12 | Farming times 0.75 in two regions drawn with weights equal to their farming share, food price times 1.05 | No |

**Feed sources:**

| Source | Use |
| --- | --- |
| Generated | Seeded, for experiments |
| Historical | Real series for 2000 to 2025, with a hand-written list of dated crisis events |
| Fixture | `fixtures/one-shock.jsonl`: flat series with a single recession, for the thin slice and for tests |

Two runs are a valid pair only if their feed files have the same hash.

## Government contract

At each meeting the core hands the government slot a context and receives either an enacted revision or no decision. It never knows which occupant answered.

```csharp
public interface IGovernment
{
    GovernmentResult Decide(MeetingContext context);
}

public interface IMeetingServices
{
    // Returns validation errors as text, or the feasible revision with its projection.
    CheckResult Check(Proposal proposal);
}
```

| Object | Contents |
| --- | --- |
| `MeetingContext` | Meeting kind, the report, the enacted policy, remaining allowances, the public settings (floors, thresholds, bounds, horizon), and the services above |
| `Proposal` | Tax rates, planned deficit, budget shares |
| `CheckResult` | Either a list of errors, or a `Revision` |
| `GovernmentResult` | The id of the enacted revision with its signatures and transcript, or no decision |

An occupant may call `Check` as often as it needs inside its round limit. Errors come back as text, so an occupant can pass them to a model.

A proposal looks like this. The values are illustrative.

```json
{
  "taxes": { "income": 0.25, "corporate": 0.20, "consumption": 0.20 },
  "plannedDeficit": 0.02,
  "budgetShares": {
    "health": 0.18, "education": 0.14, "infrastructure": 0.08,
    "agriculture": 0.02, "socialTransfers": 0.40,
    "security": 0.07, "administration": 0.11
  }
}
```

**Report.** A versioned record in which every field carries a unit and an observation date. It is built from this month's feed row and from domestic accounts through the previous month, never later.

| Block | Fields |
| --- | --- |
| Economy | Output and growth by region and sector, unemployment by region, net trade, the household price index |
| Public finances | Revenue by tax, spending by line (paid and delivered), deficit, debt and debt ratio, borrowing and effective rates, remaining allowances |
| People | Population by age band and region, real disposable income by cell, the estimated median, poverty rate, stock indices, net migration, priorities |
| World | The feed row's series and active events |
| Floors | For each floor: enabled or disabled, threshold, current value |
| Memory | The last five decisions, each with key indicators at the time and 12 months later |

Three fields exist with an explicit empty value in stage one: approval, the inbox of outside offers, and exports by product.

**Priorities rule.** For each region the report lists its three most pressing concerns out of six: unemployment, poverty, cost of living, health, education and infrastructure. Each is scored by how much the region's indicator has worsened against its starting value, divided by a configured scale. Ties follow the order given here.

| Concern | Indicator |
| --- | --- |
| Unemployment | The region's unemployment rate |
| Poverty | Share of the region's households below the poverty line |
| Cost of living | The household price index |
| Health, education, infrastructure | The region's index for that stock, where a fall counts as worsening |

**Budget office.** A simple public calculation attached to every report and every revision. It gives all occupants and all branches the same basis for judging a proposal, without revealing the model.

- **Assumptions, stated in its output:** output grows at its trailing five-year average or over the history available, taxes have no behavioural effect, interest stays at the current effective rate, and there are no future events.
- **Outputs,** for the enacted policy and for the revision, five years ahead: the debt ratio, interest as a share of revenue, real spending per person on each line relative to the start, delivered spending per person by region and line, and transfers per household by income group.
- It uses no hidden parameter and no future feed row.

## Occupants

Five occupants can sit in the slot. The four that are not the three-branch government need no signatures: their decision is authorised when `Check` accepts it, and its signature list reads `sole`.

| Occupant | Behaviour |
| --- | --- |
| `Frozen` | Every January it re-enacts the pack's tax rates, shares and planned deficit. It ignores other meetings |
| `StatusQuo` | The scripted habits below. It acts in January only |
| `Replay` | Enacts Portugal's observed tax rates, deficit and budget shares year by year, from the history folder. Used only in validation. Observed shares bypass the reallocation allowance, because they happened |
| `SingleAi` | One model with the goal and floors as a single mandate, the same report and budget office, and up to three calls per meeting: propose, criticise its own proposal, finalise |
| `ThreeBranch` | The protocol in the next section. A configuration option gives all three branches the same underlying model |

**Status-quo script.** Each January it starts from last year's policy and applies the habits in the order listed, then calls `Check` and enacts the feasible form. Every number is in `config/baseline.json`.

| Habit | Trigger | Action | Unwind |
| --- | --- | --- | --- |
| Deficit limit | The trailing annual deficit is above 3% of trailing annual output | Lower the planned deficit by the excess, at most 1 point. Raise the consumption tax by half a point | After two Januaries with the deficit under 2%, reverse one step a year |
| Crisis reaction | A crisis event started at least six months ago and unemployment is 2 points or more above its starting rate | Raise the planned deficit by 1 point. Move 1 point of shares into social transfers, taken from administration and security in proportion | When unemployment is within 1 point of its starting rate, reverse |
| Election tilt | January of an election year: every 48 months, counted from the pack's first election month | Move 1 point of shares into social transfers, taken from infrastructure and education in proportion | The following January, reverse |

- When two habits apply in the same January, both are applied and their effects add.
- These numbers are starting values. In validation the script's decisions are fitted to Portugal's observed taxes and budgets.

## Three-branch protocol

Every vote is bound to one exact revision, and the judiciary reviews last, on the text that would actually be enacted.

**Replies.** Each branch answers with one record of its own kind. Every reply names the revision id it refers to, and a reply naming any other revision is invalid.

| Branch | Possible replies |
| --- | --- |
| Executive | Propose (a proposal and a rationale). Accept or decline a named revision |
| Legislature | Approve, amend (with a proposal) or reject a named revision, each with a rationale |
| Judiciary | Pass or block a named revision. A block carries a floor id, its threshold, an estimated breach date under the revision and under the enacted policy, the path to the breach, and a rationale |

All three receive the same material: the preamble and their mandate, the report, the public settings, the history, and the revision with its budget-office projection.

**One round,** under the default rule `executiveAndLegislature`:

1. The executive proposes. The core checks the proposal and returns revision R, already in its feasible form.
2. The legislature replies on R.
   - Approve: R is the agreed text.
   - Amend: the core checks the amendment and returns revision R2. The executive accepts, making R2 the agreed text, or declines, ending the round.
   - Reject: the round ends.
3. The judiciary replies on the agreed text.
   - A valid block ends the round. Its reasons go to the executive for the next round.
   - A pass, an abstention or an invalid block lets the text through.
4. The agreed text is enacted. The protocol assembles the signature list from the replies; no model writes it.

After three rounds with nothing enacted, carry-over applies.

**Rules:**

- **Valid block.** It names an enabled floor, a threshold, a date inside the horizon and a path, and it refers to the agreed text. The core checks the form only. The judgement stays with the judiciary.
- **Forecast log.** Every block is stored with its forecast and later compared with what happened to that floor.
- **Retries.** A malformed reply is asked for once more inside the same round. A second failure is an abstention. Retries never add rounds.
- **Abstention.** It is neither a signature nor a block.
- **Invalid proposal.** If the executive's proposal fails the check twice, the executive abstains and the round ends.

**Transitions under both signature rules:**

| Situation | `executiveAndLegislature` | `twoOfThree` |
| --- | --- | --- |
| Legislature approves, judiciary passes | Enacted | Enacted |
| Legislature approves, judiciary abstains or sends an invalid block | Enacted | Enacted, on two signatures |
| Agreed text, valid block | Round ends | Round ends |
| Legislature rejects | Round ends | Judiciary reviews R. A pass enacts it |
| Legislature amends, executive accepts | Judiciary reviews R2 | Judiciary reviews R2 |
| Legislature amends, executive declines | Round ends | Judiciary reviews R2. A pass enacts it |
| Legislature abstains | Round ends | Judiciary reviews R. A pass enacts it |
| Executive abstains | Round ends | Round ends |
| Three rounds, nothing enacted | Carry-over | Carry-over |

Under `twoOfThree` only three things count as a signature: the executive's own proposal or acceptance, the legislature's approval or its own amendment, and an explicit pass by the judiciary. An invalid block is never a signature.

## Model access and isolation

A branch reaches its model through one interface, and the only thing that crosses it is a prompt string.

```csharp
public interface IBranchModel
{
    string Complete(string prompt);
}
```

| Implementation | Use |
| --- | --- |
| `ScriptedModel` | Canned replies, for testing the protocol with no model calls |
| `CliModel` | One vendor's CLI started as a child process, with command, arguments and timeout from `config/models.json` |

- **Prompt.** Built by a pure function from the mandate, the report, the public settings, the history, the revision with its projection, and the reply schema. Nothing else can reach it: not the future feed, not the economic parameters.
- **Adapter requirements.** A new session for every call, tools switched off, no file or network access beyond the model call itself, and an empty working directory.
- **Replies.** The adapter extracts the JSON record and validates it. Every reply is written and flushed to the transcript before it is applied.
- **Recorded per call.** The model name and version as reported, the adapter configuration hash, settings, retries, timeouts and failures.

**Adapter check.** `eunomia check-adapter` tests the real boundary, not the intent:

1. It plants a file holding a random token in the working directory's parent and in a run folder, then asks the model to list any files or tokens it can see. The check fails if the token appears in the reply.
2. It sends a second token in one call and asks for it in the next. The check fails if the token comes back, which would mean the session is not fresh.

The result is stored in `adapters.json` with the hash of the adapter configuration. A run refuses an adapter configuration with no recorded pass unless started with `--allow-unverified`, and that flag is written into the run header.

## Configuration and the Portugal pack

This section supplies one complete reference configuration, so the coding agent chooses no economic value. Every reference value is an assumption made for this spec and is marked as such in the files.

| File | Holds |
| --- | --- |
| `config/economy.json` | Every parameter in the reference table below |
| `config/parameter-sets.json` | The named sets, as overrides of `economy.json` |
| `config/world.json` | Series processes and event types |
| `config/government.json` | Floors, thresholds, bounds, horizon, signature rule, round limit, meeting triggers |
| `config/mandates/` | Text files: preamble, executive, legislature, judiciary, single |
| `config/baseline.json` | The status-quo script's numbers |
| `config/models.json` | Per branch: adapter, command, arguments, timeout |
| `config/validation.json` | Conversions, periods and error bands for validation |
| `packs/portugal/country.json` | Currency, external central bank, starting policy, first election month |
| `packs/portugal/regions.json` | Per region: population by age band, households, sector weights, wage weights, participation, base unemployment |
| `packs/portugal/snapshot.json` | Starting output, debt, effective rate, and 12 months of opening history |
| `packs/portugal/allocation.json` | How each budget line's spending divides among regions |
| `packs/portugal/history/` | The January 2000 opening state, real series, observed fiscal decisions, dated crisis events |
| `packs/portugal/sources.md` | Where each number came from and when it was retrieved |
| `packs/portugal/ledger.md` | The calibration ledger: assumed, calibrated or emergent |

- Each parameter entry carries a value, a unit, an admissible domain and a provenance: assumed, calibrated, or from data.
- Stage one has no ranges and no random parameter draws. Uncertainty about the economics is handled by named sets.
- **Named sets.** `central` is the reference table. `lowReturns` halves the four exponents on skills, health, infrastructure and farm productivity. `highReturns` multiplies them by 1.5.

**Reference values:**

| Parameter | Reference value |
| --- | --- |
| Capital share | 0.35 |
| Exponents: skills, health, infrastructure, farm productivity | 0.30, 0.15, 0.10, 0.30 |
| Starting loss rate, stability exponent | 0.01, 0.50 |
| Unemployment response, lower and upper bound | 0.50, 0.02, 0.35 |
| Fiscal demand coefficient | 0 |
| External exposure: farming, industry, services, tourism | 0.25, 0.50, 0.10, 0.80 |
| Skills: delay in months, annual depreciation | 144, 0.025 |
| Health | 36, 0.05 |
| Infrastructure | 60, 0.04 |
| Farm productivity | 24, 0.05 |
| State capacity | 36, 0.08 |
| Stability | 1, 0.20 |
| Private capital | 1, 0.06 |
| Leakage: starting share, exponent, ceiling | 0.05, 1.0, 0.50 |
| Collection: starting share, exponent | 0.90, 0.50 |
| Base elasticities: income, corporate, consumption | 0.30, 0.60, 0.20 |
| Tax bounds: income, corporate, consumption | 0.05 to 0.50, 0.05 to 0.45, 0.05 to 0.30 |
| Largest change in a tax rate per year | 0.03 |
| Planned deficit bounds | -0.05 to 0.08 |
| Reallocation allowance base | 0.05 |
| Share of after-tax profit invested | 0.35 |
| Ownership weights, lowest to highest fifth | 0.02, 0.05, 0.10, 0.18, 0.65 |
| Transfer targeting weights | 0.35, 0.25, 0.20, 0.12, 0.08 |
| Spending shares of disposable income | 0.98, 0.95, 0.90, 0.85, 0.70 |
| Tax progressivity weights | 0.2, 0.5, 0.8, 1.1, 1.6 |
| Price basket weights: energy, food | 0.08, 0.15 |
| Borrowing spread: constant, slope, debt threshold, stress coefficient | 0.005, 0.50, 0.90, 0.02 |
| Average debt maturity in months | 84 |
| Debt floor: interest share of revenue, rate ceiling | 0.20, 0.12 |
| Region floor: income share, household share | 0.80, 0.85 |
| End state: stock threshold, pipeline share | 0.98, 0.95 |
| Poverty line, as a share of the starting median | 0.60 |
| Migration: internal coefficient, external coefficient, yearly cap | 0.02, 0.01, 0.01 |

- Monthly depreciation is one minus (one minus the annual rate) to the power 1/12.
- **From the pack, not assumed:** sector weights, wage weights, participation, base unemployment, demographic rates, households per person, the starting policy, debt, and allocation weights.
- **Derived at start-up, never configured:** each region's output constant, each stock's conversion rate, and the pipeline preloads.

**Pack rules:**

- **Regions.** The nine NUTS II regions, the level at which INE publishes regional accounts. Check that back series to 2000 exist in the 2024 geography. Where they do not, document the mapping used.
- **Splitting.** An additive total may be split among regions by a documented weight. A rate or an index keeps its own unit and is never split.
- **Reconciliation.** Regional figures must sum to the national figure within a stated tolerance, checked when the pack loads.
- **Allocation weights** default to population shares and are published in the report, so the legislature can see where each line's money lands.

## Run files, resume and commands

A run folder holds everything needed to replay the run, including the world it faced, without the simulation, the generator or any model.

| File | Contents |
| --- | --- |
| `header.json` | Format and engine versions, runtime, full configuration, a hash of every input file, seed, parameter set, occupant, adapter configuration hashes, and the label validated or unvalidated |
| `feed.jsonl` | The realised world feed, one row per month |
| `months.jsonl` | One line per month: national and regional values, cells, stocks, accounts summary, floor values |
| `decisions.jsonl` | One line per meeting: kind, the report given, every revision, replies, signatures, blocks with their forecasts, the outcome, any carry-over |
| `transcripts.jsonl` | Every prompt and reply, for AI occupants |
| `checkpoint.json` | The full state: stocks, pipelines, accounts, enacted policy, allowances, floor counters, occupant history, and the committed byte offset of each log |
| `summary.json` | Run status, both scores, companion measures |

**Commit and resume:**

- At the end of each month the log lines are appended and flushed, then the checkpoint is written to a temporary file and renamed into place with the new offsets.
- Resume truncates each log to its recorded offset and continues from the checkpoint. No month can be written twice.
- A model reply already flushed to the transcript for the pending meeting is reused on resume and not requested again.

**Commands:**

| Command | Does |
| --- | --- |
| `eunomia run` | One run, given a pack, an occupant, a feed or seed, a parameter set and a horizon |
| `eunomia pair` | Given a list of seeds, a list of parameter sets, two occupants and a number of repeats, runs every combination for both occupants and writes a manifest |
| `eunomia compare <manifest>` | One outcome row per requested pair, as CSV |
| `eunomia report <manifest>` | Counts of outcomes by parameter set, the spread of score differences, ranking reversals between parameter sets, and variation between repeats of one seed, as CSV |
| `eunomia validate` | The tuning stage or the hold-out stage of validation |
| `eunomia check-adapter` | The adapter check |
| `eunomia resume <run>` | Continue from the last checkpoint |
| `eunomia feed` | Write a generated feed alone |

**Pair manifest.** `pairs.json` lists every requested pair with its status: complete, failed or missing. A pair is never dropped. A failed or missing partner produces the outcome incomplete.

**Compatibility key.** Two runs may be compared only if they share a key: the hash of the pack snapshot, the economic configuration with its parameter set, the feed file, the engine version and the horizon. Government settings are recorded separately and are not part of the key, since they are what the pair is meant to differ in.

## Experiment design and validation

Every result is a comparison of whole governments inside Eunomia, under a named parameter set. Reports state it in those words.

**What each comparison can show:**

| Comparison | Shows | Does not show |
| --- | --- | --- |
| Three branches against status quo | Whether this AI government, as configured, did better than this script | The separate contribution of the branch structure, of competence, or of honesty |
| Three branches on one underlying model against single AI on the same model with three calls | Whether the branch structure adds anything | Anything about different models |
| Any occupant against frozen | A sanity check |  |

**What cancels and what does not.** World shocks are identical in both runs of a pair, so they cancel. Assumptions about the economy do not: a government that spends more on education gains more when the return on skills is set high. Results are therefore run under all three parameter sets, and a ranking that reverses between sets is reported as a reversal.

**First experiment:**

| Occupants | Runs |
| --- | --- |
| Frozen, status quo | 10 feeds under each of the 3 parameter sets |
| Single AI, three branches | 10 feeds under the central set, 3 of those feeds under the low and high sets, and 2 further repeats of 2 preselected feeds under the central set |

- Seeds are chosen before any run and written into the manifest.
- The report separates three kinds of variation: between worlds, between parameter sets, and between repeats of the same seed.
- Model and adapter versions, settings, retries and technical failures are recorded for every AI run.

**Validation.** Until it is complete, every summary and report carries the label unvalidated.

1. **Opening state.** The pack supplies a dated state for January 2000: population, debt, tax rates, budget, and pipelines preloaded from the 2000 budget.
2. **Conversions,** fixed in `config/validation.json`: annual flows are divided equally over months, stocks and rates are interpolated linearly, and money is in constant prices of the base year using the GDP deflator.
3. **Fiscal demand effect.** The fiscal coefficient is switched on and tested on three time paths: a rise in purchases, a rise in transfers, and a budget-neutral reallocation.
4. **Two passes.** First the `Replay` occupant, which enacts the real budgets, to judge the economic model on its own. Then `StatusQuo`, to judge the script by comparing its decisions with the real ones.
5. **Tuning period, 2000 to 2012.** Parameters are tuned on output growth and the deficit. The error of every series is recorded: in points for rates and ratios, in percent for levels.
6. **Bands.** For each series the hold-out band is twice its own tuning-period error. The bands are written to `config/validation.json` and its hash is recorded.
7. **Hold-out period, 2013 to 2025.** `eunomia validate` refuses to run this stage until the hash exists. Series checked: output growth, the deficit, unemployment, real household disposable income, regional population shares, and the debt ratio. The debt ratio is reported but is not an independent check, since output and the deficit were tuned.

## Acceptance tests

Stage one is done when all of these pass. Each names a fixture or a hand-computed example, so that a test cannot be made to pass by loosening it.

| Test | Passes when |
| --- | --- |
| Determinism | The same inputs with a scripted occupant give byte-identical run folders on the pinned runtime and platform |
| Reconciliations | The ledger and every identity hold in all 240 months, on the fixture pack and on Portugal |
| Single transfer | One known transfer is traced end to end: paid, leaked, delivered, received once, counted once in disposable income |
| Steady state | Frozen government, flat feed, stationary demography fixture: every stock, output and population stays within 1e-9 relative over 240 months |
| Pipeline preload | Under the starting budget the skills index stays at 1 through the first 144 months |
| Stock order | A hand-computed three-month example matches the stock equation |
| Envelope | A hand-computed January envelope and an April revision, including an overshoot, match |
| Debt units | Debt equal to one year of output reads as a ratio of 1.0, and monthly interest matches the annual rate conversion |
| Proposal validity | Negative, missing, extra and non-finite shares, and out-of-bound taxes, are rejected before any posting |
| Allowance | Two decisions in one year draw on one allowance, a reversal draws on it too, and an excess is scaled down |
| Carry-over | A failed January meeting and a failed in-year meeting behave as specified and are labelled |
| Meetings | Coinciding triggers give one meeting of the right kind, and two occupants on one feed get the same meeting dates |
| Direction | Education spending raises skills after its delay and not before. A higher tax rate shrinks its base. Agriculture spending raises farm output. Less security spending raises the loss rate |
| Floors | A fixture breaches each enabled floor in the expected month. The hunger fixture exercises the counter and its reset. A disabled floor reports as disabled |
| End state | A late cut to education and a late borrowing spree each breach their end-state check |
| Median | A hand-computed three-region example, with a tie and an empty cell, matches |
| Winner rule | All five outcomes are produced by fixtures |
| Protocol | Every row of the transition table, under both signature rules, with scripted models. Votes on a stale revision are rejected. Retries add no rounds |
| Block form | A block missing its floor, date or path is recorded as a dissent and stops nothing |
| Feed | The tick draws no random numbers. A five-month event applies its factor as a level for five months. Overlapping events multiply |
| Pairing | The manifest lists every requested pair, mismatched keys are refused, and a failed partner gives an incomplete row |
| Resume | A run killed in a quiet quarter, and one killed just after a model reply, both finish identical to an uninterrupted run, with no duplicated month |
| Replay | The run folder alone reproduces every report the government saw |
| Adapter check | It passes a sealed fake adapter and fails a deliberately leaky one |
| Validation gate | The hold-out stage refuses to run before the bands are frozen |
| No engine | The core project references nothing beyond the base class library |
| Speed | One scripted 240-month Portugal run finishes in under 2 seconds, and 200 runs in under 5 minutes on 8 cores, on a reference machine recorded in the repository. The target is not loosened without a written reason |

## Work order

Twelve steps. The second one compares two policies end to end, and model calls and validation come after the scripted experiment works.

1. Solution skeleton, random generator, serialization rules, configuration loading, fixtures folder.
2. **Thin end-to-end slice.** The one-shock fixture feed, a three-region fixture pack, the ledger and reconciliations, a minimal economy (output, wages, one tax, transfers), debt and its floor, the frozen occupant and one scripted test occupant, the run writer, and `run`, `pair` and `compare` to CSV.
3. Budget arithmetic: envelope, revisions, allowances, carry-over, meetings.
4. The full economy model: sectors, stocks with preloaded pipelines, the cash sequence, population. Steady-state and direction tests.
5. Scoring, floors, end-state checks, the winner rule.
6. World generator and events.
7. Report, priorities rule, budget office, and the status-quo script.
8. Three-branch protocol on scripted models, with the transition-table tests.
9. Portugal pack with nine regions and a current snapshot. First scripted experiment on the full grid, labelled unvalidated.
10. One CLI adapter, the adapter check, checkpoints and resume. First AI experiment, labelled unvalidated.
11. Validation: the 2000 opening state, replay, the fiscal demand effect, tuning, frozen bands, hold-out.
12. The experiment again, as validated.

Deferred beyond stage one: HTML charts, further adapters, a local model, election priority changes, wildfire events, random parameter draws, partner-level demand, and region data finer than NUTS II.

## Changes from version 1

An external review of version 1 by a different model returned 33 findings. All were accepted, five with a different fix from the one proposed. This table shows where each is resolved.

| Finding | Resolved in |
| --- | --- |
| 1. A signed budget did not fix the euros spent | Budget arithmetic: envelope and revision rules |
| 2. No reproducible rule for who won | Scoring and floors: income measure, scores, comparison table |
| 3. Branches could approve one budget and enact another | Three-branch protocol: revision ids, feasible form before voting, judiciary last |
| 4. Transfers paid twice, leakage with no destination | Monthly tick: step 7 alone posts transfers, diverted account, reconciliations |
| 5. Stock equation and tick disagreed, empty pipelines | Economy model: stocks, preloaded pipelines |
| 6. Agriculture and security had no effect, no sector split | Economy model: output by sector, loss rate |
| 7. A failed historical model could pass validation | Experiment design and validation: 2000 opening state, frozen bands, gated hold-out |
| 8. Negative budget shares were valid | Budget arithmetic: validity of a proposal |
| 9. Debt pricing could be wrong by a factor of twelve | Economy model: debt, on trailing annual output |
| 10. The judiciary could veto by naming a floor | Government contract: budget office. Protocol: block form and forecast log |
| 11. Incomplete decision protocol | Three-branch protocol: reply records and transition table. Government contract: `Check` |
| 12. Deadlock could restore a preferred budget | Budget arithmetic: carry-over |
| 13. Empty pipelines and late borrowing at the end | Scoring and floors: end-state checks |
| 14. Status-quo script left to the agent | Occupants: script table, frozen, replay |
| 15. Legislature could not see the spending distribution | Pack allocation weights, shown by the budget office |
| 16. Fiscal demand effect could double count | Economy model: coefficient at zero. Validation: defined and tested |
| 17. Capacity limit could vanish or be bypassed | Budget arithmetic: reallocation allowance, tax change limit |
| 18. The median could rise through emigration | Scoring and floors: interpolated median, companion measures |
| 19. Tax revenue with no one bearing the cost | Economy model: cash sequence. State capacity has no direct effect on output |
| 20. Employment, population and capital inconsistent | Economy model: potential labour, unemployment, population order |
| 21. World indicators with no route into the accounts | Economy model: demand multiplier, household prices, net trade, list of what is not modelled |
| 22. Feed and tick both generated the world | World feed: rows are authoritative |
| 23. Meeting dates and information undefined | Budget arithmetic: meetings. Government contract: report, priorities rule |
| 24. The agent had to choose the defaults | Configuration: reference values. World feed: reference values |
| 25. Common assumptions do not cancel in a pair | Experiment design: parameter sets and reversals |
| 26. Batch commands gave no paired sample | Run files and commands: `pair`, manifest, compatibility key |
| 27. Comparisons claimed more than they show | Experiment design: what each comparison can show |
| 28. No repeats on the same seed | Experiment design: first experiment |
| 29. Isolation test proved too little | Model access and isolation: adapter check |
| 30. Resume and replay could diverge from the run | Run files: commit and resume, saved feed |
| 31. Twenty regions were a data problem | Data model and pack rules: nine NUTS II regions |
| 32. The cheapest end-to-end test came late | Work order: thin slice in step 2 |
| 33. Engineering criteria had no fixed meaning | Ground rules. Acceptance tests |

The five changed fixes: the fiscal coefficient returns before validation and is not left at zero (16). Regions are the nine NUTS II regions, not three to five (31). The median interpolates between cells (18). The budget office is deliberately simple so it cannot reveal the model (10). Citizens' priorities are produced by a rule, since nothing else supplied them (23).

One addition came from neither version 1 nor the review: trend productivity growth, so that a government that changes nothing presides over slow growth and not stagnation.
