import { FormEvent, useEffect, useRef, useState } from "react";
import { api } from "./apiClient";

type Mode = "cashier" | "merchant";
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
  mode = "cashier",
}: { initialQrPayload?: string; mode?: Mode } = {}) {
  void initialQrPayload;
  const merchantMode = mode === "merchant";
  const [tab, setTab] = useState<Tab>("purchase");
  const [staff, setStaff] = useState<Staff>();
  const [recent, setRecent] = useState<Purchase[]>([]);
  const [creatorCode, setCreatorCode] = useState("");
  const [validation, setValidation] = useState<Validation>();
  const [shopperPhoneNumber, setShopperPhoneNumber] = useState("");
  const [purchaseAmount, setPurchaseAmount] = useState("");
  const [result, setResult] = useState<Result>();
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);
  const submissionKey = useRef(crypto.randomUUID());
  const resetTimer = useRef<ReturnType<typeof setTimeout> | undefined>(
    undefined,
  );

  const recentEndpoint = merchantMode
    ? "/api/v1/merchant/purchases"
    : "/api/v1/cashier/purchases/recent";
  const validateEndpoint = merchantMode
    ? "/api/v1/merchant/checkouts/validate-creator"
    : "/api/v1/cashier/checkouts/validate-creator";
  const submitEndpoint = merchantMode
    ? "/api/v1/merchant/checkouts/by-creator"
    : "/api/v1/cashier/checkouts/by-creator";

  const resetEntryForm = () => {
    setCreatorCode("");
    setShopperPhoneNumber("");
    setPurchaseAmount("");
    setValidation(undefined);
    setResult(undefined);
    setMessage("");
    submissionKey.current = crypto.randomUUID();
  };
  const showResultThenReset = (nextMessage: string) => {
    if (resetTimer.current) clearTimeout(resetTimer.current);
    setMessage(nextMessage);
    resetTimer.current = setTimeout(resetEntryForm, 3000);
  };
  const load = async () => {
    try {
      const purchases = await api<Purchase[]>(recentEndpoint);
      setRecent(purchases);
      if (!merchantMode) {
        const cashier = await api<Staff>("/api/v1/cashier/me");
        setStaff(cashier);
      }
    } catch (error) {
      console.error(error);
      setMessage("We couldn't load this information.");
    }
  };

  useEffect(() => {
    void load();
    return () => {
      if (resetTimer.current) clearTimeout(resetTimer.current);
    };
  }, []);

  async function submit(e: FormEvent) {
    e.preventDefault();
    if (!confirm("Send this purchase to the Customer for confirmation?"))
      return;
    setResult(undefined);
    setValidation(undefined);
    setMessage("");
    setBusy(true);
    try {
      const eligibility = await api<Validation>(validateEndpoint, {
        method: "POST",
        body: JSON.stringify({ creatorCode }),
      });
      setValidation(eligibility);
      if (!eligibility.isValid) {
        showResultThenReset(
          eligibility.message ||
            "This Creator promotion is not currently eligible at this Business.",
        );
        return;
      }
      const response = await api<Result>(submitEndpoint, {
        method: "POST",
        headers: { "Idempotency-Key": submissionKey.current },
        body: JSON.stringify({
          creatorCode,
          shopperPhoneNumber,
          purchaseAmount: Number(purchaseAmount),
        }),
      });
      setResult(response);
      void load();
      if (response.code === "shopper_not_registered")
        showResultThenReset("Customer account not found.");
      else {
        showResultThenReset(
          response.code === "awaiting_shopper_confirmation"
            ? "Purchase submitted — awaiting Customer confirmation."
            : response.message,
        );
      }
    } catch (error) {
      console.error(error);
      showResultThenReset(
        (error as Error).message ||
          "We couldn't submit this sale. Please check the information and try again.",
      );
    } finally {
      setBusy(false);
    }
  }

  const tabs: [Tab, string][] = merchantMode
    ? [
        ["purchase", "Checkout"],
        ["recent", "Recent Transactions"],
      ]
    : [
        ["purchase", "New Purchase"],
        ["recent", "Recent Transactions"],
        ["profile", "Profile"],
      ];

  return (
    <div className="creator-dashboard cashier-dashboard">
      <nav className="creator-tabs" aria-label="Cashier Dashboard sections">
        {tabs.map(([id, label]) => (
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
            message.includes("awaiting Customer confirmation")
              ? "success-note"
              : "friendly-error"
          }
        >
          {message}
        </p>
      )}
      {tab === "purchase" && (
        <section className="creator-section cashier-scan">
          <h2>{merchantMode ? "Checkout" : "New Purchase"}</h2>
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
              <strong>Awaiting Customer Confirmation</strong>
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
      {!merchantMode && tab === "profile" && (
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
