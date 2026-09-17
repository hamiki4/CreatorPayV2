const apiBase = (import.meta.env.VITE_API_URL ?? "").replace(/\/$/, "");

export type ExternalSessionUser = { role: string; status: string; isOnboarding: boolean; destination: string | null };
let current: ExternalSessionUser | null = null;

export function setExternalSession(value: ExternalSessionUser | null) { current = value; }
export function getExternalSession() { return current; }
export function isExternalSession() { return current !== null; }
export function externalOnboardingPath(session: ExternalSessionUser | null) {
  if (!session?.isOnboarding) return null;
  return session.role === "Customer" ? "/onboarding/customer"
    : session.role === "Creator" ? "/onboarding/creator"
    : session.role === "MerchantAdmin" ? "/onboarding/business"
    : null;
}

export async function loadExternalSession() {
  const response = await fetch(`${apiBase}/api/v1/integration/v3/session`, { credentials: "include", cache: "no-store" });
  if (response.ok) {
    current = await response.json() as ExternalSessionUser;
    return { session: current, integrationEnabled: true, authenticationUrl: null as string | null };
  }
  current = null;
  const configuration = await fetch(`${apiBase}/api/v1/integration/v3/configuration`, { cache: "no-store" });
  if (!configuration.ok) throw new Error("Weymela authentication is temporarily unavailable.");
  const value = await configuration.json() as { enabled: boolean; authenticationUrl: string | null };
  return { session: null, integrationEnabled: value.enabled, authenticationUrl: value.authenticationUrl };
}

async function transition(path: "switch-profile" | "logout") {
  const response = await fetch(`${apiBase}/api/v1/integration/v3/${path}`, {
    method: "POST", credentials: "include",
    headers: { "Content-Type": "application/json", "X-Weymela-Product-Request": "1" }, body: "{}",
  });
  if (!response.ok) throw new Error("We couldn't complete that account action.");
  current = null;
  const value = await response.json() as { redirectUrl: string };
  if (path === "logout") {
    const authority = await fetch("/api/session/sign-out", {
      method: "POST", credentials: "same-origin",
      headers: { "Content-Type": "application/json", "X-Weymela-Request": "1" }, body: "{}",
    });
    if (!authority.ok && authority.status !== 401)
      throw new Error("We couldn't complete that account action.");
    location.replace("/sign-in");
    return;
  }
  location.replace(value.redirectUrl);
}
export function switchExternalProfile() { return transition("switch-profile"); }
export function signOutExternalSession() { return transition("logout"); }
