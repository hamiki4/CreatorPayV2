-- CreatorPay pilot read-only operations pack (PostgreSQL). Run with a read-only role.
-- Wallet reconciliation: stored balance versus immutable entries.
SELECT w."MerchantId", w."AvailableBalance" AS stored,
       COALESCE(SUM(CASE WHEN e."EntryType" IN (0,3,5) THEN e."Amount" ELSE -e."Amount" END),0) AS derived
FROM "MerchantWallets" w LEFT JOIN "MerchantWalletEntries" e ON e."MerchantWalletId"=w."Id"
GROUP BY w."MerchantId",w."AvailableBalance" HAVING w."AvailableBalance"<>COALESCE(SUM(CASE WHEN e."EntryType" IN (0,3,5) THEN e."Amount" ELSE -e."Amount" END),0);

-- Creator unpaid earnings.
SELECT "CreatorId","CurrencyCode",SUM("Amount") amount FROM "CreatorEarnings" WHERE "Status" IN (0,1) GROUP BY 1,2 ORDER BY amount DESC;
-- Customer available cashback (phone/name intentionally excluded).
SELECT "CustomerId","CurrencyCode","AvailableCashback","ReservedCashback","RecoveryBalance" FROM "CustomerWallets" WHERE "AvailableCashback">0 ORDER BY "AvailableCashback" DESC;
-- Platform revenue by UTC day.
SELECT date_trunc('day',"CreatedAtUtc") period,"CurrencyCode",SUM("Amount") revenue FROM "PlatformRevenueEntries" GROUP BY 1,2 ORDER BY 1 DESC;
-- Trial-credit usage and exhaustion.
SELECT "MerchantId","Status","ConfirmedTransactionCount","MaximumTransactions","TotalCommissionFunded","MaximumCommission" FROM "MerchantTrialCredits" ORDER BY "Status" DESC,"TotalCommissionFunded" DESC;
-- Manual payout queues.
SELECT 'creator' queue,"Id","Status","CreatedAtUtc" queued_at FROM "CreatorPayouts" WHERE "Status" IN (0,1,2)
UNION ALL SELECT 'customer',"Id","Status","RequestedAtUtc" FROM "CustomerPayoutRequests" WHERE "Status" IN (0,1) ORDER BY queued_at;
-- Posted journal imbalance detection (must return zero rows).
SELECT j."Id",j."Reference",SUM(CASE WHEN l."Type"=0 THEN l."Amount" ELSE -l."Amount" END) imbalance
FROM "FinancialJournals" j JOIN "FinancialJournalLines" l ON l."FinancialJournalId"=j."Id" GROUP BY j."Id",j."Reference"
HAVING SUM(CASE WHEN l."Type"=0 THEN l."Amount" ELSE -l."Amount" END)<>0;
-- Expired sessions still awaiting cleanup.
SELECT "Id","PublicCheckoutId","Status","ExpiresAtUtc" FROM "CheckoutSessions" WHERE "ExpiresAtUtc"<now() AND "Status" IN (0,1);
-- Campaigns expiring in the next seven days.
SELECT "Id","CampaignCode","MerchantId","CreatorId","ExpiresAtUtc" FROM "CreatorMerchantCampaigns" WHERE "Status"=2 AND "ExpiresAtUtc" BETWEEN now() AND now()+interval '7 days' ORDER BY "ExpiresAtUtc";

