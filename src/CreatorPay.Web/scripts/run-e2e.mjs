import { spawn, spawnSync } from "node:child_process";
import { randomBytes } from "node:crypto";
import net from "node:net";
import path from "node:path";
import process from "node:process";
const root = path.resolve(process.cwd(), "../.."),
  children = [];
const container = `creatorpay-e2e-${process.pid}`;
const databaseBindAddress = process.env.E2E_DOCKER_BIND ?? "127.0.0.1";
const password = `E2e!${randomBytes(18).toString("base64url")}`,
  dbPassword = randomBytes(24).toString("base64url"),
  jwt = randomBytes(48).toString("base64url"),
  otp = "654321",
  seed = {};
const redact=value=>{let output=String(value);for(const protectedValue of[password,dbPassword,jwt,otp])if(protectedValue)output=output.replaceAll(protectedValue,'[redacted]');return output.replace(/(Password=)[^;\s]+/gi,'$1[redacted]')}
const run = (cmd, args, options = {}) => {
  const result = spawnSync(cmd, args, {
    cwd: root,
    stdio: "inherit",
    shell: process.platform === "win32",
    ...options,
  });
  if (result.status !== 0)
    throw new Error(
      `${cmd} ${redact(args.join(" "))} failed (${result.status ?? result.signal ?? result.error?.message}).`,
    );
};
const capture = (cmd, args) => {
  const r = spawnSync(cmd, args, {
    cwd: root,
    encoding: "utf8",
    shell: process.platform === "win32",
  });
  if (r.status !== 0)
    throw new Error(
      `${cmd} is required. Install Docker Desktop and ensure the daemon is running.`,
    );
  return r.stdout.trim();
};
const port = () =>
  new Promise((resolve, reject) => {
    const s = net.createServer();
    s.listen(0, "127.0.0.1", () => {
      const p = s.address().port;
      s.close(() => resolve(p));
    });
    s.on("error", reject);
  });
const start = (cmd, args, env) => {
  const child = spawn(cmd, args, {
    cwd: root,
    env: { ...process.env, ...env },
    stdio: ["ignore", "pipe", "pipe"],
    shell: process.platform === "win32",
  });
  children.push(child);
  const safeLog = (data) => {
    process.stderr.write(redact(data));
  };
  child.stdout.on("data", safeLog);
  child.stderr.on("data", safeLog);
  return child;
};
const stop = (child) => {
  if (child.killed) return;
  if (process.platform === "win32")
    spawnSync("taskkill", ["/pid", String(child.pid), "/T", "/F"], {
      stdio: "ignore",
      shell: true,
    });
  else child.kill();
};
const wait = async (url) => {
  for (let i = 0; i < 90; i++) {
    try {
      const r = await fetch(url);
      if (r.ok) return;
    } catch {}
    await new Promise((r) => setTimeout(r, 1000));
  }
  throw new Error(`Timed out waiting for ${url}`);
};
try {
  capture("docker", ["version", "--format", "{{.Server.Version}}"]);
  run("docker", [
    "run",
    "--rm",
    "-d",
    "--name",
    container,
    "-e",
    `POSTGRES_PASSWORD=${dbPassword}`,
    "-e",
    "POSTGRES_USER=e2e",
    "-e",
    "POSTGRES_DB=creatorpay_e2e",
    "-p",
    `${databaseBindAddress}::5432`,
    "postgres:17-alpine",
  ]);
  let dbPort = "";
  for (let i = 0; i < 30 && !dbPort; i++) {
    try {
      dbPort = capture("docker", ["port", container, "5432/tcp"])
        .split(":")
        .at(-1);
    } catch {}
    await new Promise((r) => setTimeout(r, 500));
  }
  const directDocker = process.env.E2E_DOCKER_DIRECT === "true";
  const databaseHost = directDocker
    ? Object.values(
        JSON.parse(capture("docker", ["inspect", container]))[0]
          .NetworkSettings.Networks,
      )[0].IPAddress
    : (process.env.E2E_DOCKER_HOST ?? "127.0.0.1");
  const databasePort = directDocker ? "5432" : dbPort;
  const connection = `Host=${databaseHost};Port=${databasePort};Database=creatorpay_e2e;Username=e2e;Password=${dbPassword}`;
  for (let i = 0; i < 60; i++) {
    const ready = spawnSync(
      "docker",
      ["exec", container, "pg_isready", "-U", "e2e", "-d", "creatorpay_e2e"],
      { stdio: "ignore", shell: process.platform === "win32" },
    );
    if (ready.status === 0) break;
    await new Promise((r) => setTimeout(r, 500));
  }
  if (process.env.E2E_SKIP_TOOL_RESTORE !== "1")
    run("dotnet", ["tool", "restore"]);
  run("dotnet", [
    "tool",
    "run",
    "dotnet-ef",
    "database",
    "update",
    "--project",
    "src/CreatorPay.Infrastructure",
    "--startup-project",
    "src/CreatorPay.Api",
    "--connection",
    connection,
  ]);
  run("dotnet", [
    "build",
    "src/CreatorPay.Api/CreatorPay.Api.csproj",
    "--configuration",
    "Release",
    "--no-restore",
  ]);
  const apiPort = await port(),
    webPort = await port(),
    api = `http://127.0.0.1:${apiPort}`,
    web = `http://127.0.0.1:${webPort}`;
  start(
    "dotnet",
    [
      "run",
      "--project",
      "src/CreatorPay.Api",
      "--configuration",
      "Release",
      "--no-build",
      "--urls",
      api,
    ],
    {
      DOTNET_ENVIRONMENT: "E2E",
      ASPNETCORE_ENVIRONMENT: "E2E",
      Logging__LogLevel__Default: "Warning",
      ConnectionStrings__CreatorPayDatabase: connection,
      Authentication__Jwt__SigningKey: jwt,
      CustomerVerification__HmacSecret: randomBytes(32).toString("hex"),
      CustomerVerification__EncryptionKey: randomBytes(32).toString("hex"),
      SmsOtp__SmsProvider: "PilotTest",
      SmsOtp__HashSecret: randomBytes(32).toString("hex"),
      SmsOtp__TestCode: otp,
      SmsOtp__PilotRegistrationAutoVerifyEnabled:
        process.env.E2E_PILOT_REGISTRATION_BYPASS ?? "false",
      DepositProofStorage__RootPath: `/tmp/creatorpay-e2e-proofs-${process.pid}`,
      Cors__AllowedOrigins__0: web,
      PublicAppBaseUrl: web,
    },
  );
  await wait(`${api}/health/ready`);
  const seeded = await fetch(`${api}/api/v1/e2e/seed`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ password }),
  });
  if (!seeded.ok) throw new Error(`Fixture seeding failed (${seeded.status}).`);
  Object.assign(seed, await seeded.json());
  start(
    "npm",
    [
      "run",
      "dev",
      "--prefix",
      "src/CreatorPay.Web",
      "--",
      "--host",
      "127.0.0.1",
      "--port",
      String(webPort),
    ],
    { VITE_API_URL: api },
  );
  await wait(web);
  console.log(`E2E services ready: API ${apiPort}, frontend ${webPort}`);
  run("npx", ["playwright", "test", ...process.argv.slice(2)], {
    cwd: path.join(root, "src/CreatorPay.Web"),
    env: {
      ...process.env,
      E2E_BASE_URL: web,
      E2E_API_URL: api,
      E2E_TEST_OTP: otp,
      E2E_SHOPPER_EMAIL: seed.shopperEmail,
      E2E_SHOPPER_PHONE: seed.shopperPhone,
      E2E_SHOPPER_PASSWORD: password,
      E2E_CASHIER_EMAIL: seed.cashierEmail,
      E2E_CASHIER_PHONE: seed.cashierPhone,
      E2E_OWNER_EMAIL: seed.ownerEmail,
      E2E_OWNER_PHONE: seed.ownerPhone,
      E2E_CREATOR_EMAIL: seed.creatorEmail,
      E2E_CREATOR_PHONE: seed.creatorPhone,
      E2E_CREATOR_CODE: seed.creatorCode,
      E2E_NO_CAMPAIGN_CREATOR_CODE: seed.noCampaignCreatorCode,
      E2E_ADMIN_EMAIL: seed.adminEmail,
      E2E_ADMIN_PASSWORD: password,
      E2E_CREATOR_QR_PAYLOAD: seed.creatorQrPayload,
      E2E_NO_CAMPAIGN_QR_PAYLOAD: seed.noCampaignQrPayload,
      E2E_CONFIRMATION_EMAILS: JSON.stringify(seed.confirmationShopperEmails),
      E2E_CONFIRMATION_PHONES: JSON.stringify(seed.confirmationShopperPhones),
      E2E_CREATOR_NAME: seed.creatorName,
      E2E_ACTIVE_OFFER: seed.activeOfferCode,
      E2E_EXPIRED_OFFER: seed.expiredOfferCode,
      E2E_OFFER_QR: seed.offerQrId,
      E2E_OFFER_QR_PAYLOAD: seed.offerQrPayload,
    },
  });
} finally {
  for (const child of children.reverse()) stop(child);
  spawnSync("docker", ["stop", "--time", "5", container], {
    stdio: "ignore",
    shell: process.platform === "win32",
  });
  console.log("E2E API, frontend, and PostgreSQL processes stopped.");
}
