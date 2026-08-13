import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const pin = await readFile(new URL("../src/PinExperience.tsx", import.meta.url), "utf8");
const main = await readFile(new URL("../src/main.tsx", import.meta.url), "utf8");
const auth = await readFile(new URL("../src/AuthWorkspace.tsx", import.meta.url), "utf8");
const client = await readFile(new URL("../src/apiClient.ts", import.meta.url), "utf8");

test("normal roles are gated by PIN while PlatformAdmin remains unchanged", () => {
  assert.match(main, /user\.role === "PlatformAdmin"/);
  assert.match(main, /<PinEnrollmentGate><App \/><\/PinEnrollmentGate>/);
  assert.doesNotMatch(main, /<PinEnrollmentGate><AdminPortal/);
});

test("PIN UI enforces five digits and exposes recovery", () => {
  assert.match(pin, /\^\\d\{5\}\$/);
  assert.match(auth, /Forgot PIN/);
  assert.match(pin, /\/api\/v1\/auth\/pin\/recovery-proof/);
  assert.match(pin, /\/api\/v1\/auth\/pin\/reset/);
});

test("PIN enrollment and recovery no longer use Firebase, email, SMS, or OTP", () => {
  assert.match(pin, /Birth Date/);
  assert.match(pin, /phoneNumber,birthDate/);
  assert.doesNotMatch(pin, /firebase|recovery email|sms|one-time password/i);
  assert.doesNotMatch(main, /firebase-action|FirebaseRecoveryCallback/);
});

test("anonymous PIN unlock does not send an empty Bearer credential",()=>{
  assert.match(client,/accessToken\?\{Authorization:/);
  assert.doesNotMatch(client,/Authorization:`Bearer \$\{token\(\)\}`/);
});
