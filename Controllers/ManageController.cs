using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using StoreFront2.Models;
using StoreFront2.Data;
using System.Collections.Generic;
using System.Data.Entity;
using StoreFront2.Helpers;
using Kendo.Mvc.UI;
using Kendo.Mvc.Extensions;
using StoreFront2.ViewModels;
using Newtonsoft.Json;
using System.IO;
using System.Data;
using ExcelDataReader;
using System.Net.Mail;

namespace StoreFront2.Controllers
{
    [Authorize]
    public class ManageController : Controller
    {
        private ApplicationSignInManager _signInManager;
        private ApplicationUserManager _userManager;

        private StoreFront2Entities _sfDb = new StoreFront2Entities();

        private Site _site = new Site();

        private string _userName = "";
        private AspNetUser _userSf;
        private IQueryable<int> _sfIdList;
        private List<string> _userRoleList;

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
                if (sf == null) sf = _sfDb.StoreFronts.Where(s => s.StoreFrontName == "Ems").FirstOrDefault();
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

        public ManageController()
        {
        }

        public ManageController(ApplicationUserManager userManager, ApplicationSignInManager signInManager)
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

        //
        // GET: /Manage/Index (Shopper / My Profile)
        public async Task<ActionResult> Index()
        {
            try
            {
                var userId = User.Identity.GetUserId();
                AspNetUser selectedAspNetUser = (from u in _sfDb.AspNetUsers
                                                 where u.Id == userId
                                                 select u).FirstOrDefault();

                SystemSetting systemSetting = _sfDb.SystemSettings.Where(ss => ss.StoreFrontId == _site.StoreFrontId).FirstOrDefault();
                UserSetting selectedSetting = _sfDb.UserSettings.Where(us => us.StoreFrontId == _site.StoreFrontId && us.AspNetUserId == userId).FirstOrDefault();
                UserGroupUser userGroupUser = _sfDb.UserGroupUsers.Where(y => y.AspNetUserId == userId && y.StoreFrontId == _site.StoreFrontId).FirstOrDefault();

                var model = new IndexViewModel
                {
                    Id = selectedAspNetUser.Id,
                    SfId = selectedAspNetUser.SfId,
                    UserName = selectedAspNetUser.UserName,
                    HasPassword = HasPassword(),
                    PhoneNumber = await UserManager.GetPhoneNumberAsync(userId),
                    TwoFactor = await UserManager.GetTwoFactorEnabledAsync(userId),
                    Logins = await UserManager.GetLoginsAsync(userId),
                    BrowserRemembered = await AuthenticationManager.TwoFactorBrowserRememberedAsync(userId),
                    Company = selectedAspNetUser.Company,
                    CompanyAlias = selectedAspNetUser.CompanyAlias,
                    FirstName = selectedAspNetUser.FirstName,
                    LastName = selectedAspNetUser.LastName,
                    Address1 = selectedAspNetUser.Address1,
                    Address2 = selectedAspNetUser.Address2,
                    City = selectedAspNetUser.City,
                    State = selectedAspNetUser.State,
                    Zip = selectedAspNetUser.Zip,
                    Phone = selectedAspNetUser.Phone,
                    AlertOrderReceived = selectedSetting.AlertOrderReceived == 1,
                    AlertOrderShipped = selectedSetting.AlertOrderShipped == 1,
                    BudgetLimit = 0,
                    BudgetCurrentTotal = 0,
                    OrdersCountingAgainstBudget = 0,
                    //BudgetCurrentAvailable = Math.Max(selectedSetting.BudgetLimit - selectedSetting.BudgetCurrentTotal, 0),
                    BudgetCurrentAvailable = 0,
                    BudgetDaysUntilRefresh = 0,
                };

                //int numberOfOrdersCountedAgainstBudget = 0;
                //DateTime lastRefreshDate = DateTime.Now.Date;
                //lastRefreshDate = selectedSetting.BudgetLastResetDate.Date;
                //List<Order> ordersCounted = _sfDb.Orders.Where(o => o.DateCreated >= lastRefreshDate && o.UserId == _site.AspNetUserId && o.StoreFrontId == _site.StoreFrontId).ToList();
                //numberOfOrdersCountedAgainstBudget = ordersCounted.Count();             

                // Get User Budget Refresh Remaining Days
                //model.BudgetDaysUntilRefresh = BudgetFunctions.GetRemainingBudgetDaysUntilRefresh(_site.StoreFrontId, userId, systemSetting.BudgetRefreshSystemWide, systemSetting.BudgetNextRefreshDate, selectedSetting.BudgetNextResetDate);
                //model.OrdersCountingAgainstBudget = numberOfOrdersCountedAgainstBudget;

                if (systemSetting.BudgetEnforce == 1 && systemSetting.BudgetType == "Group Based Budget" && userGroupUser != null)
                {
                    UserGroup userGroup = _sfDb.UserGroups.Where(x => x.Id == userGroupUser.UserGroupId && x.StoreFrontId == _site.StoreFrontId).FirstOrDefault();
                    DateTime groupBudgetRefreshDate = userGroup.TimeRefresh ?? DateTime.Now;
                    int numberOfOrdersCountedAgainstBudget = 0;
                    DateTime lastRefreshDate = userGroup.DateCreated.Value.Date;
                    List<Order> ordersCounted = _sfDb.Orders.Where(o => o.DateCreated >= lastRefreshDate && o.UserId == _site.AspNetUserId && o.StoreFrontId == _site.StoreFrontId).ToList();
                    numberOfOrdersCountedAgainstBudget = ordersCounted.Count();

                    //model.BudgetLimit = BudgetFunctions.GetTotalGroupBudget(_site.StoreFrontId, selectedSetting.Id.ToString(), userGroupUser);
                    model.BudgetLimit = BudgetFunctions.GetTotalGroupBudget(_site.StoreFrontId, userGroupUser.AspNetUserId);
                    //model.BudgetRefreshDate = groupBudgetRefreshDate;
                    model.BudgetDaysUntilRefresh = BudgetFunctions.GetRemainingBudgetDaysUntilRefresh(_site.StoreFrontId, selectedSetting.Id.ToString(), systemSetting.BudgetRefreshSystemWide, systemSetting.BudgetNextRefreshDate, groupBudgetRefreshDate);
                    model.BudgetCurrentTotal = userGroup.PriceLimit - userGroup.CurrentBudgetLeft ?? 0;
                    model.BudgetCurrentAvailable = userGroup.CurrentBudgetLeft ?? 0;
                    model.OrdersCountingAgainstBudget = numberOfOrdersCountedAgainstBudget;
                }

                else if (((systemSetting.BudgetEnforce == 1) && (systemSetting.BudgetType == "User Based Budget")) ||
                     ((systemSetting.BudgetEnforce == 1) && (systemSetting.BudgetType == "Group Based Budget") && (userGroupUser == null)) ||
                     (systemSetting.BudgetEnforce != 1))
                {
                    int numberOfOrdersCountedAgainstBudget = 0;
                    DateTime lastRefreshDate = selectedSetting.BudgetLastResetDate.Date;
                    List<Order> ordersCounted = _sfDb.Orders.Where(o => o.DateCreated >= lastRefreshDate && o.UserId == _site.AspNetUserId && o.StoreFrontId == _site.StoreFrontId).ToList();
                    numberOfOrdersCountedAgainstBudget = ordersCounted.Count();

                    //DateTime userBudgetRefreshDate = new DateTime(selectedSetting.BudgetNextResetDate.Year, selectedSetting.BudgetNextResetDate.Month, selectedSetting.BudgetNextResetDate.Day);
                    model.BudgetLimit = selectedSetting.BudgetLimit;
                    //model.BudgetRefreshDate = userBudgetRefreshDate;
                    model.BudgetDaysUntilRefresh = BudgetFunctions.GetRemainingBudgetDaysUntilRefresh(_site.StoreFrontId, selectedSetting.Id.ToString(), systemSetting.BudgetRefreshSystemWide, systemSetting.BudgetNextRefreshDate, selectedSetting.BudgetNextResetDate);
                    model.BudgetCurrentTotal = selectedSetting.BudgetCurrentTotal;
                    model.BudgetCurrentAvailable = Math.Max(selectedSetting.BudgetLimit - selectedSetting.BudgetCurrentTotal, 0);
                    model.OrdersCountingAgainstBudget = numberOfOrdersCountedAgainstBudget;
                }

                else
                {
                    model.BudgetLimit = 0;
                    //model.BudgetRefreshDate = new DateTime(today.Year, 1, 1).Date;
                    model.BudgetDaysUntilRefresh = 0;
                    model.BudgetCurrentTotal = 0;
                    model.BudgetCurrentAvailable = 0;
                    model.OrdersCountingAgainstBudget = 0;
                }



                ViewBag.Countries = new SelectList(_sfDb.Countries.ToList(), "Code", "Name");
                return View(model);
            }
            catch (Exception ex)
            {
                return View("Error", new HandleErrorInfo(ex, "Home", "Index"));
            }
        }



        [TokenAuthorize]
        public ActionResult UsedGroup()
        {
            try
            {
                return View();
            }
            catch (Exception ex)
            {
                return View("Error", new HandleErrorInfo(ex, "Home", "Index"));
            }
        }

        // GET: Admin/ReadUsers
        [TokenAuthorize]
        public ActionResult ReadGroupOfUsers([DataSourceRequest] DataSourceRequest request)
        {
            try
            {
                var groupQueryList = (from ug in _sfDb.UserGroupUsers
                                      join u in _sfDb.UserGroups on ug.UserGroupId equals u.Id
                                      where ug.AspNetUserId == _userSf.Id
                                      select new GroupsViewModel()
                                      {
                                          Id = u.Id,
                                          Name = u.Name,
                                          Desc = u.Desc,
                                          DateCreated = u.DateCreated,
                                          UserId = u.UserId,
                                          UserName = u.UserName,
                                          PriceLimit = u.PriceLimit.Value,
                                          //  TotalNumberOfMembers = _sfDb.UserGroups.Sum(t => t.Id),
                                          TimeRefresh = u.TimeRefresh
                                      }
                               ).ToList();
                var queryUser = _sfDb.AspNetUsers.Where(x => x.Id == _userSf.Id).FirstOrDefault();
                if (queryUser.UserGroupId != null)
                {
                    groupQueryList.Where(x => x.Id == queryUser.UserGroupId).FirstOrDefault().DefaultGroupId = queryUser.UserGroupId;
                }

                return Json(groupQueryList.ToDataSourceResult(request));
            }
            catch (Exception ex)
            {
                return Json(new { message = "Error : " + ex.Message });
            }
        }


        public ActionResult SetDefaultGroupToUser(int groupId)
        {
            try
            {
                var query = _sfDb.AspNetUsers.Where(x => x.Id == _userSf.Id).FirstOrDefault();
                if (query != null)
                {
                    query.UserGroupId = groupId;
                    _sfDb.SaveChanges();
                }
                return Json(new { message = "Successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { message = "Error : " + ex.Message });
            }
        }

        //
        // POST: /Manage/Index (Shopper / My Profile)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Index(IndexViewModel model)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var entity = (from u in _sfDb.AspNetUsers
                                  where u.Id == model.Id
                                  select u).FirstOrDefault();

                    entity.UserName = model.UserName;
                    entity.Company = model.Company;
                    entity.CompanyAlias = model.CompanyAlias;
                    entity.FirstName = model.FirstName;
                    entity.LastName = model.LastName;
                    entity.Address1 = model.Address1;
                    entity.Address2 = model.Address2;
                    entity.City = model.City;
                    entity.State = model.State;
                    entity.Zip = model.Zip;
                    entity.Phone = model.Phone;

                    _sfDb.AspNetUsers.Attach(entity);
                    _sfDb.Entry(entity).State = EntityState.Modified;
                    await _sfDb.SaveChangesAsync();
                }
                model.HasPassword = HasPassword();

                return View(model);
            }
            catch (Exception ex)
            {
                return View("Error", new HandleErrorInfo(ex, "Home", "Index"));
            }
        }

        // GET: Manage/GetCountries
        [TokenAuthorize]
        public ActionResult GetCountries()
        {
            List<SelectListItem> countries = new List<SelectListItem>();
            countries.AddRange(_sfDb.Countries.Where(c => c.Code == "US").Select(c => new SelectListItem() { Text = c.Name, Value = c.Code }).OrderBy(c => c.Text).ToList());
            countries.AddRange(_sfDb.Countries.Where(c => c.Code != "US").Select(c => new SelectListItem() { Text = c.Name, Value = c.Code }).OrderBy(c => c.Text).ToList());
            //SelectList countriesOld = new SelectList(_sfDb.Countries.OrderBy(c => c.Name).ToList(), "Code", "Name");

            return Json(countries, JsonRequestBehavior.AllowGet);
        }

        // GET: Manage/UserAddresses_Read
        [TokenAuthorize]
        public ActionResult UserAddresses_Read([DataSourceRequest] DataSourceRequest request, int? selectedAddressId)
        {
            try
            {
                List<UserAddressViewModel> userAddresses = new List<UserAddressViewModel>();
                userAddresses = _sfDb.UserAddresses.Where(ua => ua.StoreFrontId == _site.StoreFrontId
                                                                && ua.AspNetUserId == _userSf.Id
                                                                && (((selectedAddressId ?? 0) != 0) ? ua.Id != selectedAddressId : true)).Select(ua => new UserAddressViewModel()
                                                                {
                                                                    Id = ua.Id,
                                                                    StoreFrontId = ua.StoreFrontId,
                                                                    AspNetUserId = ua.AspNetUserId,
                                                                    AddressAlias = ua.AddressAlias,
                                                                    Company = ua.Company,
                                                                    CompanyAlias = ua.CompanyAlias,
                                                                    FirstName = ua.FirstName,
                                                                    LastName = ua.LastName,
                                                                    Address1 = ua.Address1,
                                                                    Address2 = ua.Address2,
                                                                    City = ua.City,
                                                                    State = ua.State,
                                                                    Zip = ua.Zip,
                                                                    Country = ua.Country,
                                                                    Phone = ua.Phone,
                                                                    Email = ua.Email,
                                                                    DefaultShipTo = ua.DefaultShipTo == 1 ? true : false,
                                                                }).ToList();

                DataSourceResult modelList = userAddresses.ToDataSourceResult(request, ua => new UserAddressViewModel()
                {
                    Id = ua.Id,
                    StoreFrontId = ua.StoreFrontId,
                    AspNetUserId = ua.AspNetUserId,
                    AddressAlias = ua.AddressAlias,
                    Company = ua.Company,
                    CompanyAlias = ua.CompanyAlias,
                    FirstName = ua.FirstName,
                    LastName = ua.LastName,
                    Address1 = ua.Address1,
                    Address2 = ua.Address2,
                    City = ua.City,
                    State = ua.State,
                    Zip = ua.Zip,
                    Country = ua.Country,
                    Phone = ua.Phone,
                    Email = ua.Email,
                    DefaultShipTo = ua.DefaultShipTo,
                });

                return Json(modelList);
            }
            catch (Exception ex)
            {
                return Json(new { message = "Error : " + ex.Message });
            }
        }

        // GET: Manage/UserAddresses_Create
        [TokenAuthorize]
        [HttpPost]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult UserAddresses_Create([DataSourceRequest] DataSourceRequest request, UserAddressViewModel userAddress)
        {
            bool noerror = true;

            // Existing?
            UserAddress selectedUserAddress = _sfDb.UserAddresses.FirstOrDefault(ua => ua.AddressAlias == userAddress.AddressAlias && ua.AspNetUserId == _userSf.Id && ua.StoreFrontId == _site.StoreFrontId);
            if (selectedUserAddress != null)
            {
                ModelState.AddModelError("AddressAlias", "Duplicate alias, please enter different alias");
            }

            try
            {
                if (userAddress != null && ModelState.IsValid)
                {
                    // Default to this one
                    if (userAddress.DefaultShipTo)
                    {
                        List<UserAddress> listUserAddress = _sfDb.UserAddresses.Where(ua => ua.AspNetUserId == _userSf.Id && ua.StoreFrontId == _site.StoreFrontId).Select(ua => ua).ToList();
                        if (listUserAddress.Count > 0)
                            foreach (UserAddress ua in listUserAddress)
                                ua.DefaultShipTo = 0;
                    }

                    UserAddress newUserAddress = new UserAddress()
                    {
                        StoreFrontId = _site.StoreFrontId,
                        AspNetUserId = _userSf.Id,
                        AddressAlias = userAddress.AddressAlias,
                        Company = userAddress.Company,
                        CompanyAlias = userAddress.CompanyAlias,
                        FirstName = userAddress.FirstName,
                        LastName = userAddress.LastName,
                        Address1 = userAddress.Address1,
                        Address2 = userAddress.Address2,
                        City = userAddress.City,
                        State = userAddress.State,
                        Zip = userAddress.Zip,
                        Country = userAddress.Country,
                        Phone = userAddress.Phone,
                        Email = userAddress.Email,
                        DefaultShipTo = userAddress.DefaultShipTo ? 1 : 0,
                    };

                    _sfDb.UserAddresses.Add(newUserAddress);

                    if (noerror)
                    {
                        _sfDb.SaveChanges();
                        GlobalFunctions.Log(_site.StoreFrontId, _userSf.Id, "", "User Address Created", userAddress.AddressAlias);
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", errors = ex.Message });
            }

            return Json(new[] { userAddress }.AsQueryable().ToDataSourceResult(request, ModelState), JsonRequestBehavior.AllowGet);
        }

        // GET: Manage/UserAddresses_Update
        [TokenAuthorize]
        [HttpPost]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult UserAddresses_Update([DataSourceRequest] DataSourceRequest request, UserAddressViewModel userAddress)
        {
            bool noerror = true;
            try
            {
                if (userAddress != null && ModelState.IsValid)
                {
                    // Default to this one
                    if (userAddress.DefaultShipTo)
                    {
                        List<UserAddress> listUserAddress = _sfDb.UserAddresses.Where(ua => ua.AspNetUserId == _userSf.Id && ua.StoreFrontId == _site.StoreFrontId).Select(ua => ua).ToList();
                        if (listUserAddress.Count > 0)
                            foreach (UserAddress ua in listUserAddress)
                                ua.DefaultShipTo = 0;
                    }

                    UserAddress selectedUserAddress = _sfDb.UserAddresses.FirstOrDefault(ua => ua.Id == userAddress.Id);
                    if (selectedUserAddress != null)
                    {
                        selectedUserAddress.Company = userAddress.Company;
                        selectedUserAddress.CompanyAlias = userAddress.CompanyAlias;
                        selectedUserAddress.FirstName = userAddress.FirstName;
                        selectedUserAddress.LastName = userAddress.LastName;
                        selectedUserAddress.Address1 = userAddress.Address1;
                        selectedUserAddress.Address2 = userAddress.Address2;
                        selectedUserAddress.City = userAddress.City;
                        selectedUserAddress.State = userAddress.State;
                        selectedUserAddress.Zip = userAddress.Zip;
                        selectedUserAddress.Country = userAddress.Country;
                        selectedUserAddress.Phone = userAddress.Phone;
                        selectedUserAddress.Email = userAddress.Email;
                        selectedUserAddress.DefaultShipTo = userAddress.DefaultShipTo ? 1 : 0;
                    };

                    _sfDb.Entry(selectedUserAddress).State = EntityState.Modified;
                    if (noerror)
                    {
                        _sfDb.SaveChanges();
                        GlobalFunctions.Log(_site.StoreFrontId, _userSf.Id, "", "User Address Modified", selectedUserAddress.AddressAlias);
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", errors = ex.Message });
            }

            return Json(new[] { userAddress }.ToDataSourceResult(request, ModelState));
        }

        // GET: Manage/UserAddresses_Destroy
        [TokenAuthorize]
        [HttpPost]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult UserAddresses_Destroy([DataSourceRequest] DataSourceRequest request, UserAddressViewModel userAddress)
        {
            bool noerror = true;
            try
            {
                if (userAddress != null && ModelState.IsValid)
                {
                    UserAddress selectedUserAddress = _sfDb.UserAddresses.FirstOrDefault(ua => ua.Id == userAddress.Id);
                    if (selectedUserAddress != null)
                    {
                        _sfDb.UserAddresses.Remove(selectedUserAddress);
                    };

                    if (noerror)
                    {
                        _sfDb.SaveChanges();
                        GlobalFunctions.Log(_site.StoreFrontId, _userSf.Id, "", "User Address Removed", selectedUserAddress.AddressAlias);
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", errors = ex.Message });
            }

            return Json(new[] { userAddress }.ToDataSourceResult(request, ModelState));
        }

        [TokenAuthorize]
        public ActionResult ImportNewUserAddresses()
        {
            return View();
        }

        [TokenAuthorize]
        public ActionResult Read_NewUserAddresses_Grid([DataSourceRequest] DataSourceRequest request, string fileName)
        {
            try
            {
                List<UserAddressViewModel> usersNew = new List<UserAddressViewModel>();
                DataSourceResult result = new DataSourceResult();

                if (fileName == null || fileName.Length == 0) return Json(result);
                string filePath = Path.GetFileName(fileName); ; // @"c:\Franchisees.xlsx";
                string physicalPath = Path.Combine(Server.MapPath("~/Content/" + _site.StoreFrontName + "/Files"), filePath);

                DataSet rawResult;

                // https://github.com/ExcelDataReader/ExcelDataReader
                using (var stream = System.IO.File.Open(physicalPath, FileMode.Open, FileAccess.Read))
                {
                    using (var reader = ExcelReaderFactory.CreateReader(stream))
                    {
                        rawResult = reader.AsDataSet(new ExcelDataSetConfiguration()
                        {
                            ConfigureDataTable = (_) => new ExcelDataTableConfiguration()
                            {
                                UseHeaderRow = true
                            }
                        });
                    }
                }

                var workSheet = rawResult.Tables["shiptolist"];
                var rows = from DataRow a in workSheet.Rows select a;
                var cols = workSheet.Columns;
                var passwordHasher = new PasswordHasher();
                foreach (DataRow row in rows)
                {
                    string[] name = row.ItemArray[5].ToString().Split(' ');
                    string firstName = name[0];
                    string lastName = "";
                    if (name.Length == 2) lastName = name[1];
                    if (name.Length > 2) lastName = name[2];

                    usersNew.Add(new UserAddressViewModel()
                    {
                        UserName = row.ItemArray[0].ToString(),
                        AddressAlias = row.ItemArray[1].ToString(),
                        Company = row.ItemArray[2].ToString(),
                        CompanyAlias = row.ItemArray[3].ToString(),
                        FirstName = row.ItemArray[4].ToString(),
                        LastName = row.ItemArray[5].ToString(),
                        Address1 = row.ItemArray[6].ToString(),
                        Address2 = row.ItemArray[7].ToString(),
                        City = row.ItemArray[8].ToString(),
                        State = row.ItemArray[9].ToString(),
                        Zip = row.ItemArray[10].ToString(),
                        Country = row.ItemArray[11].ToString(),
                        Phone = row.ItemArray[12].ToString(),
                        Email = row.ItemArray[13].ToString(),
                        DefaultShipTo = row.ItemArray[14].ToString() == "1" ? true : false,
                    });
                }

                result = usersNew.ToDataSourceResult(request, u => new UserAddressViewModel()
                {
                    Id = u.Id,
                    UserName = u.UserName,
                    AddressAlias = u.AddressAlias,
                    Email = u.Email,
                    Company = u.Company,
                    CompanyAlias = u.CompanyAlias,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Address1 = u.Address1,
                    Address2 = u.Address2,
                    City = u.City,
                    State = u.State,
                    Zip = u.Zip,
                    Country = u.Country,
                    Phone = u.Phone,
                    DefaultShipTo = u.DefaultShipTo,
                });

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { message = "Error : " + ex.Message });
            }
        }

        // GET: Manage/UserAddresses_Create
        [TokenAuthorize]
        [HttpPost]
        public ActionResult UserAddresses_Add(UserAddressViewModel userAddress)
        {
            bool noerror = true;

            AspNetUser user = _sfDb.AspNetUsers.FirstOrDefault(u => u.UserName == userAddress.UserName);

            // Existing?
            if (user != null)
            {
                UserAddress selectedUserAddress = _sfDb.UserAddresses.FirstOrDefault(ua => ua.AddressAlias == userAddress.AddressAlias && ua.AspNetUserId == user.Id && ua.StoreFrontId == _site.StoreFrontId);
                if (selectedUserAddress != null)
                {
                    ModelState.AddModelError("AddressAlias", "Duplicate alias, please enter different alias");
                }
            }
            else ModelState.AddModelError("User", userAddress.UserName + " : UserName Not Found");

            if (userAddress == null) ModelState.AddModelError("UserAddress", "Null User Address Data");

            try
            {
                if (user != null && userAddress != null && ModelState.IsValid)
                {
                    // Default to this one
                    if (userAddress.DefaultShipTo)
                    {
                        List<UserAddress> listUserAddress = _sfDb.UserAddresses.Where(ua => ua.AspNetUserId == user.Id && ua.StoreFrontId == _site.StoreFrontId).Select(ua => ua).ToList();
                        if (listUserAddress.Count > 0)
                            foreach (UserAddress ua in listUserAddress)
                                ua.DefaultShipTo = 0;
                    }

                    UserAddress newUserAddress = new UserAddress()
                    {
                        StoreFrontId = _site.StoreFrontId,
                        AspNetUserId = user.Id,
                        AddressAlias = userAddress.AddressAlias,
                        Company = userAddress.Company,
                        CompanyAlias = userAddress.CompanyAlias,
                        FirstName = userAddress.FirstName,
                        LastName = userAddress.LastName,
                        Address1 = userAddress.Address1,
                        Address2 = userAddress.Address2,
                        City = userAddress.City,
                        State = userAddress.State,
                        Zip = userAddress.Zip,
                        Country = userAddress.Country,
                        Phone = userAddress.Phone,
                        Email = userAddress.Email,
                        DefaultShipTo = userAddress.DefaultShipTo ? 1 : 0,
                    };

                    _sfDb.UserAddresses.Add(newUserAddress);

                    if (noerror)
                    {
                        _sfDb.SaveChanges();
                        GlobalFunctions.Log(_site.StoreFrontId, _userSf.Id, "", "Import User Address for : " + user.UserName, userAddress.AddressAlias);
                    }
                }
                else
                {
                    string errorMessage = "";
                    foreach (var value in ModelState.Values)
                    {
                        foreach (var error in value.Errors)
                        {
                            errorMessage += error.ErrorMessage + "\r\n";
                        }
                    }
                    return Json(new { result = "error", message = userAddress.AddressAlias + " : " + errorMessage });
                }
            }
            catch (Exception ex)
            {
                return Json(new { result = "error", errors = ex.Message });
            }

            return Json(new { result = "success", message = "Success" });
        }

        //
        // POST: /Manage/RemoveLogin (Not used currently)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RemoveLogin(string loginProvider, string providerKey)
        {
            ManageMessageId? message;
            var result = await UserManager.RemoveLoginAsync(User.Identity.GetUserId(), new UserLoginInfo(loginProvider, providerKey));
            if (result.Succeeded)
            {
                var user = await UserManager.FindByIdAsync(User.Identity.GetUserId());
                if (user != null)
                {
                    await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
                }
                message = ManageMessageId.RemoveLoginSuccess;
            }
            else
            {
                message = ManageMessageId.Error;
            }
            return RedirectToAction("ManageLogins", new { Message = message });
        }

        //
        // GET: /Manage/AddPhoneNumber (Not used currently)
        public ActionResult AddPhoneNumber()
        {
            return View();
        }

        //
        // POST: /Manage/AddPhoneNumber (Not used currently)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> AddPhoneNumber(AddPhoneNumberViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            // Generate the token and send it
            var code = await UserManager.GenerateChangePhoneNumberTokenAsync(User.Identity.GetUserId(), model.Number);
            if (UserManager.SmsService != null)
            {
                var message = new IdentityMessage
                {
                    Destination = model.Number,
                    Body = "Your security code is: " + code
                };
                await UserManager.SmsService.SendAsync(message);
            }
            return RedirectToAction("VerifyPhoneNumber", new { PhoneNumber = model.Number });
        }

        //
        // POST: /Manage/EnableTwoFactorAuthentication (Not used currently)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> EnableTwoFactorAuthentication()
        {
            await UserManager.SetTwoFactorEnabledAsync(User.Identity.GetUserId(), true);
            var user = await UserManager.FindByIdAsync(User.Identity.GetUserId());
            if (user != null)
            {
                await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
            }
            return RedirectToAction("Index", "Manage");
        }

        //
        // POST: /Manage/DisableTwoFactorAuthentication (Not used currently)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DisableTwoFactorAuthentication()
        {
            await UserManager.SetTwoFactorEnabledAsync(User.Identity.GetUserId(), false);
            var user = await UserManager.FindByIdAsync(User.Identity.GetUserId());
            if (user != null)
            {
                await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
            }
            return RedirectToAction("Index", "Manage");
        }

        //
        // GET: /Manage/VerifyPhoneNumber (Not used currently)
        public async Task<ActionResult> VerifyPhoneNumber(string phoneNumber)
        {
            var code = await UserManager.GenerateChangePhoneNumberTokenAsync(User.Identity.GetUserId(), phoneNumber);
            // Send an SMS through the SMS provider to verify the phone number
            return phoneNumber == null ? View("Error") : View(new VerifyPhoneNumberViewModel { PhoneNumber = phoneNumber });
        }

        //
        // POST: /Manage/VerifyPhoneNumber (Not used currently)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> VerifyPhoneNumber(VerifyPhoneNumberViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            var result = await UserManager.ChangePhoneNumberAsync(User.Identity.GetUserId(), model.PhoneNumber, model.Code);
            if (result.Succeeded)
            {
                var user = await UserManager.FindByIdAsync(User.Identity.GetUserId());
                if (user != null)
                {
                    await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
                }
                return RedirectToAction("Index", new { Message = ManageMessageId.AddPhoneSuccess });
            }
            // If we got this far, something failed, redisplay form
            ModelState.AddModelError("", "Failed to verify phone");
            return View(model);
        }

        //
        // POST: /Manage/RemovePhoneNumber (Not used currently)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RemovePhoneNumber()
        {
            var result = await UserManager.SetPhoneNumberAsync(User.Identity.GetUserId(), null);
            if (!result.Succeeded)
            {
                return RedirectToAction("Index", new { Message = ManageMessageId.Error });
            }
            var user = await UserManager.FindByIdAsync(User.Identity.GetUserId());
            if (user != null)
            {
                await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
            }
            return RedirectToAction("Index", new { Message = ManageMessageId.RemovePhoneSuccess });
        }

        //
        // GET: /Manage/ChangePassword
        public ActionResult ChangePassword()
        {
            return View();
        }

        //
        // POST: /Manage/ChangePassword
        [HttpPost]
        [TokenAuthorize]
        public async Task<ActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    if (Request.IsAjaxRequest())
                        return Json(new { result = "Error", message = "Password Removal Error" });
                    else
                        return View(model);
                }
                var result = await UserManager.ChangePasswordAsync(User.Identity.GetUserId(), model.OldPassword, model.NewPassword);
                if (result.Succeeded)
                {
                    var user = await UserManager.FindByIdAsync(User.Identity.GetUserId());
                    if (user != null)
                    {
                        await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
                    }

                    // update user
                    AspNetUser usersf = _sfDb.AspNetUsers.Where(u => u.Id == user.Id).FirstOrDefault();
                    usersf.LastPasswordChangedDate = DateTime.Now.Date;
                    usersf.IsPasswordTemporary = 0;
                    //_sfDb.Entry(usersf).State = EntityState.Modified;
                    _sfDb.SaveChanges();

                    if (Request.IsAjaxRequest())
                        return Json(new { result = "Success", message = "Password Changed Successfully" });
                    else
                        return RedirectToAction("Index", new { Message = ManageMessageId.ChangePasswordSuccess });
                }
                else
                {
                    if (Request.IsAjaxRequest())
                        return Json(new { result = "Error", message = "Error", errorlist = result.Errors });
                    else
                        return View(model);
                }
                //return View(model);
            }
            catch (Exception ex)
            {
                if (Request.IsAjaxRequest())
                    return Json(new { result = "Error", message = ex.Message });
                else
                    return View("Error", new HandleErrorInfo(ex, "Dashboard", "MyWindow"));
            }
        }

        //
        // GET: /Manage/SetPassword
        public ActionResult SetPassword()
        {
            return View();
        }

        //
        // POST: /Manage/SetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SetPassword(SetPasswordViewModel model)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var result = await UserManager.AddPasswordAsync(User.Identity.GetUserId(), model.NewPassword);
                    if (result.Succeeded)
                    {
                        var user = await UserManager.FindByIdAsync(User.Identity.GetUserId());
                        if (user != null)
                        {
                            await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
                        }
                        return RedirectToAction("Index", new { Message = ManageMessageId.SetPasswordSuccess });
                    }
                    AddErrors(result);
                }

                // If we got this far, something failed, redisplay form
                return View(model);
            }
            catch (Exception ex)
            {
                return View("Error", new HandleErrorInfo(ex, "Dashboard", "MyWindow"));
            }
        }

        [TokenAuthorize]
        [HttpPost]
        public async Task<ActionResult> UserBudgetRefreshNowRequest(IndexViewModel user)
        {
            try
            {
                List<string> emailSentList = new List<string>();

                //****** admins <----- WRONG !!!!
                List<AspNetUser> admins = (from u in _sfDb.AspNetUsers
                                           join us in _sfDb.UserSettings on u.Id equals us.AspNetUserId
                                           where us.StoreFrontId == _site.StoreFrontId && us.AlertOnBudgetRefreshRequest == 1
                                           select u).ToList();

                string baseUrl = string.Format("{0}://{1}", Request.Url.Scheme, Request.Url.Authority);
                string strDeepLink = baseUrl + "/Admin/UserDetail/" + user.SfId.ToString() + "?tab=permissions";

                // email the users
                //Microsoft.AspNet.Identity.IdentityMessage messageObject = new Microsoft.AspNet.Identity.IdentityMessage()
                //{
                string Subject = "";
                string Body = "";
                Subject = "BUDGET REFRESH REQUEST";            
                Body = "You have received a new budget refresh request from: " + user.FirstName + " " + user.LastName + ". <br />" +
                "<br />" +
                "Budget Snapshot For (" + user.FirstName + " " + user.LastName + "): <br />" +
                "Max Budget: " + (user.BudgetLimit).ToString("c") + "<br />" +
                "Budget Used: " + (user.BudgetCurrentTotal).ToString("c") + "<br />" +
                "No. of Orders Counting Against Budget: " + (user.OrdersCountingAgainstBudget).ToString("n0") + "<br />" +
                "Current Available Budget: " + (user.BudgetCurrentAvailable).ToString("c") + "<br />" +
                "Days Until Budget Refresh: " + user.BudgetDaysUntilRefresh.ToString("n0") + "<br />" +
                "<br />" +
                "To refresh this users budget you may follow this link or access the user details page. <br />" +
                @" - <a href=""" + strDeepLink + @""">CLICK HERE</a><br>" +
                "<br>" +
                "**** PLEASE NOTE: THIS IS AN AUTOMATED EMAIL - PLEASE DO NOT REPLY ****</p>";
                //};

                string emailFrom = _sfDb.SystemSettings.Where(ss => ss.StoreFrontId == _site.StoreFrontId).FirstOrDefault().AlertFromEmail;

                MailMessage mail = new MailMessage();
                mail.From = new MailAddress(emailFrom);
                mail.Subject = Subject;
                mail.Body = Body;
                mail.IsBodyHtml = true;
                SmtpClient smtp = new SmtpClient();
                smtp.Host = "mail.c2devgrp.com";
                smtp.Port = 25;
                smtp.UseDefaultCredentials = false;
                smtp.Credentials = new System.Net.NetworkCredential("alerts@c2devgrp.com", "atl@sn0rth"); // Enter seders User name and password                                                                                                                    
                smtp.EnableSsl = false; //smtp.EnableSsl = true;

                mail.To.Clear();
                foreach (AspNetUser u in admins)
                {
                    mail.To.Add(u.Email);
                }

                if (mail.To.Count >= 1)
                {
                    smtp.Send(mail);
                    await Task.Delay(200);
                }
                // Email admin for the request
                /*foreach (AspNetUser u in admins)
                {
                    if ((u.Email ?? "").Length > 0)
                    {
                        messageObject.Destination = u.Email.ToLower();
                        if (!emailSentList.Contains(messageObject.Destination)) await SmtpEmailService.SendAsync(messageObject, emailFrom);
                        emailSentList.Add(messageObject.Destination);
                        await Task.Delay(200);
                    }
                }*/

                return Json(new { message = "Success" });
            }
            catch (Exception ex)
            {
                return Json(new { message = "Error : ", errordetail = ex.Message });
            }
        }

        //
        // GET: /Manage/ManageLogins (Not used currently)
        public async Task<ActionResult> ManageLogins(ManageMessageId? message)
        {
            ViewBag.StatusMessage =
                message == ManageMessageId.RemoveLoginSuccess ? "The external login was removed."
                : message == ManageMessageId.Error ? "An error has occurred."
                : "";
            var user = await UserManager.FindByIdAsync(User.Identity.GetUserId());
            if (user == null)
            {
                return View("Error");
            }
            var userLogins = await UserManager.GetLoginsAsync(User.Identity.GetUserId());
            var otherLogins = AuthenticationManager.GetExternalAuthenticationTypes().Where(auth => userLogins.All(ul => auth.AuthenticationType != ul.LoginProvider)).ToList();
            ViewBag.ShowRemoveButton = user.PasswordHash != null || userLogins.Count > 1;
            return View(new ManageLoginsViewModel
            {
                CurrentLogins = userLogins,
                OtherLogins = otherLogins
            });
        }

        //
        // POST: /Manage/LinkLogin (Not used currently)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LinkLogin(string provider)
        {
            // Request a redirect to the external login provider to link a login for the current user
            return new AccountController.ChallengeResult(provider, Url.Action("LinkLoginCallback", "Manage"), User.Identity.GetUserId());
        }





        //
        // GET: /Manage/LinkLoginCallback (Not used currently)
        public async Task<ActionResult> LinkLoginCallback()
        {
            var loginInfo = await AuthenticationManager.GetExternalLoginInfoAsync(XsrfKey, User.Identity.GetUserId());
            if (loginInfo == null)
            {
                return RedirectToAction("ManageLogins", new { Message = ManageMessageId.Error });
            }
            var result = await UserManager.AddLoginAsync(User.Identity.GetUserId(), loginInfo.Login);
            return result.Succeeded ? RedirectToAction("ManageLogins") : RedirectToAction("ManageLogins", new { Message = ManageMessageId.Error });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _userManager != null)
            {
                _userManager.Dispose();
                _userManager = null;
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

        private bool HasPassword()
        {
            var user = UserManager.FindById(User.Identity.GetUserId());
            if (user != null)
            {
                return user.PasswordHash != null;
            }
            return false;
        }

        private bool HasPhoneNumber()
        {
            var user = UserManager.FindById(User.Identity.GetUserId());
            if (user != null)
            {
                return user.PhoneNumber != null;
            }
            return false;
        }

        public enum ManageMessageId
        {
            AddPhoneSuccess,
            ChangePasswordSuccess,
            SetTwoFactorSuccess,
            SetPasswordSuccess,
            RemoveLoginSuccess,
            RemovePhoneSuccess,
            Error
        }

        #endregion
    }
}
