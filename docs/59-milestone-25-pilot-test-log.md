# Milestone 25 pilot test log

## Duplicate Welcome Heading

| Field | Value |
|---|---|
| Expected | The page displays only one welcome heading. |
| Actual | Only one welcome heading is displayed in both English and Amharic. |
| Result | PASS |
| Status | Fixed |

Manually verified during the Milestone 25 pilot test.

## Registration usability findings

| Test | Expected | Actual | Result | Status |
|---|---|---|---|---|
| Registration failure feedback | A failed registration displays the specific validation or server reason. | Safe, specific validation and conflict messages are displayed without a Support ID; unexpected failures retain Support ID behavior. | PASS | Fixed |
| Registration tab state leakage | Switching account types clears the previous form state and message. | The failure message and entered values remain visible when switching between registration tabs. | FAIL | Open |
| Registration form complexity | Public registration collects only information required to create and review the account. | Content Creator registration requests several fields that are unnecessary at initial signup. | FAIL | Open |
| Password confirmation | Every public registration form requires password confirmation. | All public registration forms require matching password confirmation before submission. | PASS | Fixed |
| Public role terminology | Public-facing role names are easy for users to understand. | The UI uses Customer, Creator, and Merchant. | FAIL | Open |

## Remaining registration findings

| Test | Result | Status |
|---|---|---|
| Bilingual Language Label | PASS | Fixed |
| Shopper Registration | PASS | Fixed |
| Content Creator Registration | PASS | Fixed |
| Business Owner Registration | PASS | Fixed |
| Specific Registration Error Feedback | PASS | Fixed |

## Consistent Password Requirements

| Field | Value |
|---|---|
| Expected | All public registration forms display and enforce the same live password rules. |
| Actual | All three public registration forms display the live checklist and consistently enforce the complete password rules. |
| Result | PASS |
| Status | Fixed |

Manually verified during the Milestone 25 pilot test.

## Ethiopian Mobile Number Validation and Normalization

| Field | Value |
|---|---|
| Expected | All public registration roles accept valid Ethiopian 09, 07, +2519, and +2517 formats, normalize them to E.164, and reject invalid formats with a safe message. |
| Actual | All three public registration roles accept the supported Ethiopian formats, normalize local numbers to E.164, reject invalid formats, and detect equivalent duplicate numbers. |
| Result | PASS |
| Status | Fixed |

Manually verified results:

- `0911234567` accepted.
- `0712345678` accepted.
- `+251911234567` accepted.
- `+251711234567` accepted.
- Invalid prefixes rejected.
- Too-short and too-long numbers rejected.
- Local numbers normalized to E.164.
- `0911234567` and `+251911234567` treated as the same number.
- Duplicate phone returns HTTP 409.
- HTTP 400 and 409 responses do not display a Support ID.
- Shopper, Content Creator, and Business Owner registrations all succeed.
- Successful registrations do not display a Support ID.
