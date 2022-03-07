using Kendo.Mvc.Extensions;
using StoreFront2.Data;
using StoreFront2.Helpers;
using StoreFront2.Models;
using StoreFront2.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.Entity.SqlServer;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace StoreFront2.Controllers
{
    public class HomeController : Controller
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
                var token = Request.Params[0];

                // Check the requesting Url
                //var request = System.Web.HttpContext.Current.Request;
                string baseUrl = new Uri(Request.Url, Url.Content("~")).AbsoluteUri;
                //for debug
                //string baseUrl = "http://localhost:60221/"; 
                
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

        [TokenAuthorize]
        public ActionResult Index() //string token
        {
            try
            {
                _site.AdminAsShopper = true;
                Session["Site"] = _site;

                //DateTime daysAgo30 = DateTime.Now.AddDays(-30);
                //DateTime daysAgo60 = DateTime.Now.AddDays(-60);
                //DateTime yearFirstDate = new DateTime(DateTime.Now.Year, 1, 1);

                DateTime today = DateTime.Now;
                DateTime daysAgo30 = today.AddDays(-30);
                DateTime daysAgo60 = today.AddDays(-60);

                bool isSunday = today.DayOfWeek == 0;
                var dayOfWeek = isSunday == false ? (int)today.DayOfWeek : 7;
                DateTime weekFirstDate = today.AddDays(((int)dayOfWeek * -1) + 1);

                DateTime monthFirstDate = new DateTime(today.Year, today.Month, 1);

                int currentQtr = (int)((today.Month - 1) / 3) + 1;
                DateTime quarterFirstDate = new DateTime(today.Year, (currentQtr - 1) * 3 + 1, 1);

                DateTime yearFirstDate = new DateTime(today.Year, 1, 1);

                List<Order> storeFrontOrders = new List<Order>();
                storeFrontOrders = _sfDb.Orders.Where(o => o.StoreFrontId == _site.StoreFrontId && o.UserId == _userSf.Id).ToList();

                OrderStatusTotal orderStatus = new OrderStatusTotal()
                {
                    TotCN = storeFrontOrders.Where(o => o.OrderStatus == "CN").Select(o => o.Id).Distinct().Count(),
                    TotPS = storeFrontOrders.Where(o => o.OrderStatus == "PS").Select(o => o.Id).Distinct().Count(),
                    TotSH = storeFrontOrders.Where(o => o.OrderStatus == "SH").Select(o => o.Id).Distinct().Count(),
                    TotBO = storeFrontOrders.Where(o => o.OrderStatus == "BO").Select(o => o.Id).Distinct().Count(),
                    TotPB = storeFrontOrders.Where(o => o.OrderStatus == "PB").Select(o => o.Id).Distinct().Count(),
                    TotOH = storeFrontOrders.Where(o => o.OrderStatus == "OH").Select(o => o.Id).Distinct().Count(),
                    Tot30 = storeFrontOrders.Where(o => o.DateCreated >= daysAgo30).Select(o => o.Id).Distinct().Count(),
                    DaysAgo30 = daysAgo30,
                    Tot60 = storeFrontOrders.Where(o => o.DateCreated >= daysAgo60).Select(o => o.Id).Distinct().Count(),
                    DaysAgo60 = daysAgo60,
                    TotMonth = storeFrontOrders.Where(o => o.DateCreated >= monthFirstDate).Select(o => o.Id).Distinct().Count(),
                    MonthFirstDate = monthFirstDate,
                    TotQtr = storeFrontOrders.Where(o => o.DateCreated >= quarterFirstDate).Select(o => o.Id).Distinct().Count(),
                    QuarterFirstDate = quarterFirstDate,
                    TotYtd = storeFrontOrders.Where(o => o.DateCreated >= yearFirstDate).Select(o => o.Id).Distinct().Count(),
                    YearFirstDate = yearFirstDate,
                };

                //ViewBag.OrderStatus = orderStatus;

                //// For chart


                List<int> monthlyOrder = (from o in _sfDb.Orders
                                          group o by SqlFunctions.DatePart("m", o.DateCreated) into grp
                                          select grp.Count(o => true)).ToList();
                //double[] totalMonthlyOrder = new double[monthlyOrder.Count];
                //for (int i = 0; i < monthlyOrder.Count; i++)
                //{
                //    totalMonthlyOrder[i] = Convert.ToDouble(monthlyOrder[i]);
                //}

                List<int> monthlyShip = (from o in _sfDb.Orders
                                         group o by SqlFunctions.DatePart("m", o.ShipDate) into grp
                                         select grp.Count(o => true)).ToList();
                //double[] totalMonthlyShip = new double[monthlyShip.Count];
                //for (int i = 0; i < monthlyShip.Count; i++)
                //{
                //    totalMonthlyShip[i] = Convert.ToDouble(monthlyShip[i]);
                //}

                //ViewBag.TotalMonthlyOrder = totalMonthlyOrder;
                //ViewBag.TotalMonthlyShip = totalMonthlyShip;

                UserSetting selectedSetting = _sfDb.UserSettings.Where(us => us.StoreFrontId == _site.StoreFrontId && us.AspNetUserId == _userSf.Id).FirstOrDefault();
                SystemSetting systemsettings = _sfDb.SystemSettings.Where(x => x.StoreFrontId == _site.StoreFrontId).FirstOrDefault();

                //int numberOfOrdersCountedAgainstBudget = 0;
                //DateTime lastRefreshDate = DateTime.Now.Date;
                //lastRefreshDate = selectedSetting.BudgetLastResetDate.Date;
                //List<Order> ordersCounted = _sfDb.Orders.Where(o => o.DateCreated >= lastRefreshDate && o.UserId == _site.AspNetUserId && o.StoreFrontId == _site.StoreFrontId).ToList();
                //numberOfOrdersCountedAgainstBudget = ordersCounted.Count();

                MyWindowViewModel model = new MyWindowViewModel();
                var usergroup = _sfDb.UserGroupUsers.Where(x => x.AspNetUserId == _userSf.Id && x.StoreFrontId == _site.StoreFrontId).FirstOrDefault();

                if (systemsettings.BudgetEnforce == 1 && systemsettings.BudgetType == "Group Based Budget" && usergroup != null)
                {                     
                    var usergroupBudget = _sfDb.UserGroups.Where(x => x.StoreFrontId == _site.StoreFrontId && usergroup.UserGroupId == x.Id).FirstOrDefault();
                    var userGroupUser = _sfDb.UserGroupUsers.Where(y => y.AspNetUserId == _userSf.Id && y.UserGroupId == usergroup.UserGroupId).FirstOrDefault();

                    int numberOfOrdersCountedAgainstBudget = 0;
                    DateTime lastRefreshDate = usergroupBudget.DateCreated.Value.Date;
                    List<Order> ordersCounted = _sfDb.Orders.Where(o => o.DateCreated >= lastRefreshDate && o.UserId == _site.AspNetUserId && o.StoreFrontId == _site.StoreFrontId).ToList();
                    numberOfOrdersCountedAgainstBudget = ordersCounted.Count();

                    model.OrderStatus = orderStatus;
                    model.TotalMonthlyOrder = monthlyOrder.Count;
                    model.TotalMonthlyShip = monthlyShip.Count;
                    model.BudgetLimit = usergroupBudget.PriceLimit ?? 0;
                    //model.BudgetCurrentTotal = selectedSetting.BudgetCurrentTotal;
                    //model.BudgetCurrentTotal = model.BudgetLimit - BudgetFunctions.GetBudgetRemaining(_site.StoreFrontId, userGroupUser);
                    model.BudgetCurrentTotal = usergroupBudget.PriceLimit - usergroupBudget.CurrentBudgetLeft ?? 0;
                    //model.OrdersCountingAgainstBudget = BudgetFunctions.GetOrdersCountedAgainstBudget(_site.StoreFrontId, _userSf.Id);
                    model.OrdersCountingAgainstBudget = numberOfOrdersCountedAgainstBudget;
                    model.BudgetCurrentAvailable = usergroupBudget.CurrentBudgetLeft ?? 0;
                    //model.BudgetDaysUntilRefresh = BudgetFunctions.GetRemainingBudgetDaysUntilRefresh(_site.StoreFrontId, _userSf.Id, systemsettings.BudgetRefreshSystemWide, systemsettings.BudgetNextRefreshDate, selectedSetting.BudgetNextResetDate);
                    model.BudgetDaysUntilRefresh = BudgetFunctions.GetRemainingBudgetDaysUntilRefresh(_site.StoreFrontId, _userSf.Id, systemsettings.BudgetRefreshSystemWide, systemsettings.BudgetNextRefreshDate, usergroupBudget.TimeRefresh ?? DateTime.Now);
                    //model.BudgetLastResetDate = selectedSetting.BudgetLastResetDate;
                    //model.BudgetNextResetDate = selectedSetting.BudgetNextResetDate;
                    model.BudgetLastResetDate = lastRefreshDate;
                    model.BudgetNextResetDate = usergroupBudget.TimeRefresh ?? DateTime.Now;
                }

                if ( ((systemsettings.BudgetEnforce == 1) && (systemsettings.BudgetType == "User Based Budget")) ||
                     ((systemsettings.BudgetEnforce == 1) && (systemsettings.BudgetType == "Group Based Budget") && (usergroup == null)) ||
                     (systemsettings.BudgetEnforce != 1) )
                {
                    int numberOfOrdersCountedAgainstBudget = 0;
                    DateTime lastRefreshDate = selectedSetting.BudgetLastResetDate.Date;
                    List<Order> ordersCounted = _sfDb.Orders.Where(o => o.DateCreated >= lastRefreshDate && o.UserId == _site.AspNetUserId && o.StoreFrontId == _site.StoreFrontId).ToList();
                    numberOfOrdersCountedAgainstBudget = ordersCounted.Count();
                    model.OrderStatus = orderStatus;
                    model.TotalMonthlyOrder = monthlyOrder.Count;
                    model.TotalMonthlyShip = monthlyShip.Count;
                    model.BudgetLimit = selectedSetting.BudgetLimit;
                    model.BudgetCurrentTotal = selectedSetting.BudgetCurrentTotal;
                    //model.BudgetCurrentTotal = model.BudgetLimit - selectedSetting.BudgetCurrentTotal;
                    //model.OrdersCountingAgainstBudget = BudgetFunctions.GetOrdersCountedAgainstBudget(_site.StoreFrontId, _userSf.Id);
                    model.OrdersCountingAgainstBudget = numberOfOrdersCountedAgainstBudget;
                    model.BudgetCurrentAvailable = Math.Max(selectedSetting.BudgetLimit - selectedSetting.BudgetCurrentTotal, 0);
                    model.BudgetDaysUntilRefresh = BudgetFunctions.GetRemainingBudgetDaysUntilRefresh(_site.StoreFrontId, _userSf.Id, systemsettings.BudgetRefreshSystemWide, systemsettings.BudgetNextRefreshDate, selectedSetting.BudgetNextResetDate);
                    model.BudgetLastResetDate = selectedSetting.BudgetLastResetDate;
                    model.BudgetNextResetDate = selectedSetting.BudgetNextResetDate;
                }

                return View(model);
            }
            catch (Exception ex)
            {
                return View("Error", new HandleErrorInfo(ex, "Account", "Login"));
            }

        }

        public ActionResult VendorIndex() //string token
        {
            try
            {
                _site.AdminAsShopper = true;
                Session["Site"] = _site;

                //DateTime daysAgo30 = DateTime.Now.AddDays(-30);
                //DateTime daysAgo60 = DateTime.Now.AddDays(-60);
                //DateTime yearFirstDate = new DateTime(DateTime.Now.Year, 1, 1);

                DateTime today = DateTime.Now;
                DateTime daysAgo30 = today.AddDays(-30);
                DateTime daysAgo60 = today.AddDays(-60);

                bool isSunday = today.DayOfWeek == 0;
                var dayOfWeek = isSunday == false ? (int)today.DayOfWeek : 7;
                DateTime weekFirstDate = today.AddDays(((int)dayOfWeek * -1) + 1);

                DateTime monthFirstDate = new DateTime(today.Year, today.Month, 1);

                int currentQtr = (int)((today.Month - 1) / 3) + 1;
                DateTime quarterFirstDate = new DateTime(today.Year, (currentQtr - 1) * 3 + 1, 1);

                DateTime yearFirstDate = new DateTime(today.Year, 1, 1);

                List<Order> storeFrontOrders = new List<Order>();
                Vendor vendor = _sfDb.Vendors.Where(v => v.AspNetUserId == _userSf.Id).FirstOrDefault();
                if (vendor != null)
                {
                    storeFrontOrders = (from o in _sfDb.Orders
                                        join od in _sfDb.OrderDetails on o.Id equals od.OrderId
                                        where o.StoreFrontId == _site.StoreFrontId && od.IsFulfilledByVendor == 1 && od.VendorId == vendor.Id
                                        select o).ToList();
                }

                OrderStatusTotal orderStatus = new OrderStatusTotal()
                {
                    TotCN = storeFrontOrders.Where(o => o.OrderStatus == "CN").Select(o => o.Id).Distinct().Count(),
                    TotRP = storeFrontOrders.Where(o => o.OrderStatus == "RP" || o.OrderStatus == "PH").Select(o => o.Id).Distinct().Count(),
                    TotSH = storeFrontOrders.Where(o => o.OrderStatus == "SH").Select(o => o.Id).Distinct().Count(),
                    TotBO = storeFrontOrders.Where(o => o.OrderStatus == "BO").Select(o => o.Id).Distinct().Count(),
                    TotPB = storeFrontOrders.Where(o => o.OrderStatus == "PB").Select(o => o.Id).Distinct().Count(),
                    TotOH = storeFrontOrders.Where(o => o.OrderStatus == "OH").Select(o => o.Id).Distinct().Count(),
                    Tot30 = storeFrontOrders.Where(o => o.DateCreated >= daysAgo30).Select(o => o.Id).Distinct().Count(),
                    DaysAgo30 = daysAgo30,
                    Tot60 = storeFrontOrders.Where(o => o.DateCreated >= daysAgo60).Select(o => o.Id).Distinct().Count(),
                    DaysAgo60 = daysAgo60,
                    TotMonth = storeFrontOrders.Where(o => o.DateCreated >= monthFirstDate).Select(o => o.Id).Distinct().Count(),
                    MonthFirstDate = monthFirstDate,
                    TotQtr = storeFrontOrders.Where(o => o.DateCreated >= quarterFirstDate).Select(o => o.Id).Distinct().Count(),
                    QuarterFirstDate = quarterFirstDate,
                    TotYtd = storeFrontOrders.Where(o => o.DateCreated >= yearFirstDate).Select(o => o.Id).Distinct().Count(),
                    YearFirstDate = yearFirstDate,
                };

                //ViewBag.OrderStatus = orderStatus;

                //// For chart


                List<int> monthlyOrder = (from o in _sfDb.Orders
                                          group o by SqlFunctions.DatePart("m", o.DateCreated) into grp
                                          select grp.Count(o => true)).ToList();
                //double[] totalMonthlyOrder = new double[monthlyOrder.Count];
                //for (int i = 0; i < monthlyOrder.Count; i++)
                //{
                //    totalMonthlyOrder[i] = Convert.ToDouble(monthlyOrder[i]);
                //}

                List<int> monthlyShip = (from o in _sfDb.Orders
                                         group o by SqlFunctions.DatePart("m", o.ShipDate) into grp
                                         select grp.Count(o => true)).ToList();
                //double[] totalMonthlyShip = new double[monthlyShip.Count];
                //for (int i = 0; i < monthlyShip.Count; i++)
                //{
                //    totalMonthlyShip[i] = Convert.ToDouble(monthlyShip[i]);
                //}

                //ViewBag.TotalMonthlyOrder = totalMonthlyOrder;
                //ViewBag.TotalMonthlyShip = totalMonthlyShip;

                UserSetting selectedSetting = _sfDb.UserSettings.Where(us => us.StoreFrontId == _site.StoreFrontId && us.AspNetUserId == _userSf.Id).FirstOrDefault();
                SystemSetting systemSetting = _sfDb.SystemSettings.Where(ss => ss.StoreFrontId == _site.StoreFrontId).FirstOrDefault();

                int numberOfOrdersCountedAgainstBudget = 0;
                DateTime lastRefreshDate = DateTime.Now.Date;
                lastRefreshDate = selectedSetting.BudgetLastResetDate.Date;
                List<Order> ordersCounted = _sfDb.Orders.Where(o => o.DateCreated >= lastRefreshDate && o.UserId == _site.AspNetUserId && o.StoreFrontId == _site.StoreFrontId).ToList();
                numberOfOrdersCountedAgainstBudget = ordersCounted.Count();

                MyWindowViewModel model = new MyWindowViewModel();

                model.OrderStatus = orderStatus;
                model.TotalMonthlyOrder = monthlyOrder.Count;
                model.TotalMonthlyShip = monthlyShip.Count;
                model.BudgetLimit = selectedSetting.BudgetLimit;
                model.BudgetCurrentTotal = selectedSetting.BudgetCurrentTotal;
                //model.OrdersCountingAgainstBudget = BudgetFunctions.GetOrdersCountedAgainstBudget(_site.StoreFrontId, _userSf.Id);
                model.OrdersCountingAgainstBudget = numberOfOrdersCountedAgainstBudget;
                model.BudgetCurrentAvailable = Math.Max(selectedSetting.BudgetLimit - selectedSetting.BudgetCurrentTotal, 0);
                model.BudgetDaysUntilRefresh = BudgetFunctions.GetRemainingBudgetDaysUntilRefresh(_site.StoreFrontId, _userSf.Id, systemSetting.BudgetRefreshSystemWide, systemSetting.BudgetNextRefreshDate, selectedSetting.BudgetNextResetDate);
                model.BudgetLastResetDate = selectedSetting.BudgetLastResetDate;
                model.BudgetNextResetDate = selectedSetting.BudgetNextResetDate;
                return View(model);
            }
            catch (Exception ex)
            {
                return View("Error", new HandleErrorInfo(ex, "Account", "Login"));
            }

        }
    }
}
