# Fare Engine

Use a dedicated policy-driven fare engine.

```text
FareCalculationRequest
    ↓
RouteSegmentResolver
    ↓
ApplicableFareRuleSelector
    ↓
FareCalculator
    ↓
FareCalculationResult
```

Fare rules should be versioned with effective dates.

Potential inputs:

- Origin
- Destination
- Passenger category
- Service type
- Travel date
- Fare rule version

Do not hard-code current fare values in application code.

## Phase 1 fare (provisional tech-lead ruling)

**Provisional tech-lead ruling (hein, 2026-09-28; T-062) — not a Myanma Railways answer; still open with Myanma Railways** (`docs/19-open-questions.md` OQ9, OQ17, OQ21):

- Phase 1 has one flat fare of 800 MMK per journey, for every passenger and every origin → destination.
- The fare is held as data (a fare table), in whole kyat. No fare amount appears in code.
- Distance bands and an AC-coach fare may be added later, as fare-table data, without a code change. Passenger categories stay open (OQ9); how journey length is measured on the loop, and whether a passenger may choose the direction, stay open (OQ17).
