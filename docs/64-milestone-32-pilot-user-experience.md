# Milestone 32: pilot user experience

Milestone 32 adds a shared, role-aware pilot experience for Shoppers, Creators, Business Owners, Cashiers, and Supervisors. First-run walkthroughs are stored locally per role and version so no onboarding behavior profile is sent to the API. Contextual Help links to the existing Help Center, and pilot feedback reuses support requests, platform notifications, operational audit events, rate limiting, and Platform Admin authorization.

English and Amharic cover walkthroughs, contextual help, offline state, session warnings, feedback, toasts, and checkout satisfaction. Product terminology uses Shopper, Creator, Business Owner, Offer, Cashier, Supervisor, checkout, and cashback consistently. Existing API/domain names remain unchanged for compatibility.

The shared layer includes keyboard-visible focus, modal semantics, live status and alert regions, descriptive controls, reduced-motion and forced-color handling, and narrow-screen layouts. Offline messaging explicitly states that checkout and financial operations require connectivity. Session warnings appear during the final two minutes of the access-token lifetime and offer a refresh-token-backed continuation.

Pilot feedback rejects likely email, phone, card, markup, or oversized content. Satisfaction surveys accept only a 1–5 rating for a completed checkout belonging to the signed-in Shopper and prevent a second stored rating. Low ratings notify platform operations. Feedback and rating audit metadata contains only category, route, language, rating, and operational identifiers—never contact or payment data.

The browser analytics hook emits `weymela:ux` custom events with an event name, timestamp, and non-personal context. It has no network transport by design; a future consent-aware adapter can subscribe without changing authentication or financial workflows.
