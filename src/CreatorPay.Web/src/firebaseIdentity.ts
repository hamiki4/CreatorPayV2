import { initializeApp, type FirebaseApp } from "firebase/app";
import { getAuth, isSignInWithEmailLink, sendSignInLinkToEmail, signInWithEmailLink } from "firebase/auth";

const callbackPath = "/auth/firebase-action";
const allowedOrigin = location.origin;
let app: FirebaseApp | null = null;

function firebaseApp() {
  const config = {
    apiKey: import.meta.env.VITE_FIREBASE_API_KEY,
    authDomain: import.meta.env.VITE_FIREBASE_AUTH_DOMAIN,
    projectId: import.meta.env.VITE_FIREBASE_PROJECT_ID,
    appId: import.meta.env.VITE_FIREBASE_APP_ID,
  };
  if (!config.apiKey || !config.authDomain || !config.projectId || !config.appId)
    throw new Error("Firebase email verification is not configured.");
  return (app ??= initializeApp(config));
}

export function firebaseConfigured() {
  return Boolean(import.meta.env.VITE_FIREBASE_API_KEY && import.meta.env.VITE_FIREBASE_AUTH_DOMAIN && import.meta.env.VITE_FIREBASE_PROJECT_ID && import.meta.env.VITE_FIREBASE_APP_ID);
}

export async function sendFirebaseEmailLink(email: string, purpose: "enroll" | "recover") {
  const url = new URL(callbackPath, allowedOrigin);
  url.searchParams.set("purpose", purpose);
  localStorage.setItem("weymela_firebase_email", email);
  localStorage.setItem("weymela_firebase_purpose", purpose);
  await sendSignInLinkToEmail(getAuth(firebaseApp()), email, { url: url.toString(), handleCodeInApp: true });
}

export async function completeFirebaseEmailLink() {
  if (location.pathname !== callbackPath || !isSignInWithEmailLink(getAuth(firebaseApp()), location.href)) return null;
  const purpose = localStorage.getItem("weymela_firebase_purpose");
  const email = localStorage.getItem("weymela_firebase_email");
  if (!email || (purpose !== "enroll" && purpose !== "recover")) throw new Error("Start email verification again from Weymela.");
  const credential = await signInWithEmailLink(getAuth(firebaseApp()), email, location.href);
  const idToken = await credential.user.getIdToken(true);
  localStorage.removeItem("weymela_firebase_email");
  localStorage.removeItem("weymela_firebase_purpose");
  return { email, purpose, idToken } as const;
}
