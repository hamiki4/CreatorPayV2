# Pilot go-live checklist

- [ ] Isolated Pilot environment, generated secrets, least-privilege read-only reporting role
- [ ] Seed/reset idempotency and all ten role accounts verified
- [ ] Scenarios A–H pass with correlation IDs and balanced journals
- [ ] Desktop/mobile, keyboard, screen-reader, contrast, English/Amharic review signed off
- [ ] Normal and Slow-3G targets met; SignalR reconnect/polling and offline refusal verified
- [ ] Pilot metrics reviewed; duplicates, posting failures and imbalances are zero
- [ ] Authorization/security gates pass with no open Critical/High findings
- [ ] Manual deposit, creator payout, customer payout and recovery evidence reconciles
- [ ] Backup/restore and immutable rollback smoke tests pass
- [ ] CI, containers and final validation commands pass
- [ ] Support owner/on-call/escalation contacts confirmed
- [ ] No gateway, bank, Telebirr, CBE Birr or automatic transfer integration exists

The pilot lead and operations owner record a dated go/no-go decision. An unchecked item means no-go.
