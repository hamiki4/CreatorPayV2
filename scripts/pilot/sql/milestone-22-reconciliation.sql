-- Milestone 22 read-only validation. Execute with a database role that has SELECT only.
-- Full transaction/journal trace
SELECT p."PublicTransactionId",p."TransactionDateUtc",p."PurchaseAmount",s."TotalCommissionAmount",
       s."CreatorCommissionAmount",s."CustomerCashbackAmount",s."PlatformCommissionAmount",
       j."Reference",l."Account",l."Type",l."Amount"
FROM "PurchaseTransactions" p JOIN "CommissionCalculationSnapshots" s ON s."Id"=p."CommissionCalculationSnapshotId"
JOIN "FinancialJournals" j ON j."RelatedTransactionId"=p."Id" JOIN "FinancialJournalLines" l ON l."FinancialJournalId"=j."Id"
ORDER BY p."TransactionDateUtc" DESC,j."Reference",l."Account";

-- Reconciliations: each query should return zero rows.
SELECT w."MerchantId",w."AvailableBalance",COALESCE(SUM(CASE WHEN e."EntryType" IN (0,2,3,5,7) THEN e."Amount" ELSE -e."Amount" END),0) derived
FROM "MerchantWallets" w LEFT JOIN "MerchantWalletEntries" e ON e."MerchantWalletId"=w."Id" GROUP BY 1,2
HAVING w."AvailableBalance"<>COALESCE(SUM(CASE WHEN e."EntryType" IN (0,2,3,5,7) THEN e."Amount" ELSE -e."Amount" END),0);
SELECT j."Id",j."Reference",SUM(CASE WHEN l."Type"=0 THEN l."Amount" ELSE -l."Amount" END) imbalance FROM "FinancialJournals" j JOIN "FinancialJournalLines" l ON l."FinancialJournalId"=j."Id" GROUP BY 1,2 HAVING SUM(CASE WHEN l."Type"=0 THEN l."Amount" ELSE -l."Amount" END)<>0;
SELECT "CheckoutSessionId",count(*) FROM "PurchaseTransactions" WHERE "CheckoutSessionId" IS NOT NULL GROUP BY 1 HAVING count(*)>1;
SELECT "Id","PublicCheckoutId","Status","ExpiresAtUtc" FROM "CheckoutSessions" WHERE "ExpiresAtUtc"<now() AND "Status" IN (0,1,2);
SELECT "Id","CampaignCode","Status","ExpiresAtUtc" FROM "CreatorMerchantCampaigns" WHERE "ExpiresAtUtc"<now() AND "Status" IN (2,3);

-- Payout and recovery queues (no protected contact data).
SELECT 'creator' kind,"CreatorId" owner,"CurrencyCode",SUM("Amount") amount FROM "CreatorPayouts" WHERE "Status" IN (1,2,3) GROUP BY 1,2,3
UNION ALL SELECT 'customer',"CustomerId","CurrencyCode",SUM("Amount") FROM "CustomerPayoutRequests" WHERE "Status" IN (0,1) GROUP BY 1,2,3;
SELECT 'creator' kind,"CreatorId" owner,"CurrencyCode",SUM("OutstandingAmount") outstanding FROM "CreatorRecoveryBalances" WHERE "OutstandingAmount">0 GROUP BY 1,2,3
UNION ALL SELECT 'customer',"CustomerId","CurrencyCode",SUM("OutstandingAmount") FROM "CustomerRecoveryBalances" WHERE "OutstandingAmount">0 GROUP BY 1,2,3;

-- Daily operational summary.
SELECT date_trunc('day',p."TransactionDateUtc") day,count(*) purchases,sum(p."PurchaseAmount") volume,
       sum(s."CreatorCommissionAmount") creator,sum(s."CustomerCashbackAmount") cashback,sum(s."PlatformCommissionAmount") platform
FROM "PurchaseTransactions" p JOIN "CommissionCalculationSnapshots" s ON s."Id"=p."CommissionCalculationSnapshotId" GROUP BY 1 ORDER BY 1 DESC;
