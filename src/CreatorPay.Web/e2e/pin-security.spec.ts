import { expect, test } from "@playwright/test";

const api=process.env.E2E_API_URL!, password=process.env.E2E_SHOPPER_PASSWORD!;
const initialPin="12345",newPin="54321",birth={day:"2",month:"1",year:"1990"};

test.setTimeout(420_000);

async function clearSession(page:any){await page.evaluate(()=>{localStorage.removeItem("creatorpay_access_token");localStorage.removeItem("creatorpay_refresh_token")});}
async function fillBirth(page:any){await page.getByLabel("Day").fill(birth.day);await page.getByLabel("Month").fill(birth.month);await page.getByLabel("Year").fill(birth.year)}
async function fillPin(page:any,label:string,value:string){await page.getByLabel(label,{exact:true}).fill(value)}

test("public authentication UX stays compact and verification-free",async({page})=>{
  await page.goto("/");await expect(page.getByRole("heading",{name:"Sign In"})).toBeVisible();await expect(page.getByText("Welcome back. Keep shopping, promoting, and earning with Weymela.")).toBeVisible();
  await page.getByRole("button",{name:"Forgot Password"}).click();await expect(page.getByRole("heading",{name:"Forgot Password"})).toBeVisible();await expect(page.getByRole("button",{name:"Request Password Reset"})).toBeVisible();await expect(page.getByLabel("Day")).toBeVisible();await expect(page.getByText(/Weymela Support/)).toBeVisible();await expect(page.getByText(/verification code|OTP|Firebase|SMS/i)).toHaveCount(0);expect(await page.evaluate(()=>document.documentElement.scrollWidth<=document.documentElement.clientWidth)).toBeTruthy();
  await page.getByRole("button",{name:"Back to Sign In"}).click();await page.getByRole("button",{name:"Sign Up to Weymela"}).click();await expect(page.getByText("Welcome to Weymela — where shoppers save, creators earn, and businesses grow.")).toBeVisible();await expect(page.getByRole("button",{name:"Sign Up to Weymela"})).toHaveCount(0);await expect(page.getByRole("button",{name:"Back to Sign In"})).toBeVisible();expect(await page.evaluate(()=>document.documentElement.scrollWidth<=document.documentElement.clientWidth)).toBeTruthy();
});

test("simplified phone, PIN, birth-date recovery, lockout, signup, and admin flow",async({page,request},testInfo)=>{
  const profile=testInfo.project.name;
  const phone=profile==="mobile"?"+251977100002":"+251977100001";
  const registered=await request.post(`${api}/api/v1/customers/register`,{data:{displayName:`${profile} auth user`,email:null,phoneNumber:phone,password,confirmation:password,birthDate:"1990-01-02"}});
  expect(registered.status()).toBe(201);

  await page.goto("/");
  await expect(page.getByRole("heading",{name:"Sign In"})).toBeVisible();
  await expect(page.getByText("Welcome back. Keep shopping, promoting, and earning with Weymela.")).toBeVisible();
  await page.getByRole("button",{name:"Sign Up to Weymela"}).click();
  await expect(page.getByText("Welcome to Weymela — where shoppers save, creators earn, and businesses grow.")).toBeVisible();
  await expect(page.getByRole("button",{name:"Sign Up to Weymela"})).toHaveCount(0);
  await expect(page.getByRole("button",{name:"Back to Sign In"})).toBeVisible();await page.getByRole("button",{name:"Back to Sign In"}).click();
  await page.getByRole("button",{name:"Forgot Password"}).click();await expect(page.getByRole("heading",{name:"Forgot Password"})).toBeVisible();await expect(page.getByText("Request help resetting your password.")).toBeVisible();await expect(page.getByRole("button",{name:"Request Password Reset"})).toBeVisible();await expect(page.getByText(/Weymela Support/)).toBeVisible();await expect(page.getByText(/verification code|OTP|Firebase|SMS/i)).toHaveCount(0);expect(await page.evaluate(()=>document.documentElement.scrollWidth<=document.documentElement.clientWidth)).toBeTruthy();await page.getByRole("button",{name:"Back to Sign In"}).click();
  await page.getByLabel("Phone Number").fill(phone);
  await page.getByLabel("Password").fill(password);
  await page.locator("form").getByRole("button",{name:"Sign In",exact:true}).click();
  await expect(page.getByRole("heading",{name:"Create your 5-digit PIN"})).toBeVisible();
  await fillPin(page,"Create your 5-digit PIN",initialPin);
  await fillPin(page,"Confirm PIN",initialPin);
  await page.getByRole("button",{name:"Create PIN"}).click();
  await expect(page.getByRole("heading",{name:"Shopper Dashboard"})).toBeVisible();

  await clearSession(page);await page.goto("/");
  await expect(page.getByRole("heading",{name:"Enter your 5-digit PIN"})).toBeVisible();
  await expect(page.getByText(/Firebase|verification email/i)).toHaveCount(0);
  await fillPin(page,"5-digit PIN",initialPin);
  await page.locator("form").getByRole("button",{name:"Sign In",exact:true}).click();
  await expect(page).toHaveURL(/\/shopper/);

  await clearSession(page);await page.goto("/");
  await expect(page.getByRole("button",{name:"Forgot PIN"})).toBeVisible();

  await page.getByRole("button",{name:"Forgot PIN"}).click();
  await expect(page.getByRole("heading",{name:"Forgot PIN"})).toBeVisible();
  await fillBirth(page);await page.getByRole("button",{name:"Continue"}).click();
  await expect(page.getByRole("heading",{name:"Create New 5-digit PIN"})).toBeVisible();
  await fillPin(page,"New PIN",newPin);await fillPin(page,"Confirm PIN",newPin);
  await page.getByRole("button",{name:"Reset PIN"}).click();
  await expect(page.getByText("PIN reset. Sign in with your new PIN.")).toBeVisible();
  await page.getByRole("button",{name:"Back"}).click();
  await fillPin(page,"5-digit PIN",initialPin);await page.locator("form").getByRole("button",{name:"Sign In",exact:true}).click();
  await expect(page.getByRole("alert")).toContainText("Invalid phone number or PIN.");
  await page.waitForTimeout(1100);
  await fillPin(page,"5-digit PIN",newPin);await page.locator("form").getByRole("button",{name:"Sign In",exact:true}).click();
  await expect(page).toHaveURL(/\/shopper/);

  await clearSession(page);await page.goto("/");

  await page.getByRole("button",{name:"Sign Up to Weymela"}).click();
  await expect(page.getByRole("heading",{name:"Sign Up to Weymela"})).toBeVisible();
  await expect(page.getByRole("button",{name:"Shopper Registration"})).toBeVisible();
  await expect(page.getByRole("button",{name:"Content Creator Registration"})).toBeVisible();
  await expect(page.getByRole("button",{name:"Business Owner Registration"})).toBeVisible();

  await page.evaluate(()=>localStorage.clear());await page.goto("/");
  await page.getByLabel("Phone Number").fill(process.env.E2E_ADMIN_EMAIL!);
  await page.getByLabel("Password").fill(password);
  await page.locator("form").getByRole("button",{name:"Sign In",exact:true}).click();
  await expect(page).toHaveURL(/\/admin/);
  await expect(page.getByText("Platform Admin")).toBeVisible();
});
