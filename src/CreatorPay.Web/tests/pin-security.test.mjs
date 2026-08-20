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
  assert.match(pin, /\/api\/v1\/auth\/pin\/reset-with-password/);
  assert.match(pin, /Confirm your account password/);
});

test("restricted account login messages are driven by structured status codes", () => {
  for (const code of ["AccountDeactivated", "AccountSuspended", "AccountLocked"]) {
    assert.match(auth, new RegExp(code));
  }
  assert.match(auth, /authErrorMessage/);
  assert.ok(client.includes("class ApiError extends Error"));
  assert.ok(client.includes("readonly status:number=0"));
  assert.ok(client.includes("readonly title?:string"));
  assert.doesNotMatch(pin, /AccountDeactivated|AccountSuspended|AccountLocked/);
});

test("PIN enrollment and recovery no longer use Firebase, email, SMS, or OTP", () => {
  assert.doesNotMatch(pin, /Birth Date|birthDate|birthDay|birthMonth|birthYear/);
  assert.match(pin, /phoneNumber,password,newPin:pin,confirmation/);
  assert.doesNotMatch(pin, /firebase|recovery email|sms|one-time password/i);
  assert.doesNotMatch(main, /firebase-action|FirebaseRecoveryCallback/);
});

test("anonymous PIN unlock does not send an empty Bearer credential",()=>{
  assert.match(client,/accessToken\?\{Authorization:/);
  assert.doesNotMatch(client,/Authorization:`Bearer \$\{token\(\)\}`/);
});
