using System;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Cookies;
using StoreFront2.Models;
using StoreFront2.Data;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.IO;
using Newtonsoft.Json;
using System.Web.Routing;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Data.Entity;
using System.Net.Http;
using System.Net.Http.Headers;
using StoreFront2.Helpers;
using System.Security.Principal;

namespace StoreFront2.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private ApplicationSignInManager _signInManager;
        private ApplicationUserManager _userManager;
        private ApplicationDbContext _identityDb;
        private Site _site = new Site();

        private string _userName = "";
        private AspNetUser _userSf;
        private IQueryable<int> _sfIdList;
        private List<string> _userRoleList;

        private StoreFront2Entities _sfDb = new StoreFront2Entities();

        public AccountController()
        {
            _identityDb = new ApplicationDbContext();
        }

        protected override void Initialize(RequestContext requestContext)
        {
            base.Initialize(requestContext);

            if (Request != null)
            {
                // Check the requesting Url
                //var request = System.Web.HttpContext.Current.Request;
                string baseUrl = new Uri(Request.Url, Url.Content("~")).AbsoluteUri;

                // Restore or create Site values
                StoreFront sf = _sfDb.StoreFronts.Where(s => s.BaseUrl == baseUrl).FirstOrDefault();
                if (sf == null) sf = _sfDb.StoreFronts.Where(s => s.StoreFrontName == "ImpDynamics").FirstOrDefault();
                _site = (Site)Session["Site"];
                if (_site == null)
                {
                    _site = new Site();
                    _site.StoreFrontId = sf.Id;
                    _site.StoreFrontName = sf.StoreFrontName;
                    _site.AdminAsShopper = false;
                    _site.LayoutPath = sf.LayoutPath;
                    _site.StylePath = sf.StylePath;
                    _site.SiteIcon = sf.SiteIcon;
                    _site.SiteTitle = sf.SiteTitle;
                    _site.SiteFooter = sf.SiteFooter;
                    _site.SiteAuth = new UserPermission();
                    _site.SiteUserSetting = new UserSetting();
                }
                _site.Setting = _sfDb.SystemSettings.Where(s => s.StoreFrontId == sf.Id).FirstOrDefault();

                // Retrieve this logged in user record
                _userName = User.Identity.Name;
                if (_userName != null && _userName.Length > 0)
                {
                    _userSf = _sfDb.AspNetUsers.Where(u => u.UserName == _userName).FirstOrDefault();

                    _sfIdList = from usf in _sfDb.UserStoreFronts
                                where usf.AspNetUserId == _userSf.Id
                                select usf.StoreFrontId;
                    _userRoleList = _sfDb.AspNetRoles.Where(t => t.AspNetUsers.Any(u => u.UserName == _userName)).Select(r => r.Name).ToList();

                    // Retrieve existing cart items
                    List<Cart> existingCartItems = (from c in _sfDb.Carts
                                                    where c.UserId == _userSf.Id && c.StoreFrontId == _site.StoreFrontId && c.Count > 0
                                                    select c).ToList();
                    Session["Cart"] = existingCartItems;
                    ViewBag.CartItemCount = existingCartItems.Count();
                    Session["CartItemCount"] = ViewBag.CartItemCount;

                    // requery permissions
                    var query = (from up in _sfDb.UserPermissions
                                 where up.AspNetUserId == _userSf.Id && up.StoreFrontId == _site.StoreFrontId
                                 select up).FirstOrDefault();
                    if (query == null)
                    {
                        query = new UserPermission()
                        {
                            AdminUserModify = 0,
                            AdminSettingModify = 0,
                            InventoryItemModify = 0,
                            InventoryRestrictCategory = 0,
                            InventoryCategoryModify = 0,
                            OrderRestrictShipMethod = 0,
                            OrderCreate = 0,
                            OrderCancel = 0,
                        };
                    }
                    _site.SiteAuth = query;

                    // query user setting
                    UserSetting userSetting = _sfDb.UserSettings.Where(us => us.AspNetUserId == _userSf.Id && us.StoreFrontId == sf.Id).FirstOrDefault();
                    if (userSetting == null) userSetting = new UserSetting() { BudgetCurrentTotal = 0, BudgetIgnore = 0, BudgetLimit = 0, BudgetResetInterval = 0, BudgetLastResetDate = new DateTime(1, 1, 1), BudgetNextResetDate = new DateTime(1, 1, 1) };

                    _site.SiteUserSetting = userSetting;

                    _site.IsVendor = _userSf.IsVendor == 1;
                    _site.IsPunchOutUser = _userSf.IsPunchOutUser == 1;
                }

                Session["Site"] = _site;

            }
        }

        public AccountController(ApplicationUserManager userManager, ApplicationSignInManager signInManager)
        {
            UserManager = userManager;
            SignInManager = signInManager;
        }

        public ApplicationSignInManager SignInManager
        {
            get
            {
                return _signInManager ?? HttpContext.GetOwinContext().Get<ApplicationSignInManager>();
            }
            private set
            {
                _signInManager = value;
            }
        }

        public ApplicationUserManager UserManager
        {
            get
            {
                return _userManager ?? HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
            }
            private set
            {
                _userManager = value;
            }
        }

        [AllowAnonymous]
        public ActionResult c2(string accesscode)
        {
            string url = Request.UrlReferrer == null ? "" : Request.UrlReferrer.ToString();
            if (accesscode == "rita")
            {
                return View();
            }
            else
            {
                return View("Error", new HandleErrorInfo(new Exception("Non-authorized access from [" + url + "]"), "Account", "Login"));
            }
        }

        //
        // GET: /Account/Login
        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            ViewBag.SiteTitle = _site.SiteTitle;
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        //
        // GET: /Account/Login
        [AllowAnonymous]
        public async Task<ActionResult> AutoLogin(string key)
        {
            // From PunchOut Request, it will have a key
            if (key != null && key.Length > 0)
            {
                string[] keyValues = key.Split('~');
                string currencyFlag = keyValues[0].Substring(0, 1);
                string userId = keyValues[0].Substring(1, keyValues[0].Length - 1);
                string buyerCookie = keyValues[1];

                var user = await UserManager.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (user != null)
                {
                    await SignInManager.SignInAsync(user, false, false);

                    _site.IsPunchOutUser = true;
                    _site.AdminAsShopper = true;
                    _site.CurrencyFlag = Convert.ToInt16(currencyFlag);
                    _site.BuyerCookie = buyerCookie;
                    Session["Site"] = _site;

                    GlobalFunctions.Log(_site.StoreFrontId, user.Id, "AutoLogin", "Request.UserHostAddress", Request.UserHostAddress);
                    GlobalFunctions.Log(_site.StoreFrontId, user.Id, "AutoLogin", "BuyerCookie", buyerCookie);

                    // Clear cart
                    List<Cart> prevCartItems = (from c in _sfDb.Carts where c.UserId == userId && c.StoreFrontId == _site.StoreFrontId && c.CartId == buyerCookie select c).ToList();
                    foreach (Cart c in prevCartItems)
                    {
                        _sfDb.Carts.Remove(c);
                    }
                    _sfDb.SaveChanges();

                    return RedirectToAction("ProductList", "Order");
                }
                else
                    return RedirectToAction("Login");
            }
            else
                return RedirectToAction("Login");

        }

        //
        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        //[ValidateAntiForgeryToken]
        public async Task<ActionResult> Login(LoginViewModel model, string returnUrl, string key)
        {
            try
            {
                Site _site = (Site)Session["Site"];

                if (!ModelState.IsValid)
                {
                    return View(model);
                }
                SignInStatus result = await SignInManager.PasswordSignInAsync(model.UserName, model.Password, model.RememberMe, shouldLockout: false);

                // 8/22/19 -PS- Token retrieval and storage

                // 8/26/19 -PS- Also sign in to the MVC local account
                // This doesn't count login failures towards account lockout
                // To enable password failures to trigger account lockout, change to shouldLockout: true
                switch (result)
                {
                    case SignInStatus.Success:

                        // store user id in session
                        AspNetUser user = _sfDb.AspNetUsers.Where(u => u.UserName == model.UserName).FirstOrDefault();
                        _site.AspNetUserId = user != null ? user.Id : "";

                        List<int> usfIdList = _sfDb.UserStoreFronts.Where(usf => usf.AspNetUserId == _site.AspNetUserId).Select(usf => usf.StoreFrontId).ToList();

                        if (!usfIdList.Contains(_site.StoreFrontId))
                        {
                            ModelState.AddModelError("", "Invalid Username or Password");
                            AuthenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
                            return View(model);
                        }

                        if (user.EnforcePasswordChange == 1)
                        {
                            if (user.LastPasswordChangedDate != null)
                            {
                                DateTime lastDate = user.LastPasswordChangedDate ?? DateTime.Now.Date.AddDays(-30);
                                if (lastDate.AddDays(30) < DateTime.Now.Date)
                                {
                                    return RedirectToAction("ChangePassword", "Manage");
                                }
                            }
                        }

                        if (user.SkipUrlReferrerChk == 1) { }
                        else
                        {
                            string url = Request.UrlReferrer == null ? "" : Request.UrlReferrer.ToString();
                            // If Rita franchise only allow from ritasfranchises.com
                            if (usfIdList.Contains(1)) // Rita Storefront
                            {
                                var uri = new Uri(url);
                                var allowedList = _sfDb.UrlReferrers.Where(u => u.Url == uri.Host + (uri.Port.ToString().Length > 0 ? ":" + uri.Port : ""));
                                if (allowedList == null || allowedList.Count() == 0)
                                {
                                    user.AccessRestricted = 1;
                                    ModelState.AddModelError("", "Please access from CoolNet");
                                    AuthenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
                                }
                            }
                        }

                        //check User's Default currency
                        UserSetting userSetting1 = _sfDb.UserSettings.Where(us => us.AspNetUserId == _site.AspNetUserId && us.StoreFrontId == _site.StoreFrontId).FirstOrDefault();
                        if (userSetting1.DefaultCurrency == "CAD")
                        {
                            _site.CurrencyFlag = 2;
                        }
                        else
                        {
                            _site.CurrencyFlag = 1;
                        }

                        if (user.AccessRestricted == 1)
                        {
                            ModelState.AddModelError("", "Restricted Access");
                            ViewBag.Url = Request.UrlReferrer;
                            return View(model);
                        }

                        // Vendor vs User login
                        if (user.IsVendor == 1)
                        {
                            if (returnUrl == null || returnUrl.Length == 0 || returnUrl == "/") returnUrl = "/Home/VendorIndex";
                        }
                        else
                        {

                            // Clear cart
                            List<Cart> prevCartItems = (from c in _sfDb.Carts where c.UserId == _site.AspNetUserId && c.StoreFrontId == _site.StoreFrontId select c).ToList();
                            foreach (Cart c in prevCartItems)
                            {
                                _sfDb.Carts.Remove(c);
                            }

                            // Check if time to clear budget
                            UserSetting userSetting = _sfDb.UserSettings.Where(us => us.AspNetUserId == _site.AspNetUserId).FirstOrDefault();
                            SystemSetting systemSetting = _sfDb.SystemSettings.Where(ss => ss.StoreFrontId == _site.StoreFrontId).FirstOrDefault();
                            if (userSetting != null)
                            {
                                DateTime nextResetDate;
                                if (systemSetting.BudgetRefreshSystemWide == 1)
                                {
                                    nextResetDate = new DateTime(systemSetting.BudgetRefreshLastResetDate.Year + 1, systemSetting.BudgetRefreshStartDate.Month, systemSetting.BudgetRefreshStartDate.Day);

                                    if (nextResetDate.Date <= DateTime.Now.Date)
                                    {
                                        systemSetting.BudgetRefreshLastResetDate = DateTime.Now.Date;
                                        _sfDb.Entry(systemSetting).State = EntityState.Modified;

                                        // Reset also for all users, unless it has been reset today already
                                        List<UserSetting> allUserSetting = _sfDb.UserSettings.Where(us => us.StoreFrontId == _site.StoreFrontId).ToList();
                                        foreach (UserSetting us in allUserSetting)
                                        {
                                            if (us.BudgetLastResetDate.Date != DateTime.Now.Date)
                                            {
                                                us.BudgetLastResetDate = DateTime.Now.Date;
                                                us.BudgetCurrentTotal = 0;
                                                _sfDb.Entry(us).State = EntityState.Modified;
                                            }
                                        }
                                    }
                                }

                                // Always reset the user budget at the interval specified, unless it has been reset today already
                                //nextResetDate = userSetting.BudgetLastResetDate.AddDays(Convert.ToDouble(userSetting.BudgetResetInterval));
                                //if (nextResetDate.Date <= DateTime.Now.Date && userSetting.BudgetLastResetDate.Date != DateTime.Now.Date && systemSetting.BudgetRefreshSystemWide == 1)
                                //{
                                //    userSetting.BudgetLastResetDate = DateTime.Now.Date;
                                //    userSetting.BudgetCurrentTotal = 0;
                                //    _sfDb.Entry(userSetting).State = EntityState.Modified;
                                //}

                            }

                            _sfDb.SaveChanges();
                        }

                        //Session["Site"] = _site;

                        return RedirectToLocal(returnUrl);
                    case SignInStatus.LockedOut:
                        return View("Lockout");
                    case SignInStatus.RequiresVerification:
                        return RedirectToAction("SendCode", new { ReturnUrl = returnUrl, RememberMe = model.RememberMe });
                    case SignInStatus.Failure:
                    default:
                        ModelState.AddModelError("", "Invalid login attempt.");
                        return View(model);
                }
            }
            catch (Exception ex)
            {
                return View("Error", new HandleErrorInfo(ex, "Account", "Login"));
            }
        }

        // 
        // Get Token from WebApi (Not used currently)
        public async Task<string> GetToken(string username, string password)
        {
            //WebRequest request = WebRequest.Create("http://localhost:60221/Token");
            WebRequest request = WebRequest.Create("https://ritas.sgdistribution.com/Token");
            request.Method = "POST";
            request.ContentType = "application/json";
            string postJson = String.Format("grant_type={0}&UserName={1}&Password={2}", "password", username, password);
            byte[] bytes = Encoding.UTF8.GetBytes(postJson);
            using (Stream stream = await request.GetRequestStreamAsync())
            {
                stream.Write(bytes, 0, bytes.Length);
            }

            try
            {
                HttpWebResponse httpResponse = (HttpWebResponse)(await request.GetResponseAsync());
                string json;
                using (Stream responseStream = httpResponse.GetResponseStream())
                {
                    json = new StreamReader(responseStream).ReadToEnd();
                }
                json = json.Replace(".issued", "issued");
                json = json.Replace(".expires", "expires");
                TokenResponseModel tokenResponse = JsonConvert.DeserializeObject<TokenResponseModel>(json);

                int indexGMT = tokenResponse.expires.IndexOf("GMT");
                tokenResponse.expires = tokenResponse.expires.Substring(0, indexGMT - 1);
                DateTime expiresUtcDt = DateTime.ParseExact(tokenResponse.expires, "ddd, dd MMM yyyy HH:mm:ss", CultureInfo.InvariantCulture);
                string expiresString = expiresUtcDt.ToLocalTime().ToString();

                return tokenResponse.access_token + ":" + expiresString;
            }
            catch (Exception ex)
            {
                return "Failure : " + ex.Message;
            }

        }

        //
        // GET: /Account/VerifyCode (Not used currently)
        [AllowAnonymous]
        public async Task<ActionResult> VerifyCode(string provider, string returnUrl, bool rememberMe)
        {
            // Require that the user has already logged in via username/password or external login
            if (!await SignInManager.HasBeenVerifiedAsync())
            {
                return View("Error");
            }
            return View(new VerifyCodeViewModel { Provider = provider, ReturnUrl = returnUrl, RememberMe = rememberMe });
        }

        //
        // POST: /Account/VerifyCode (Not used currently)
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> VerifyCode(VerifyCodeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // The following code protects for brute force attacks against the two factor codes. 
            // If a user enters incorrect codes for a specified amount of time then the user account 
            // will be locked out for a specified amount of time. 
            // You can configure the account lockout settings in IdentityConfig
            var result = await SignInManager.TwoFactorSignInAsync(model.Provider, model.Code, isPersistent: model.RememberMe, rememberBrowser: model.RememberBrowser);
            switch (result)
            {
                case SignInStatus.Success:
                    return RedirectToLocal(model.ReturnUrl);
                case SignInStatus.LockedOut:
                    return View("Lockout");
                case SignInStatus.Failure:
                default:
                    ModelState.AddModelError("", "Invalid code.");
                    return View(model);
            }
        }

        //
        // GET: /Account/Register
        [AllowAnonymous]
        public ActionResult Register()
        {
            ViewBag.Name = new SelectList(_identityDb.Roles.Where(u => !u.Name.Contains("Admin")).ToList(), "Name", "Name");
            return View();
        }

        //
        // POST: /Account/Register
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Register(RegisterViewModel model)
        {
            try
            {
                AspNetUser userCheck = _sfDb.AspNetUsers.Where(u => u.UserName == model.UserName).FirstOrDefault();
                if (userCheck != null)
                {
                    ModelState.AddModelError("UserName", "Username is not available");
                }

                if (ModelState.IsValid)
                {
                    var user = new ApplicationUser { UserName = model.UserName, Email = model.Email };
                    var result = await UserManager.CreateAsync(user, model.Password);
                    if (result.Succeeded)
                    {
                        await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);

                        // For more information on how to enable account confirmation and password reset please visit http://go.microsoft.com/fwlink/?LinkID=320771
                        // Send an email with this link
                        // string code = await UserManager.GenerateEmailConfirmationTokenAsync(user.Id);
                        // var callbackUrl = Url.Action("ConfirmEmail", "Account", new { userId = user.Id, code = code }, protocol: Request.Url.Scheme);
                        // await UserManager.SendEmailAsync(user.Id, "Confirm your account", "Please confirm your account by clicking <a href=\"" + callbackUrl + "\">here</a>");

                        await this.UserManager.AddToRoleAsync(user.Id, model.UserRole); // 9/2/19 -PS- add roles

                        // Add permission
                        UserPermission up = new UserPermission()
                        {
                            AspNetUserId = user.Id,
                            AdminSettingModify = 0,
                            AdminUserModify = 0,
                            InventoryItemModify = 0,
                            InventoryRestrictCategory = 0,
                            InventoryCategoryModify = 0,
                            OrderCreate = 1
                        };
                        _sfDb.UserPermissions.Add(up);

                        // Add to UserStoreFronts
                        UserStoreFront usf = new UserStoreFront()
                        {
                            AspNetUserId = user.Id,
                            StoreFrontId = 1
                        };
                        _sfDb.UserStoreFronts.Add(usf);

                        _sfDb.SaveChanges();

                        return RedirectToAction("Index", "Home");
                    }
                    AddErrors(result);
                }

                // If we got this far, something failed, redisplay form
                ViewBag.Name = new SelectList(_identityDb.Roles.Where(u => !u.Name.Contains("Admin")).ToList(), "Name", "Name");
                return View(model);
            }
            catch (Exception ex)
            {
                return View("Error", new HandleErrorInfo(ex, "Account", "Login"));
            }

        }

        //
        // GET: /Account/ConfirmEmail (Not used currently)
        [AllowAnonymous]
        public async Task<ActionResult> ConfirmEmail(string userId, string code)
        {
            if (userId == null || code == null)
            {
                return View("Error");
            }
            var result = await UserManager.ConfirmEmailAsync(userId, code);
            return View(result.Succeeded ? "ConfirmEmail" : "Error");
        }

        //
        // GET: /Account/ForgotPassword
        [AllowAnonymous]
        public ActionResult ForgotPassword()
        {
            return View();
        }

        //
        // POST: /Account/ForgotPassword
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var user = await UserManager.FindByEmailAsync(model.Email);
                    if (user == null || !(await UserManager.IsEmailConfirmedAsync(user.Id)))
                    {
                        // Don't reveal that the user does not exist or is not confirmed
                        return View("ForgotPasswordConfirmation");
                    }

                    // For more information on how to enable account confirmation and password reset please visit http://go.microsoft.com/fwlink/?LinkID=320771
                    // Send an email with this link
                    string code = await UserManager.GeneratePasswordResetTokenAsync(user.Id);
                    var callbackUrl = Url.Action("ResetPassword", "Account", new { userId = user.Id, code = code }, protocol: Request.Url.Scheme);
                    await UserManager.SendEmailAsync(user.Id, _site.SiteTitle + " Online Ordering Portal – Password Reset",
                        _site.SiteTitle + " Online Ordering Portal – Password Reset <br />" +
                        "*** <br />" +
                        "Someone has requested to reset the password associated with this email address. <br />" +
                        "If you wish to proceed, please follow the link below. <br />" +
                        "<br />" +
                        "<a href=\"" + callbackUrl + "\">Password Reset Link</a><br />" +
                        "<br />" +
                        "If you did not make this request, please disregard this email or contact your corporate office for assistance. <br />" +
                        "<br />" +
                        "*** <br />" +
                        "NOTE: THIS IS AN AUTOMATED EMAIL – PLEASE DO NOT REPLY <br />" +
                        "***");
                    return RedirectToAction("ForgotPasswordConfirmation", "Account");
                }

                // If we got this far, something failed, redisplay form
                return View(model);
            }
            catch (Exception ex)
            {
                return View("Error", new HandleErrorInfo(ex, "Home", "Index"));
            }
        }

        //
        // GET: /Account/ForgotPasswordConfirmation
        [AllowAnonymous]
        public ActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        //
        // GET: /Account/ResetPassword
        [AllowAnonymous]
        public ActionResult ResetPassword(string code)
        {
            return code == null ? View("Error") : View();
        }

        //
        // POST: /Account/ResetPassword
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return View(model);
                }
                var user = await UserManager.FindByNameAsync(model.UserName);
                if (user == null)
                {
                    // Don't reveal that the user does not exist
                    return RedirectToAction("ResetPasswordConfirmation", "Account");
                }
                var result = await UserManager.ResetPasswordAsync(user.Id, model.Code, model.Password);
                if (result.Succeeded)
                {
                    return RedirectToAction("ResetPasswordConfirmation", "Account");
                }
                AddErrors(result);
                return View();
            }
            catch (Exception ex)
            {
                return View("Error", new HandleErrorInfo(ex, "Home", "Index"));
            }
        }

        //
        // GET: /Account/ResetPasswordConfirmation
        [AllowAnonymous]
        public ActionResult ResetPasswordConfirmation()
        {
            return View();
        }

        //
        // POST: /Account/ExternalLogin (Not used currently)
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult ExternalLogin(string provider, string returnUrl)
        {
            // Request a redirect to the external login provider
            return new ChallengeResult(provider, Url.Action("ExternalLoginCallback", "Account", new { ReturnUrl = returnUrl }));
        }

        //
        // GET: /Account/SendCode (Not used currently)
        [AllowAnonymous]
        public async Task<ActionResult> SendCode(string returnUrl, bool rememberMe)
        {
            var userId = await SignInManager.GetVerifiedUserIdAsync();
            if (userId == null)
            {
                return View("Error");
            }
            var userFactors = await UserManager.GetValidTwoFactorProvidersAsync(userId);
            var factorOptions = userFactors.Select(purpose => new SelectListItem { Text = purpose, Value = purpose }).ToList();
            return View(new SendCodeViewModel { Providers = factorOptions, ReturnUrl = returnUrl, RememberMe = rememberMe });
        }

        //
        // POST: /Account/SendCode (Not used currently)
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SendCode(SendCodeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View();
            }

            // Generate the token and send it
            if (!await SignInManager.SendTwoFactorCodeAsync(model.SelectedProvider))
            {
                return View("Error");
            }
            return RedirectToAction("VerifyCode", new { Provider = model.SelectedProvider, ReturnUrl = model.ReturnUrl, RememberMe = model.RememberMe });
        }

        //
        // GET: /Account/ExternalLoginCallback (Not used currently)
        [AllowAnonymous]
        public async Task<ActionResult> ExternalLoginCallback(string returnUrl)
        {
            var loginInfo = await AuthenticationManager.GetExternalLoginInfoAsync();
            if (loginInfo == null)
            {
                return RedirectToAction("Login");
            }

            // Sign in the user with this external login provider if the user already has a login
            var result = await SignInManager.ExternalSignInAsync(loginInfo, isPersistent: false);
            switch (result)
            {
                case SignInStatus.Success:
                    return RedirectToLocal(returnUrl);
                case SignInStatus.LockedOut:
                    return View("Lockout");
                case SignInStatus.RequiresVerification:
                    return RedirectToAction("SendCode", new { ReturnUrl = returnUrl, RememberMe = false });
                case SignInStatus.Failure:
                default:
                    // If the user does not have an account, then prompt the user to create an account
                    ViewBag.ReturnUrl = returnUrl;
                    ViewBag.LoginProvider = loginInfo.Login.LoginProvider;
                    return View("ExternalLoginConfirmation", new ExternalLoginConfirmationViewModel { Email = loginInfo.Email });
            }
        }

        //
        // POST: /Account/ExternalLoginConfirmation (Not used currently)
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ExternalLoginConfirmation(ExternalLoginConfirmationViewModel model, string returnUrl)
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Manage");
            }

            if (ModelState.IsValid)
            {
                // Get the information about the user from the external login provider
                var info = await AuthenticationManager.GetExternalLoginInfoAsync();
                if (info == null)
                {
                    return View("ExternalLoginFailure");
                }
                var user = new ApplicationUser { UserName = model.Email, Email = model.Email };
                var result = await UserManager.CreateAsync(user);
                if (result.Succeeded)
                {
                    result = await UserManager.AddLoginAsync(user.Id, info.Login);
                    if (result.Succeeded)
                    {
                        await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
                        return RedirectToLocal(returnUrl);
                    }
                }
                AddErrors(result);
            }

            ViewBag.ReturnUrl = returnUrl;
            return View(model);
        }

        //
        // POST: /Account/LogOff
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LogOff()
        {
            AuthenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
            HttpContext.User = new GenericPrincipal(new GenericIdentity(string.Empty), null);
            ////return RedirectToAction("Index", "Home");
            //return View();
            return RedirectToRoute(new { controller = "Account", action = "Login" });

        }

        //
        // GET: /Account/ExternalLoginFailure (Not used currently)
        [AllowAnonymous]
        public ActionResult ExternalLoginFailure()
        {
            return View();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_userManager != null)
                {
                    _userManager.Dispose();
                    _userManager = null;
                }

                if (_signInManager != null)
                {
                    _signInManager.Dispose();
                    _signInManager = null;
                }
            }

            base.Dispose(disposing);
        }

        #region Helpers
        // Used for XSRF protection when adding external logins
        private const string XsrfKey = "XsrfId";

        private IAuthenticationManager AuthenticationManager
        {
            get
            {
                return HttpContext.GetOwinContext().Authentication;
            }
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error);
            }
        }

        private ActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Home");
        }

        internal class ChallengeResult : HttpUnauthorizedResult
        {
            public ChallengeResult(string provider, string redirectUri)
                : this(provider, redirectUri, null)
            {
            }

            public ChallengeResult(string provider, string redirectUri, string userId)
            {
                LoginProvider = provider;
                RedirectUri = redirectUri;
                UserId = userId;
            }

            public string LoginProvider { get; set; }
            public string RedirectUri { get; set; }
            public string UserId { get; set; }

            public override void ExecuteResult(ControllerContext context)
            {
                var properties = new AuthenticationProperties { RedirectUri = RedirectUri };
                if (UserId != null)
                {
                    properties.Dictionary[XsrfKey] = UserId;
                }
                context.HttpContext.GetOwinContext().Authentication.Challenge(properties, LoginProvider);
            }
        }
        #endregion
    }
}
