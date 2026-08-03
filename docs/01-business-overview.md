# Business Overview

CreatorPay connects creators with participating merchants while ensuring that every commission is authorized by both the platform and the merchant.

## Core promotion flow

1. A creator registers with CreatorPay.
2. a Platform Admin reviews and approves the creator.
3. The approved creator contacts or searches for a participating merchant.
4. The creator requests permission to promote that merchant.
5. A merchant manager approves or rejects the creator.
6. Approval changes the `MerchantCreatorPartnership` to `Approved`; this partnership is required for promotion.
7. Only an approved creator with a transaction-eligible `Approved` partnership can generate commission transactions for that merchant.
8. Future cashier QR validation must verify the creator's platform approval and active merchant partnership.
9. Each merchant's commission rate and the creator/platform split will be configurable.
10. Creator earnings will be recorded immediately and paid weekly.

No business workflow is implemented in Foundation Milestone 1.
