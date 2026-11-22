# Eunomia — Sectors and Levers

Nov 22, 2026 · Design for discussion. Nothing here is built yet.

## Why

Version one governs by allocating funds: each year the government sets three tax rates, a deficit and seven budget shares, and money fills national stocks. A euro for education does the same work whether schools are well or badly run, open to everyone or to a few, free or paid for. "Competent government" therefore means only "competent budgeting".

This document gives sectors depth, so the government decides how things are run and not only how much they get. It joins four threads that were planned separately: management quality (build plan), delegation (design notes), regions, and EU funds. They all follow one pattern, the sector template.

## Principles

1. **The state sets opportunities; households decide.** The state controls cost, quality and access. How much households want schooling or care responds to those, but it is theirs: it differs by region and income group, and the model takes it as given. The model never credits the state with what families achieved, nor blames it for what families chose.
2. **Distribution is geography and income.** Results are tracked by income fifth now, and by region and income fifth once regions exist. Access in Portugal has always depended as much on where people lived as on what they earned.
3. **The model shows consequences; it does not decide values.** Whether education is a right is a constitutional choice made by the player before a run, not a conclusion of the model.
4. **Every mechanism is an assumption with a range.** Each one is recorded in the calibration ledger as assumed, calibrated or from data, and the named parameter sets span its plausible strengths.
5. **Finding the best system and judging the AI are separate.** What works best inside the model is found by systematic search. Whether an AI can govern is tested by putting it in the seat without the answer.
6. **Depth follows need.** A mechanism enters when a run or a known gap calls for it, and only if it changes a decision someone could take.

## Three kinds of choice

| Kind | Examples | Who decides | When |
| --- | --- | --- | --- |
| **Constitution** | Compulsory schooling age; free basic education; a right to basic care | The player, before the run | Never during a run |
| **Regimes** | Public, mixed or private provision; university fees; means-testing; earmarked taxes | The government | Rarely |
| **Yearly levers** | Tax rates, deficit, budget shares, each sector's management share | The government | Every budget |

Regime changes are deliberately slow and costly:

- A change is decided in a budget meeting like any other decision, and is recorded with its reasons.
- It uses up part of the yearly reallocation allowance, so it competes with budget moves for the state's limited capacity to change.
- It takes effect over a transition of several years.
- Reversing it costs the same again.

**Commitments are regimes that bind future budgets.** An earmarked education tax is, economically, a tax plus spending: money is interchangeable, so the earmark alone changes nothing. What it does change is the future. It protects slow investments from short-term cuts, which is exactly the failure the first AI runs showed when they ran down administration in their final years. An earmark is modelled as that: a floor on the line's share for a number of years, costly to repeal.

## The sector template

```
money → management and absorption → coverage and quality → results by group
                                         ↑
                         household demand (cost, quality, access, the group's own propensity)
```

Every sector has the same five parts.

| Part | What it does |
| --- | --- |
| **Budget line** | The sector's share of the envelope, as now |
| **Management quality** | An index, 1 at the start. Effective spending is money delivered times management quality. It rises when part of the line (the management share, a yearly lever) goes to running the sector, and drains when that is neglected. It has diminishing returns and a ceiling |
| **Absorption limit** | When the sector's real spending grows faster than its management can handle in a year, most of the extra is wasted. Better management raises the limit. This also ends the stock-size artefact, where a tiny line could be tripled for outsized returns |
| **Coverage and quality** | The sector's outputs: how many people it reaches, and how much it does for each |
| **Regimes** | Three or four structural choices per sector, each with a small, stated mechanism |

Results reach households by group: each sector changes something about each income fifth (skills, health, income), not one national number.

## Education, worked through

### What is tracked

| Quantity | Meaning |
| --- | --- |
| **Coverage** by level and group | Share of each new generation in each income fifth that completes basic, secondary and higher education |
| **Quality** by level | How much a year at that level teaches, as an index |
| **Skills** by group | The average skills of each fifth's workforce |

This separates what one index merged. Salazar-era Portugal had low coverage (widespread illiteracy, a deep gap between Lisbon, Porto or Coimbra and the interior) alongside high quality for those who stayed. A mass system can reach everyone at a lower top standard. The model can now represent both, and every mix in between.

### Generations

Each year a new generation enters the workforce with the skills its schools gave it, and the oldest generation retires: about one fortieth of each group's workforce is replaced every year. Skills therefore change slowly, cohort by cohort, which is what makes the 40-year horizon meaningful. A school reform reaches the median worker only as its pupils become a large share of the workforce.

### Effects

- **Output** uses the average skills of the whole workforce, as the skills index does now.
- **Wages by group** depend on each fifth's own skills relative to the others. This is the main new effect: education changes inequality, not only total output.

### What drives coverage (household demand)

Coverage for each level and group rises with:

- the compulsory schooling rule, which sets a floor for basic and secondary levels;
- a lower net cost to families (fees minus grants);
- higher quality, which raises what schooling is worth;
- access: once regions exist, distance to school;
- the group's own propensity, taken from data and never treated as the state's achievement;
- **the parents' schooling:** each group's propensity rises with its own past skills, because children of schooled parents tend to stay in school longer. This gives the 40-year horizon its own momentum: a generation schooled today raises the next one's demand for schooling. It also amplifies every other assumption, so its strength is spanned by the named sets.

Places are not modelled separately: the absorption limit already stops a school system from growing faster than it can be run.

### What drives quality

Effective spending per pupil (with diminishing returns), the sector's management quality, and the standards regime below.

### Regimes

| Regime | Options | Mechanism |
| --- | --- | --- |
| **Provision** | Public; mixed (public funding, private schools allowed, with vouchers); private with grants | Two separate effects. **Efficiency** (learning per euro against public schools) spans 0.85 to 1.15 across the named sets, central 1.0, because the evidence is mixed. **Sorting**: in mixed and private systems families top up and schools select pupils, which changes who learns more than how much in total |
| **University fees** | Free; flat fee; fee scaled by income | Fees raise revenue and lower coverage, most for groups most sensitive to price. Income-scaled fees need means-testing |
| **Means-testing** | Universal support; support tested by income | Testing aims money at lower fifths, but costs administration and loses those who never apply. Take-up rises with state capacity |
| **Standards** | Comprehensive; selective | Selective systems teach more to those who stay, and keep fewer of the groups with lower propensity. Comprehensive systems do the reverse |
| **Earmark** | None; an education tax scaled by income and earmarked | A commitment: education's share cannot fall below its level at enactment, minus a small margin, for a set number of years. Repeal is costly |

### Constitution options for the player

- Compulsory schooling age: none, 15, or 18 (Portugal's rule since 2009).
- Free basic education: yes or no.
- A right to education, enforced as a floor: coverage of basic education stays above a threshold.

### Data for Portugal

Literacy and attainment from the censuses, early school leaving from Eurostat (it fell from over 40% around 2000 to under 10% in recent years), attainment by age group (which shows the generations), international test scores from 2000, and regional differences from INE. A good validation test: fed real history, the model should roughly reproduce Portugal's rise in attainment since 2000.

## Health, in brief

The same template. Coverage is the share of needs met in time; quality is what each treatment achieves. Regimes: co-payments (none or scaled by income), provision (public, or mixed with contracted private care), and waiting-time guarantees. Health by group feeds each fifth's participation in work and its productivity. With regions, the health regions become units with their own management quality.

## Other sectors

Infrastructure, agriculture, security and administration get management quality and the absorption limit first. Regimes come later, and only if a run shows they matter. Justice as its own line, the "justice system", is proposed for after the absorption limit exists.

## Finding the best system, and judging the AI

The AI can only discover what the simulation encodes. If the model says public schooling works best, an AI that "finds" it has found our assumption. So the two questions are kept apart:

- **What is best inside the model** is found by systematic search: every combination of regimes, run with a scripted yearly policy across many worlds and all named sets. This is the oracle from the design notes, and it needs no model calls.
- **Whether an AI can govern** is tested by putting it in the seat without the answer. Its regime choices and results are compared with the search, and its reasoning is read.

Reports state results as "best in the model under these assumptions". A regime that wins under every named set is a finding. A winner that flips between sets is also a finding: it shows what the answer depends on.

The AI's real advantage is context. Language models know the research on education and health finance. That knowledge only helps if the model is close enough to reality for it to apply, which is what validation is for.

## How delegation fits

Sector depth is what gives ministers something to do. A minister's levers are the sector's regimes, its management share and, with regions, the split between regions. The executive keeps the envelopes, taxes and deficit. Without this document's levers, tier 2 of the delegation plan would be theatre.

## Order of work

1. **Template core for every line:** management quality and the absorption limit, at national level. Rerun the returns table; the stock-size artefact should disappear. *Done Nov 22, 2026: the artefact is gone (build plan, seventh look).*
2. **Education outputs:** skills by income fifth, generations, wages by group, coverage and quality.
3. **Education regimes and constitution options,** the policy-resistance switch, then the regime search (no model calls).
4. **Health** on the same template.
5. **Regions,** with the map: distribution by region and fifth, and the health regions.
6. **EU funds,** and the Ministry of Economy's levers.
7. **AI ministers.**

Each step ends with tests, a rerun of the returns table and the pairs, and findings recorded in the build plan.

## Decided

- **Intergenerational transmission: yes.** Built into what drives coverage (Nov 22, 2026).
- **School places: the absorption limit is enough.** No separate model of places (Nov 22, 2026).
- **Private provision: two effects, not one.** Efficiency (learning per euro against public) spans 0.85 to 1.15 across the named sets with central 1.0, so no set leans either way by default; sorting (top-ups and pupil selection) is modelled separately and mainly shifts who learns. The range is an assumption, recorded as such (Nov 22, 2026).
- **Resistance is to policies, never to the AI.** Whether people would accept an AI government is outside the question. But losers from a change push back whoever governs, and the status-quo script already behaves as if constrained by that, so leaving it out would hand the AI a free advantage. Resistance therefore applies identically to every occupant: a regime change or cut that hits a group hard and fast triggers strikes or protest, costing output in that sector for a while. It sits behind a switch, off by default, so results can be compared with and without friction (Nov 22, 2026).

No open questions remain in this document.

