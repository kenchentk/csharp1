using System;
using System.Linq;
using System.Data;
using System.Collections.Generic;
using System.Web.Mvc;
using System.Web.Routing;
using Kendo.Mvc.Extensions;
using StoreFront2.Data;
using StoreFront2.ViewModels;
using StoreFront2.Models;
using StoreFront2.Helpers;
using Kendo.Mvc.UI;
using System.Data.Entity;
using System.Threading.Tasks;
using Microsoft.AspNet.Identity;
using System.Web;
using Microsoft.AspNet.Identity.Owin;
using ExcelDataReader;
using System.IO;
using System.Diagnostics;
using System.Drawing;
using System.Net.Mail;
using System.Net;

namespace StoreFront2.Controllers
{

    public class AdminController : Controller
    {
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
                            AdminUserGroupModify = 0,
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

        private ApplicationSignInManager _signInManager;
        private ApplicationUserManager _userManager;
        private ApplicationDbContext _identityDb;

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

        public AdminController()
        {
            _identityDb = new ApplicationDbContext();
        }

        public AdminController(ApplicationUserManager userManager, ApplicationSignInManager signInManager)
        {
            _identityDb = new ApplicationDbContext();
            UserManager = userManager;
            SignInManager = signInManager;
        }

        //
        // POST: /Admin/CheckUserPermission/<aspnetuserid> (Not used currently)
        [TokenAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CheckUserPermission(string userId)
        {
            var query = (from up in _sfDb.UserPermissions
                         where up.AspNetUserId == userId && up.StoreFrontId == _site.StoreFrontId
                         select up).FirstOrDefault();

            return Json(query);
        }

        // POST: Admin/Index/5 (not used currently)
        [TokenAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Index(StoreFrontViewModel model)
        {
            if (ModelState.IsValid)
            {
                StoreFront selectedSF = _sfDb.StoreFronts.Find(model.Id);

                selectedSF.StoreFrontName = model.Name;
                selectedSF.CustomerServiceRep = model.CustomerServiceRep;
                selectedSF.OfficeNumber = model.OfficeNumber;
                selectedSF.OfficeHours = model.OfficeHours;
                selectedSF.Email = model.Email;
                selectedSF.BaseUrl = model.BaseUrl;
                selectedSF.LayoutPath = model.LayoutPath;
                selectedSF.SiteIcon = model.SiteIcon;
                selectedSF.SiteTitle = model.SiteTitle;
                selectedSF.LoginImage = model.LoginImage;
                selectedSF.SiteFooter = model.SiteFooter;
                selectedSF.TemplateId = model.TemplateId;

                _sfDb.Entry(selectedSF).State = EntityState.Modified;
                _sfDb.SaveChanges();
                return RedirectToAction("Index");
            }
            return View(model);
        }

        #region System Settings
        //
        // GET: /Admin/SystemSettings 
        public ActionResult SystemSettings()
        {
            try
            {
                var userId = User.Identity.GetUserId();
                var selectedAspNetUser = (from u in _sfDb.AspNetUsers
                                          where u.Id == userId
                                          select u).FirstOrDefault();

                var selectedSystemSetting = (from s in _sfDb.SystemSettings where s.StoreFrontId == _site.StoreFrontId select s).FirstOrDefault();

                var model = new SystemSettingViewModel
                {
                    FromCompany = selectedSystemSetting.FromCompany,
                    FromFirstName = selectedSystemSetting.FromFirstName,
                    FromLastName = selectedSystemSetting.FromLastName,
                    FromAddress1 = selectedSystemSetting.FromAddress1,
                    FromAddress2 = selectedSystemSetting.FromAddress2,
                    FromCity = selectedSystemSetting.FromCity,
                    FromState = selectedSystemSetting.FromState,
                    FromZip = selectedSystemSetting.FromZip,
                    FromCountry = selectedSystemSetting.FromCountry,
                    FromPhone = selectedSystemSetting.FromPhone,

                    DisplayProductPrices = selectedSystemSetting.DisplayProductPrices == 1,
                    DisplayOrderValues = selectedSystemSetting.DisplayOrderValues == 1,
                    DisplayOrderValuesFor = selectedSystemSetting.DisplayOrderValuesFor,
                    DisplayInventoryAvailability = selectedSystemSetting.DisplayInventoryAvailability == 1,
                    DisplayInventoryAvailabilityFor = selectedSystemSetting.DisplayInventoryAvailabilityFor == "Stock Status" ? "In Stock / Out of Stock Only" : "Actual Inventory for All Items",
                    TurnOnProductMinMaxLevels = selectedSystemSetting.TurnOnProductMinMaxLevels == 1,
                    TurnOnProductMinMaxLevelsFor = selectedSystemSetting.TurnOnProductMinMaxLevelsFor == "Enforce Per Item" ? "Enforce Per Item" : "Enforce Per Group",
                    DisableOutOfStockOrdering = selectedSystemSetting.DisableOutOfStockOrdering == 1,
                    BudgetType = selectedSystemSetting.BudgetType,
                    BudgetRefreshDayOfTheMonth = selectedSystemSetting.BudgetRefreshDayOfTheMonth.ToString(),
                    BudgetRefreshDayOfTheWeek = selectedSystemSetting.BudgetRefreshDayOfTheWeek,
                    DefaultBudgetRefreshFrequency = selectedSystemSetting.DefaultBudgetRefreshFrequency,
                    AlertOrderReceived = selectedSystemSetting.AlertOrderReceived == 1,
                    AlertOrderShipped = selectedSystemSetting.AlertOrderShipped == 1,
                    AlertBudgetRefreshed = selectedSystemSetting.AlertBudgetRefreshed == 1,
                    AlertItemOutOfStock = selectedSystemSetting.AlertItemOutOfStock == 1,
                    BudgetLimitDefault = selectedSystemSetting.BudgetLimitDefault,
                    BudgetRefreshPeriodDefault = selectedSystemSetting.BudgetRefreshPeriodDefault.ToString(),
                    BudgetEnforce = selectedSystemSetting.BudgetEnforce == 1,
                    BudgetRefreshSystemWide = selectedSystemSetting.BudgetRefreshSystemWide == 1,
                    BudgetRefreshPerUser = selectedSystemSetting.BudgetRefreshSystemWide == 0,
                    BudgetRefreshStartDate = selectedSystemSetting.BudgetRefreshStartDate,
                    BudgetNextRefreshDate = selectedSystemSetting.BudgetNextRefreshDate,
                    OnHold = selectedSystemSetting.AllOrdersOnHold == 1,
                    LogoPath = selectedSystemSetting.LogoPath == "" ? "/Content/" + _site.StoreFrontName + "/Images/Logo.jpg" : selectedSystemSetting.LogoPath,
                    LogoUploadDate = selectedSystemSetting.LogoUploadDate,
                };

                List<ShipCarrierViewModel> carriers = (from c in _sfDb.ShipCarriers
                                                       where c.StoreFrontId == _site.StoreFrontId
                                                       select new ShipCarrierViewModel()
                                                       {
                                                           Id = c.Id,
                                                           Name = c.Name,
                                                           Enabled = c.Enabled,
                                                       }).ToList();
                foreach (ShipCarrierViewModel carrier in carriers)
                {
                    List<ShipMethodViewModel> shipmethods = (from m in _sfDb.ShipMethods
                                                             where m.CarrierId == carrier.Id
                                                             select new ShipMethodViewModel()
                                                             {
                                                                 Id = m.Id,
                                                                 MethodName = m.MethodName,
                                                                 Code = m.Code,
                                                                 Domestic = m.Domestic,
                                                                 Enabled = m.Enabled,
                                                             }).ToList();
                    carrier.ShipMethods = shipmethods;
                }

                ViewBag.Days365 = Enumerable.Range(0, 366).Select(d => d.ToString());
                ViewBag.Days28 = Enumerable.Range(1, 29).Select(d => d.ToString());
                ViewBag.ShipCarriers = carriers;
                ViewBag.DisplayOrderValuesForList = new List<string>() { "Admins Only", "Everyone" };
                ViewBag.DisplayInventoryAvailabilityForList = new List<string>() { "In Stock / Out of Stock Only", "Actual Inventory for All Items" };
                ViewBag.TurnOnProductMinMaxLevelsForList = new List<string>() { "Enforce Per Item", "Enforce Per Group" };
                ViewBag.DisplayBudgetTypeList = new List<string>() { "Group Based Budget", "User Based Budget" };
                ViewBag.DisplayBudgetFrequencyList = new List<string>() { "Yearly", "Monthly", "Weekly" };
                ViewBag.DisplayBudgetRefreshDayOfTheWeekList = new List<string>() { "", "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
      
                return View(model);
            }
            catch (Exception ex)
            {
                return View("Error", new HandleErrorInfo(ex, "Home", "Index"));
            }
        }

        //
        // POST: /Manage/Index (Shopper / My Profile)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SystemSettings(SystemSettingViewModel model)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var entity2 = (from s in _sfDb.SystemSettings
                                   where s.StoreFrontId == _site.StoreFrontId
                                   select s).FirstOrDefault();

                    entity2.FromCompany = model.FromCompany;
                    entity2.FromFirstName = model.FromFirstName;
                    entity2.FromLastName = model.FromLastName;
                    entity2.FromAddress1 = model.FromAddress1;
                    entity2.FromAddress2 = model.FromAddress2;
                    entity2.FromCity = model.FromCity;
                    entity2.FromState = model.FromState;
                    entity2.FromZip = model.FromZip;
                    entity2.FromCountry = model.FromCountry;
                    entity2.FromPhone = model.FromPhone;
                      
                    entity2.DisplayProductPrices = model.DisplayProductPrices ? 1 : 0;
                    entity2.DisplayOrderValues = model.DisplayOrderValues ? 1 : 0;
                    entity2.DisplayOrderValuesFor = model.DisplayOrderValuesFor;
                    entity2.DisplayInventoryAvailability = model.DisplayInventoryAvailability ? 1 : 0;
                    entity2.DisplayInventoryAvailabilityFor = model.DisplayInventoryAvailabilityFor == "In Stock / Out of Stock Only" ? "Stock Status" : "Actual Inventory";
                    entity2.TurnOnProductMinMaxLevels = model.TurnOnProductMinMaxLevels ? 1 : 0;
                    entity2.TurnOnProductMinMaxLevelsFor = model.TurnOnProductMinMaxLevelsFor == "Enforce Per Item" ? "Enforce Per Item" : "Enforce Per Group";
                    entity2.DisableOutOfStockOrdering = model.DisableOutOfStockOrdering ? 1 : 0;
                    entity2.BudgetType = model.BudgetType;
                    entity2.BudgetRefreshDayOfTheMonth = Convert.ToInt16(model.BudgetRefreshDayOfTheMonth);
                    entity2.DefaultBudgetRefreshFrequency = model.DefaultBudgetRefreshFrequency;
                    entity2.BudgetRefreshDayOfTheWeek = model.BudgetRefreshDayOfTheWeek;

                    entity2.AlertOrderReceived = model.AlertOrderReceived ? 1 : 0;
                    entity2.AlertOrderShipped = model.AlertOrderShipped ? 1 : 0;
                    entity2.AlertBudgetRefreshed = model.AlertBudgetRefreshed ? 1 : 0;
                    entity2.AlertItemOutOfStock = model.AlertItemOutOfStock ? 1 : 0;
                    entity2.BudgetLimitDefault = model.BudgetLimitDefault;
                    entity2.BudgetRefreshPeriodDefault = Convert.ToInt16(model.BudgetRefreshPeriodDefault);
                    entity2.BudgetEnforce = model.BudgetEnforce ? 1 : 0;
                    entity2.BudgetRefreshSystemWide = model.BudgetRefreshSystemWide ? 1 : 0;
                    entity2.BudgetRefreshStartDate = model.BudgetRefreshStartDate;
                    entity2.BudgetNextRefreshDate = model.BudgetNextRefreshDate;

                    entity2.LogoPath = model.LogoPath;
                    entity2.LogoUploadDate = model.LogoUploadDate;
                    entity2.AllOrdersOnHold = Convert.ToInt16(model.OnHold);
                    _sfDb.Entry(entity2).State = EntityState.Modified;
                    _sfDb.SaveChanges();
                }

                List<ShipCarrierViewModel> carriers = (from c in _sfDb.ShipCarriers
                                                       where c.StoreFrontId == _site.StoreFrontId
                                                       select new ShipCarrierViewModel()
                                                       {
                                                           Id = c.Id,
                                                           Name = c.Name,
                                                       }).ToList();
                foreach (ShipCarrierViewModel carrier in carriers)
                {
                    List<ShipMethodViewModel> shipmethods = (from m in _sfDb.ShipMethods
                                                             where m.CarrierId == carrier.Id
                                                             select new ShipMethodViewModel()
                                                             {
                                                                 Id = m.Id,
                                                                 MethodName = m.MethodName,
                                                                 Code = m.Code,
                                                                 Domestic = m.Domestic,
                                                                 MaxWeight = m.MaxWeight,
                                                                 Enabled = m.Enabled
                                                             }).ToList();
                    carrier.ShipMethods = shipmethods;
                }

                ViewBag.Days365 = Enumerable.Range(0, 366).Select(d => d.ToString());
                ViewBag.Days28 = Enumerable.Range(1, 29).Select(d => d.ToString());
                ViewBag.ShipCarriers = carriers;
                ViewBag.DisplayOrderValuesForList = new List<string>() { "Admins Only", "Everyone" };
                ViewBag.DisplayInventoryAvailabilityForList = new List<string>() { "In Stock / Out of Stock Only", "Actual Inventory for All Items" };
                ViewBag.TurnOnProductMinMaxLevelsForList = new List<string>() { "Enforce Per Item", "Enforce Per Group" };
                ViewBag.DisplayBudgetTypeList = new List<string>() { "Group Based Budget", "User Based Budget" };
                ViewBag.DisplayBudgetFrequencyList = new List<string>() { "Yearly", "Monthly", "Weekly" };
                ViewBag.DisplayBudgetRefreshDayOfTheWeekList = new List<string>() { "", "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };

                return View(model);
            }
            catch (Exception ex)
            {
                return View("Error", new HandleErrorInfo(ex, "Home", "Index"));
            }
        }


        [TokenAuthorize]
        [HttpPost]
        public ActionResult ShipMethod_UpdateShipMethod(ShipMethodViewModel paramShipMethod)
        {
            try
            {
                // Get the shipmethod info
                ShipMethod selectedShipMethod = (from sm in _sfDb.ShipMethods
                                                 where sm.Id == paramShipMethod.Id
                                                 select sm).FirstOrDefault();

                // Save Changes
                if (paramShipMethod.Code != null && paramShipMethod.Code.Length > 0)
                {

                    selectedShipMethod.Code = paramShipMethod.Code;
                    selectedShipMethod.Enabled = paramShipMethod.Enabled;
                    _sfDb.Entry(selectedShipMethod).State = EntityState.Modified;
                    _sfDb.SaveChanges();
                }
                else
                {
                    return Json(new { result = "Error", message = "Blank code not allowed" });
                }

                return Json(new { result = "Success" });

            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }
        #endregion

        #region Users
        // GET: Admin/UsersGroups
        [TokenAuthorize]
        public ActionResult UsersGroups(FilterViewModel paramFilter)
        {
            try
            {
                if (paramFilter == null)
                {
                    paramFilter = new FilterViewModel()
                    {
                        Status = "Active"
                    };
                }
                return View(paramFilter);
            }
            catch (Exception ex)
            {
                return View("Error", new HandleErrorInfo(ex, "Dashboard", "MyWindow"));
            }
        }
        public ActionResult ReadGroups([DataSourceRequest] DataSourceRequest request)
        {
            try
            {
                string _userName = User.Identity.Name;

                // Retrieve this logged in user record
                var userSf = _sfDb.AspNetUsers.Where(u => u.UserName == _userName).FirstOrDefault();
                var userRoleList = _sfDb.AspNetRoles.Where(t => t.AspNetUsers.Any(u => u.UserName == _userName)).Select(r => r.Name).ToList();


                //List<int> storeFrontIdList = _sfDb.UserStoreFronts.Where(usf => usf.AspNetUserId == userSf.Id).Select(usf => usf.StoreFrontId).ToList();

                List<UserGroup> usersAll = new List<UserGroup>();
                UserSetting selectedUserSetting = (from us in _sfDb.UserSettings
                                                   where us.AspNetUserId == userSf.Id && us.StoreFrontId == _site.StoreFrontId
                                                   select us).FirstOrDefault();
                if (selectedUserSetting == null ? false : selectedUserSetting.AllowAdminAccess == 1)
                {
                    /*if (_userSf.UserName.ToLower() == "c2devgroup" || _userSf.UserName.ToLower() == "peter")
                        usersAll = (from u in _sfDb.UserGroups where storeFrontIdList.Contains((int)u.StoreFrontId) select u).ToList();
                    else
                        usersAll = (from u in _sfDb.UserGroups where storeFrontIdList.Contains((int)u.StoreFrontId) select u).ToList();
                        */
                    usersAll = (from u in _sfDb.UserGroups where u.StoreFrontId == _site.StoreFrontId select u).ToList();
                }

                DataSourceResult modelList = usersAll.ToDataSourceResult(request, u => new UserGroup()
                {
                    Id = u.Id,
                    StoreFrontId = u.StoreFrontId,
                    Name = u.Name,
                    Desc = u.Desc,
                    DateCreated = u.DateCreated,
                    UserId = u.UserId,
                    UserName = u.UserName,
                    PriceLimit = u.PriceLimit,
                    TimeRefresh = u.TimeRefresh,
                });

                return Json(modelList);
            }
            catch (Exception ex)
            {
                return Json(new { message = "Error : " + ex.Message });
            }

        }


        [TokenAuthorize]
        public ActionResult GroupAdd()
        {
            GroupsViewModel model = new GroupsViewModel();
            return View(model);
        }


        [TokenAuthorize]
        [HttpPost]
        public async Task<ActionResult> GroupAdd(GroupsViewModel model)
        {
            try
            {
                UserGroup newGroup = new UserGroup()
                {
                    StoreFrontId = _site.StoreFrontId,
                    Name = model.Name,
                    Desc = model.Desc,
                    UserId = null,
                    UserName = null,
                    DateCreated = DateTime.Now,
                    PriceLimit = model.PriceLimit,
                    CurrentBudgetLeft = model.PriceLimit,
                    TimeRefresh = model.TimeRefresh
                };
                _sfDb.UserGroups.Add(newGroup);
                _sfDb.SaveChanges();

                ViewBag.StatusMessage = "Saved Sucessfully";
            
                model.Id = newGroup.Id;
                model.StoreFrontId = newGroup.Id;

                return Redirect("/Admin/GroupDetail/" + model.Id);
            }
            catch (Exception ex)
            {
                if (Request.IsAjaxRequest())
                    return Json(new { result = "Error", message = ex.Message });
                else
                    return View("Error", new HandleErrorInfo(ex, "Admin", "UsersGroups"));
            }
        }

        [HttpGet]
        [TokenAuthorize]
        public async Task<ActionResult> GroupDetail(int? id, string statusMessage)
        {
            try
            {
                //var query = from g in _sfDb.UserGroups
                //            where g.Id == id.Value
                //            select new GroupsViewModel
                //            {
                //                Id = g.Id,
                //                StoreFrontId = g.StoreFrontId == null ? 0 : g.StoreFrontId.Value,
                //                Name = g.Name,
                //                Desc = g.Desc,
                //                UserId = g.UserId,
                //                UserName = g.UserName,
                //                DateCreated = g.DateCreated,
                //                PriceLimit = g.PriceLimit == null ? 0 : g.PriceLimit.Value,
                //                TimeRefresh = g.TimeRefresh,
                //                CurrentBudgetLeft = g.CurrentBudgetLeft == null ? 0 : 0
                //            };
                //return View(query.FirstOrDefault());

                Session["AsyncAction"] = "";
                Session["ProductsSelected"] = new List<ProductViewModel>();
                //Session["searchProducts"] = "";
                GroupsViewModel model = new GroupsViewModel();
                UserGroup userGroup = _sfDb.UserGroups.Where(ug => ug.Id == id).FirstOrDefault();

                model.Id = userGroup.Id;
                model.StoreFrontId = userGroup.StoreFrontId ?? 0;
                model.Name = userGroup.Name;
                model.Desc = userGroup.Desc;
                model.UserId = userGroup.UserId;
                model.UserName = userGroup.UserName;
                model.DateCreated = userGroup.DateCreated;
                model.PriceLimit = userGroup.PriceLimit ?? 0;
                model.TimeRefresh = userGroup.TimeRefresh;
                model.CurrentBudgetLeft = userGroup.CurrentBudgetLeft ?? 0;

                model.UserGroupProducts = (from ugp in _sfDb.UserGroupProducts
                                           join p in _sfDb.Products on ugp.ProductId equals p.Id
                                            where ugp.UserGroupId == model.Id && ugp.StoreFrontId == _site.StoreFrontId
                                            select new UserGroupProductsVM()
                                            {
                                                Id = ugp.Id,
                                                ProductId = ugp.ProductId,
                                                UserGroupId = ugp.UserGroupId,
                                                StoreFrontId = ugp.StoreFrontId ?? 0,
                                                MinQty  =ugp.MinQty,
                                                MaxQty = ugp.MaxQty,
                                                ProductCode = p.ProductCode,
                                            }).OrderBy(p => p.ProductCode).ToList();


                // Get the products this product belongs to
                var selectedProducts = (from ugp in _sfDb.UserGroupProducts
                                            join p in _sfDb.Products on ugp.ProductId equals p.Id
                                            where ugp.UserGroupId == model.Id && ugp.StoreFrontId == _site.StoreFrontId
                                            select new UserGroupProductsVM()
                                            {
                                                Id = ugp.Id,
                                                ProductId = ugp.ProductId
                                            }).ToList();

                //string txtSearch = Session["ProductsSearch"].ToString();
                //ViewBag.ProductsSearch = txtSearch;
                if (Session["ProductsSearch"] == null)
                {
                    var productUnselected1 = (from p in _sfDb.Products
                                              where p.StoreFrontId == _site.StoreFrontId 
                                              select new ProductViewModel()
                                              {
                                                  Id = p.Id,
                                                  ProductCode = p.ProductCode,
                                                  MinQty = p.MinQty,
                                                  MaxQty = p.MaxQty
                                              }).ToList();
                    var productUnselected = (from p in productUnselected1
                                             where !selectedProducts.Any(sp => sp.ProductId == p.Id)
                                             select new ProductViewModel()
                                             {
                                                 Id = p.Id,
                                                 ProductCode = p.ProductCode,
                                                 MinQty = p.MinQty,
                                                 MaxQty = p.MaxQty
                                             }).ToList();
                    ViewBag.availableProducts = productUnselected;
                    ViewBag.ProductsSearch = "";
                }
                else
                {
                    string txtSearch = Session["ProductsSearch"].ToString();
                    var productUnselected1 = (from p in _sfDb.Products
                                              where p.StoreFrontId == _site.StoreFrontId && p.ProductCode.Contains(txtSearch)
                                              select new ProductViewModel()
                                              {
                                                  Id = p.Id,
                                                  ProductCode = p.ProductCode,
                                                  MinQty = p.MinQty,
                                                  MaxQty = p.MaxQty
                                              }).ToList();
                    var productUnselected = (from p in productUnselected1
                                             where !selectedProducts.Any(sp => sp.ProductId == p.Id)
                                             select new ProductViewModel()
                                             {
                                                 Id = p.Id,
                                                 ProductCode = p.ProductCode,
                                                 MinQty = p.MinQty,
                                                 MaxQty = p.MaxQty
                                             }).ToList();
                    ViewBag.availableProducts = productUnselected;
                    ViewBag.ProductsSearch = txtSearch;
                }

                //var productUnselected1 = (from p in _sfDb.Products
                //                            where p.StoreFrontId == _site.StoreFrontId && p.ProductCode.Contains(txtSearch)
                //                            select new ProductViewModel()
                //                            {
                //                                Id = p.Id,
                //                                ProductCode = p.ProductCode,
                //                                MinQty = p.MinQty,
                //                                MaxQty = p.MaxQty
                //                            }).ToList();
                //var productUnselected = (from p in productUnselected1
                //                            where !selectedProducts.Any(sp => sp.ProductId == p.Id)
                //                            select new ProductViewModel()
                //                            {
                //                                Id = p.Id,
                //                                ProductCode = p.ProductCode,
                //                                MinQty = p.MinQty,
                //                                MaxQty = p.MaxQty
                //                            }).ToList();
                //ViewBag.availableProducts = productUnselected;
                //ViewBag.availableProducts = Session["ProductsSearch"];

                ViewBag.StatusMessage = statusMessage;
                return View(model);
            }
            catch (Exception ex)
            {
                if (Request.IsAjaxRequest())
                    return Json(new { result = "Error", message = ex.Message });
                else
                    return View("Error", new HandleErrorInfo(ex, "Admin", "UsersGroups"));
            }
        }

        [HttpPost]
        [TokenAuthorize]
        public async Task<ActionResult> GroupDetail(GroupsViewModel model)
        {
            try
            {
                //Session["ProductsSearch"] = "";
                if (ModelState.IsValid)
                {
                    //var query = (from g in _sfDb.UserGroups
                    //             where g.Id == model.Id
                    //             select g).FirstOrDefault();

                    //query.StoreFrontId = model.StoreFrontId;
                    //query.Name = model.Name;
                    //query.Desc = model.Desc;
                    //query.UserId = model.UserId;
                    //query.UserName = model.UserName;
                    //query.DateCreated = model.DateCreated;
                    //query.PriceLimit = model.PriceLimit;
                    //query.TimeRefresh = model.TimeRefresh;
                    //_sfDb.SaveChanges();

                    //ViewBag.StatusMessage = "Saved Sucessfully";
                    //return View(model);
         
                    UserGroup selecteUserGroup = _sfDb.UserGroups.Find(model.Id);

                    selecteUserGroup.Name = model.Name;
                    selecteUserGroup.Desc = model.Desc;
                    selecteUserGroup.UserId = model.UserId;
                    selecteUserGroup.UserName = model.UserName;
                    selecteUserGroup.DateCreated = model.DateCreated;
                    selecteUserGroup.PriceLimit = model.PriceLimit;
                    selecteUserGroup.TimeRefresh = model.TimeRefresh;

                    _sfDb.Entry(selecteUserGroup).State = EntityState.Modified;
                    _sfDb.SaveChanges();

                    try
                    {
                        // Save the product categories 
                        var ProductsSelected = (List<ProductViewModel>)Session["ProductsSelected"];
                        if (ProductsSelected != null && ProductsSelected.Count > 0)
                        {
                            //Session["ProductsSearch"] = "";
                            foreach (var p in ProductsSelected)
                            {
                                UserGroupProduct selecteduserGroupProduct = new UserGroupProduct();
                                selecteduserGroupProduct.UserGroupId = model.Id;
                                selecteduserGroupProduct.ProductId = p.Id;
                                selecteduserGroupProduct.MinQty = p.MinQty;
                                selecteduserGroupProduct.MaxQty = p.MaxQty;
                                selecteduserGroupProduct.EnableMinQty = 1;
                                selecteduserGroupProduct.EnableMaxQty = 1;
                                selecteduserGroupProduct.StoreFrontId = model.StoreFrontId;
                                _sfDb.UserGroupProducts.Add(selecteduserGroupProduct);
                                _sfDb.SaveChanges();
                            }
                        }
                    }
                    catch
                    {

                    }
                    ViewBag.StatusMessage = "Saved Sucessfully";

                    string action = Session["AsyncAction"].ToString();
                    if (action == "")
                    {
                        ViewBag.StatusMessage = "Saved Sucessfully";
                    }
                    else
                    {
                        if (action == "Productremoved")
                        {
                            ViewBag.StatusMessage = "Product removed Sucessfully";
                        }
                        else if (action == "Productupdated")
                        {
                            ViewBag.StatusMessage = "Product updated Sucessfully";
                        }
                    }
                    //return View(model);

                    //return RedirectToAction("GroupDetail", "Admin", new { id = model.Id, statusMessage = "Saved Sucessfully" });   
                    return RedirectToAction("GroupDetail", "Admin", new { id = model.Id, statusMessage = ViewBag.StatusMessage });
                }
                return View(model);
            }
            catch (Exception ex)
            {
                if (Request.IsAjaxRequest())
                    return Json(new { result = "Error", message = ex.Message });
                else
                    return View("Error", new HandleErrorInfo(ex, "Admin", "UsersGroups"));
            }
        }

        [TokenAuthorize]
        public ActionResult Async_Remove_UserGroupProducts(int UserGroupProductsId)
        {
            try
            {
                UserGroupProduct selectUserGroupProduct;

                selectUserGroupProduct = (from ugp in _sfDb.UserGroupProducts
                                          where ugp.Id == UserGroupProductsId
                                          select ugp).FirstOrDefault();

                if (selectUserGroupProduct != null)
                {
                    _sfDb.UserGroupProducts.Remove(selectUserGroupProduct);
                    _sfDb.SaveChanges();
                    Session["AsyncAction"] = "Productremoved";
                }
                // Return an empty string to signify success
                return Json(new { result = "success" }, "text/plain");
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }

        [TokenAuthorize]
        public ActionResult Products_Search_Clear(int UserGroupId)
        {
            Session["ProductsSearch"] = "";
            //return RedirectToAction("GroupDetail", "Admin", new { id = UserGroupId });
            //return Redirect("/Admin/GroupDetail/" + UserGroupId);
            return Json(new { message = "Products_Search_Clear" });
        }

        [TokenAuthorize]
        public ActionResult Products_Search(string textSearch)
        {
            Session["ProductsSearch"] = textSearch;
            try
            {
                return Json(new { result = "ProductId is 0" }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }

        [TokenAuthorize]
        public ActionResult Products_SaveSelected(List<List<int>> ProductIdList)
        {
            Session["ProductsSelected"] = "";
            try
            {
                var products = new List<ProductViewModel>();
                //var deatils = new List<>();
                foreach (var deatils in ProductIdList)
                {
                    int pId = 0;
                    int pMin = 0;
                    int pMax = 0;
                    int i = 0;
                    foreach (var d in deatils)
                    {
                        if (i == 0)
                        {
                            pId = (int)d;
                        }
                        if (i == 1)
                        {
                            pMin = (int)d;
                        }
                        if (i == 2)
                        {
                            pMax = (int)d;
                        }
                        i = i + 1;
                    }

                    var selectedProductList = (from p in _sfDb.Products
                                                where p.Id == pId
                                               select p).FirstOrDefault();

                    products.Add(new ProductViewModel
                    {
                        Id = selectedProductList.Id,
                        ProductCode = selectedProductList.ProductCode,                        
                        MinQty = pMin,
                        MaxQty = pMax
                    });
                }
                Session["ProductsSelected"] = products;
                return Json(new { result = "ProductId is 0" }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }
        
        [TokenAuthorize]
        public ActionResult Async_Update_UserGroupProducts(int UserGroupProductsId, int newMinQty, int newMaxQty)
        {
            try
            {
                UserGroupProduct selectUserGroupProduct;

                selectUserGroupProduct = (from ugp in _sfDb.UserGroupProducts
                                          where ugp.Id == UserGroupProductsId
                                          select ugp).FirstOrDefault();
                selectUserGroupProduct.MinQty = newMinQty;
                selectUserGroupProduct.MaxQty = newMaxQty;
                _sfDb.Entry(selectUserGroupProduct).State = EntityState.Modified;
                _sfDb.SaveChanges();
                Session["AsyncAction"] = "Productupdated";

                return Json(new { result = "success" }, "text/plain");
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }

        [TokenAuthorize]
        public ActionResult Async_UpdateAll_UserGroupProducts(int UserGroupId, int newMinQty, int newMaxQty)
        {
            try
            {
                List<UserGroupProduct> userGroupProducts = (from ugp in _sfDb.UserGroupProducts
                                          where ugp.UserGroupId == UserGroupId
                                          select ugp).ToList();
                foreach (UserGroupProduct ugp in userGroupProducts)
                {
                    UserGroupProduct selectUserGroupProduct;
                    selectUserGroupProduct = (from s in _sfDb.UserGroupProducts
                                              where s.Id == ugp.Id
                                              select s).FirstOrDefault();
                    selectUserGroupProduct.MinQty = newMinQty;
                    selectUserGroupProduct.MaxQty = newMaxQty;
                    _sfDb.Entry(selectUserGroupProduct).State = EntityState.Modified;
                    _sfDb.SaveChanges();
                    Session["AsyncAction"] = "Productupdated";
                }
                return Json(new { result = "success" }, "text/plain");
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }

        [Authorize]
        [HttpPost]
        public ActionResult AddUserToGroup(string[] userIds, int? groupId)
        {
            try
            {
                var groupQueryObj = _sfDb.UserGroups.Where(x => x.Id == groupId).FirstOrDefault();
                if (groupQueryObj != null)
                {
                    foreach (var userId in userIds)
                    {
                        var query = _sfDb.UserGroupUsers.Where(x => x.AspNetUserId == userId).FirstOrDefault();
                        if (query == null)
                        {
                            UserGroupUser obj = new UserGroupUser();
                            obj.AspNetUserId = userId;
                            obj.UserGroupId = groupId.Value;
                            obj.PriceLimit = groupQueryObj.PriceLimit;
                            obj.TimeRefresh = groupQueryObj.TimeRefresh;
                            obj.Active = 1;
                            _sfDb.UserGroupUsers.Add(obj);
                            _sfDb.SaveChanges();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { message = "Error : " + ex.Message });
            }
            return Json(new { result = "Success" }, JsonRequestBehavior.AllowGet);
        }

        // GET: Admin/ReadUsers
        [TokenAuthorize]
        public ActionResult ReadUsersOfGroup([DataSourceRequest] DataSourceRequest request, int groupId)
        {
            try
            {
                var query = (from u in _sfDb.AspNetUsers
                             join ug in _sfDb.UserGroupUsers on u.Id equals ug.AspNetUserId
                             where ug.UserGroupId == groupId && u.Status == 1
                             select new UsersViewModel()
                             {
                                 AspNetUserId = u.Id,
                                 Id = u.SfId,
                                 Email = u.Email,
                                 UserName = u.UserName,
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
                                 AccessRestricted = u.AccessRestricted == 1 ? true : false,
                                 Status = (short)u.Status,
                                 OnHold = u.OnHold == 1 ? true : false,
                                 BudgetLimit = _sfDb.UserGroups.Where(x => x.Id == ug.UserGroupId && x.StoreFrontId == _site.StoreFrontId).FirstOrDefault().PriceLimit ?? 0,
                                 BudgetCurrentTotal = _sfDb.UserGroups.Where(x => x.Id == ug.UserGroupId && x.StoreFrontId == _site.StoreFrontId).FirstOrDefault().CurrentBudgetLeft ?? 0,
                             }
                               ).ToList();

                return Json(query.ToDataSourceResult(request));
            }
            catch (Exception ex)
            {
                return Json(new { message = "Error : " + ex.Message });
            }
        }


        // GET: Manage/Vendors_Destroy
        [TokenAuthorize]
        [HttpPost]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult UserInGroup_Destroy([DataSourceRequest] DataSourceRequest request, UsersViewModel userView, int? groupId)
        {
            bool noerror = true;
            try
            {
                if (userView != null && ModelState.IsValid)
                {
                    var query = _sfDb.UserGroupUsers.FirstOrDefault(v => v.AspNetUserId == userView.AspNetUserId && v.UserGroupId == groupId.Value);
                    if (query != null)
                    {
                        _sfDb.UserGroupUsers.Remove(query);
                    };

                    if (noerror)
                    {
                        _sfDb.SaveChanges();
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", errors = ex.Message });
            }

            return Json(new[] { userView }.ToDataSourceResult(request, ModelState));
        }

        // GET: Admin/Users
        [TokenAuthorize]
        public ActionResult Users(FilterViewModel paramFilter)
        {
            try
            {
                if (paramFilter == null)
                {
                    paramFilter = new FilterViewModel()
                    {
                        Status = "Active"
                    };
                }
                return View(paramFilter);
            }
            catch (Exception ex)
            {
                return View("Error", new HandleErrorInfo(ex, "Dashboard", "MyWindow"));
            }
        }


        // GET: Admin/ReadUsers
        [TokenAuthorize]
        public ActionResult ReadAllUsers([DataSourceRequest] DataSourceRequest request)
        {
            try
            {
                // Retrieve this logged in user record
                var userSf = _sfDb.AspNetUsers.Select(u => new UsersViewModel()
                {
                    AspNetUserId = u.Id,
                    Id = u.SfId,
                    Email = u.Email,
                    UserName = u.UserName,
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
                    AccessRestricted = u.AccessRestricted == 1 ? true : false,
                    Status = (short)u.Status,
                    OnHold = u.OnHold == 1 ? true : false,
                }).ToList();

                return Json(userSf.ToDataSourceResult(request));
            }
            catch (Exception ex)
            {
                return Json(new { message = "Error : " + ex.Message });
            }
        }

        // GET: Admin/ReadUsers
        [TokenAuthorize]
        public ActionResult ReadUsers([DataSourceRequest] DataSourceRequest request)
        {
            try
            {
                string _userName = User.Identity.Name;
                DataSourceResult modelList = null;
                // Retrieve this logged in user record
                var userSf = _sfDb.AspNetUsers.Where(u => u.UserName == _userName).FirstOrDefault();
                var userRoleList = _sfDb.AspNetRoles.Where(t => t.AspNetUsers.Any(u => u.UserName == _userName)).Select(r => r.Name).ToList();
                //var storeFronts = _sfDb.StoreFronts.Where(u => u.UserStoreFronts.Where(usf => usf.AspNetUserId == userSf.Id).Select(usf => usf.StoreFrontId).Contains(u.Id));



                //var storeFrontNameList = storeFronts.Select(sf => sf.StoreFrontName).ToList();
                //var storeFrontIdList = storeFronts.Select(sf => sf.Id).ToList();

                List<int> storeFrontIdList = _sfDb.UserStoreFronts.Where(usf => usf.AspNetUserId == userSf.Id).Select(usf => usf.StoreFrontId).ToList();


                // If Admin user, can list all the users for the storefront, otherwise, just show one user
                List<AspNetUser> usersAll = new List<AspNetUser>();
                //if (userRoleList.Contains("SuperAdmin"))
                //{
                //    usersAll = (from u in _sfDb.AspNetUsers
                //                where storeFrontIdList.Contains(_sfDb.UserStoreFronts.Where(usf => usf.AspNetUserId == u.Id).FirstOrDefault().StoreFrontId)
                //                select u).ToList();
                //}
                //else if (userRoleList.Contains("Admin"))
                UserSetting selectedUserSetting = (from us in _sfDb.UserSettings
                                                   where us.AspNetUserId == userSf.Id && us.StoreFrontId == _site.StoreFrontId
                                                   select us).FirstOrDefault();
                if (selectedUserSetting == null ? false : selectedUserSetting.AllowAdminAccess == 1)
                {
                    if (_userSf.UserName.ToLower() == "c2devgroup" || _userSf.UserName.ToLower() == "peter")
                        usersAll = (from u in _sfDb.AspNetUsers
                                    where _sfDb.UserStoreFronts.Where(usf => usf.AspNetUserId == u.Id && usf.StoreFrontId == _site.StoreFrontId).FirstOrDefault().StoreFrontId == _site.StoreFrontId
                                    && u.IsVendor == 0
                                    select u).ToList();
                    else
                        usersAll = (from u in _sfDb.AspNetUsers
                                    where _sfDb.UserStoreFronts.Where(usf => usf.AspNetUserId == u.Id && usf.StoreFrontId == _site.StoreFrontId).FirstOrDefault().StoreFrontId == _site.StoreFrontId
                                    && !u.AspNetRoles.Contains(_sfDb.AspNetRoles.Where(r => r.Name == "SuperAdmin").FirstOrDefault())
                                    && u.IsVendor == 0
                                    select u).ToList();
                }
                else
                {
                    usersAll.Add(userSf);
                }

                SystemSetting systemsettings = _sfDb.SystemSettings.Where(x => x.StoreFrontId == _site.StoreFrontId).FirstOrDefault();

            
                if (systemsettings.BudgetEnforce == 1 && systemsettings.BudgetType == "Group Based Budget")
                {
                    UserGroupUser userGroupUser = _sfDb.UserGroupUsers.Where(y => y.AspNetUserId == _site.AspNetUserId && y.StoreFrontId == _site.StoreFrontId).FirstOrDefault();
                    //UserGroup userGroup = _sfDb.UserGroups.Where(xx => xx.StoreFrontId == _site.StoreFrontId && userGroupUser.UserGroupId == xx.Id).FirstOrDefault();
         
                    modelList = usersAll.ToDataSourceResult(request, u => new UsersViewModel()
                    {
                        Id = u.SfId,
                        Email = u.Email,
                        UserName = u.UserName,
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
                        AccessRestricted = u.AccessRestricted == 1 ? true : false,
                        Status = (short)u.Status,
                        OnHold = u.OnHold == 1 ? true : false,
                        /*
                        BudgetLimit = _sfDb.UserGroups.Where(x => x.Id == _sfDb.UserGroupUsers.Where(y => y.AspNetUserId == u.Id && x.StoreFrontId == _site.StoreFrontId).FirstOrDefault().UserGroupId && x.StoreFrontId == _site.StoreFrontId).DefaultIfEmpty().FirstOrDefault().PriceLimit ?? 0,
                        BudgetCurrentTotal = (_sfDb.UserGroups.Where(x => x.Id == _sfDb.UserGroupUsers.Where(y => y.AspNetUserId == u.Id && x.StoreFrontId == _site.StoreFrontId).FirstOrDefault().UserGroupId && x.StoreFrontId == _site.StoreFrontId).DefaultIfEmpty().FirstOrDefault().PriceLimit ?? 0)     
                        - (_sfDb.UserGroups.Where(x => x.Id == _sfDb.UserGroupUsers.Where(y => y.AspNetUserId == u.Id && x.StoreFrontId == _site.StoreFrontId).FirstOrDefault().UserGroupId && x.StoreFrontId == _site.StoreFrontId).DefaultIfEmpty().FirstOrDefault().CurrentBudgetLeft ?? 0),
                        BudgetCurrentTotal = 1 == 2 ? 3333 : 9999,
                        */
                        BudgetLimit = BudgetFunctions.GetTotalGroupBudget(_site.StoreFrontId, u.Id),
                        BudgetCurrentTotal = BudgetFunctions.GetBudgetRemaining(_site.StoreFrontId, u.Id),
                        UserGroupsList = BudgetFunctions.UserGroupsList(_site.StoreFrontId, u.Id)
                    });

                  
                }
                else if (systemsettings.BudgetEnforce == 1 && systemsettings.BudgetType == "User Based Budget")
                {
                    modelList = usersAll.ToDataSourceResult(request, u => new UsersViewModel()
                    {
                        Id = u.SfId,
                        Email = u.Email,
                        UserName = u.UserName,
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
                        AccessRestricted = u.AccessRestricted == 1 ? true : false,
                        Status = (short)u.Status,
                        OnHold = u.OnHold == 1 ? true : false,
                        BudgetLimit = _sfDb.UserSettings.Where(us => us.AspNetUserId == u.Id).FirstOrDefault().BudgetLimit,
                        BudgetCurrentTotal = _sfDb.UserSettings.Where(us => us.AspNetUserId == u.Id).FirstOrDefault().BudgetCurrentTotal,
                        UserGroupsList = BudgetFunctions.UserGroupsList(_site.StoreFrontId, u.Id),
                    });

                }
                else
                {
                    modelList = usersAll.ToDataSourceResult(request, u => new UsersViewModel()
                    {
                        Id = u.SfId,
                        Email = u.Email,
                        UserName = u.UserName,
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
                        AccessRestricted = u.AccessRestricted == 1 ? true : false,
                        Status = (short)u.Status,
                        OnHold = u.OnHold == 1 ? true : false,
                        BudgetLimit = 0,
                        BudgetCurrentTotal = 0,
                        UserGroupsList = BudgetFunctions.UserGroupsList(_site.StoreFrontId, u.Id),
                    });

                }


                return Json(modelList);
            }
            catch (Exception ex)
            {
                return Json(new { message = "Error : " + ex.Message });
            }
        }

        [TokenAuthorize]
        public ActionResult ImportNewUsers()
        {
            return View();
        }

        [TokenAuthorize]
        public ActionResult Read_NewUsers_Grid([DataSourceRequest] DataSourceRequest request, string fileName)
        {
            try
            {
                List<UsersViewModel> usersNew = new List<UsersViewModel>();
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

                var workSheet = rawResult.Tables["storelist"];
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

                    usersNew.Add(new UsersViewModel()
                    {
                        UserName = row.ItemArray[0].ToString(),
                        Password = row.ItemArray[1].ToString(), // passwordHasher.HashPassword(row.ItemArray[1].ToString()),
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
                        AccessRestricted = row.ItemArray[14].ToString() == "1" ? true : false,
                        OnHold = row.ItemArray[15].ToString() == "1" ? true : false,
                        Status = Convert.ToInt16(row.ItemArray[16].ToString() == "1" ? 1 : 0),
                    });
                }

                result = usersNew.ToDataSourceResult(request, u => new UsersViewModel()
                {
                    Id = u.Id,
                    UserName = u.UserName,
                    Password = u.Password,
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
                    AccessRestricted = u.AccessRestricted,
                    OnHold = u.OnHold,
                    Status = u.Status,
                });

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { message = "Error : " + ex.Message });
            }
        }

        [TokenAuthorize]
        public ActionResult Async_Save(IEnumerable<HttpPostedFileBase> files)
        {
            try
            {
                string physicalPath = string.Empty;

                // The Name of the Upload component is "files"
                if (files != null)
                {
                    foreach (var file in files)
                    {
                        // Some browsers send file names with full path.
                        // We are only interested in the file name.
                        var fileName = Path.GetFileName(file.FileName);
                        physicalPath = Path.Combine(Server.MapPath("~/Content/" + _site.StoreFrontName + "/Files"), fileName);

                        // The files are not actually saved in this demo
                        file.SaveAs(physicalPath);
                    }
                }

                return Json(new { result = "Success", message = physicalPath }, "text/plain");
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }

        // (Not used currently)
        [TokenAuthorize]
        public ActionResult Save_NewUsers_Grid([DataSourceRequest] DataSourceRequest request)
        {


            return Json(new { result = "Success" });
        }


        #region Group Tab 
        [TokenAuthorize]
        public ActionResult User_UserGroups(UsersViewModel user)
        {
            try
            {
                List<GroupsViewModel> userGroups = new List<GroupsViewModel>();
                List<string> selectedUserGroups = new List<string>();
                UserSetting selectedUserSetting = (from us in _sfDb.UserSettings
                                                   where us.AspNetUserId == user.AspNetUserId && us.StoreFrontId == _site.StoreFrontId
                                                   select us).FirstOrDefault();
                userGroups = _sfDb.UserGroups.Where(c => c.StoreFrontId == _site.StoreFrontId)
                            .Select(c => new GroupsViewModel
                            {
                                Id = c.Id,
                                Name = c.Name
                            })
                            .OrderBy(e => e.Name).ToList();

                selectedUserGroups = (from ugu in _sfDb.UserGroupUsers
                                      join ug in _sfDb.UserGroups on ugu.UserGroupId equals ug.Id
                                      where ugu.AspNetUserId == user.AspNetUserId && ug.StoreFrontId == _site.StoreFrontId
                                      select ug.Name).ToList();

                // Remove selected userGroups
                foreach (var sc in selectedUserGroups)
                {
                    userGroups.Remove(userGroups.Where(c => c.Name == sc).FirstOrDefault());
                }

                return Json(userGroups, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { message = "Error : " + ex.Message });
            }
        }

        [TokenAuthorize]
        public ActionResult UserGroupUsers_SetSelected(UsersViewModel user)
        {
            try
            {
                var selectedUserGroups = (from ugu in _sfDb.UserGroupUsers
                                          join ug in _sfDb.UserGroups on ugu.UserGroupId equals ug.Id
                                          where ugu.AspNetUserId == user.AspNetUserId && ug.StoreFrontId == _site.StoreFrontId
                                          select new GroupsViewModel()
                                          {
                                              Id = ug.Id,
                                              Name = ug.Name
                                          }).ToList();

                return Json(selectedUserGroups, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { message = "Error : " + ex.Message });
            }
        }

        [TokenAuthorize]
        public ActionResult UserGroupUsers_SaveSelected(List<GroupsViewModel> userGroups, UsersViewModel user)
        {
            try
            {
                if (user.AspNetUserId.Length != 0)
                {
                    var prevUserGroupUsersList = (from ugu in _sfDb.UserGroupUsers
                                                  where ugu.AspNetUserId == user.AspNetUserId
                                                  select ugu).ToList();

                    var curSelectedUserGroupUserList = userGroups;

                    // removed UserGroup Users
                    foreach (var ug in prevUserGroupUsersList)
                    {
                        bool shouldDelete = false;
                        if (userGroups == null) shouldDelete = true;
                        else
                        {
                            var query = from input in userGroups
                                        where input.Id == ug.UserGroupId
                                        select input;
                            if (query.Count() == 0) shouldDelete = true;
                        }

                        if (shouldDelete)
                        {
                            var deleteMe = (from ugu in _sfDb.UserGroupUsers
                                            where ugu.Id == ug.Id
                                            select ugu).FirstOrDefault();
                            _sfDb.UserGroupUsers.Remove(deleteMe);
                        }
                    }

                    // added user group
                    if (curSelectedUserGroupUserList != null)
                    {                        
                        foreach (var ug in curSelectedUserGroupUserList)
                        {
                            var ugPriceLimit = _sfDb.UserGroups.Where(x => x.Id == ug.Id && x.StoreFrontId == _site.StoreFrontId).FirstOrDefault().PriceLimit ?? 0;
                            var ugTimeRefresh = _sfDb.UserGroups.Where(x => x.Id == ug.Id && x.StoreFrontId == _site.StoreFrontId).FirstOrDefault().TimeRefresh ?? DateTime.Now.Date;
                            var query = from prevUgu in prevUserGroupUsersList
                                        where prevUgu.UserGroupId == ug.Id
                                        select prevUgu;
                            if (query.Count() == 0)
                            {
                                var addMe = new UserGroupUser()
                                {
                                    AspNetUserId = user.AspNetUserId,
                                    UserGroupId = ug.Id,
                                    Active = 1,
                                    PriceLimit = ugPriceLimit,
                                    TimeRefresh = ugTimeRefresh,
                                };
                                _sfDb.UserGroupUsers.Add(addMe);
                            }
                        }
                    }

                    _sfDb.SaveChanges();

                    return Json(new { result = "Success" }, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    // Save the userGroups anyways in the Session
                    Session["UserGroupsSelected"] = userGroups;

                    return Json(new { result = "UserId is blank" }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { message = "Error : " + ex.Message });
            }
        }
        #endregion


        [TokenAuthorize]
        public ActionResult Categories_SaveSelected(List<int> categoryIdList)
        {
            Session["CategoriesSelected"] = "";
            try
            {
                var categories = new List<CategoryViewModel>();
                foreach (var cid in categoryIdList)
                {
                    var selectedCategoryList = (from c in _sfDb.Categories
                                                where c.Id == cid
                                                select c).FirstOrDefault();

                    categories.Add(new CategoryViewModel
                    {
                        Id = selectedCategoryList.Id,
                        Name = selectedCategoryList.Name,
                        Desc = selectedCategoryList.Desc
                    });
                }
                Session["CategoriesSelected"] = categories;
                return Json(new { result = "ProductId is 0" }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }

        [TokenAuthorize]
        public ActionResult Async_Remove_Categories(int UserCategoryId)
        {
            try
            {
                UserCategory selectUserCategory;

                selectUserCategory = (from uc in _sfDb.UserCategories
                                         where uc.Id == UserCategoryId
                                         select uc).FirstOrDefault();

                if (selectUserCategory != null)
                {
                    _sfDb.UserCategories.Remove(selectUserCategory);
                    _sfDb.SaveChanges();
                }
                // Return an empty string to signify success
                return Json(new { result = "success" }, "text/plain");
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }

        [TokenAuthorize]
        public ActionResult User_Categories(UsersViewModel user)
        {
            try
            {
                List<CategoryViewModel> categories = new List<CategoryViewModel>();
                List<string> selectedCategories = new List<string>();
                UserSetting selectedUserSetting = (from us in _sfDb.UserSettings
                                                   where us.AspNetUserId == user.AspNetUserId && us.StoreFrontId == _site.StoreFrontId
                                                   select us).FirstOrDefault();
                if ((selectedUserSetting == null ? false : selectedUserSetting.AllowAdminAccess == 1) || _site.SiteAuth.InventoryRestrictCategory == 0)
                {
                    // load everything
                    categories = _sfDb.Categories.Where(c => c.StoreFrontId == _site.StoreFrontId)
                                .Select(c => new CategoryViewModel
                                {
                                    Id = c.Id,
                                    Name = c.Name
                                })
                                .OrderBy(e => e.Name).ToList();

                }
                else
                {
                    categories = (from uc in _sfDb.UserCategories
                                  join c in _sfDb.Categories on uc.CategoryId equals c.Id
                                  where uc.AspNetUserId == _userSf.Id && c.StoreFrontId == _site.StoreFrontId
                                  orderby c.Name
                                  select new CategoryViewModel
                                  {
                                      Id = c.Id,
                                      Name = c.Name
                                  }).ToList();

                }

                selectedCategories = (from uc in _sfDb.UserCategories
                                      join c in _sfDb.Categories on uc.CategoryId equals c.Id
                                      where uc.AspNetUserId == user.AspNetUserId && c.StoreFrontId == _site.StoreFrontId
                                      select c.Name).ToList();

                // Remove selected categories
                foreach (var sc in selectedCategories)
                {
                    categories.Remove(categories.Where(c => c.Name == sc).FirstOrDefault());
                }

                return Json(categories, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { message = "Error : " + ex.Message });
            }
        }

        [TokenAuthorize]
        public ActionResult UserCategories_SetSelected(UsersViewModel user)
        {
            try
            {
                var selectedCategories = (from uc in _sfDb.UserCategories
                                          join c in _sfDb.Categories on uc.CategoryId equals c.Id
                                          where uc.AspNetUserId == user.AspNetUserId && c.StoreFrontId == _site.StoreFrontId
                                          select new CategoryViewModel()
                                          {
                                              Id = c.Id,
                                              Name = c.Name
                                          }).ToList();

                return Json(selectedCategories, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { message = "Error : " + ex.Message });
            }
        }

        [TokenAuthorize]
        public ActionResult UserCategories_SaveSelected(List<CategoryViewModel> categories, UsersViewModel user)
        {
            try
            {
                if (user.AspNetUserId.Length != 0)
                {
                    var prevUserCategoryList = (from uc in _sfDb.UserCategories
                                                where uc.AspNetUserId == user.AspNetUserId
                                                select uc).ToList();

                    var curSelectedUserCategoryList = categories;

                    // removed category
                    foreach (var c in prevUserCategoryList)
                    {
                        bool shouldDelete = false;
                        if (categories == null) shouldDelete = true;
                        else
                        {
                            var query = from input in categories
                                        where input.Id == c.CategoryId
                                        select input;
                            if (query.Count() == 0) shouldDelete = true;
                        }

                        if (shouldDelete)
                        {
                            var deleteMe = (from uc in _sfDb.UserCategories
                                            where uc.Id == c.Id
                                            select uc).FirstOrDefault();
                            _sfDb.UserCategories.Remove(deleteMe);
                        }
                    }

                    // added category
                    if (curSelectedUserCategoryList != null)
                    {
                        foreach (var c in curSelectedUserCategoryList)
                        {
                            var query = from prevUc in prevUserCategoryList
                                        where prevUc.CategoryId == c.Id
                                        select prevUc;
                            if (query.Count() == 0)
                            {
                                var addMe = new UserCategory()
                                {
                                    AspNetUserId = user.AspNetUserId,
                                    CategoryId = c.Id
                                };
                                _sfDb.UserCategories.Add(addMe);
                            }
                        }
                    }

                    _sfDb.SaveChanges();

                    return Json(new { result = "Success" }, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    // Save the categories anyways in the Session
                    Session["CategoriesSelected"] = categories;

                    return Json(new { result = "ProductId is 0" }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { message = "Error : " + ex.Message });
            }
        }

        [TokenAuthorize]
        public ActionResult User_ShipMethods(UsersViewModel user)
        {
            try
            {
                List<ShipMethodViewModel> shipMethods = new List<ShipMethodViewModel>();
                List<string> selectedShipMethods = new List<string>();

                //if (_userRoleList.Contains("Admin") || _userRoleList.Contains("SuperAdmin"))
                //{
                //    // load everything
                //    shipMethods = _sfDb.ShipMethods
                //                .Select(s => new ShipMethodViewModel
                //                {
                //                    Id = s.Id,
                //                    MethodName = s.MethodName
                //                })
                //                .OrderBy(s => s.MethodName).ToList();

                //}
                //else
                //{
                shipMethods = (from sm in _sfDb.ShipMethods
                               join sc in _sfDb.ShipCarriers on sm.CarrierId equals sc.Id
                               where sm.Enabled == 1 && sc.StoreFrontId == _site.StoreFrontId
                               select new ShipMethodViewModel
                               {
                                   Id = sm.Id,
                                   MethodName = sm.MethodName
                               }).ToList();
                //}

                selectedShipMethods = (from usm in _sfDb.UserShipMethods
                                       join sm in _sfDb.ShipMethods on usm.ShipMethodId equals sm.Id
                                       join sc in _sfDb.ShipCarriers on sm.CarrierId equals sc.Id
                                       where usm.AspNetUserId == user.AspNetUserId && sc.StoreFrontId == _site.StoreFrontId
                                       select sm.MethodName).ToList();

                // Remove selected ShipMethods
                foreach (var ssm in selectedShipMethods)
                {
                    shipMethods.Remove(shipMethods.Where(c => c.MethodName == ssm).FirstOrDefault());
                }

                return Json(shipMethods, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { message = "Error : " + ex.Message });
            }
        }

        [TokenAuthorize]
        public ActionResult UserShipMethods_SetSelected(UsersViewModel user)
        {
            try
            {
                var selectedShipMethods = (from usm in _sfDb.UserShipMethods
                                           join sm in _sfDb.ShipMethods on usm.ShipMethodId equals sm.Id
                                           join sc in _sfDb.ShipCarriers on sm.CarrierId equals sc.Id
                                           where usm.AspNetUserId == user.AspNetUserId && sc.StoreFrontId == _site.StoreFrontId
                                           select new ShipMethodViewModel()
                                           {
                                               Id = sm.Id,
                                               MethodName = sm.MethodName
                                           }).ToList();

                return Json(selectedShipMethods, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { message = "Error : " + ex.Message });
            }
        }

        [TokenAuthorize]
        public ActionResult UserShipMethods_SaveSelected(List<ShipMethodViewModel> shipMethods, UsersViewModel user)
        {
            try
            {
                if (user.AspNetUserId.Length != 0)
                {
                    var prevUserShipMethodList = (from usm in _sfDb.UserShipMethods
                                                  join sm in _sfDb.ShipMethods on usm.ShipMethodId equals sm.Id
                                                  join sc in _sfDb.ShipCarriers on sm.CarrierId equals sc.Id
                                                  where usm.AspNetUserId == user.AspNetUserId && sc.StoreFrontId == _site.StoreFrontId
                                                  select usm).ToList();

                    var curSelectedUserShipMethodList = shipMethods;

                    // removed ShipMethod
                    foreach (var c in prevUserShipMethodList)
                    {
                        bool shouldDelete = false;
                        if (shipMethods == null) shouldDelete = true;
                        else
                        {
                            var query = from input in shipMethods
                                        where input.Id == c.ShipMethodId
                                        select input;
                            if (query.Count() == 0) shouldDelete = true;
                        }

                        if (shouldDelete)
                        {
                            var deleteMe = (from uc in _sfDb.UserShipMethods
                                            where uc.Id == c.Id
                                            select uc).FirstOrDefault();
                            _sfDb.UserShipMethods.Remove(deleteMe);
                        }
                    }

                    // added ShipMethod
                    if (curSelectedUserShipMethodList != null)
                    {
                        foreach (var c in curSelectedUserShipMethodList)
                        {
                            var query = from prevUc in prevUserShipMethodList
                                        where prevUc.ShipMethodId == c.Id
                                        select prevUc;
                            if (query.Count() == 0)
                            {
                                var addMe = new UserShipMethod()
                                {
                                    AspNetUserId = user.AspNetUserId,
                                    ShipMethodId = c.Id
                                };
                                _sfDb.UserShipMethods.Add(addMe);
                            }
                        }
                    }

                    _sfDb.SaveChanges();

                    return Json(new { result = "Success" }, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    // Save the ShipMethods anyways in the Session
                    Session["ShipMethodsSelected"] = shipMethods;

                    return Json(new { result = "ProductId is 0" }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { message = "Error : " + ex.Message });
            }
        }

        [TokenAuthorize]
        [HttpPost]
        public ActionResult UserBudgetLimitRefreshNow(UsersViewModel user)
        {
            try
            {
                UserSetting selectedUserSetting = (from us in _sfDb.UserSettings
                                                   where us.AspNetUserId == user.AspNetUserId && us.StoreFrontId == _site.StoreFrontId
                                                   select us).FirstOrDefault();
                selectedUserSetting.BudgetLimit = user.BudgetLimit;
                _sfDb.Entry(selectedUserSetting).State = EntityState.Modified;
                _sfDb.SaveChanges();               
                return Json(new { message = "Success", BudgetLimit = user.BudgetLimit });
            }
            catch (Exception ex)
            {
                //return Json(new { message = "Error", errordetail = ex.Message + "\n" + ex.InnerException.Message + "\n" + ex.InnerException.InnerException.Message });
                return Json(new { message = "Error", errordetail = ex.Message
    });
            }
        }

        [TokenAuthorize]
        [HttpPost]
        public ActionResult UserBudgetRefreshNow(UsersViewModel user)
        {
            try
            {
                UserSetting selectedUserSetting = (from us in _sfDb.UserSettings
                                                   where us.AspNetUserId == user.AspNetUserId && us.StoreFrontId == _site.StoreFrontId
                                                   select us).FirstOrDefault();
                SystemSetting selectSystemSetting = (from ss in _sfDb.SystemSettings
                                                     where ss.StoreFrontId == _site.StoreFrontId
                                                     select ss).FirstOrDefault();

                //selectedUserSetting.BudgetLimit = selectSystemSetting.BudgetLimitDefault;
                //selectedUserSetting.BudgetLimit = user.BudgetLimit;
                selectedUserSetting.BudgetCurrentTotal = 0;
                selectedUserSetting.BudgetLastResetDate = DateTime.Now.Date;
                selectedUserSetting.BudgetResetInterval = selectSystemSetting.BudgetRefreshPeriodDefault;
                selectedUserSetting.AdditionalBudgetLimit = 0;
                //selectedUserSetting.BudgetNextResetDate = DateTime.Now.Date;
                selectedUserSetting.BudgetNextResetDate = DateTime.Now.Date.AddDays(Convert.ToDouble(selectSystemSetting.BudgetRefreshPeriodDefault)).Date;

                _sfDb.Entry(selectedUserSetting).State = EntityState.Modified;
                _sfDb.SaveChanges();

                string Subject = "";
                string Body = "";
                Subject = "SFDYN - A BUDGET HAS REFRESHED";
                Body = "Good News! A budget has refreshed!<br />" +
                "<br />" +
                "Budget Type: " + selectSystemSetting.BudgetType.ToString() + "): <br />" +
                "Budget Limit: " + (selectedUserSetting.BudgetLimit).ToString("c") + "<br />" +
                "Refresh Date: " + (selectedUserSetting.BudgetLastResetDate).ToString("MM/dd/yyyy") + "<br />" +
                "<br />" +
                "To view more information related to this budget refresh, please log in. <br />" +
                "**** PLEASE NOTE: THIS IS AN AUTOMATED EMAIL - PLEASE DO NOT REPLY ****</p>";

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

                if (selectedUserSetting.AlertOnBudgetRefreshRequest == 1)
                {
                    mail.To.Add(user.Email);
                    smtp.Send(mail);
                    //await Task.Delay(200);
                }

                var newBudgetDaysUntilRefresh = BudgetFunctions.GetRemainingBudgetDaysUntilRefresh(_site.StoreFrontId, user.AspNetUserId, selectSystemSetting.BudgetRefreshSystemWide, selectSystemSetting.BudgetNextRefreshDate, selectedUserSetting.BudgetNextResetDate);
                //return Json(new { message = "Success", BudgetLimit = user.BudgetLimit });  
                return Json(new { message = "Success", BudgetDaysUntilRefresh = newBudgetDaysUntilRefresh });  
            }
            catch (Exception ex)
            {
                //return Json(new { message = "Error", errordetail = ex.Message + "\n" + ex.InnerException.Message + "\n" + ex.InnerException.InnerException.Message });
                return Json(new { message = "Error", errordetail = ex.Message });
            }
        }

        [TokenAuthorize]
        [HttpPost]
        public ActionResult sendBudgetAlert(UsersViewModel user)
        {
            try
            {
                UserSetting selectedUserSetting = (from us in _sfDb.UserSettings
                                                   where us.AspNetUserId == user.AspNetUserId && us.StoreFrontId == _site.StoreFrontId
                                                   select us).FirstOrDefault();
                SystemSetting selectSystemSetting = (from ss in _sfDb.SystemSettings
                                                     where ss.StoreFrontId == _site.StoreFrontId
                                                     select ss).FirstOrDefault();
                AspNetUser selectAspNetUser = (from nu in _sfDb.AspNetUsers
                                               where nu.Id == user.AspNetUserId
                                               select nu).FirstOrDefault();

                if (selectedUserSetting.AlertOnBudgetRefreshRequest == 1)
                {
                    var sendMail = new SendMail();
                    sendMail.From = selectSystemSetting.NotifyFromEmail;
                    sendMail.To = selectAspNetUser.Email;
                    //sendMail.CC = "kenchengpa@gmail.com,kenchengwa@gmail.com";
                    sendMail.Subject = "Budget refreshed";
                    sendMail.Body = "123 456 789 0";

                    MailMessage mail = new MailMessage();
                    mail.To.Add(sendMail.To);
                    mail.CC.Add(sendMail.CC);
                    mail.From = new MailAddress(sendMail.From);
                    mail.Subject = sendMail.Subject;
                    mail.Body = sendMail.Body;
                    mail.IsBodyHtml = true;
                    SmtpClient smtp = new SmtpClient();
                    smtp.Host = "mail.c2devgrp.com";
                    smtp.Port = 25;
                    smtp.UseDefaultCredentials = false;
                    smtp.Credentials = new System.Net.NetworkCredential("alerts@c2devgrp.com", "atl@sn0rth"); // Enter seders User name and password  
                    //smtp.EnableSsl = true;
                    smtp.EnableSsl = false;
                    smtp.Send(mail);
                }

                return Json(new { message = "Success", alertdatetime = DateTime.Now.Date.ToString("MM/dd/yyyy") });
            }
            catch (Exception ex)
            {
                //return Json(new { message = "Error", errordetail = ex.Message + "\n" + ex.InnerException.Message + "\n" + ex.InnerException.InnerException.Message });
                return Json(new { message = "Error", errordetail = ex.Message });
            }
        }

        // GET: Admin/UserDetail/5
        [TokenAuthorize]
        public ActionResult UserDetail(int? id, string statusMessage, string tab)
        {
            try
            {
                Session["CategoriesSelected"] = new List<CategoryViewModel>();
                UsersViewModel model = new UsersViewModel();
                //bool isAdmin = false;
                AspNetUser user = _sfDb.AspNetUsers.Where(u => u.SfId == id).FirstOrDefault();
                var userRoleList = _sfDb.AspNetRoles.Where(t => t.AspNetUsers.Any(u => u.SfId == id)).Select(r => r.Name).ToList();

                model.Id = user.SfId;
                model.AspNetUserId = user.Id;
                model.Email = user.Email;
                model.UserName = user.UserName;
                model.Company = user.Company;
                model.CompanyAlias = user.CompanyAlias;
                model.FirstName = user.FirstName;
                model.LastName = user.LastName;
                model.Address1 = user.Address1;
                model.Address2 = user.Address2;
                model.City = user.City;
                model.State = user.State;
                model.Zip = user.Zip;
                model.Country = user.Country ?? "US";
                model.Phone = user.Phone;
                model.AccessRestricted = user.AccessRestricted == 1 ? true : false;
                model.UserRole = userRoleList.FirstOrDefault();
                model.Status = (short)user.Status;
                model.FacilityId = user.FacilityId == null ? 0 : (int)user.FacilityId;
                model.OnHold = user.OnHold == 1 ? true : false;

                model.Permission = (from up in _sfDb.UserPermissions
                                    where up.AspNetUserId == model.AspNetUserId && up.StoreFrontId == _site.StoreFrontId
                                    select up).FirstOrDefault();

                SystemSetting systemSetting = _sfDb.SystemSettings.Where(ss => ss.StoreFrontId == _site.StoreFrontId).FirstOrDefault();
                UserGroupUser userGroupUser = _sfDb.UserGroupUsers.Where(y => y.AspNetUserId == model.AspNetUserId && y.StoreFrontId == _site.StoreFrontId).FirstOrDefault();
                model.Settings = _sfDb.UserSettings.Where(us => us.AspNetUserId == model.AspNetUserId && us.StoreFrontId == _site.StoreFrontId).FirstOrDefault();
                if (model.Settings == null)
                {
                    model.Settings = new UserSetting()
                    {
                        AspNetUserId = model.AspNetUserId,
                        StoreFrontId = _site.StoreFrontId,
                        BudgetIgnore = 0,
                        BudgetLimit = systemSetting.BudgetLimitDefault,
                        BudgetCurrentTotal = 0,
                        BudgetResetInterval = systemSetting.BudgetRefreshPeriodDefault, // not used use the system setting period
                        BudgetLastResetDate = DateTime.Now,
                        BudgetNextResetDate = DateTime.Now,
                        AlertOrderReceived = 0,
                        AlertOrderShipped = 0,
                        AlertOnBudgetRefreshRequest = 0,
                    };
                    _sfDb.UserSettings.Add(model.Settings);
                    _sfDb.SaveChanges();
                }
                //model.BudgetLimit = model.Settings.BudgetLimit;
                //model.BudgetCurrentTotal = model.Settings.BudgetCurrentTotal;
                model.BudgetRefreshPeriod = systemSetting.BudgetRefreshPeriodDefault.ToString();
                model.AllowAdminAccess = model.Settings.AllowAdminAccess == 1;
                model.AlertOrderReceived = model.Settings.AlertOrderReceived == 1 ? true : false;
                model.AlertOrderShipped = model.Settings.AlertOrderShipped == 1 ? true : false;
                model.AlertOnBudgetRefreshRequest = model.Settings.AlertOnBudgetRefreshRequest == 1 ? true : false;
                model.AlertOrderReceivedFor = model.Settings.AlertOrderReceivedFor;
                model.AlertOrderShippedFor = model.Settings.AlertOrderShippedFor;

                model.DefaultCurrency = model.Settings.DefaultCurrency;
                model.BudgetEnforce = systemSetting.BudgetEnforce == 1 ? true : false;
                model.BudgetMethod = systemSetting.BudgetType;
                model.DefaultBudget = systemSetting.BudgetLimitDefault;

                //BY "Group Based Budget" / "User Based Budget"
                if (userGroupUser != null && model.BudgetMethod == "Group Based Budget")
                {
                    //model.BudgetCurrentTotal = model.BudgetLimit - BudgetFunctions.GetBudgetRemaining(_site.StoreFrontId, userGroupUser);
                    //model.BudgetRemaning = BudgetFunctions.GetBudgetRemaining(_site.StoreFrontId, userGroupUser); 
                    model.BudgetLimit = _sfDb.UserGroups.Where(x => x.Id == userGroupUser.UserGroupId && x.StoreFrontId == _site.StoreFrontId).FirstOrDefault().PriceLimit ?? 0;
                    model.BudgetRemaning = _sfDb.UserGroups.Where(x => x.Id == userGroupUser.UserGroupId && x.StoreFrontId == _site.StoreFrontId).FirstOrDefault().CurrentBudgetLeft ?? 0;
                    var NextResetDate = _sfDb.UserGroups.Where(x => x.Id == userGroupUser.UserGroupId && x.StoreFrontId == _site.StoreFrontId).FirstOrDefault().TimeRefresh ?? DateTime.Now;
                    model.BudgetCurrentTotal = model.BudgetLimit - model.BudgetRemaning;
                    model.AdditionalBudgetLimit = 0;
                    model.BudgetDaysUntilRefresh = BudgetFunctions.GetRemainingBudgetDaysUntilRefresh(_site.StoreFrontId, model.AspNetUserId, systemSetting.BudgetRefreshSystemWide, systemSetting.BudgetNextRefreshDate, NextResetDate);
                }
                else if (model.BudgetMethod == "User Based Budget")
                {
                    model.BudgetLimit = model.Settings.BudgetLimit;
                    model.BudgetCurrentTotal = model.Settings.BudgetCurrentTotal;
                    model.BudgetRemaning = model.BudgetLimit - model.BudgetCurrentTotal;
                    model.AdditionalBudgetLimit = model.Settings.AdditionalBudgetLimit ?? 0;
                    model.BudgetDaysUntilRefresh = BudgetFunctions.GetRemainingBudgetDaysUntilRefresh(_site.StoreFrontId, model.AspNetUserId, systemSetting.BudgetRefreshSystemWide, systemSetting.BudgetNextRefreshDate, model.Settings.BudgetNextResetDate);
                }
                //model.BudgetRemaning = BudgetFunctions.GetBudgetRemaining(_site.StoreFrontId, model.AspNetUserId, userGroupUser);
                //model.BudgetDaysUntilRefresh = BudgetFunctions.GetRemainingBudgetDaysUntilRefresh(_site.StoreFrontId, user.Id, systemSetting.BudgetRefreshSystemWide, systemSetting.BudgetNextRefreshDate, userSetting.BudgetNextResetDate);
                else
                {
                    model.BudgetLimit = 0;
                    model.BudgetCurrentTotal = 0;
                    model.BudgetRemaning = 0;
                    model.AdditionalBudgetLimit = 0;
                    model.BudgetDaysUntilRefresh = 0;
                }

                model.UserCategories = (from uc in _sfDb.UserCategories
                                        join c in _sfDb.Categories on uc.CategoryId equals c.Id
                                        where uc.AspNetUserId == model.AspNetUserId && c.StoreFrontId == _site.StoreFrontId
                                        select new UserCategoriesVM()
                                            {
                                                Id = uc.Id,                                                
                                                AspNetUserId = uc.AspNetUserId,
                                                CategoryId = uc.CategoryId,
                                                Name = c.Name,
                                                Desc = c.Desc,
                                            }).OrderBy(c => c.Name).ToList();

                // Get the categories this product belongs to
                var selectedCategories = (from uc in _sfDb.UserCategories
                                          join c in _sfDb.Categories on uc.CategoryId equals c.Id
                                          where uc.AspNetUserId == model.AspNetUserId && c.StoreFrontId == _site.StoreFrontId
                                          select new CategoryViewModel()
                                          {
                                              Id = c.Id,
                                              Name = c.Name,
                                              Desc = c.Desc
                                          }).ToList();
                ViewBag.Categories = new SelectList(selectedCategories, "Name", "Name");

                var categoryUnselected1 = (from c in _sfDb.Categories
                                           where c.StoreFrontId == _site.StoreFrontId
                                           select new CategoryViewModel()
                                           {
                                               Id = c.Id,
                                               Name = c.Name,
                                               Desc = c.Desc
                                           }).ToList();
                var categoryUnselected = (from c in categoryUnselected1
                                          where !selectedCategories.Any(sc => sc.Id == c.Id)
                                          select new CategoryViewModel()
                                          {
                                              Id = c.Id,
                                              Name = c.Name,
                                              Desc = c.Desc
                                          }).ToList();
                ViewBag.availableCategories = categoryUnselected;

                ViewBag.StatusMessage = statusMessage;
                ViewBag.RoleNames = new SelectList(_identityDb.Roles.Where(u => !u.Name.Contains("SuperAdmin")).ToList(), "Name", "Name");
                ViewBag.Countries = new SelectList(_sfDb.Countries.ToList(), "Code", "Name");
                ViewBag.Days365 = Enumerable.Range(0, 366).Select(d => d.ToString());
                ViewBag.DisplayDefaultCurrencyList = new List<string>() { "USD", "CAD" };
                ViewBag.AlertOrderReceivedForList = new List<string>() { "All Orders", "For Orders Placed By Me Only", "For Orders Placed By My Group Members Only" };
                ViewBag.AlertOrderShippedForList = new List<string>() { "All Orders", "For Orders Placed By Me Only", "For Orders Placed By My Group Members Only" };

                if (tab != null)
                    ViewBag.Tab = tab;
                else
                    ViewBag.Tab = "summary";

                return View(model);
            }
            catch (Exception ex)
            {
                return View("Error", new HandleErrorInfo(ex, "Dashboard", "MyWindow"));
            }

        }

        // POST: Admin/UserDetail/5
        [TokenAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> UserDetail(UsersViewModel model)
        {
            try
            {
                ViewBag.Countries = new SelectList(_sfDb.Countries.ToList(), "Code", "Name");

                if (ModelState.IsValid)
                {
                    AspNetUser selectedUser = _sfDb.AspNetUsers.Find(model.AspNetUserId);

                    selectedUser.Email = model.Email;
                    selectedUser.UserName = model.UserName;

                    //var checkPassword = new PasswordHasher();
                    //selectedUser.PasswordHash = checkPassword.HashPassword(model.Password);
                    selectedUser.Company = model.Company;
                    selectedUser.CompanyAlias = model.CompanyAlias;
                    selectedUser.FirstName = model.FirstName;
                    selectedUser.LastName = model.LastName;
                    selectedUser.Address1 = model.Address1;
                    selectedUser.Address2 = model.Address2;
                    selectedUser.City = model.City;
                    selectedUser.State = model.State;
                    selectedUser.Zip = model.Zip;
                    selectedUser.Country = model.Country;
                    selectedUser.Phone = model.Phone;
                    selectedUser.AccessRestricted = model.AccessRestricted ? 1 : 0;
                    selectedUser.Status = (short)model.Status;
                    selectedUser.FacilityId = model.FacilityId;
                    selectedUser.OnHold = model.OnHold ? 1 : 0;
                    selectedUser.IsVendor = 0;

                    // Check admin status changed
                    bool isAdmin = false;
                    var user = _sfDb.AspNetUsers.Where(u => u.SfId == selectedUser.SfId).Select(u => u).FirstOrDefault();
                    var userRoleList = _sfDb.AspNetRoles.Where(t => t.AspNetUsers.Any(u => u.SfId == selectedUser.SfId)).Select(r => r.Name).ToList();

                    UserSetting selectedUserSetting = (from us in _sfDb.UserSettings
                                                       where us.AspNetUserId == model.AspNetUserId && us.StoreFrontId == _site.StoreFrontId
                                                       select us).FirstOrDefault();
                    if (selectedUserSetting == null ? false : selectedUserSetting.AllowAdminAccess == 1)
                    {
                        isAdmin = true;
                    }

                    if (model.UserRole == "Admin" && !isAdmin)
                    {
                        // add user to Admin role
                        await this.UserManager.AddToRoleAsync(user.Id, "Admin");
                    }
                    else if (model.UserRole == "User" && isAdmin)
                    {
                        // remove from Admin role
                        await this.UserManager.RemoveFromRoleAsync(user.Id, "Admin");
                    }

                    // Save user permissions
                    UserPermission selectedPermission = (from up in _sfDb.UserPermissions
                                                         where up.AspNetUserId == model.AspNetUserId && up.StoreFrontId == _site.StoreFrontId
                                                         select up).FirstOrDefault();
                    selectedPermission.AdminUserModify = model.Permission.AdminUserModify;
                    selectedPermission.AdminUserGroupModify = model.Permission.AdminUserGroupModify;
                    selectedPermission.AdminSettingModify = model.Permission.AdminSettingModify;
                    selectedPermission.InventoryItemModify = model.Permission.InventoryItemModify;
                    selectedPermission.InventoryRestrictCategory = model.Permission.InventoryRestrictCategory;
                    selectedPermission.InventoryCategoryModify = model.Permission.InventoryCategoryModify;
                    selectedPermission.OrderRestrictShipMethod = model.Permission.OrderRestrictShipMethod;
                    selectedPermission.OrderCreate = model.Permission.OrderCreate;
                    selectedPermission.OrderCancel = model.Permission.OrderCancel;
                    selectedPermission.VendorModify = model.Permission.VendorModify;

                    // Save user other settings
                    //UserSetting selectedUserSetting = _sfDb.UserSettings.Where(us => us.AspNetUserId == model.AspNetUserId && us.StoreFrontId == _site.StoreFrontId).FirstOrDefault();                   
                    //selectedUserSetting.BudgetCurrentTotal = model.Settings.BudgetCurrentTotal;                
                    selectedUserSetting.AllowAdminAccess = model.AllowAdminAccess ? 1 : 0;
                    selectedUserSetting.AlertOrderReceived = model.AlertOrderReceived ? 1 : 0;
                    selectedUserSetting.AlertOrderShipped = model.AlertOrderShipped ? 1 : 0;
                    selectedUserSetting.AlertOnBudgetRefreshRequest = model.AlertOnBudgetRefreshRequest ? 1 : 0;
                    selectedUserSetting.AlertOrderReceivedFor = model.AlertOrderReceivedFor;
                    selectedUserSetting.AlertOrderShippedFor = model.AlertOrderShippedFor;
                    selectedUserSetting.DefaultCurrency = model.DefaultCurrency;
                    selectedUserSetting.BudgetIgnore = model.Settings.BudgetIgnore; 
                    //if (selectedUserSetting == null)
                    //{
                    //    selectedUserSetting.BudgetIgnore = model.Settings.BudgetIgnore;
                    //    selectedUserSetting.BudgetLimit = model.Settings.BudgetLimit;
                    //    selectedUserSetting.BudgetResetInterval = Convert.ToInt16(model.BudgetRefreshPeriod); // this contains the SystemSettings.BudgetRefreshPeriodDefault
                    //    selectedUserSetting.BudgetLastResetDate = DateTime.Today;
                    //    selectedUserSetting.BudgetNextResetDate = DateTime.Today.AddDays(model.Settings.BudgetResetInterval.Value);
                    //    selectedUserSetting.DefaultCurrency = "USD";
                    //}

                    _sfDb.Entry(selectedUserSetting).State = EntityState.Modified;
                    _sfDb.Entry(selectedPermission).State = EntityState.Modified;
                    _sfDb.Entry(selectedUser).State = EntityState.Modified;
                    _sfDb.SaveChanges();


                    try
                    {
                        // Save the product categories 
                        var CategoriesSelected = (List<CategoryViewModel>)Session["CategoriesSelected"];
                        if (CategoriesSelected != null && CategoriesSelected.Count > 0)
                        {
                            foreach (var c in CategoriesSelected)
                            {
                                UserCategory selecteduserCategory = new UserCategory();
                                selecteduserCategory.AspNetUserId = model.AspNetUserId;
                                selecteduserCategory.CategoryId = c.Id;
                                _sfDb.UserCategories.Add(selecteduserCategory);
                                _sfDb.SaveChanges();
                            }
                        }
                    }
                    catch
                    {

                    }

                    return RedirectToAction("UserDetail", "Admin", new { id = model.Id, statusMessage = "Saved Sucessfully" });
                }

                ViewBag.RoleNames = new SelectList(_identityDb.Roles.Where(u => !u.Name.Contains("SuperAdmin")).ToList(), "Name", "Name");
                ViewBag.Days365 = Enumerable.Range(0, 366).Select(d => d.ToString());
                ViewBag.DisplayDefaultCurrencyList = new List<string>() { "USD", "CAD" };
                ViewBag.AlertOrderReceivedForList = new List<string>() { "All Orders", "For Orders Placed By Me Only", "For Orders Placed By My Group Members Only" };
                ViewBag.AlertOrderShippedForList = new List<string>() { "All Orders", "For Orders Placed By Me Only", "For Orders Placed By My Group Members Only" };

                return View(model);
            }
            catch (Exception ex)
            {
                return View("Error", new HandleErrorInfo(ex, "Admin", "Users"));
            }
        }

        // GET: Admin/UserAdd
        [TokenAuthorize]
        public ActionResult UserAdd()
        {
            UsersViewModel model = new UsersViewModel();
            model.Status = 1;
            model.UserRole = "User";
            model.Country = "US";

            ViewBag.RoleNames = new SelectList(_identityDb.Roles.Where(u => !u.Name.Contains("SuperAdmin")).ToList(), "Name", "Name");
            ViewBag.Countries = new SelectList(_sfDb.Countries.ToList(), "Code", "Name");

            return View(model);
        }

        // POST: Admin/UserAdd
        [TokenAuthorize]
        [HttpPost]
        public async Task<ActionResult> UserAdd(UsersViewModel model)
        {
            try
            {
                ViewBag.Countries = new SelectList(_sfDb.Countries.ToList(), "Code", "Name");

                if (model.Password == null || model.ConfirmPassword == null)
                {
                    ModelState.AddModelError("", "Password / Confirmation invalid");
                    ViewBag.RoleNames = new SelectList(_identityDb.Roles.Where(u => !u.Name.Contains("SuperAdmin")).ToList(), "Name", "Name");
                    return View(model);
                }

                if (model.UserRole == null)
                {
                    //ModelState.AddModelError("", "Please select Role");
                    //ViewBag.RoleNames = new SelectList(_identityDb.Roles.Where(u => !u.Name.Contains("SuperAdmin")).ToList(), "Name", "Name");
                    //return View(model);
                    model.UserRole = "User";
                }

                if (ModelState.IsValid)
                {
                    var user = new ApplicationUser { UserName = model.UserName, Email = model.Email };
                    var result = await UserManager.CreateAsync(user, model.Password);
                    if (result.Succeeded)
                    {
                        await this.UserManager.AddToRoleAsync(user.Id, model.UserRole); // 9/2/19 -PS- add roles

                        // Add the profile data
                        AspNetUser selectedUser = _sfDb.AspNetUsers.Find(user.Id);
                        SystemSetting systemSetting = _sfDb.SystemSettings.Where(ss => ss.StoreFrontId == _site.StoreFrontId).FirstOrDefault();

                        UserSetting newUserSetting = new UserSetting()
                        {
                            AspNetUserId = user.Id,
                            StoreFrontId = _site.StoreFrontId,
                            BudgetIgnore = 0,
                            BudgetCurrentTotal = 0,
                            //BudgetLastResetDate = new DateTime(1, 1, 1),
                            //BudgetNextResetDate = new DateTime(1, 1, 1),
                            BudgetLastResetDate = DateTime.Today,
                            BudgetNextResetDate = DateTime.Today.AddDays(systemSetting.BudgetRefreshPeriodDefault.Value),
                            BudgetResetInterval = systemSetting.BudgetRefreshPeriodDefault,
                            BudgetLimit = systemSetting.BudgetLimitDefault,
                            AllowAdminAccess = model.AllowAdminAccess ? 1 : 0,
                            //AlertOrderReceived = model.AlertOrderReceived ? 1 : 0,
                            //AlertOrderShipped = model.AlertOrderShipped ? 1 : 0,
                            //AlertOnBudgetRefreshRequest = model.AlertOnBudgetRefreshRequest ? 1 : 0,
                            //AlertOrderReceivedFor = model.AlertOrderReceivedFor,
                            //AlertOrderShippedFor = model.AlertOrderShippedFor,
                            DefaultCurrency = "USD",
                        };
                        _sfDb.UserSettings.Add(newUserSetting);

                        List<UserStoreFront> selectedUserStoreFronts = new List<UserStoreFront>();
                        selectedUserStoreFronts.Add(new UserStoreFront() { AspNetUserId = user.Id, StoreFrontId = _site.StoreFrontId });

                        List<UserPermission> selectedUserPermissions = new List<UserPermission>();
                        selectedUserPermissions.Add(new UserPermission()
                        {
                            AspNetUserId = user.Id,
                            StoreFrontId = _site.StoreFrontId,
                            AdminUserModify = 0,
                            AdminUserGroupModify = 0,
                            AdminSettingModify = 0,
                            InventoryItemModify = 0,
                            InventoryRestrictCategory = 0,
                            InventoryCategoryModify = 0,
                            OrderRestrictShipMethod = 0,
                            OrderCreate = 1,
                            OrderCancel = 0,
                            VendorModify = 0,
                        });

                        selectedUser.Email = model.Email;
                        selectedUser.UserName = model.UserName;
                        selectedUser.Company = model.Company;
                        selectedUser.CompanyAlias = model.CompanyAlias;
                        selectedUser.FirstName = model.FirstName;
                        selectedUser.LastName = model.LastName;
                        selectedUser.Address1 = model.Address1;
                        selectedUser.Address2 = model.Address2;
                        selectedUser.City = model.City;
                        selectedUser.State = model.State;
                        selectedUser.Zip = model.Zip;
                        selectedUser.Country = model.Country;
                        selectedUser.Phone = model.Phone;
                        selectedUser.AccessRestricted = model.AccessRestricted ? 1 : 0;
                        selectedUser.Status = 1;
                        selectedUser.FacilityId = model.FacilityId;
                        selectedUser.OnHold = model.OnHold ? 1 : 0;
                        selectedUser.IsVendor = 0;

                        //foreach (var usf in selectedUserStoreFronts) { selectedUser.UserStoreFronts.Add(usf); }
                        foreach (var up in selectedUserPermissions) { _sfDb.UserPermissions.Add(up); }
                        foreach (var usf in selectedUserStoreFronts) { _sfDb.UserStoreFronts.Add(usf); }

                        try
                        {
                            _sfDb.Entry(selectedUser).State = EntityState.Modified;
                            _sfDb.SaveChanges();

                        }
                        catch (Exception ex)
                        {
                            if (Request.IsAjaxRequest())
                            {
                                Debug.WriteLine("Error : " + ex.Message);
                                return Json(new { result = "error", message = ex.Message });
                            }
                            else
                            {
                                return View("Error", new HandleErrorInfo(ex, "Admin", "Users"));
                            }
                        }

                        if (Request.IsAjaxRequest())
                            return Json(new { result = "success" });
                        else
                            return RedirectToAction("Users", "Admin", new { id = model.Id });
                    }
                    else
                    {
                        // Update user
                        if (Request.IsAjaxRequest())
                        {
                            if (result.Errors.Contains("Name " + user.UserName + " is already taken."))
                            {
                                AspNetUser selectedUser = _sfDb.AspNetUsers.Find(user.Id);
                                selectedUser.Email = model.Email;
                                selectedUser.Company = model.Company;
                                selectedUser.CompanyAlias = model.CompanyAlias;
                                selectedUser.FirstName = model.FirstName;
                                selectedUser.LastName = model.LastName;
                                selectedUser.Address1 = model.Address1;
                                selectedUser.Address2 = model.Address2;
                                selectedUser.City = model.City;
                                selectedUser.State = model.State;
                                selectedUser.Zip = model.Zip;
                                selectedUser.Country = model.Country;
                                selectedUser.Phone = model.Phone;
                                selectedUser.AccessRestricted = model.AccessRestricted ? 1 : 0;
                                selectedUser.Status = (short)model.Status;
                                selectedUser.OnHold = model.OnHold ? 1 : 0;
                                try
                                {
                                    _sfDb.Entry(selectedUser).State = EntityState.Modified;
                                    _sfDb.SaveChanges();
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine("Error : " + ex.Message);
                                    return Json(new { result = "error", message = ex.Message });
                                }
                                return Json(new { result = "updated", message = "updated " + user.UserName });
                            }
                            else
                            {
                                return Json(new { result = "error", message = result.Errors.FirstOrDefault() });
                            }
                        }
                        else
                        {
                            ModelState.AddModelError("", result.Errors.FirstOrDefault());
                            ViewBag.RoleNames = new SelectList(_identityDb.Roles.Where(u => !u.Name.Contains("SuperAdmin")).ToList(), "Name", "Name");
                            return View(model);
                        }
                    }
                }
                if (Request.IsAjaxRequest())
                {
                    string errorMessage = "";
                    foreach (var value in ModelState.Values)
                    {
                        foreach (var error in value.Errors)
                        {
                            errorMessage += error.ErrorMessage + "\r\n";
                        }
                    }
                    return Json(new { result = "error", message = model.UserName + " : " + errorMessage });
                }
                else
                    return View(model);
            }
            catch (Exception ex)
            {
                if (Request.IsAjaxRequest())
                    return Json(new { result = "Error", message = ex.Message });
                else
                    return View("Error", new HandleErrorInfo(ex, "Admin", "Users"));
            }

        }

        // POST: Admin/UserDelete/5 (not used currently)
        [TokenAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UserDelete(UsersViewModel model)
        {
            return RedirectToAction("Users", "Admin");
        }

        [TokenAuthorize]
        [HttpPost]
        public ActionResult User_ChangePassword(string userId, string password)
        {
            try
            {
                IdentityResult result;
                result = UserManager.RemovePassword(userId);
                if (result.Succeeded)
                {
                    result = UserManager.AddPassword(userId, password);
                    if (result.Succeeded)
                    {
                        return Json(new { result = "Success", message = "Password Changed Successfully" });
                    }
                    else
                    {
                        return Json(new { result = "Error", message = result.Errors });
                    }
                }
                return Json(new { result = "Error", message = "Password Removal Error" });
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }

        }

        // POST: /Admin/ChangeUserName
        [TokenAuthorize]
        [HttpPost]
        public async Task<ActionResult> ChangeUserName(string aspNetUserId, string userName)
        {
            try
            {
                var user = await UserManager.FindByIdAsync(aspNetUserId);
                if (user == null)
                {
                    return Json(new { message = "No user found" });
                }
                else
                {
                    AspNetUser userStoreFront = _sfDb.AspNetUsers.Where(u => u.Id == aspNetUserId).FirstOrDefault();
                    userStoreFront.UserName = userName;
                    _sfDb.Entry(userStoreFront).State = EntityState.Modified;
                    _sfDb.SaveChanges();

                    return Json(new { message = "Username changed" });
                }

            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }

        // POST: /Account/ResetPassword
        [TokenAuthorize]
        [HttpPost]
        public async Task<ActionResult> User_ResetPassword(string userEmail)
        {
            try
            {
                var user = await UserManager.FindByEmailAsync(userEmail);
                if (user == null) // || !(await UserManager.IsEmailConfirmedAsync(user.Id)))
                {
                    return Json(new { message = "No user found with that email" });
                }

                string code = await UserManager.GeneratePasswordResetTokenAsync(user.Id);
                var callbackUrl = Url.Action("ResetPassword", "Account", new { userId = user.Id, code = code }, protocol: Request.Url.Scheme);
                await UserManager.SendEmailAsync(user.Id, "Reset Password", "Please reset your password by clicking <a href=\"" + callbackUrl + "\">here</a>");
                return Json(new { message = "Email sent" });
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }

        [TokenAuthorize]
        public ActionResult UI_ToggleStatus(int id)
        {
            try
            {
                int currentStatus = -1;

                var queryStatus = from p in _sfDb.AspNetUsers
                                  where p.SfId == id
                                  select p;

                if (queryStatus.Count() > 0)
                {
                    currentStatus = queryStatus.FirstOrDefault().Status;

                    if (currentStatus == 1)
                        currentStatus = 0;
                    else
                        currentStatus = 1;

                    AspNetUser selectedUser = queryStatus.FirstOrDefault();
                    selectedUser.Status = (short)currentStatus;
                    _sfDb.Entry(selectedUser).State = EntityState.Modified;
                    _sfDb.SaveChanges();
                }

                return Json(new { result = "Success", status = currentStatus }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", status = "Error", message = ex.Message });
            }
        }


        #endregion





        #region Vendors
        // GET: Admin/Vendors
        [TokenAuthorize]
        public ActionResult Vendors(FilterViewModel paramFilter)
        {
            try
            {
                if (paramFilter == null)
                {
                    paramFilter = new FilterViewModel()
                    {
                        Status = "Active"
                    };
                }
                return View(paramFilter);
            }
            catch (Exception ex)
            {
                return View("Error", new HandleErrorInfo(ex, "Dashboard", "MyWindow"));
            }
        }

        // GET: Admin/ReadVendors
        [TokenAuthorize]
        public ActionResult ReadVendors([DataSourceRequest] DataSourceRequest request)
        {
            try
            {
                List<VendorViewModel> vendorsAll = new List<VendorViewModel>();
                UserSetting selectedUserSetting = (from us in _sfDb.UserSettings
                                                   where us.AspNetUserId == _userSf.Id && us.StoreFrontId == _site.StoreFrontId
                                                   select us).FirstOrDefault();

                vendorsAll = (from v in _sfDb.Vendors
                              join u in _sfDb.AspNetUsers on v.AspNetUserId equals u.Id
                              where v.StoreFrontId == _site.StoreFrontId && u.IsVendor == 1
                              select new VendorViewModel()
                              {
                                  Id = v.Id,
                                  StoreFrontId = v.StoreFrontId,
                                  AspNetUserId = v.AspNetUserId,
                                  AspNetUserSfId = u.SfId,
                                  UserName = u.UserName,
                                  Alias = v.Alias,
                                  Company = u.Company,
                                  Address1 = u.Address1,
                                  Address2 = u.Address2,
                                  City = u.City,
                                  State = u.State,
                                  Zip = u.Zip,
                                  Country = u.Country,
                                  Phone = u.Phone,
                                  Email = u.Email,
                                  Status = u.Status == 1,
                              }).ToList();

                DataSourceResult modelList = vendorsAll.ToDataSourceResult(request);

                return Json(modelList);
            }
            catch (Exception ex)
            {
                return Json(new { message = "Error : " + ex.Message });
            }

        }

        // GET: Admin/VendorAdd
        [TokenAuthorize]
        public ActionResult VendorAdd()
        {
            VendorViewModel model = new VendorViewModel();
            model.Status = true;
            model.Country = "US";

            ViewBag.Countries = new SelectList(_sfDb.Countries.ToList(), "Code", "Name");

            return View(model);
        }

        // POST: Admin/VendorAdd
        [TokenAuthorize]
        [HttpPost]
        public async Task<ActionResult> VendorAdd(VendorViewModel model)
        {
            try
            {
                bool noerror = true;
                Vendor selectedVendor;
                ViewBag.Countries = new SelectList(_sfDb.Countries.ToList(), "Code", "Name");

                // Existing alias?
                selectedVendor = _sfDb.Vendors.FirstOrDefault(v => v.Alias == model.Alias);
                if (selectedVendor != null)
                {
                    ModelState.AddModelError("Alias", "Duplicate alias, please enter different alias");
                }

                if (model.Password == null || model.ConfirmPassword == null)
                {
                    ModelState.AddModelError("", "Password / Confirmation invalid");
                    return View(model);
                }

                if (ModelState.IsValid)
                {
                    var user = new ApplicationUser { UserName = model.UserName, Email = model.Email };
                    var result = await UserManager.CreateAsync(user, model.Password);
                    if (result.Succeeded)
                    {
                        // Add the profile data
                        AspNetUser selectedUser = _sfDb.AspNetUsers.Find(user.Id);
                        SystemSetting systemSetting = _sfDb.SystemSettings.Where(ss => ss.StoreFrontId == _site.StoreFrontId).FirstOrDefault();

                        UserSetting newUserSetting = new UserSetting()
                        {
                            AspNetUserId = user.Id,
                            StoreFrontId = _site.StoreFrontId,
                            BudgetIgnore = 0,
                            BudgetCurrentTotal = 0,
                            BudgetLastResetDate = new DateTime(1, 1, 1),
                            BudgetNextResetDate = new DateTime(1, 1, 1),
                            BudgetResetInterval = systemSetting.BudgetRefreshPeriodDefault,
                            BudgetLimit = systemSetting.BudgetLimitDefault,
                            AllowAdminAccess = 0,
                            AlertOrderReceived = 0,
                            AlertOrderShipped = 0,
                            AlertOnBudgetRefreshRequest = 0,
                        };
                        _sfDb.UserSettings.Add(newUserSetting);

                        List<UserStoreFront> selectedUserStoreFronts = new List<UserStoreFront>();
                        selectedUserStoreFronts.Add(new UserStoreFront() { AspNetUserId = user.Id, StoreFrontId = _site.StoreFrontId });

                        List<UserPermission> selectedUserPermissions = new List<UserPermission>();
                        selectedUserPermissions.Add(new UserPermission()
                        {
                            AspNetUserId = user.Id,
                            StoreFrontId = _site.StoreFrontId,
                            AdminUserModify = 0,
                            AdminUserGroupModify = 0,
                            AdminSettingModify = 0,
                            InventoryItemModify = 0,
                            InventoryRestrictCategory = 0,
                            InventoryCategoryModify = 0,
                            OrderRestrictShipMethod = 0,
                            OrderCreate = 0,
                            OrderCancel = 0,
                            VendorModify = 0,
                        });

                        selectedUser.Email = model.Email;
                        selectedUser.UserName = model.UserName;
                        selectedUser.Company = model.Company;
                        selectedUser.CompanyAlias = model.CompanyAlias;
                        selectedUser.FirstName = "";
                        selectedUser.LastName = "";
                        selectedUser.Address1 = model.Address1;
                        selectedUser.Address2 = model.Address2;
                        selectedUser.City = model.City;
                        selectedUser.State = model.State;
                        selectedUser.Zip = model.Zip;
                        selectedUser.Country = model.Country;
                        selectedUser.Phone = model.Phone;
                        selectedUser.AccessRestricted = 0;
                        selectedUser.Status = 1;
                        selectedUser.FacilityId = 0;
                        selectedUser.OnHold = 0;
                        selectedUser.IsVendor = 1;

                        foreach (var up in selectedUserPermissions) { _sfDb.UserPermissions.Add(up); }
                        foreach (var usf in selectedUserStoreFronts) { _sfDb.UserStoreFronts.Add(usf); }

                        Vendor newVendor = new Vendor()
                        {
                            StoreFrontId = _site.StoreFrontId,
                            AspNetUserId = selectedUser.Id,
                            Alias = model.Alias,
                        };

                        _sfDb.Vendors.Add(newVendor);

                        try
                        {
                            if (noerror)
                            {
                                _sfDb.Entry(selectedUser).State = EntityState.Modified;
                                _sfDb.SaveChanges();
                                GlobalFunctions.Log(_site.StoreFrontId, _userSf.Id, "", "Vendor Created", model.Alias);
                            }

                        }
                        catch (Exception ex)
                        {
                            return View("Error", new HandleErrorInfo(ex, "Admin", "Vendors"));
                        }

                        return RedirectToAction("Vendors", "Admin");
                    }
                    else
                    {
                        foreach (string error in result.Errors)
                        {
                            ModelState.AddModelError("", error);
                        }
                        return View(model);
                    }
                }
                return View(model);
            }
            catch (Exception ex)
            {
                return View("Error", new HandleErrorInfo(ex, "Admin", "Vendors"));
            }
        }

        // GET: Admin/VendorDetail/5
        [TokenAuthorize]
        public ActionResult VendorDetail(int? id, string statusMessage, string tab)
        {
            try
            {
                VendorViewModel model = new VendorViewModel();
                Vendor vendor = _sfDb.Vendors.Where(v => v.Id == id).FirstOrDefault();
                AspNetUser user = _sfDb.AspNetUsers.Where(u => u.Id == vendor.AspNetUserId).FirstOrDefault();

                model.Id = vendor.Id;
                model.AspNetUserId = user.Id;
                model.AspNetUserSfId = user.SfId;
                model.Alias = vendor.Alias;
                model.Email = user.Email;
                model.UserName = user.UserName;
                model.Company = user.Company;
                model.Address1 = user.Address1;
                model.Address2 = user.Address2;
                model.City = user.City;
                model.State = user.State;
                model.Zip = user.Zip;
                model.Country = user.Country ?? "US";
                model.Phone = user.Phone;
                model.Status = user.Status == 1;

                ViewBag.StatusMessage = statusMessage;
                ViewBag.Countries = new SelectList(_sfDb.Countries.ToList(), "Code", "Name");

                if (tab != null)
                    ViewBag.Tab = tab;
                else
                    ViewBag.Tab = "summary";

                return View(model);
            }
            catch (Exception ex)
            {
                return View("Error", new HandleErrorInfo(ex, "Dashboard", "MyWindow"));
            }

        }

        // POST: Admin/UserDetail/5
        [TokenAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult VendorDetail(VendorViewModel model)
        {
            try
            {
                ViewBag.Countries = new SelectList(_sfDb.Countries.ToList(), "Code", "Name");

                if (ModelState.IsValid)
                {
                    AspNetUser selectedUser = _sfDb.AspNetUsers.Find(model.AspNetUserId);

                    selectedUser.Email = model.Email;
                    selectedUser.UserName = model.UserName;
                    selectedUser.Company = model.Company;
                    selectedUser.Address1 = model.Address1;
                    selectedUser.Address2 = model.Address2;
                    selectedUser.City = model.City;
                    selectedUser.State = model.State;
                    selectedUser.Zip = model.Zip;
                    selectedUser.Country = model.Country;
                    selectedUser.Phone = model.Phone;
                    selectedUser.AccessRestricted = 0;
                    selectedUser.Status = model.Status ? 1 : 0;
                    selectedUser.FacilityId = 0;
                    selectedUser.OnHold = 0;
                    selectedUser.IsVendor = 1;

                    _sfDb.Entry(selectedUser).State = EntityState.Modified;

                    Vendor selectedVendor = _sfDb.Vendors.Find(model.Id);

                    selectedVendor.Alias = model.Alias;
                    _sfDb.Entry(selectedVendor).State = EntityState.Modified;

                    _sfDb.SaveChanges();

                    return RedirectToAction("VendorDetail", "Admin", new { id = model.Id, statusMessage = "Saved Sucessfully" });
                }

                return View(model);
            }
            catch (Exception ex)
            {
                return View("Error", new HandleErrorInfo(ex, "Admin", "Users"));
            }
        }

        #endregion

        public JsonResult SaveLogo()
        {
            string filename = String.Empty;
            const string sessionKey = "UploadFILE";
            if (HttpContext.Request.Files != null && HttpContext.Request.Files.Count > 0 && HttpContext.Session != null)
            {
                List<HttpPostedFileBase> files = HttpContext.Session[sessionKey] as List<HttpPostedFileBase>;
                foreach (string fileName in HttpContext.Request.Files)
                {
                    HttpPostedFileBase newFile = HttpContext.Request.Files[fileName];
                    //if (files == null)
                    //{
                    files = new List<HttpPostedFileBase> { newFile };
                    //}
                    //else
                    //{
                    //    files.Add(newFile);
                    //}
                    HttpContext.Session[sessionKey] = files;
                    if (newFile != null)
                    {
                        filename = Path.GetFileName(newFile.FileName).Replace("\"", string.Empty).Replace("'", string.Empty);
                        var physicalPath = Path.Combine(Server.MapPath("~/Content/" + _site.StoreFrontName + "/Images"), filename);
                        newFile.SaveAs(physicalPath);
                    }
                }
            }
            return Json(new { Type = "Upload", FileName = filename, DateUploaded = DateTime.Now.ToString("MM/dd/yyyy") }, JsonRequestBehavior.AllowGet);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _sfDb.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
