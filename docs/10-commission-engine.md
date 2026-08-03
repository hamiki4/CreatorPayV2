# Commission Engine

## Resolution and versioning

At transaction time choose the first active, date-effective, scope-matching rule:

1. campaign-specific creator rule;
2. merchant-creator partnership rule;
3. merchant default rule;
4. platform default rule.

A rule defines commission basis (MVP: percentage of eligible purchase amount), commission rate, creator share rate and platform share rate, currency, effective UTC interval, version and author. Splits must total 100% of commission; none is hard-coded. Published rule versions are immutable; corrections create later versions with non-overlapping effective windows. Campaign/location/category bounds must match.

Calculate using decimal arithmetic: `gross commission = round(purchase × rate, 2)`, then `creator = round(gross × creator share, 2)` and `platform = gross − creator`, ensuring exact conservation. ETB is stored at two decimal places unless payment/legal requirements decide otherwise. Negative/zero, excessive amount/rate and unsupported currency fail validation.

| Example | Resolution | Result |
|---|---|---|
| 2,000 ETB, merchant 10%, creator share 60% | merchant default | commission 200.00; creator 120.00; platform 80.00 ETB |
| 750 ETB, campaign 12%, creator share 65% | campaign overrides 8% partnership | commission 90.00; creator 58.50; platform 31.50 ETB |
| 99.99 ETB, 7.5%, creator share 55% | platform default | commission 7.50; creator 4.13; platform 3.37 ETB |

Each transaction snapshots rule ID/version/source, all rates, basis, rounding mode, input, gross commission, both shares and currency. Refunds/reversals reference that snapshot rather than current configuration. Rule publication, retirement and override assignment are audited and concurrency controlled.
