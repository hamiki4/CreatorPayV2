import { FormEvent, useEffect, useRef, useState } from "react";
import { api } from "./apiClient";

type Staff = {
  firstName: string;
  lastName: string;
  username: string;
  businessName: string;
  locations: { id: string; name: string; isPrimary: boolean }[];
};
type Purchase = {
  transactionId: string;
  publicTransactionId: string;
  status: string;
  purchaseAmount: number;
  confirmedAtUtc?: string;
  creatorDisplayName: string;
};
type Result = {
  status: string;
  code: string;
  message: string;
  checkout?: {
    publicCheckoutId: string;
    purchaseAmount?: number;
    creatorName?: string;
  };
};
type Validation = {
  isValid: boolean;
  code: string;
  message: string;
  creatorName?: string;
  businessName?: string;
};
type Tab = "purchase" | "recent" | "profile";
const money = (value: number) =>
  new Intl.NumberFormat("en-ET", { style: "currency", currency: "ETB" }).format(
    value,
  );
const transactionDate = (value?: string) =>
  value
    ? new Intl.DateTimeFormat("en-GB", {
        day: "2-digit",
        month: "2-digit",
        year: "numeric",
      }).format(new Date(value))
    : "—";
const transactionTime = (value?: string) =>
  value
    ? new Intl.DateTimeFormat("en-GB", {
        hour: "numeric",
        minute: "2-digit",
        hour12: true,
      }).format(new Date(value))
    : "—";

export function CashierCheckoutWorkspace({
  initialQrPayload,
}: { initialQrPayload?: string } = {}) {
  void initialQrPayload;
  const [tab, setTab] = useState<Tab>("purchase"),
    [staff, setStaff] = useState<Staff>(),
    [recent, setRecent] = useState<Purchase[]>([]);
  const [creatorCode, setCreatorCode] = useState(""),
    [validation, setValidation] = useState<Validation>(),
    [shopperPhoneNumber, setShopperPhoneNumber] = useState(""),
    [purchaseAmount, setPurchaseAmount] = useState("");
  const [result, setResult] = useState<Result>(),
    [message, setMessage] = useState(""),
    [busy, setBusy] = useState(false),
    submissionKey = useRef(crypto.randomUUID());
  const load = () =>
    Promise.all([
      api<Staff>("/api/v1/cashier/me"),
      api<Purchase[]>("/api/v1/cashier/purchases/recent"),
    ])
      .then(([s, p]) => {
        setStaff(s);
        setRecent(p);
      })
      .catch((error) => {
        console.error(error);
        setMessage("We couldn't load this information.");
      });
  useEffect(() => {
    void load();
  }, []);
  async function submit(e: FormEvent) {
    e.preventDefault();
    if (!confirm("Send this purchase to the Shopper for confirmation?")) return;
    setResult(undefined);
    setValidation(undefined);
    setMessage("");
    setBusy(true);
    try {
      const eligibility = await api<Validation>(
        "/api/v1/cashier/checkouts/validate-creator",
        { method: "POST", body: JSON.stringify({ creatorCode }) },
      );
      setValidation(eligibility);
      if (!eligibility.isValid) {
        setMessage(
          eligibility.message ||
            "This Creator promotion is not currently eligible at this Business.",
        );
        return;
      }
      const response = await api<Result>(
        "/api/v1/cashier/checkouts/by-creator",
        {
          method: "POST",
          headers: { "Idempotency-Key": submissionKey.current },
          body: JSON.stringify({
            creatorCode,
            shopperPhoneNumber,
            purchaseAmount: Number(purchaseAmount),
          }),
        },
      );
      setResult(response);
      if (response.code === "shopper_not_registered")
        setMessage("Shopper account not found.");
      else
        setMessage(
          response.code === "awaiting_shopper_confirmation"
            ? "Awaiting Shopper Confirmation"
            : response.message,
        );
    } catch (error) {
      console.error(error);
      setMessage(
        (error as Error).message ||
          "We couldn't submit this sale. Please check the information and try again.",
      );
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="creator-dashboard cashier-dashboard">
      <nav className="creator-tabs" aria-label="Cashier Dashboard sections">
        {(
          [
            ["purchase", "New Purchase"],
            ["recent", "Recent Transactions"],
            ["profile", "Profile"],
          ] as [Tab, string][]
        ).map(([id, label]) => (
          <button
            key={id}
            className={tab === id ? "active" : ""}
            onClick={() => setTab(id)}
          >
            {label}
          </button>
        ))}
      </nav>
      {message && (
        <p
          role="status"
          className={
            message === "Awaiting Shopper Confirmation"
              ? "success-note"
              : "friendly-error"
          }
        >
          {message}
        </p>
      )}
      {tab === "purchase" && (
        <section className="creator-section cashier-scan">
          <h2>New Purchase</h2>
          {result?.status !== "AwaitingShopperConfirmation" && (
            <form className="panel form cashier-sale" onSubmit={submit}>
              <label>
                Creator ID
                <input
                  required
                  inputMode="numeric"
                  autoComplete="off"
                  minLength={4}
                  maxLength={4}
                  pattern="[1-9][0-9]{3}"
                  value={creatorCode}
                  onChange={(e) => {
                    setCreatorCode(
                      e.target.value.replace(/\D/g, "").slice(0, 4),
                    );
                    setValidation(undefined);
                    submissionKey.current = crypto.randomUUID();
                  }}
                  placeholder="Creator ID"
                />
              </label>
              <label>
                Customer Phone Number
                <input
                  required
                  inputMode="tel"
                  autoComplete="tel"
                  value={shopperPhoneNumber}
                  onChange={(e) => setShopperPhoneNumber(e.target.value)}
                  placeholder="Customer Phone Number"
                />
              </label>
              <label>
                Purchase Amount (ETB)
                <input
                  required
                  type="number"
                  min="0.01"
                  step="0.01"
                  value={purchaseAmount}
                  onChange={(e) => setPurchaseAmount(e.target.value)}
                  placeholder="Purchase Amount"
                />
              </label>
              <button className="full" disabled={busy}>
                {busy ? "Submitting…" : "Submit Purchase"}
              </button>
            </form>
          )}
          {validation?.isValid && (
            <aside
              className="success-note cashier-validation"
              aria-label="Eligible Creator"
            >
              <p>
                <strong>Creator:</strong> {validation.creatorName}
              </p>
              <p>
                <strong>Business:</strong> {validation.businessName}
              </p>
              <p>
                <strong>Status:</strong> Eligible
              </p>
            </aside>
          )}
          {result?.status === "AwaitingShopperConfirmation" && (
            <aside className="success-note">
              <strong>Awaiting Shopper Confirmation</strong>
              <p>Reference: {result.checkout?.publicCheckoutId}</p>
              <p>
                Amount:{" "}
                {money(
                  result.checkout?.purchaseAmount ?? Number(purchaseAmount),
                )}
              </p>
            </aside>
          )}
        </section>
      )}
      {tab === "recent" && (
        <section className="creator-section">
          <h2>Recent Transactions</h2>
          {recent.length ? (
            <div className="cashier-transactions">
              <div className="cashier-transaction-row headings">
                <span>Amount</span>
                <span>Status</span>
                <span>Date</span>
                <span>Time</span>
              </div>
              {[...recent]
                .sort(
                  (a, b) =>
                    new Date(b.confirmedAtUtc ?? 0).getTime() -
                    new Date(a.confirmedAtUtc ?? 0).getTime(),
                )
                .map((x) => (
                  <article
                    className="cashier-transaction-row"
                    key={x.transactionId}
                  >
                    <strong data-label="Amount">
                      {money(x.purchaseAmount)}
                    </strong>
                    <span data-label="Status">
                      <span className="status-badge">{x.status}</span>
                    </span>
                    <span data-label="Date">
                      {transactionDate(x.confirmedAtUtc)}
                    </span>
                    <span data-label="Time">
                      {transactionTime(x.confirmedAtUtc)}
                    </span>
                  </article>
                ))}
            </div>
          ) : (
            <p className="compact-empty">No recent transactions yet.</p>
          )}
        </section>
      )}
      {tab === "profile" && (
        <section className="creator-section">
          <h2>Profile</h2>
          <div className="compact-panel">
            <div>
              <strong>
                {staff ? `${staff.firstName} ${staff.lastName}` : "Cashier"}
              </strong>
              <p>{staff?.businessName}</p>
              <p>{staff?.locations.map((x) => x.name).join(", ")}</p>
              <small>Username: {staff?.username}</small>
            </div>
          </div>
        </section>
      )}
    </div>
  );
}
