import { useEffect, useState } from "react";
import { api, statusLabel } from "./apiClient";
const money = (n: number, c = "ETB") =>
  new Intl.NumberFormat("en-ET", { style: "currency", currency: c }).format(n);
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

export function CreatorEarningsWorkspace() {
  const [summary, setSummary] = useState<Summary>();
  const [earnings, setEarnings] = useState<Earning[]>([]);
  const [payouts, setPayouts] = useState<Payout[]>([]);
  const [error, setError] = useState("");
  useEffect(() => {
    Promise.all([
      api<Summary>("/api/v1/creator/earnings/summary"),
      api<Earning[]>("/api/v1/creator/earnings"),
      api<Payout[]>("/api/v1/creator/payouts"),
    ])
      .then(([s, e, p]) => {
        setSummary(s);
        setEarnings(e);
        setPayouts(p);
      })
      .catch((x) => setError(x.message));
  }, []);
  return (
    <section className="creator-section">
      <h2>Earnings &amp; Payouts</h2>
      {error && <p className="friendly-error">{error}</p>}
      {summary && (
        <div className="metric-grid compact-metrics">
          <article>
            <span>Available balance</span>
            <strong>
              {money(summary.availableBalance, summary.currencyCode)}
            </strong>
          </article>
          <article>
            <span>Pending payout</span>
            <strong>
              {money(
                summary.pendingBalance + summary.scheduledBalance,
                summary.currencyCode,
              )}
            </strong>
          </article>
        </div>
      )}
      <h3>Earnings history</h3>
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Earned</th>
              <th>Status</th>
              <th>Amount</th>
              <th>Available</th>
            </tr>
          </thead>
          <tbody>
            {earnings.map((x) => (
              <tr key={x.id}>
                <td>{new Date(x.earnedAtUtc).toLocaleDateString()}</td>
                <td>
                  <Badge value={x.status} />
                </td>
                <td>{money(x.amount, x.currencyCode)}</td>
                <td>{new Date(x.availableAtUtc).toLocaleDateString()}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <h3>Payout history</h3>
      {payouts.map((x) => (
        <article className="compact-panel" key={x.id}>
          <Badge value={x.status} />
          <strong>
            {x.publicPayoutId} · {money(x.amount, x.currencyCode)}
          </strong>
        </article>
      ))}
    </section>
  );
}

const displayDate = (value: string) =>
  new Intl.DateTimeFormat("en-GB", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  }).format(new Date(value));
const displayDateTime = (value: string) =>
  new Intl.DateTimeFormat("en-GB", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    hour12: true,
  })
    .format(new Date(value))
    .replace(",", "");
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
          Shoppers
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
          <h3>Current {tab === "creators" ? "Creator" : "Shopper"} Cycle</h3>
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
                : "Search Shopper Name"}
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
                    {tab === "creators" ? "Creator Name" : "Shopper Name"}
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
                    <td>
                      {x.partyName}
                      {tab === "shoppers" && (
                        <small className="secondary-id">{x.partyId}</small>
                      )}
                    </td>
                    {tab === "creators" && <td>{x.partyId}</td>}
                    <td>{money(x.eligibleAmount, cycle.currencyCode)}</td>
                    <td>{money(x.reservedAmount, cycle.currencyCode)}</td>
                    <td>
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
          <h3>{tab === "creators" ? "Creator" : "Shopper"} Payout History</h3>
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
  return <div className="table-wrap"><table className="platform-revenue-history"><thead><tr><th>Period Start</th><th>Period End</th><th>Transactions</th><th>Platform Revenue</th></tr></thead><tbody>{rows.map((x)=><tr key={x.periodStartUtc}><td>{displayDate(x.periodStartUtc)}</td><td>{displayDate(x.periodEndUtc)}</td><td>{x.transactionCount}</td><td>{money(x.platformRevenue,currency)}</td></tr>)}</tbody></table></div>;
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
              <td>{displayDate(x.cycleStartUtc)}</td>
              <td>{displayDateTime(x.cutoffAtUtc)}</td>
              <td>{displayDate(x.payoutDateUtc)}</td>
              <td>{x.partyCount}</td>
              <td>{money(x.totalAmount)}</td>
              <td>
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
    <div className="table-wrap"><table className="admin-payout-history-table"><thead><tr><th>{"Name"}</th><th>Public ID</th><th>Cycle Start</th><th>Cutoff</th><th>Payout Date</th><th>Eligible Amount</th><th>Paid Amount</th><th>Status</th><th>Reference</th></tr></thead><tbody>{page.items.map((x) => <tr key={x.payoutReference}><td>{x.partyName}</td><td>{x.partyId}</td><td>{displayDate(x.cycleStartUtc)}</td><td>{displayDateTime(x.cutoffAtUtc)}</td><td>{x.payoutDateUtc ? displayDate(x.payoutDateUtc) : "—"}</td><td>{money(x.eligibleAmount, currency)}</td><td>{money(x.paidAmount, currency)}</td><td><Badge value={x.status} /></td><td><code>{x.payoutReference}</code>{x.batchReference && <small className="secondary-id">{x.batchReference}</small>}</td></tr>)}</tbody></table></div>
    <div className="pagination-controls" aria-label="Payout history pages"><button disabled={page.page <= 1} onClick={() => onPage(page.page - 1)}>Previous</button><span>Page {page.page} of {totalPages}</span><button disabled={page.page >= totalPages} onClick={() => onPage(page.page + 1)}>Next</button></div>
  </>;
}
