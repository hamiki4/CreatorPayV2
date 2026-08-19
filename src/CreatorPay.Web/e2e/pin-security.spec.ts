import { expect, test } from "@playwright/test";

const api=process.env.E2E_API_URL!, password=process.env.E2E_SHOPPER_PASSWORD!;
const initialPin="12345",newPin="54321";

test.setTimeout(420_000);

async function clearSession(page:any){await page.evaluate(()=>{localStorage.removeItem("creatorpay_access_token");localStorage.removeItem("creatorpay_refresh_token")});}
async function openSignIn(page:any){const button=page.getByRole('button',{name:'Already have an account? Sign In'});if(await button.isVisible())await button.click()}
async function fillPin(page:any,label:string,value:string){const entries=page.locator('label.pin-entry input');if(await entries.count()===2){await entries.nth(/confirm/i.test(label)?1:0).fill(value);return}await page.getByRole('textbox',{name:label,exact:true}).fill(value)}

test("public authentication UX stays compact and verification-free",async({page})=>{
  await page.goto("/");await openSignIn(page);await expect(page.getByRole("heading",{name:"Sign In"})).toBeVisible();await expect(page.getByText("Welcome back. Keep shopping, promoting, and earning with Weymela.")).toBeVisible();
  await page.getByRole("button",{name:"Forgot Password"}).click();await expect(page.getByRole("heading",{name:"Forgot Password"})).toBeVisible();await expect(page.getByRole("button",{name:"Request Password Reset"})).toBeVisible();await expect(page.getByLabel("Day")).toHaveCount(0);await expect(page.getByText(/Weymela Support/)).toBeVisible();await expect(page.getByText(/verification code|OTP|Firebase|SMS/i)).toHaveCount(0);expect(await page.evaluate(()=>document.documentElement.scrollWidth<=document.documentElement.clientWidth)).toBeTruthy();
  await page.getByRole("button",{name:"Back to Sign In"}).click();await page.getByRole("button",{name:"Sign Up to Weymela"}).click();await expect(page.getByText("Welcome to Weymela — where shoppers save, creators earn, and businesses grow.")).toBeVisible();await expect(page.getByRole("button",{name:"Sign Up to Weymela"})).toHaveCount(0);await expect(page.getByRole("button",{name:"Back to Sign In"})).toBeVisible();expect(await page.evaluate(()=>document.documentElement.scrollWidth<=document.documentElement.clientWidth)).toBeTruthy();
});

test("simplified phone, PIN recovery, lockout, signup, and admin flow",async({page,request},testInfo)=>{
  const profile=testInfo.project.name;
  const phone=profile==="mobile"?"+251977100002":"+251977100001";
  const registered=await request.post(`${api}/api/v1/customers/register`,{data:{displayName:`${profile} auth user`,email:null,phoneNumber:phone,password,confirmation:password}});
  expect(registered.status()).toBe(201);

  await page.goto("/");await openSignIn(page);
  await expect(page.getByRole("heading",{name:"Sign In"})).toBeVisible();
  await expect(page.getByText("Welcome back. Keep shopping, promoting, and earning with Weymela.")).toBeVisible();
  await page.getByRole("button",{name:"Sign Up to Weymela"}).click();
  await expect(page.getByText("Welcome to Weymela — where shoppers save, creators earn, and businesses grow.")).toBeVisible();
  await expect(page.getByRole("button",{name:"Sign Up to Weymela"})).toHaveCount(0);
  await expect(page.getByRole("button",{name:"Back to Sign In"})).toBeVisible();await page.getByRole("button",{name:"Back to Sign In"}).click();
  await page.getByRole("button",{name:"Forgot Password"}).click();await expect(page.getByRole("heading",{name:"Forgot Password"})).toBeVisible();await expect(page.getByText("Request help resetting your password.")).toBeVisible();await expect(page.getByRole("button",{name:"Request Password Reset"})).toBeVisible();await expect(page.getByText(/Weymela Support/)).toBeVisible();await expect(page.getByText(/verification code|OTP|Firebase|SMS/i)).toHaveCount(0);expect(await page.evaluate(()=>document.documentElement.scrollWidth<=document.documentElement.clientWidth)).toBeTruthy();await page.getByRole("button",{name:"Back to Sign In"}).click();
  await page.getByLabel("Phone Number").fill(phone);
  await page.getByRole('textbox',{name:'Password'}).fill(password);
  await page.locator("form").getByRole("button",{name:"Sign In",exact:true}).click();
  await expect(page.getByRole("heading",{name:"Create 5-digit PIN"})).toBeVisible();
  await fillPin(page,"Create 5-digit PIN",initialPin);
  await fillPin(page,"Confirm PIN",initialPin);
  await page.getByRole("button",{name:"Create PIN"}).click();
  await expect(page.getByRole("navigation",{name:"Shopper navigation"})).toBeVisible();

  await clearSession(page);await page.goto("/");await openSignIn(page);
  await expect(page.getByRole("heading",{name:"Enter your 5-digit PIN"})).toBeVisible();
  await expect(page.getByText(/Firebase|verification email/i)).toHaveCount(0);
  await fillPin(page,"5-digit PIN",initialPin);
  await page.locator("form").getByRole("button",{name:"Sign In",exact:true}).click();
  await expect(page).toHaveURL(/\/shopper/);

  await clearSession(page);await page.goto("/");await openSignIn(page);
  await expect(page.getByRole("button",{name:"Forgot PIN"})).toBeVisible();

  await page.getByRole("button",{name:"Forgot PIN"}).click();
  await expect(page.getByRole("heading",{name:"Reset 5-digit PIN"})).toBeVisible();
  await page.getByLabel("Password",{exact:true}).fill(password);
  await fillPin(page,"New PIN",newPin);await fillPin(page,"Confirm PIN",newPin);
  await page.getByRole("button",{name:"Reset PIN"}).click();
  await expect(page.getByText("PIN reset. Sign in with your new PIN.")).toBeVisible();
  await page.getByRole("button",{name:"Back"}).click();
  await fillPin(page,"5-digit PIN",initialPin);await page.locator("form").getByRole("button",{name:"Sign In",exact:true}).click();
  await expect(page.getByRole("alert")).toContainText("Invalid phone number or PIN.");
  await page.waitForTimeout(1100);
  await fillPin(page,"5-digit PIN",newPin);await page.locator("form").getByRole("button",{name:"Sign In",exact:true}).click();
  await expect(page).toHaveURL(/\/shopper/);

  await clearSession(page);await page.goto("/");await openSignIn(page);

  await page.getByRole("button",{name:"Sign Up to Weymela"}).click();
  await expect(page.getByRole("heading",{name:"Sign Up to Weymela"})).toBeVisible();
  await expect(page.getByRole("button",{name:"Shopper Registration"})).toBeVisible();
  await expect(page.getByRole("button",{name:"Content Creator Registration"})).toBeVisible();
  await expect(page.getByRole("button",{name:"Business Owner Registration"})).toBeVisible();

  await page.evaluate(()=>localStorage.clear());await page.goto("/");await openSignIn(page);
  await page.getByLabel("Phone Number").fill(process.env.E2E_ADMIN_EMAIL!);
  await page.getByRole('textbox',{name:'Password'}).fill(password);
  await page.locator("form").getByRole("button",{name:"Sign In",exact:true}).click();
  await expect(page).toHaveURL(/\/admin/);
  await expect(page.getByRole("navigation",{name:"Admin navigation"})).toBeVisible();await expect(page.getByRole("heading",{name:"Dashboard"})).toBeVisible();
});
