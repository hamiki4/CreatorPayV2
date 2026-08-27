import { FormEvent, useEffect, useMemo, useState } from "react";
import { businessTypes } from "./AuthWorkspace";
import { api } from "./apiClient";

type BusinessTypeMinimum = {
  businessType: string;
  minimumBusinessWalletBalance: number;
  versionNumber?: number;
  effectiveFromUtc?: string;
};

type Settings = {
  merchantCommissionRatePercent: number;
  creatorSharePercent: number;
  shopperSharePercent: number;
  platformSharePercent: number;
  minimumTikTokFollowers: number;
  currencyCode: string;
  effectiveFromUtc: string;
  creatorCutoffDay: string;
  creatorCutoffTime: string;
  creatorPayoutDay: string;
  shopperCutoffDay: number;
  shopperCutoffTime: string;
  shopperPayoutDay: number;
  payoutScheduleEffectiveFromUtc: string;
  businessTypeMinimumWalletBalances: BusinessTypeMinimum[];
};

type FormState = {
  merchant: string;
  creator: string;
  shopper: string;
  platform: string;
  tikTokFollowers: string;
  creatorCutoffDay: string;
  creatorCutoffTime: string;
  creatorPayoutDay: string;
  shopperCutoffDay: string;
  shopperCutoffTime: string;
  shopperPayoutDay: string;
  effectiveMode: "Now" | "Scheduled";
  scheduledFor: string;
  minimums: Record<string, string>;
};

type MinimumRequest = {
  businessType: string;
  minimumBusinessWalletBalance: number;
};

const businessTypeValues = businessTypes.map((item) => item.value);
const nowInput = () => new Date().toISOString().slice(0, 16);
const laterInput = () => new Date(Date.now() + 60 * 60 * 1000).toISOString().slice(0, 16);

function emptyMinimums() {
  return Object.fromEntries(businessTypeValues.map((value) => [value, "1200"])) as Record<string, string>;
}

function toInputValue(value: string | undefined) {
  if (!value) return nowInput();
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? nowInput() : parsed.toISOString().slice(0, 16);
}

function buildRequest(form: FormState): {
  merchantCommissionRatePercent: number;
  creatorSharePercent: number;
  shopperSharePercent: number;
  platformSharePercent: number;
  businessTypeMinimumWalletBalances: MinimumRequest[];
  minimumTikTokFollowers: number;
  applyNow: boolean;
  effectiveFromUtc: string | null;
  creatorCutoffDay: string;
  creatorCutoffTime: string;
  creatorPayoutDay: string;
  shopperCutoffDay: number;
  shopperCutoffTime: string;
  shopperPayoutDay: number;
} {
  return {
    merchantCommissionRatePercent: Number(form.merchant),
    creatorSharePercent: Number(form.creator),
    shopperSharePercent: Number(form.shopper),
    platformSharePercent: Number(form.platform),
    businessTypeMinimumWalletBalances: businessTypeValues.map((businessType) => ({
      businessType,
      minimumBusinessWalletBalance: Number(form.minimums[businessType]),
    })),
    minimumTikTokFollowers: Number(form.tikTokFollowers),
    applyNow: form.effectiveMode === "Now",
    effectiveFromUtc: form.effectiveMode === "Scheduled" ? `${form.scheduledFor}:00Z` : null,
    creatorCutoffDay: form.creatorCutoffDay,
    creatorCutoffTime: `${form.creatorCutoffTime}:00`,
    creatorPayoutDay: form.creatorPayoutDay,
    shopperCutoffDay: Number(form.shopperCutoffDay),
    shopperCutoffTime: `${form.shopperCutoffTime}:00`,
    shopperPayoutDay: Number(form.shopperPayoutDay),
  };
}

export function CommissionWorkspace({ role }: { role: string }) {
  const admin = role === "PlatformAdmin";
  const [settings, setSettings] = useState<Settings>();
  const [form, setForm] = useState<FormState>({
    merchant: "",
    creator: "",
    shopper: "",
    platform: "",
    tikTokFollowers: "",
    creatorCutoffDay: "Friday",
    creatorCutoffTime: "00:00",
    creatorPayoutDay: "Saturday",
    shopperCutoffDay: "0",
    shopperCutoffTime: "00:00",
    shopperPayoutDay: "1",
    effectiveMode: "Now",
    scheduledFor: laterInput(),
    minimums: emptyMinimums(),
  });
  const [message, setMessage] = useState("");
  const [loading, setLoading] = useState(true);

  const load = () =>
    api<Settings>("/api/v1/admin/financial-settings")
      .then((value) => {
        setSettings(value);
        const minima = emptyMinimums();
        for (const businessType of businessTypeValues) {
          minima[businessType] = "1200";
        }
        for (const minimum of value.businessTypeMinimumWalletBalances ?? []) {
          if ((businessTypeValues as readonly string[]).includes(minimum.businessType)) {
            minima[minimum.businessType] = String(minimum.minimumBusinessWalletBalance);
          }
        }
        setForm({
          merchant: String(value.merchantCommissionRatePercent),
          creator: String(value.creatorSharePercent),
          shopper: String(value.shopperSharePercent),
          platform: String(value.platformSharePercent),
          tikTokFollowers: String(value.minimumTikTokFollowers),
          creatorCutoffDay: value.creatorCutoffDay,
          creatorCutoffTime: value.creatorCutoffTime.slice(0, 5),
          creatorPayoutDay: value.creatorPayoutDay,
          shopperCutoffDay: String(value.shopperCutoffDay),
          shopperCutoffTime: value.shopperCutoffTime.slice(0, 5),
          shopperPayoutDay: String(value.shopperPayoutDay),
          effectiveMode: "Now",
          scheduledFor: toInputValue(value.payoutScheduleEffectiveFromUtc),
          minimums: minima,
        });
      })
      .catch((error) => {
        console.error(error);
        setMessage("We couldn't load financial settings.");
      })
      .finally(() => setLoading(false));

  useEffect(() => {
    if (admin) void load();
  }, [admin]);

  const minimumRows = useMemo(
    () =>
      businessTypeValues.map((businessType) => ({
        businessType,
        label: businessTypes.find((item) => item.value === businessType)?.en ?? businessType,
      })),
    [],
  );

  async function save(e: FormEvent) {
    e.preventDefault();
    setMessage("");
    const body = buildRequest(form);
    if (body.creatorSharePercent + body.shopperSharePercent + body.platformSharePercent !== 100) {
      setMessage("Creator, Shopper, and Platform percentages must total 100%.");
      return;
    }
    if (body.businessTypeMinimumWalletBalances.some((row) => !Number.isFinite(row.minimumBusinessWalletBalance) || row.minimumBusinessWalletBalance < 0)) {
      setMessage("Business-type minimums must be zero or greater.");
      return;
    }
    if (!body.applyNow) {
      if (!body.effectiveFromUtc) {
        setMessage("Schedule Effective From (UTC) is required when scheduling later.");
        return;
      }
      const scheduled = new Date(body.effectiveFromUtc);
      if (Number.isNaN(scheduled.getTime()) || scheduled.getTime() <= Date.now()) {
        setMessage("Schedule Effective From (UTC) must be a future UTC date and time.");
        return;
      }
    }
    if (!confirm("Save these financial settings for new transactions? Historical transactions will not change.")) return;
    try {
      await api("/api/v1/admin/financial-settings", { method: "PUT", body: JSON.stringify(body) });
      await load();
      setMessage("Financial settings saved. New transactions use the new effective version.");
    } catch (error) {
      console.error(error);
      setMessage(error instanceof Error ? error.message : "Unable to save financial settings.");
    }
  }

  if (!admin) {
    return (
      <section>
        <h2>Commission</h2>
        <p>Commission configuration is available to Platform Admin only.</p>
      </section>
    );
  }

  const days = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];
  const scheduled = form.effectiveMode === "Scheduled";

  return (
    <section>
      <div className="title">
        <div>
          <p className="eyebrow">Financial configuration</p>
          <h2>Financial Settings</h2>
        </div>
      </div>
      {message && <aside role="status">{message}</aside>}
      {loading ? (
        <p>Loading…</p>
      ) : settings ? (
        <form className="panel form" onSubmit={save}>
          <p>Changes are versioned and apply only to new transactions and future payout schedules.</p>

          <label>
            Commission
            <input
              required
              type="number"
              min="0"
              max="100"
              step="0.01"
              value={form.merchant}
              onChange={(e) => setForm({ ...form, merchant: e.target.value })}
            />
            <span>%</span>
          </label>

          <label>
            Minimum TikTok Followers
            <input
              required
              type="number"
              min="0"
              step="1"
              value={form.tikTokFollowers}
              onChange={(e) => setForm({ ...form, tikTokFollowers: e.target.value })}
            />
          </label>

          <fieldset>
            <legend>Split of that commission</legend>
            <label>
              Creator
              <input
                required
                type="number"
                min="0"
                max="100"
                step="0.01"
                value={form.creator}
                onChange={(e) => setForm({ ...form, creator: e.target.value })}
              />
              <span>%</span>
            </label>
            <label>
              Shopper
              <input
                required
                type="number"
                min="0"
                max="100"
                step="0.01"
                value={form.shopper}
                onChange={(e) => setForm({ ...form, shopper: e.target.value })}
              />
              <span>%</span>
            </label>
            <label>
              Platform
              <input
                required
                type="number"
                min="0"
                max="100"
                step="0.01"
                value={form.platform}
                onChange={(e) => setForm({ ...form, platform: e.target.value })}
              />
              <span>%</span>
            </label>
          </fieldset>

          <fieldset className="financial-settings-table-wrap">
            <legend>Minimum Business Wallet Balance by Business Type</legend>
            <table className="financial-settings-table">
              <thead>
                <tr>
                  <th>Business Type</th>
                  <th>Minimum Wallet Balance (ETB)</th>
                </tr>
              </thead>
              <tbody>
                {minimumRows.map((row) => (
                  <tr key={row.businessType}>
                    <th scope="row">{row.label}</th>
                    <td>
                      <input
                        required
                        type="number"
                        min="0"
                        step="0.01"
                        value={form.minimums[row.businessType]}
                        onChange={(e) =>
                          setForm({
                            ...form,
                            minimums: { ...form.minimums, [row.businessType]: e.target.value },
                          })
                        }
                      />
                      <span>ETB</span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </fieldset>

          <fieldset>
            <legend>Creator Payout — Weekly</legend>
            <label>
              Cutoff Day
              <select value={form.creatorCutoffDay} onChange={(e) => setForm({ ...form, creatorCutoffDay: e.target.value })}>
                {days.map((day) => (
                  <option key={day}>{day}</option>
                ))}
              </select>
            </label>
            <label>
              Cutoff Time
              <input type="time" value={form.creatorCutoffTime} onChange={(e) => setForm({ ...form, creatorCutoffTime: e.target.value })} />
            </label>
            <label>
              Payout Day
              <select value={form.creatorPayoutDay} onChange={(e) => setForm({ ...form, creatorPayoutDay: e.target.value })}>
                {days.map((day) => (
                  <option key={day}>{day}</option>
                ))}
              </select>
            </label>
          </fieldset>

          <fieldset>
            <legend>Shopper Payout — Monthly</legend>
            <label>
              Cutoff Day (0 = last day)
              <input type="number" min="0" max="31" value={form.shopperCutoffDay} onChange={(e) => setForm({ ...form, shopperCutoffDay: e.target.value })} />
            </label>
            <label>
              Cutoff Time
              <input type="time" value={form.shopperCutoffTime} onChange={(e) => setForm({ ...form, shopperCutoffTime: e.target.value })} />
            </label>
            <label>
              Payout Day
              <input type="number" min="1" max="31" value={form.shopperPayoutDay} onChange={(e) => setForm({ ...form, shopperPayoutDay: e.target.value })} />
            </label>
          </fieldset>

          <fieldset>
            <legend>Effective</legend>
            <label className="effective-choice">
              <input
                type="radio"
                name="effective-mode"
                checked={form.effectiveMode === "Now"}
                onChange={() => setForm({ ...form, effectiveMode: "Now" })}
              />
              Now
            </label>
            <label className="effective-choice">
              <input
                type="radio"
                name="effective-mode"
                checked={scheduled}
                onChange={() =>
                  setForm({
                    ...form,
                    effectiveMode: "Scheduled",
                    scheduledFor: form.scheduledFor || laterInput(),
                  })
                }
              />
              Schedule for later
            </label>

            {scheduled && (
              <label>
                Schedule Effective From (UTC)
                <input
                  required
                  type="datetime-local"
                  value={form.scheduledFor}
                  onChange={(e) => setForm({ ...form, scheduledFor: e.target.value })}
                />
              </label>
            )}
          </fieldset>

          <button>Save Settings</button>
        </form>
      ) : null}
    </section>
  );
}
