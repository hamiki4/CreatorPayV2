const CANONICAL_ORIGIN = "https://weymela.com";
const LEGACY_ORIGIN = "https://www.weymela.com";

/** Retires legacy www PWA state before the application can make API calls. */
export const canonicalOriginMigration: Promise<void> =
  window.location.origin !== LEGACY_ORIGIN
    ? Promise.resolve()
    : (async () => {
        const registrations =
          "serviceWorker" in navigator
            ? await navigator.serviceWorker.getRegistrations().catch(() => [])
            : [];
        await Promise.all(registrations.map((registration) => registration.unregister()));

        if ("caches" in window) {
          const keys = await caches.keys().catch(() => []);
          await Promise.all(
            keys
              .filter((key) => key.startsWith("creatorpay-shell-"))
              .map((key) => caches.delete(key)),
          );
        }

        const target = `${CANONICAL_ORIGIN}${window.location.pathname}${window.location.search}${window.location.hash}`;
        window.location.replace(target);
        // Do not render or issue API calls while the legacy origin is retiring.
        await new Promise<void>(() => undefined);
      })();
