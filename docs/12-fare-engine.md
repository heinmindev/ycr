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
