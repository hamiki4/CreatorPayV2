import { useEffect, useState } from "react";
import { api, statusLabel } from "./apiClient";
import { formatAmount, formatDate, formatDateTime } from "./displayFormat";
const money = (n: number, _currency?: string) => formatAmount(n);
const Badge = ({ value }: { value: string }) => (
  <span className={`badge status-${value.toLowerCase()}`}>
    {statusLabel(value)}
  </span>
);
type Summary = {
  currencyCode: string;
  availableBalance: number;
  pendingBalance: number;
  scheduledBalance: number;
};
type Earning = {
  id: string;
  amount: number;
  currencyCode: string;
  status: string;
  earnedAtUtc: string;
  availableAtUtc: string;
};
type Payout = {
  id: string;
  publicPayoutId: string;
  amount: number;
  currencyCode: string;
  status: string;
  scheduledAtUtc: string;
  creatorId?: string;
};
type Line = {
  partyId: string;
  partyName: string;
  eligibleAmount: number;
  reservedAmount: number;
  status: string;
  payoutId?: string;
  payoutBatchId?: string;
};
type HistoryRow = {
  cycleStartUtc: string;
  cutoffAtUtc: string;
  payoutDateUtc: string;
  partyCount: number;
  totalAmount: number;
  status: string;
};
type PayoutHistoryRow = {
  partyName: string; partyId: string; cycleStartUtc: string; cutoffAtUtc: string;
  payoutDateUtc?: string; eligibleAmount: number; paidAmount: number; status: string;
  payoutReference: string; batchReference?: string;
};
type PayoutHistoryPage = { items: PayoutHistoryRow[]; page: number; pageSize: number; totalCount: number; totalPaidAmount: number };
type Cycle = {
  currencyCode: string;
  cycleStartUtc: string;
  cutoffAtUtc: string;
  payoutDateUtc: string;
  lastPayoutDateUtc?: string;
  scheduledTotal: number;
  reservedTotal: number;
  paidTotal: number;
  lines: Line[];
  history: HistoryRow[];
};
type RevenueHistoryRow = {
  periodStartUtc: string;
  periodEndUtc: string;
  transactionCount: number;
  platformRevenue: number;
};
type Revenue = {
  currencyCode: string;
  cycleStartUtc: string;
  cycleEndUtc: string;
  currentRevenue: number;
  currentTransactionCount: number;
  history: RevenueHistoryRow[];
};

const displayDate = formatDate;
const displayDateTime = formatDateTime;
export function AdminPayoutWorkspace() {
  const [tab, setTab] = useState<"creators" | "shoppers" | "revenue">(
      "creators",
    ),
    [cycle, setCycle] = useState<Cycle>(),
    [revenue, setRevenue] = useState<Revenue>(),
    [history, setHistory] = useState<PayoutHistoryPage>(),
    [search, setSearch] = useState(""),
    [appliedSearch, setAppliedSearch] = useState(""),
    [fromDate, setFromDate] = useState(""),
    [toDate, setToDate] = useState(""),
    [statusFilter, setStatusFilter] = useState(""),
    [historyPage, setHistoryPage] = useState(1),
    [busy, setBusy] = useState(""),
    [error, setError] = useState("");
  const loadHistory = (page = historyPage, term = appliedSearch, filters = { from: fromDate, to: toDate, status: statusFilter }) => {
    if (tab === "revenue") return Promise.resolve();
    const params = new URLSearchParams({ page: String(page), pageSize: "25" });
    if (term.trim()) params.set("search", term.trim());
    if (filters.from) params.set("fromUtc", `${filters.from}T00:00:00.000Z`);
    if (filters.to) params.set("toUtc", `${filters.to}T23:59:59.999Z`);
    if (filters.status) params.set("status", filters.status);
    return api<PayoutHistoryPage>(`/api/v1/admin/payout-history/${tab}?${params}`).then(setHistory);
  };
  const load = () =>
    (tab === "revenue"
      ? api<Revenue>("/api/v1/admin/payout-cycles/platform-revenue").then(
          (r) => {
            setRevenue(r);
            setError("");
          },
        )
      : Promise.all([api<Cycle>(`/api/v1/admin/payout-cycles/${tab}`), loadHistory()]).then(([c]) => {
          setCycle(c); setError("");
        })
    ).catch((x) => setError(x.message));
  useEffect(() => {
    setSearch("");
    setAppliedSearch("");
    setFromDate(""); setToDate(""); setStatusFilter(""); setHistoryPage(1);
    if (tab === "revenue") void load();
    else Promise.all([api<Cycle>(`/api/v1/admin/payout-cycles/${tab}`), loadHistory(1, "", { from: "", to: "", status: "" })])
      .then(([c]) => { setCycle(c); setError(""); })
      .catch((x) => setError(x.message));
  }, [tab]);
  async function post(path: string, body?: unknown, key?: string) {
    return api(path, {
      method: "POST",
      headers: key ? { "Idempotency-Key": key } : undefined,
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  }
  async function markPaid(line: Line) {
    if (
      !line.payoutId ||
      line.reservedAmount <= 0 ||
      !confirm("Confirm this payout has been sent?")
    )
      return;
    setBusy(line.payoutId);
    setError("");
    try {
      if (tab === "creators") {
        if (!line.payoutBatchId) throw Error("Payout batch is unavailable.");
        await post(
          `/api/v1/admin/payout-batches/${line.payoutBatchId}/process`,
        );
        await post(`/api/v1/admin/payouts/${line.payoutId}/submit`, {
          providerReference: `manual-${line.payoutId}`,
        });
        await post(`/api/v1/admin/payouts/${line.payoutId}/mark-paid`);
      } else {
        await post(
          `/api/v1/admin/customer-payouts/${line.payoutId}/processing`,
        );
        await post(
          `/api/v1/admin/customer-payouts/${line.payoutId}/mark-paid`,
          {
            externalMethod: "Manual",
            externalReference: `manual-${line.payoutId}`,
            paidAtUtc: new Date().toISOString(),
            safeNote: "Finalized by Platform Admin",
          },
          `paid-${line.payoutId}`,
        );
      }
      await load();
    } catch (x) {
      setError((x as Error).message);
    } finally {
      setBusy("");
    }
  }
  const term = appliedSearch.trim().toLowerCase(),
    lines =
      cycle?.lines.filter(
        (x) =>
          !term ||
          x.partyName.toLowerCase().includes(term) ||
          (tab === "creators" && x.partyId.toLowerCase().includes(term)),
      ) ?? [];
  return (
    <section>
      <h2>Payout Cycles</h2>
      <nav className="subtabs">
        <button
          className={tab === "creators" ? "active" : ""}
          onClick={() => setTab("creators")}
        >
          Creators
        </button>
        <button
          className={tab === "shoppers" ? "active" : ""}
          onClick={() => setTab("shoppers")}
        >
          Customers
        </button>
        <button
          className={tab === "revenue" ? "active" : ""}
          onClick={() => setTab("revenue")}
        >
          Platform Revenue
        </button>
      </nav>
      {error && (
        <p className="friendly-error" role="status">
          {error}
        </p>
      )}
      {tab !== "revenue" && cycle && (
        <>
          <h3>Current {tab === "creators" ? "Creator" : "Customer"} Cycle</h3>
          <div className="summary-grid payout-cycle-summary">
            <article>
              <span>Cycle Start</span>
              <strong>{displayDate(cycle.cycleStartUtc)}</strong>
            </article>
            <article>
              <span>Cutoff</span>
              <strong>{displayDateTime(cycle.cutoffAtUtc)}</strong>
            </article>
            <article>
              <span>Payout Date</span>
              <strong>{displayDate(cycle.payoutDateUtc)}</strong>
            </article>
            <article>
              <span>Eligible/Scheduled Total</span>
              <strong>{money(cycle.scheduledTotal, cycle.currencyCode)}</strong>
            </article>
            <article>
              <span>Reserved/In Batch</span>
              <strong>{money(cycle.reservedTotal, cycle.currencyCode)}</strong>
            </article>
            <article>
              <span>Paid This Cycle</span>
              <strong>{money(cycle.paidTotal, cycle.currencyCode)}</strong>
            </article>
          </div>
          <form
            className="filter-bar"
            onSubmit={(e) => {
              e.preventDefault();
              setAppliedSearch(search);
              setHistoryPage(1);
              void loadHistory(1, search);
            }}
          >
            <label>
              {tab === "creators"
                ? "Search Creator Name or Creator ID"
                : "Search Customer Name"}
              <input
                type="search"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
              />
            </label>
            <button>Search</button>
            <label>From <input type="date" value={fromDate} onChange={(e) => setFromDate(e.target.value)} /></label>
            <label>To <input type="date" value={toDate} onChange={(e) => setToDate(e.target.value)} /></label>
            <label>Status <select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)}><option value="">All</option><option value="Paid">Paid</option><option value="Requested">Requested</option><option value="Processing">Processing</option><option value="Scheduled">Scheduled</option><option value="Failed">Failed</option><option value="Cancelled">Cancelled</option></select></label>
            <button
              type="button"
              className="quiet"
              onClick={() => {
                setSearch("");
                setAppliedSearch("");
                setFromDate(""); setToDate(""); setStatusFilter(""); setHistoryPage(1); void loadHistory(1, "", { from: "", to: "", status: "" });
              }}
            >
              Clear
            </button>
          </form>
          <div className="table-wrap">
            <table className="admin-payout-table">
              <thead>
                <tr>
                  <th>
                    {tab === "creators" ? "Creator Name" : "Customer Name"}
                  </th>
                  {tab === "creators" && <th>Creator ID</th>}
                  <th>
                    {tab === "creators"
                      ? "Eligible Amount"
                      : "Eligible Cashback"}
                  </th>
                  <th>Reserved/In Batch</th>
                  <th>Payout Status</th>
                </tr>
              </thead>
              <tbody>
                {lines.map((x) => (
                  <tr key={x.partyId}>
                    <td data-label={tab === "creators" ? "Creator Name" : "Customer Name"}>
                      {x.partyName}
                      {tab === "shoppers" && (
                        <small className="secondary-id">{x.partyId}</small>
                      )}
                    </td>
                    {tab === "creators" && <td data-label="Creator ID">{x.partyId}</td>}
                    <td data-label={tab === "creators" ? "Eligible Amount" : "Eligible Cashback"}>{money(x.eligibleAmount, cycle.currencyCode)}</td>
                    <td data-label="Reserved/In Batch">{money(x.reservedAmount, cycle.currencyCode)}</td>
                    <td data-label="Payout Status">
                      {x.status === "PAID" ? (
                        <Badge value="PAID" />
                      ) : (
                        <button
                          className="payout-status-control"
                          disabled={
                            !x.payoutId ||
                            x.reservedAmount <= 0 ||
                            busy === x.payoutId
                          }
                          onClick={() => void markPaid(x)}
                          title={
                            x.reservedAmount > 0
                              ? "Mark this externally sent payout as paid"
                              : "No reserved payout is ready"
                          }
                        >
                          {busy === x.payoutId ? "Updating…" : "UNPAID"}
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <h3>{tab === "creators" ? "Creator" : "Customer"} Payout History</h3>
          <PayoutHistoryTable page={history} currency={cycle.currencyCode} onPage={(p) => { setHistoryPage(p); void loadHistory(p); }} />
        </>
      )}
      {tab === "revenue" && revenue && (
        <>
          <h3>Current Platform Revenue Period</h3>
          <div className="summary-grid revenue-period-summary">
            <article>
              <span>Period Start</span>
              <strong>{displayDate(revenue.cycleStartUtc)}</strong>
            </article>
            <article>
              <span>Period End</span>
              <strong>{displayDate(revenue.cycleEndUtc)}</strong>
            </article>
            <article>
              <span>Transactions</span>
              <strong>{revenue.currentTransactionCount}</strong>
            </article>
            <article>
              <span>Platform Revenue</span>
              <strong>
                {money(revenue.currentRevenue, revenue.currencyCode)}
              </strong>
            </article>
          </div>
          <h3>Platform Revenue History</h3>
          <RevenueHistory rows={revenue.history} currency={revenue.currencyCode} />
        </>
      )}
    </section>
  );
}
function RevenueHistory({rows,currency}:{rows:RevenueHistoryRow[];currency:string}) {
  return <div className="table-wrap"><table className="platform-revenue-history"><thead><tr><th>Period Start</th><th>Period End</th><th>Transactions</th><th>Platform Revenue</th></tr></thead><tbody>{rows.map((x)=><tr key={x.periodStartUtc}><td data-label="Period Start">{displayDate(x.periodStartUtc)}</td><td data-label="Period End">{displayDate(x.periodEndUtc)}</td><td data-label="Transactions">{x.transactionCount}</td><td data-label="Platform Revenue">{money(x.platformRevenue,currency)}</td></tr>)}</tbody></table></div>;
}
function History({ rows }: { rows: HistoryRow[] }) {
  return (
    <div className="table-wrap">
      <table>
        <thead>
          <tr>
            <th>Cycle Start</th>
            <th>Cutoff</th>
            <th>Payout Date</th>
            <th>Count</th>
            <th>Total</th>
            <th>Status</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((x, i) => (
            <tr key={`${x.cutoffAtUtc}-${i}`}>
              <td data-label="Cycle Start">{displayDate(x.cycleStartUtc)}</td>
              <td data-label="Cutoff">{displayDateTime(x.cutoffAtUtc)}</td>
              <td data-label="Payout Date">{displayDate(x.payoutDateUtc)}</td>
              <td data-label="Count">{x.partyCount}</td>
              <td data-label="Total">{money(x.totalAmount)}</td>
              <td data-label="Status">
                <Badge value={x.status} />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
function PayoutHistoryTable({ page, currency, onPage }: { page?: PayoutHistoryPage; currency: string; onPage: (page: number) => void }) {
  if (!page) return <p className="muted">No payout history found.</p>;
  const totalPages = Math.max(1, Math.ceil(page.totalCount / page.pageSize));
  return <>
    <div className="summary-grid payout-history-totals"><article><span>Payout records</span><strong>{page.totalCount}</strong></article><article><span>Total paid</span><strong>{money(page.totalPaidAmount, currency)}</strong></article></div>
    <div className="table-wrap"><table className="admin-payout-history-table"><thead><tr><th>{"Name"}</th><th>Public ID</th><th>Cycle Start</th><th>Cutoff</th><th>Payout Date</th><th>Eligible Amount</th><th>Paid Amount</th><th>Status</th><th>Reference</th></tr></thead><tbody>{page.items.map((x) => <tr key={x.payoutReference}><td data-label="Name">{x.partyName}</td><td data-label="Public ID">{x.partyId}</td><td data-label="Cycle Start">{displayDate(x.cycleStartUtc)}</td><td data-label="Cutoff">{displayDateTime(x.cutoffAtUtc)}</td><td data-label="Payout Date">{x.payoutDateUtc ? displayDate(x.payoutDateUtc) : "—"}</td><td data-label="Eligible Amount">{money(x.eligibleAmount, currency)}</td><td data-label="Paid Amount">{money(x.paidAmount, currency)}</td><td data-label="Status"><Badge value={x.status} /></td><td data-label="Reference"><code>{x.payoutReference}</code>{x.batchReference && <small className="secondary-id">{x.batchReference}</small>}</td></tr>)}</tbody></table></div>
    <div className="pagination-controls" aria-label="Payout history pages"><button disabled={page.page <= 1} onClick={() => onPage(page.page - 1)}>Previous</button><span>Page {page.page} of {totalPages}</span><button disabled={page.page >= totalPages} onClick={() => onPage(page.page + 1)}>Next</button></div>
  </>;
}
