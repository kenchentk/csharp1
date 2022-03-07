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
using System.Net;

namespace StoreFront2.Controllers
{
    public class DashboardController : Controller
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

        // GET: Dashboard
        [TokenAuthorize]
        public ActionResult MyWindow()
        {
            try
            {
                _site.AdminAsShopper = false;
                Session["Site"] = _site;

                var storeFrontList = from sf in _sfDb.StoreFronts
                                     join usf in _sfDb.UserStoreFronts on sf.Id equals usf.StoreFrontId
                                     where usf.AspNetUserId == _userSf.Id
                                     select sf;

                DateTime today = DateTime.Today;
                DateTime daysAgo30 = today.AddDays(-30).Date;
                DateTime daysAgo60 = today.AddDays(-60).Date;

                bool isSunday = today.DayOfWeek == 0;
                var dayOfWeek = isSunday == false ? (int)today.DayOfWeek : 7;
                DateTime weekFirstDate = today.AddDays(((int)dayOfWeek * -1) + 1).Date;

                DateTime monthFirstDate = new DateTime(today.Year, today.Month, 1).Date;

                int currentQtr = (int)((today.Month - 1) / 3) + 1;
                DateTime quarterFirstDate = new DateTime(today.Year, (currentQtr - 1) * 3 + 1, 1).Date;

                DateTime yearFirstDate = new DateTime(today.Year, 1, 1).Date;

                var storeFrontOrders = _sfDb.Orders.Where(o => o.StoreFrontId == _site.StoreFrontId);

                var orderReceived = new OrderStatusTotal()
                {
                    TotRP = storeFrontOrders.Where(o => o.OrderStatus == "RP").Count(),
                    TotIP = storeFrontOrders.Where(o => o.OrderStatus == "IP").Count(),
                    TotPS = storeFrontOrders.Where(o => o.OrderStatus == "PS").Count(),
                    TotOH = storeFrontOrders.Where(o => o.OrderStatus == "OH").Count(),
                    TotSH = storeFrontOrders.Where(o => o.OrderStatus == "SH").Count(),
                    TotBO = storeFrontOrders.Where(o => o.OrderStatus == "BO").Count(),
                    TotCN = storeFrontOrders.Where(o => o.OrderStatus == "CN").Count(),
                    TotPB = storeFrontOrders.Where(o => o.OrderStatus == "PB").Count(),
                    Tot30 = storeFrontOrders.Where(o => o.DateCreated >= daysAgo30).Count(),
                    DaysAgo30 = daysAgo30,
                    Tot60 = storeFrontOrders.Where(o => o.DateCreated >= daysAgo60).Count(),
                    DaysAgo60 = daysAgo60,
                    TotDay = storeFrontOrders.Where(o => DbFunctions.TruncateTime(o.DateCreated) == today).Count(),
                    Today = today,
                    TotWeek = storeFrontOrders.Where(o => o.DateCreated >= weekFirstDate).Count(),
                    WeekFirstDate = weekFirstDate,
                    TotMonth = storeFrontOrders.Where(o => o.DateCreated >= monthFirstDate).Count(),
                    MonthFirstDate = monthFirstDate,
                    TotQtr = storeFrontOrders.Where(o => o.DateCreated >= quarterFirstDate).Count(),
                    QuarterFirstDate = quarterFirstDate,
                    TotYtd = storeFrontOrders.Where(o => o.DateCreated >= yearFirstDate).Count(),
                    YearFirstDate = yearFirstDate,
                };

                ViewBag.OrderReceived = orderReceived;

                var orderShipped = new OrderStatusTotal()
                {
                    Tot30 = (from o in storeFrontOrders 
                             join ot in _sfDb.OrderTrackings on o.Id equals ot.OrderNumber
                             where o.OrderStatus == "SH" && ot.DateCreated >= daysAgo30
                             select o.Id).Count(),
                    DaysAgo30 = daysAgo30,
                    Tot60 = (from o in storeFrontOrders
                             join ot in _sfDb.OrderTrackings on o.Id equals ot.OrderNumber
                             where o.OrderStatus == "SH" && ot.DateCreated >= daysAgo60
                             select o.Id).Count(),
                    DaysAgo60 = daysAgo60,
                    TotDay = (from o in storeFrontOrders
                              join ot in _sfDb.OrderTrackings on o.Id equals ot.OrderNumber
                              where o.OrderStatus == "SH" && ot.DateCreated == today
                              select o.Id).Count(),
                    Today = today,
                    TotWeek = (from o in storeFrontOrders
                               join ot in _sfDb.OrderTrackings on o.Id equals ot.OrderNumber
                               where o.OrderStatus == "SH" && ot.DateCreated >= weekFirstDate
                               select o.Id).Count(),
                    WeekFirstDate = weekFirstDate,
                    TotMonth = (from o in storeFrontOrders
                                join ot in _sfDb.OrderTrackings on o.Id equals ot.OrderNumber
                                where o.OrderStatus == "SH" && ot.DateCreated >= monthFirstDate
                                select o.Id).Count(),
                    MonthFirstDate = monthFirstDate,
                    TotQtr = (from o in storeFrontOrders
                              join ot in _sfDb.OrderTrackings on o.Id equals ot.OrderNumber
                              where o.OrderStatus == "SH" && ot.DateCreated >= quarterFirstDate
                              select o.Id).Count(),
                    QuarterFirstDate = quarterFirstDate,
                    TotYtd = (from o in storeFrontOrders
                              join ot in _sfDb.OrderTrackings on o.Id equals ot.OrderNumber
                              where o.OrderStatus == "SH" && ot.DateCreated >= yearFirstDate
                              select o.Id).Count(),
                    YearFirstDate = yearFirstDate,
                };

                ViewBag.OrderShipped = orderShipped;

                SystemSetting systemSetting = _sfDb.SystemSettings.Where(ss => ss.StoreFrontId == _site.StoreFrontId).FirstOrDefault();
                UserSetting userSetting = _sfDb.UserSettings.Where(u => u.AspNetUserId == _userSf.Id && u.StoreFrontId == _site.StoreFrontId).FirstOrDefault();
                DashboardMyWindowViewModel model = new DashboardMyWindowViewModel();
                //DateTime systemWideNextBudgetRefresh = new DateTime(DateTime.Now.Year, systemSetting.BudgetRefreshStartDate.Month, systemSetting.BudgetRefreshStartDate.Day);
                /*DateTime systemWideNextBudgetRefresh = new DateTime(systemSetting.BudgetRefreshStartDate.Year, systemSetting.BudgetRefreshStartDate.Month, systemSetting.BudgetRefreshStartDate.Day);
                if (DateTime.Compare(DateTime.Now.Date, systemWideNextBudgetRefresh.Date) < 0)
                    //systemWideNextBudgetRefresh = systemWideNextBudgetRefresh.AddDays(systemWideNextBudgetRefresh.Subtract(DateTime.Now.Date).TotalDays);
                    systemWideNextBudgetRefresh = systemWideNextBudgetRefresh;
                else
                    systemWideNextBudgetRefresh = new DateTime(DateTime.Now.Year + 1, systemWideNextBudgetRefresh.Month, systemWideNextBudgetRefresh.Day);
                */
                //model.DefaultBudgetLimit = systemSetting.BudgetLimitDefault;
                //model.BudgetRefreshDate = systemWideNextBudgetRefresh;
                //model.BudgetDaysUntilRefresh = Convert.ToInt32(BudgetFunctions.GetSystemWideDaysRemaining(_site.StoreFrontId, systemSetting.BudgetRefreshSystemWide, systemSetting.BudgetNextRefreshDate));

                //DateTime systemWideNextBudgetRefresh = new DateTime(systemSetting.BudgetNextRefreshDate.Year, systemSetting.BudgetNextRefreshDate.Month, systemSetting.BudgetNextRefreshDate.Day);
                //DateTime userBudgetRefreshDate = new DateTime(userSetting.BudgetNextResetDate.Year, userSetting.BudgetNextResetDate.Month, userSetting.BudgetNextResetDate.Day);
                //DateTime groupBudgetRefreshDate = new DateTime(userSetting.BudgetNextResetDate.Year, userSetting.BudgetNextResetDate.Month, userSetting.BudgetNextResetDate.Day);

                /*if (DateTime.Compare(userBudgetRefreshDate, systemWideNextBudgetRefresh.Date) < 0)
                    model.BudgetRefreshDate = userBudgetRefreshDate;
                else
                    model.BudgetRefreshDate = systemWideNextBudgetRefresh;
                model.BudgetDaysUntilRefresh = BudgetFunctions.GetRemainingBudgetDaysUntilRefresh(_site.StoreFrontId, userSetting.Id.ToString(), systemSetting.BudgetRefreshSystemWide, systemSetting.BudgetNextRefreshDate, userSetting.BudgetNextResetDate);
                */
                UserGroupUser userGroupUser = _sfDb.UserGroupUsers.Where(y => y.AspNetUserId == _site.AspNetUserId && y.StoreFrontId == _site.StoreFrontId).FirstOrDefault();
                              
                if (systemSetting.BudgetEnforce == 1 && systemSetting.BudgetType == "Group Based Budget" && userGroupUser != null)
                {
                    UserGroup userGroup = _sfDb.UserGroups.Where(x => x.Id == userGroupUser.UserGroupId && x.StoreFrontId == _site.StoreFrontId).FirstOrDefault();
                    DateTime groupBudgetRefreshDate = userGroup.TimeRefresh ?? DateTime.Now;
                    //model.BudgetLimit = BudgetFunctions.GetTotalGroupBudget(_site.StoreFrontId, userSetting.Id.ToString(), userGroupUser);
                    model.BudgetLimit = BudgetFunctions.GetTotalGroupBudget(_site.StoreFrontId, userGroupUser.AspNetUserId);
                    //model.BudgetLimit = userGroup.PriceLimit ?? 0;
                    //model.BudgetLimit = Convert.ToInt32(_sfDb.UserGroups.Where(x => x.Id == userGroupUser.UserGroupId && x.StoreFrontId == _site.StoreFrontId).FirstOrDefault().PriceLimit);
                    model.BudgetRefreshDate = groupBudgetRefreshDate;
                    model.BudgetDaysUntilRefresh = BudgetFunctions.GetRemainingBudgetDaysUntilRefresh(_site.StoreFrontId, userSetting.Id.ToString(), systemSetting.BudgetRefreshSystemWide, systemSetting.BudgetNextRefreshDate, groupBudgetRefreshDate);
                    model.BudgetCurrentTotal = userGroup.PriceLimit - userGroup.CurrentBudgetLeft ?? 0; 
                    model.BudgetCurrentAvailable = userGroup.CurrentBudgetLeft ?? 0;
                }

                //if (systemSetting.BudgetEnforce == 1 && systemSetting.BudgetType == "User Based Budget")
                else if (((systemSetting.BudgetEnforce == 1) && (systemSetting.BudgetType == "User Based Budget")) ||
                     ((systemSetting.BudgetEnforce == 1) && (systemSetting.BudgetType == "Group Based Budget") && (userGroupUser == null)) ||
                     (systemSetting.BudgetEnforce != 1))
                {
                    DateTime userBudgetRefreshDate = new DateTime(userSetting.BudgetNextResetDate.Year, userSetting.BudgetNextResetDate.Month, userSetting.BudgetNextResetDate.Day);
                    model.BudgetLimit = userSetting.BudgetLimit;
                    model.BudgetRefreshDate = userBudgetRefreshDate;
                    model.BudgetDaysUntilRefresh = BudgetFunctions.GetRemainingBudgetDaysUntilRefresh(_site.StoreFrontId, userSetting.Id.ToString(), systemSetting.BudgetRefreshSystemWide, systemSetting.BudgetNextRefreshDate, userSetting.BudgetNextResetDate);
                    model.BudgetCurrentTotal = userSetting.BudgetCurrentTotal;
                    model.BudgetCurrentAvailable = Math.Max(userSetting.BudgetLimit - userSetting.BudgetCurrentTotal, 0);      
                }

                else
                {
                    model.BudgetLimit = 0;
                    model.BudgetRefreshDate = new DateTime(today.Year, 1, 1).Date;
                    model.BudgetDaysUntilRefresh = 0;
                    model.BudgetCurrentTotal = 0;
                    model.BudgetCurrentAvailable = 0;
                }

                return View(model);

            }
            catch (Exception ex)
            {
                return View("Error", new HandleErrorInfo(ex, "Account", "Login"));
            }
        }

        #region Dashboard/Admin

        // GET: Dashboard/Admin (Not used currently)
        [TokenAuthorize]
        public ActionResult Admin(int? id)
        {
            StoreFrontViewModel model = new StoreFrontViewModel();
            try
            {
                string _userName = User.Identity.Name;

                var storeFrontList = (from sf in _sfDb.StoreFronts
                                      join usf in _sfDb.UserStoreFronts on sf.Id equals usf.StoreFrontId
                                      select sf).ToList();

                //_sfDb.StoreFronts.Where(u => u.Id == id).ToList();
                if (storeFrontList.Count == 0)
                {
                    return View("Error", new HandleErrorInfo(new Exception("No associated storefront with that user"), "Dashboard", "MyWindow"));
                }
                //model.StoreFronts = storeFrontList.OrderBy(s => s.StoreFrontName).Select(s => new SelectListItem { Text = s.StoreFrontName, Value = s.Id.ToString() });
                int? displayId = id == null ? storeFrontList.First().Id : id;
                model.StoreFronts = storeFrontList.ToSelectListItems(displayId);

                StoreFront selectedSF = storeFrontList.Where(s => s.Id == displayId).FirstOrDefault();

                model.Id = selectedSF.Id;
                model.Name = selectedSF.StoreFrontName;
                model.CustomerServiceRep = selectedSF.CustomerServiceRep;
                model.OfficeNumber = selectedSF.OfficeNumber;
                model.OfficeHours = selectedSF.OfficeHours;
                model.Email = selectedSF.Email;
                model.BaseUrl = selectedSF.BaseUrl;
                model.LayoutPath = selectedSF.LayoutPath;
                model.SiteIcon = selectedSF.SiteIcon;
                model.SiteTitle = selectedSF.SiteTitle;
                model.LoginImage = selectedSF.LoginImage;
                model.SiteFooter = selectedSF.SiteFooter;
                model.TemplateId = selectedSF.TemplateId;

            }
            catch (Exception ex)
            {
                return View("Error", new HandleErrorInfo(ex, "Dashboard", "MyWindow"));
            }
            return View(model);
        }

        // GET: Dashboard/AdminEdit/5 (Not used currently)
        [TokenAuthorize]
        public ActionResult AdminEdit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            StoreFront selectedSF = _sfDb.StoreFronts.Find(id);

            if (selectedSF == null)
            {
                return HttpNotFound();
            }

            StoreFrontViewModel model = new StoreFrontViewModel()
            {
                Id = selectedSF.Id,
                Name = selectedSF.StoreFrontName,
                CustomerServiceRep = selectedSF.CustomerServiceRep,
                OfficeNumber = selectedSF.OfficeNumber,
                OfficeHours = selectedSF.OfficeHours,
                Email = selectedSF.Email,
                BaseUrl = selectedSF.BaseUrl,
                LayoutPath = selectedSF.LayoutPath,
                SiteIcon = selectedSF.SiteIcon,
                SiteTitle = selectedSF.SiteTitle,
                LoginImage = selectedSF.LoginImage,
                SiteFooter = selectedSF.SiteFooter,
                TemplateId = selectedSF.TemplateId
            };

            return View(model);
        }

        // POST: Dashboard/AdminEdit/5 (Not used currently)
        [TokenAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AdminEdit(StoreFrontViewModel model)
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
                return RedirectToAction("Admin", new { id = model.Id });
            }
            return View(model);
        }

        #endregion Dashboard/Admin

        #region Dashboard/Users (Not used currently)

        // GET: Dashboard/Users (Not used currently)
        [TokenAuthorize]
        public ActionResult Users(int? id)
        {
            // Display all the users for the StoreFronts managable by this user logged in
            UsersViewModel model = new UsersViewModel();

            string _userName = User.Identity.Name;

            var queryStoreFrontId = from usf in _sfDb.UserStoreFronts
                                    join u in _sfDb.AspNetUsers on usf.AspNetUserId equals u.Id
                                    join sf in _sfDb.StoreFronts on usf.StoreFrontId equals sf.Id
                                    where u.UserName == _userName
                                    select sf.Id;

            var queryUsers = from u in _sfDb.AspNetUsers
                             join usf in _sfDb.UserStoreFronts on u.Id equals usf.AspNetUserId
                             where queryStoreFrontId.Contains(usf.StoreFrontId)
                             select u;

            List<StoreFront> listStoreFront = (from sf in _sfDb.StoreFronts
                                               where queryStoreFrontId.Contains(sf.Id)
                                               select sf).ToList<StoreFront>();

            if (queryUsers.Count().Equals(0))
            {
                // nothing found
            }
            else
            {
                int? displayId = id == null ? queryUsers.First().SfId : id;

                AspNetUser displayUser = queryUsers.Where(u => u.SfId == (int)displayId).FirstOrDefault();

                model.Users = queryUsers.ToSelectListItems(displayId);

                model.Id = displayUser.SfId;
                model.Email = displayUser.Email;
                model.UserName = displayUser.UserName;
                model.Company = displayUser.Company;
                model.CompanyAlias = displayUser.CompanyAlias;
                model.FirstName = displayUser.FirstName;
                model.LastName = displayUser.LastName;
                model.Address1 = displayUser.Address1;
                model.Address2 = displayUser.Address2;
                model.City = displayUser.City;
                model.State = displayUser.State;
                model.Zip = displayUser.Zip;
                model.Country = displayUser.Country;
                model.Phone = displayUser.Phone;
                model.AccessRestricted = displayUser.AccessRestricted == 1;
                model.Status = (short)displayUser.Status;
                model.StoreFronts = (from sf in _sfDb.StoreFronts
                                     where queryStoreFrontId.Contains(sf.Id)
                                     select sf).ToList<StoreFront>();
            }

            return View(model);
        }

        // GET: Dashboard/UsersEdit/5 (Not used currently)
        [TokenAuthorize]
        public ActionResult UsersEdit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            AspNetUser selectedUser = _sfDb.AspNetUsers.Where(u => u.SfId == id).FirstOrDefault();

            if (selectedUser == null)
            {
                return HttpNotFound();
            }

            UsersViewModel model = new UsersViewModel()
            {
                Id = selectedUser.SfId,
                Email = selectedUser.Email,
                UserName = selectedUser.UserName,
                Company = selectedUser.Company,
                CompanyAlias = selectedUser.CompanyAlias,
                FirstName = selectedUser.FirstName,
                LastName = selectedUser.LastName,
                Address1 = selectedUser.Address1,
                Address2 = selectedUser.Address2,
                City = selectedUser.City,
                State = selectedUser.State,
                Zip = selectedUser.Zip,
                Country = selectedUser.Country,
                Phone = selectedUser.Phone,
                AccessRestricted = selectedUser.AccessRestricted == 1,
                Status = (short)selectedUser.Status
            };

            return View(model);
        }

        // POST: Dashboard/UsersEdit/5 (Not used currently)
        [TokenAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UsersEdit(UsersViewModel model)
        {
            if (ModelState.IsValid)
            {
                AspNetUser selectedUser = _sfDb.AspNetUsers.Where(u => u.SfId == model.Id).FirstOrDefault();

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
                selectedUser.Status = (short)model.Status;

                _sfDb.Entry(selectedUser).State = EntityState.Modified;
                _sfDb.SaveChanges();
                return RedirectToAction("Users", new { id = model.Id });
            }
            return View(model);
        }
        #endregion Dashboard/Users

        // GET: Dashboard/Delete/5 (Not used currently)
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            StoreFront storeFront = _sfDb.StoreFronts.Find(id);
            if (storeFront == null)
            {
                return HttpNotFound();
            }
            return View(storeFront);
        }

        // POST: Dashboard/Delete/5 (Not used currently)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            StoreFront storeFront = _sfDb.StoreFronts.Find(id);
            _sfDb.StoreFronts.Remove(storeFront);
            _sfDb.SaveChanges();
            return RedirectToAction("Index");
        }

        // GET: Dashboard/MyOffice
        [TokenAuthorize]
        public ActionResult MyOffice()
        {
            return View();
        }

        // GET: Dashboard/Broadcasts
        [TokenAuthorize]
        public ActionResult Broadcasts()
        {
            return View();
        }

        // GET: Dashboard/Reminders
        [TokenAuthorize]
        public ActionResult Reminders()
        {
            return View();
        }

        // GET: Dashboard/DocumentManager
        [TokenAuthorize]
        public ActionResult DocumentManager()
        {
            return View();
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
