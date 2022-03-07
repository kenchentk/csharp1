using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Kendo.Mvc.Extensions;
using Kendo.Mvc.UI;
using StoreFront2.Data;
using StoreFront2.Helpers;
using StoreFront2.Models;
using StoreFront2.ViewModels;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNet.Identity.Owin;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Drawing;
using System.Xml.Linq;
using System.Xml;
using RestSharp;
using HtmlAgilityPack;
using System.Net.Http;
using System.IO;
using System.Net.Mail;

namespace StoreFront2.Controllers
{
    public class OrderController : Controller
    {
        private StoreFront2Entities _sfDb = new StoreFront2Entities();

        private Site _site = new Site();

        private string _userName = "";
        private AspNetUser _userSf;
        private IQueryable<int> _sfIdList;
        private List<string> _userRoleList;

        private ApplicationUserManager _userManager;

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

        // Vendor / My Orders
        [TokenAuthorize]
        public ActionResult VendorOrders(FilterViewModel paramFilter)
        {
            List<ShipMethod> allowedShipMethods = new List<ShipMethod>();
            if (_site.SiteAuth.OrderRestrictShipMethod == 0)
            {
                allowedShipMethods = (from sm in _sfDb.ShipMethods
                                      join sc in _sfDb.ShipCarriers on sm.CarrierId equals sc.Id
                                      where sm.Enabled == 1 && sc.Enabled == 1 && sc.StoreFrontId == _site.StoreFrontId
                                      select sm).ToList();
            }
            else
            {
                allowedShipMethods = (from usm in _sfDb.UserShipMethods
                                      join sm in _sfDb.ShipMethods on usm.ShipMethodId equals sm.Id
                                      join sc in _sfDb.ShipCarriers on sm.CarrierId equals sc.Id
                                      where sm.Enabled == 1 && usm.AspNetUserId == _userSf.Id && sc.Enabled == 1 && sc.StoreFrontId == _site.StoreFrontId
                                      select sm).ToList();
            }

            ViewBag.AllowedShipMethods = new SelectList(allowedShipMethods, "Code", "MethodName");

            List<OrderStatus> orderStatuses = new List<OrderStatus>();
            orderStatuses = GlobalConstants.OrderStatuses.Where(os => os.Code == paramFilter.Status).ToList();
            paramFilter.SelectedStatuses = orderStatuses;

            return View(paramFilter);
        }

        // Shopper / My Orders
        [TokenAuthorize]
        public ActionResult Index(FilterViewModel paramFilter)
        {
            return View(paramFilter);
        }

        // Admin / Current Orders
        [TokenAuthorize]
        public ActionResult OrdersAll(FilterViewModel paramFilter)
        {
            try
            {
                string[] statusList;
                if (paramFilter != null && paramFilter.Status != null && paramFilter.Status.Length > 0)
                {
                    paramFilter.SelectedStatuses = new List<OrderStatus>();
                    statusList = paramFilter.Status.Split(',');
                    foreach (var status in statusList)
                    {
                        paramFilter.SelectedStatuses.Add(new OrderStatus() { Code = status });
                    }
                }
                return View(paramFilter);
            }
            catch (Exception ex)
            {
                return View("Error", new HandleErrorInfo(ex, "Order", "OrdersAll"));
            }
        }

        // Admin / Current Orders / Order Detail
        [TokenAuthorize]
        public ActionResult OrderAllDetail(Order order)
        {
            try
            {
                List<OrderDetailViewModel> orderDetails = (from od in _sfDb.OrderDetails
                                                           join p in _sfDb.Products on od.ProductId equals p.Id
                                                           join o in _sfDb.Orders on od.OrderId equals o.Id
                                                           where od.OrderId == order.Id
                                                           select new OrderDetailViewModel()
                                                           {
                                                               Id = od.Id,
                                                               OrderId = od.OrderId,
                                                               SFOrderNumber = od.SFOrderNumber,
                                                               ProductId = od.ProductId,
                                                               ProductCode = p.ProductCode,
                                                               ShortDesc = p.ShortDesc,
                                                               LongDesc = p.LongDesc,
                                                               Qty = od.Qty,
                                                               EnableMinQty = p.EnableMinQty,
                                                               MinOrder = p.MinQty,
                                                               EnableMaxQty = p.EnableMaxQty,
                                                               MaxOrder = p.MaxQty,
                                                               Status = od.Status,
                                                               Price = od.Price,
                                                               Total = od.Price * od.Qty,
                                                               ImageRelativePath = _sfDb.ProductImages.Where(pi => pi.ProductId == p.Id && (pi.DisplayOrder ?? 1) == 1).Select(pif => pif.RelativePath).FirstOrDefault() ?? "Content/" + _site.StoreFrontName + "/Images/default.png"
                                                           }).ToList();

                Order currentOrder = (from o in _sfDb.Orders
                                      where o.Id == order.Id
                                      select o).FirstOrDefault();

                foreach (OrderDetailViewModel od in orderDetails)
                {
                    List<TrackingViewModel> otQuery = (from ot in _sfDb.OrderTrackings
                                                       where ot.OrderNumber == order.Id
                                                       select new TrackingViewModel()
                                                       {
                                                           Id = ot.Id,
                                                           TrackingNumber = ot.TrackingNumber,
                                                           DateCreated = ot.DateCreated ?? new DateTime(1, 1, 1),
                                                           Carrier = ot.Carrier,
                                                           ShipMethod = ot.ShipMethod,
                                                       }).ToList();

                    foreach (TrackingViewModel t in otQuery)
                    {
                        string trackingDetail = t.TrackingNumber;
                        switch (t.Carrier)
                        {
                            case "Fedex":
                                trackingDetail = "<a href='http://www.fedex.com/apps/fedextrack/?action=track&trackingnumber=" + t.TrackingNumber + "' target='_blank' style = 'color:blue; text-decoration: underline'>" + t.TrackingNumber + "</a><br>";
                                break;
                            case "Ups":
                                trackingDetail = "<a href='http://wwwapps.ups.com/WebTracking/processInputRequest?HTMLVersion=5.0&loc=en_US&Requester=UPSHome&tracknum=" + t.TrackingNumber + "&AgreeToTermsAndConditions=yes&ignore=&track.x=26&track.y=15' target='_blank' style = 'color:blue; text-decoration: underline'>" + t.TrackingNumber + "</a><br>";
                                break;
                            case "Usps":
                                trackingDetail = "<a href='http://trkcnfrm1.smi.usps.com/PTSInternetWeb/InterLabelInquiry.do?CAMEFROM=OK&strOrigTrackNum=" + t.TrackingNumber + "' target='_blank' style = 'color:blue; text-decoration: underline'>" + t.TrackingNumber + "</a><br>";
                                break;
                            default:
                                break;
                        }

                        t.TrackingNumber = trackingDetail;
                    }

                    ViewBag.OrderTrackings = otQuery.ToList();

                }

                ViewBag.Order = currentOrder;

                OrderShipTo orderShipTo = _sfDb.OrderShipTos.Where(ost => ost.OrderId == order.Id).FirstOrDefault();
                if (orderShipTo == null) orderShipTo = new OrderShipTo();
                ViewBag.ShippingTo = orderShipTo;

                ViewBag.ShippingFrom = _sfDb.AspNetUsers.Where(u => u.Id == currentOrder.UserId).FirstOrDefault();

                OrderNote orderNotes = _sfDb.OrderNotes.Where(on => on.OrderId == order.Id).FirstOrDefault();
                if (orderNotes == null) orderNotes = new OrderNote();
                ViewBag.Notes = orderNotes.Note;

                OrderTracking orderTracking = _sfDb.OrderTrackings.Where(ot => ot.OrderNumber == order.Id).OrderByDescending(ot => ot.DateCreated).Take(1).FirstOrDefault();
                if (orderTracking == null) orderTracking = new OrderTracking();
                ViewBag.ShippingInfo = orderTracking;

                string carrierName = "";
                string methodName = "";
                ShipMethod shipMethod = _sfDb.ShipMethods.Where(sm => sm.Id == currentOrder.ShipMethodId).FirstOrDefault();
                if (shipMethod != null)
                {
                    methodName = shipMethod.MethodName;
                    carrierName = _sfDb.ShipCarriers.Where(sc => sc.Id == shipMethod.CarrierId).FirstOrDefault().Name;
                }
                ViewBag.Carrier = carrierName;
                ViewBag.ShipMethodRequested = methodName;

                try
                {
                    ViewBag.OrderStatus = GlobalConstants.OrderStatuses.Where(os => os.Code == currentOrder.OrderStatus).FirstOrDefault().Desc;
                }
                catch
                {
                    Debug.WriteLine("Error OrderStatus");
                }

                List<SelectListItem> actionList = new List<SelectListItem>() {
                                                        new SelectListItem() { Text = "Choose", Value = "" },
                                                        new SelectListItem() { Text = "Print Order", Value = "actionPrintOrder" },
                                                        };
                if (!_site.IsVendor) actionList.Add(new SelectListItem() { Text = "Order Again", Value = "actionOrderAgain" });

                if ("RP".Contains(currentOrder.OrderStatus))
                    actionList.Add(new SelectListItem() { Text = "Put Order On Hold", Value = "actionPutOnHold" });
                if ("OH".Contains(currentOrder.OrderStatus))
                    actionList.Add(new SelectListItem() { Text = "Put Order Off Hold", Value = "actionPutOffHold" });

                if (_site.SiteAuth.OrderCancel == 1 && "IP".Contains(currentOrder.OrderStatus))
                    actionList.Add(new SelectListItem() { Text = "Reset Order", Value = "actionResetOrder" });

                if (_site.SiteAuth.OrderCancel == 1 && "RP,OH".Contains(currentOrder.OrderStatus))
                    actionList.Add(new SelectListItem() { Text = "Cancel Order", Value = "actionCancelOrder" });

                // If the order is cancelled and user is authorized to cancel, add Reinstate Order
                if (_site.SiteAuth.OrderCancel == 1 && "CN".Contains(currentOrder.OrderStatus))
                    actionList.Add(new SelectListItem() { Text = "Reinstate Order", Value = "actionReinstateOrder" });

                ViewBag.ActionList = actionList;

                return View(orderDetails);
            }
            catch (Exception ex)
            {
                return View("Error", new HandleErrorInfo(ex, "Order", "OrdersAll"));
            }
        }

        [TokenAuthorize]
        public ActionResult OrdersArchived(FilterViewModel paramFilter)
        {
            try
            {
                string[] statusList;
                if (paramFilter != null && paramFilter.Status != null && paramFilter.Status.Length > 0)
                {
                    paramFilter.SelectedStatuses = new List<OrderStatus>();
                    statusList = paramFilter.Status.Split(',');
                    foreach (var status in statusList)
                    {
                        paramFilter.SelectedStatuses.Add(new OrderStatus() { Code = status });
                    }
                }
                return View(paramFilter);
            }
            catch (Exception ex)
            {
                return View("Error", new HandleErrorInfo(ex, "Order", "Index"));
            }
        }

        // Shopper / My Orders / Detail
        [TokenAuthorize]
        public ActionResult OrderDetails(Order order)
        {
            try
            {
                List<OrderDetailViewModel> orderDetails = (from od in _sfDb.OrderDetails
                                                           join p in _sfDb.Products on od.ProductId equals p.Id
                                                           where od.OrderId == order.Id
                                                           select new OrderDetailViewModel()
                                                           {
                                                               Id = od.Id,
                                                               OrderId = od.OrderId,
                                                               SFOrderNumber = od.SFOrderNumber,
                                                               ProductId = od.ProductId,
                                                               ProductCode = p.ProductCode,
                                                               ShortDesc = p.ShortDesc,
                                                               LongDesc = p.LongDesc,
                                                               Qty = od.Qty,
                                                               EnableMinQty = p.EnableMinQty,
                                                               MinOrder = p.MinQty,
                                                               EnableMaxQty = p.EnableMaxQty,
                                                               MaxOrder = p.MaxQty,
                                                               Status = od.Status,
                                                               Price = od.Price,
                                                               Total = od.Price * od.Qty,
                                                               ImageRelativePath = _sfDb.ProductImages.Where(pi => pi.ProductId == p.Id).Select(pif => pif.RelativePath).FirstOrDefault() ?? "Content/" + _site.StoreFrontName + "/Images/default.png"
                                                           }).ToList();

                var currentOrder = (from o in _sfDb.Orders
                                    where o.Id == order.Id
                                    select o).FirstOrDefault();

                foreach (OrderDetailViewModel od in orderDetails)
                {
                    //var osQuery = from os
                    //              in _sfDb.OrderShipments
                    //              where os.OrderId == order.Id
                    //              select new ShipmentViewModel()
                    //              {
                    //                  Id = os.Id,
                    //                  DateShipped = os.DateShipped,
                    //                  TrackId = os.TrackId,
                    //                  TrackingNumber = _sfDb.OrderTrackings.Where(ot => ot.Id == os.TrackId).FirstOrDefault().TrackingNumber,
                    //                  Status = os.Status,
                    //                  ShipmentDetails = _sfDb.OrderShipmentDetails.Where(osd =>
                    //                    osd.OrderShipmentId == os.Id).Select(sd =>
                    //                        new ShipmentDetailViewModel()
                    //                        {
                    //                            Id = sd.Id,
                    //                            OrderShipmentId = os.Id,
                    //                            ProductId = sd.ProductId,
                    //                            ProductCode = sd.Product.ProductCode,
                    //                            PickPackCode = sd.Product.PickPackCode,
                    //                            ShortDesc = sd.Product.ShortDesc,
                    //                            Qty = sd.Qty,
                    //                        }
                    //                    ).ToList(),
                    //              };

                    //if (osQuery.Count() > 0)
                    //    od.OrderShipments = osQuery.ToList();

                    //ViewBag.OrderShipments = osQuery.ToList();
                    List<TrackingViewModel> otQuery = (from ot in _sfDb.OrderTrackings
                                                       where ot.OrderNumber == order.Id
                                                       select new TrackingViewModel()
                                                       {
                                                           Id = ot.Id,
                                                           TrackingNumber = ot.TrackingNumber,
                                                           DateCreated = ot.DateCreated ?? new DateTime(1, 1, 1),
                                                           Carrier = ot.Carrier,
                                                           ShipMethod = ot.ShipMethod,
                                                       }).ToList();

                    foreach (TrackingViewModel t in otQuery)
                    {
                        string trackingDetail = t.TrackingNumber;
                        switch (t.Carrier)
                        {
                            case "Fedex":
                                trackingDetail = "<a href='http://www.fedex.com/apps/fedextrack/?action=track&trackingnumber=" + t.TrackingNumber + "' target='_blank' style = 'color:blue; text-decoration: underline'>" + t.TrackingNumber + "</a><br>";
                                break;
                            case "Ups":
                                trackingDetail = "<a href='http://wwwapps.ups.com/WebTracking/processInputRequest?HTMLVersion=5.0&loc=en_US&Requester=UPSHome&tracknum=" + t.TrackingNumber + "&AgreeToTermsAndConditions=yes&ignore=&track.x=26&track.y=15' target='_blank' style = 'color:blue; text-decoration: underline'>" + t.TrackingNumber + "</a><br>";
                                break;
                            case "Usps":
                                trackingDetail = "<a href='http://trkcnfrm1.smi.usps.com/PTSInternetWeb/InterLabelInquiry.do?CAMEFROM=OK&strOrigTrackNum=" + t.TrackingNumber + "' target='_blank' style = 'color:blue; text-decoration: underline'>" + t.TrackingNumber + "</a><br>";
                                break;
                            default:
                                break;
                        }

                        t.TrackingNumber = trackingDetail;
                    }

                    ViewBag.OrderTrackings = otQuery.ToList();

                }

                ViewBag.Order = currentOrder;

                OrderShipTo orderShipTo = _sfDb.OrderShipTos.Where(ost => ost.OrderId == order.Id).FirstOrDefault();
                if (orderShipTo == null) orderShipTo = new OrderShipTo();
                ViewBag.ShippingTo = orderShipTo;

                ViewBag.ShippingFrom = _sfDb.AspNetUsers.Where(u => u.Id == currentOrder.UserId).FirstOrDefault();

                OrderNote orderNotes = _sfDb.OrderNotes.Where(on => on.OrderId == order.Id).FirstOrDefault();
                if (orderNotes == null) orderNotes = new OrderNote();
                ViewBag.Notes = orderNotes.Note;

                OrderTracking orderTracking = _sfDb.OrderTrackings.Where(ot => ot.OrderNumber == order.Id).OrderByDescending(ot => ot.DateCreated).Take(1).FirstOrDefault();
                if (orderTracking == null) orderTracking = new OrderTracking();
                ViewBag.ShippingInfo = orderTracking;

                string carrierName = "";
                string methodName = "";
                ShipMethod shipMethod = _sfDb.ShipMethods.Where(sm => sm.Id == currentOrder.ShipMethodId).FirstOrDefault();
                if (shipMethod != null)
                {
                    methodName = shipMethod.MethodName;
                    carrierName = _sfDb.ShipCarriers.Where(sc => sc.Id == shipMethod.CarrierId).FirstOrDefault().Name;
                }
                ViewBag.Carrier = carrierName;
                ViewBag.ShipMethodRequested = methodName;

                try
                {
                    ViewBag.OrderStatus = GlobalConstants.OrderStatuses.Where(os => os.Code == currentOrder.OrderStatus).FirstOrDefault().Desc;
                }
                catch
                {
                    Debug.WriteLine("Error OrderStatus");
                }

                List<SelectListItem> actionList = new List<SelectListItem>() {
                                                        new SelectListItem() { Text = "Choose", Value = "" },
                                                        new SelectListItem() { Text = "Print Order", Value = "actionPrintOrder" },
                                                        };
                if (!_site.IsVendor) actionList.Add(new SelectListItem() { Text = "Order Again", Value = "actionOrderAgain" });

                if (_site.SiteAuth.OrderCancel == 1 && "RP,OH".Contains(currentOrder.OrderStatus))
                    actionList.Add(new SelectListItem() { Text = "Cancel Order", Value = "actionCancelOrder" });

                if (_site.SiteAuth.OrderCancel == 1 && "IP".Contains(currentOrder.OrderStatus))
                    actionList.Add(new SelectListItem() { Text = "Reset Order", Value = "actionResetOrder" });

                // If the order is cancelled and user is authorized to cancel, add Reinstate Order
                if (_site.SiteAuth.OrderCancel == 1 && "CN".Contains(currentOrder.OrderStatus))
                    actionList.Add(new SelectListItem() { Text = "Reinstate Order", Value = "actionReinstateOrder" });

                ViewBag.ActionList = actionList;

                return View(orderDetails);
            }
            catch (Exception ex)
            {
                return View("Error", new HandleErrorInfo(ex, "Order", "Index"));
            }
        }

        [TokenAuthorize]
        public ActionResult ReadShipments([DataSourceRequest] DataSourceRequest request, Order order)
        {
            try
            {
                //IQueryable<OrderShipment> orderShipments = _sfDb.OrderShipments.Where(os => os.OrderId == order.Id);
                //DataSourceResult result = orderShipments.ToDataSourceResult(request, os => new ShipmentViewModel
                //{
                //    Id = os.Id,
                //    DateShipped = os.DateShipped,
                //    TrackId = os.TrackId,
                //    TrackingNumber = _sfDb.OrderTrackings.Where(ot => ot.Id == os.TrackId).FirstOrDefault().TrackingNumber,
                //    Status = os.Status,
                //});

                List<ShipmentViewModel> orderDetails = (from ot in _sfDb.OrderTrackings
                                                        where ot.OrderNumber == order.Id
                                                        select new ShipmentViewModel()
                                                        {
                                                            Id = ot.Id,
                                                            OrderId = ot.OrderNumber ?? 0,
                                                            DateShipped = ot.DateCreated ?? new DateTime(1, 1, 1),
                                                            TrackingNumber = ot.TrackingNumber,
                                                            Carrier = ot.Carrier,
                                                            Status = ot.DeliveryStatus ?? 0,
                                                        }).ToList();

                foreach (ShipmentViewModel t in orderDetails)
                {
                    string trackingDetail = t.TrackingNumber;
                    switch (t.Carrier)
                    {
                        case "Fedex":
                            trackingDetail = "<a href='http://www.fedex.com/apps/fedextrack/?action=track&trackingnumber=" + t.TrackingNumber + "' target='_blank' style = 'color:blue; text-decoration: underline'>" + t.TrackingNumber + "</a><br>";
                            break;
                        case "Ups":
                            trackingDetail = "<a href='http://wwwapps.ups.com/WebTracking/processInputRequest?HTMLVersion=5.0&loc=en_US&Requester=UPSHome&tracknum=" + t.TrackingNumber + "&AgreeToTermsAndConditions=yes&ignore=&track.x=26&track.y=15' target='_blank' style = 'color:blue; text-decoration: underline'>" + t.TrackingNumber + "</a><br>";
                            break;
                        case "Usps":
                            trackingDetail = "<a href='http://trkcnfrm1.smi.usps.com/PTSInternetWeb/InterLabelInquiry.do?CAMEFROM=OK&strOrigTrackNum=" + t.TrackingNumber + "' target='_blank' style = 'color:blue; text-decoration: underline'>" + t.TrackingNumber + "</a><br>";
                            break;
                        default:
                            break;
                    }

                    t.TrackingNumber = trackingDetail;
                }

                var retList = Json(orderDetails.ToDataSourceResult(request));

                return retList;
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }

        [TokenAuthorize]
        public ActionResult ReadShipmentDetails([DataSourceRequest] DataSourceRequest request, OrderShipment orderShipment)
        {
            try
            {
                IQueryable<OrderShipmentDetail> orderShipmentDetails = _sfDb.OrderShipmentDetails.Where(osd => osd.OrderShipmentId == orderShipment.Id);
                DataSourceResult result = orderShipmentDetails.ToDataSourceResult(request, osd => new ShipmentDetailViewModel
                {
                    Id = osd.Id,
                    Qty = osd.Qty,
                    ProductCode = _sfDb.Products.Where(p => p.Id == osd.ProductId).FirstOrDefault().ProductCode,
                    ShortDesc = _sfDb.Products.Where(p => p.Id == osd.ProductId).FirstOrDefault().ShortDesc,
                });

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }

        [TokenAuthorize]
        public ActionResult ReadTrackings([DataSourceRequest] DataSourceRequest request, Order order)
        {
            try
            {
                IQueryable<OrderTracking> orderTrackings = _sfDb.OrderTrackings.Where(ot => ot.OrderNumber == order.Id);
                DataSourceResult result = orderTrackings.ToDataSourceResult(request, ot => new
                {
                    Id = ot.Id,
                    TrackingNumber = ot.TrackingNumber,
                    //EmsOrderNumber = ot.EmsOrderNumber,
                    DateCreated = ot.DateCreated,
                    Carrier = ot.Carrier,
                    ShipMethod = ot.ShipMethod,
                    PublishedRate = ot.PublishedRate,
                    AdjustedRate = ot.AdjustedRate,
                });
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }

        [TokenAuthorize]
        public ActionResult Orders_Read([DataSourceRequest] DataSourceRequest request)
        {
            try
            {
                List<OrderViewModel> orders = (from o in _sfDb.Orders
                                               join u in _sfDb.AspNetUsers on o.UserId equals u.Id
                                               join sm in _sfDb.ShipMethods on o.ShipMethodId equals sm.Id
                                               join od in _sfDb.OrderDetails on o.Id equals od.OrderId
                                               where o.StoreFrontId == _site.StoreFrontId
                                               //&& (o.OrderStatus == "RP" || o.OrderStatus == "IP" || o.OrderStatus == "PS")
                                               select new OrderViewModel
                                               {
                                                   Id = o.Id,
                                                   SFOrderNumber = o.SFOrderNumber,
                                                   PONumber = o.PONumber,
                                                   DateCreated = o.DateCreated,
                                                   OrderStatus = o.OrderStatus,
                                                   OnHold = o.OnHold,
                                                   UserName = o.UserName,
                                                   Company = u.Company,
                                                   CompanyAlias = u.CompanyAlias,
                                                   FirstName = u.FirstName,
                                                   LastName = u.LastName,
                                                   City = u.City,
                                                   State = u.State,
                                                   Zip = u.Zip,
                                                   ShipMethodCode = sm.Code,
                                                   TotalPrice = (od.Qty * od.Price),
                                               }).ToList();

                var resultGroupOrders = from x in orders
                                        group x.TotalPrice by
                                        new
                                        {
                                            x.Id,
                                            x.SFOrderNumber,
                                            x.PONumber,
                                            x.DateCreated,
                                            x.OrderStatus,
                                            x.OnHold,
                                            x.UserName,
                                            x.Company,
                                            x.CompanyAlias,
                                            x.FirstName,
                                            x.LastName,
                                            x.City,
                                            x.State,
                                            x.Zip,
                                            x.ShipMethodCode
                                        }
                              into g
                                        select new
                                        {
                                            g.Key.Id,
                                            g.Key.SFOrderNumber,
                                            g.Key.PONumber,
                                            g.Key.DateCreated,
                                            g.Key.OrderStatus,
                                            g.Key.OnHold,
                                            g.Key.UserName,
                                            g.Key.Company,
                                            g.Key.CompanyAlias,
                                            g.Key.FirstName,
                                            g.Key.LastName,
                                            g.Key.City,
                                            g.Key.State,
                                            g.Key.Zip,
                                            g.Key.ShipMethodCode,
                                            TotalPrice = g.Sum()
                                        };

                //DataSourceResult result = orders.ToDataSourceResult(request, o => new OrderViewModel
                DataSourceResult result = resultGroupOrders.ToDataSourceResult(request, o => new OrderViewModel
                {
                    Id = o.Id,
                    SFOrderNumber = o.SFOrderNumber,
                    PONumber = o.PONumber,
                    DateCreated = o.DateCreated,
                    DateShipped = _sfDb.OrderTrackings.Where(ot => ot.OrderNumber == o.Id).Select(ot => ot.DateCreated).FirstOrDefault(),
                    TrackingNumbers = string.Join("<br>", _sfDb.OrderTrackings.Where(ot => ot.OrderNumber == o.Id).Select(ot => ot.TrackingNumber).FirstOrDefault()),
                    OrderStatus = o.OrderStatus,
                    OrderStatusDesc = GlobalConstants.OrderStatuses.Where(os => os.Code == o.OrderStatus).Select(os => os.Desc).FirstOrDefault(),
                    OnHold = o.OnHold,
                    UserName = o.UserName,
                    Company = o.Company,
                    CompanyAlias = o.CompanyAlias,
                    FirstName = o.FirstName,
                    LastName = o.LastName,
                    City = o.City,
                    State = o.State,
                    Zip = o.Zip,
                    ShipMethodCode = o.ShipMethodCode,
                    TotalPrice = o.TotalPrice,
                });

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }

        }

        [TokenAuthorize]
        public ActionResult Orders_Read_Archived([DataSourceRequest] DataSourceRequest request)
        {
            try
            {
                List<OrderViewModel> orders = (from o in _sfDb.Orders
                                               join u in _sfDb.AspNetUsers on o.UserId equals u.Id
                                               join sm in _sfDb.ShipMethods on o.ShipMethodId equals sm.Id
                                               //join od in _sfDb.OrderDetails on o.Id equals od.OrderId
                                               where o.StoreFrontId == _site.StoreFrontId
                                               && (o.OrderStatus == "CN" || o.OrderStatus == "SH" || o.OrderStatus == "RT")
                                               select new OrderViewModel
                                               {
                                                   Id = o.Id,
                                                   SFOrderNumber = o.SFOrderNumber,
                                                   PONumber = o.PONumber,
                                                   DateCreated = o.DateCreated,
                                                   OrderStatus = o.OrderStatus,
                                                   OnHold = o.OnHold,
                                                   UserName = o.UserName,
                                                   Email = u.Email,
                                                   Company = u.Company,
                                                   CompanyAlias = u.CompanyAlias,
                                                   FirstName = u.FirstName,
                                                   LastName = u.LastName,
                                                   City = u.City,
                                                   State = u.State,
                                                   Zip = u.Zip,
                                                   ShipMethodCode = sm.Code,
                                                   TotalPrice = 0,
                                               }).ToList();

                foreach (OrderViewModel o in orders)
                {
                    List<OrderTracking> shipTrackings = _sfDb.OrderTrackings.Where(ot => ot.OrderNumber == o.Id).Select(ot => ot).ToList();
                    if (shipTrackings.Count > 0)
                    {
                        o.DateShipped = shipTrackings[0].DateCreated;
                        o.TrackingNumbers = string.Join("<br>", shipTrackings.Select(ot => ot.TrackingNumber));
                    }
                    o.OrderStatusDesc = GlobalConstants.OrderStatuses.Where(os => os.Code == o.OrderStatus).Select(os => os.Desc).FirstOrDefault();
                }

                DataSourceResult result = orders.ToDataSourceResult(request, o => new OrderViewModel
                {
                    Id = o.Id,
                    SFOrderNumber = o.SFOrderNumber,
                    PONumber = o.PONumber,
                    DateCreated = o.DateCreated,
                    DateShipped = o.DateShipped,
                    TrackingNumbers = o.TrackingNumbers,
                    OrderStatus = o.OrderStatus,
                    OrderStatusDesc = o.OrderStatusDesc,
                    OnHold = o.OnHold,
                    UserName = o.UserName,
                    Company = o.Company,
                    CompanyAlias = o.CompanyAlias,
                    FirstName = o.FirstName,
                    LastName = o.LastName,
                    City = o.City,
                    State = o.State,
                    Zip = o.Zip,
                    ShipMethodCode = o.ShipMethodCode,
                    TotalPrice = o.TotalPrice,
                });

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }

        [TokenAuthorize]
        public ActionResult Order_SetStatus(List<int> listOrders, string status)
        {
            try
            {
                if (status.Length == 0 || listOrders == null)
                {
                    return Json(new { value = "error: missing data" }, JsonRequestBehavior.AllowGet);
                }

                var orders = from o in _sfDb.Orders
                             where listOrders.Contains(o.Id)
                             select o;

                foreach (var o in orders)
                {
                    o.OrderStatus = status;
                    _sfDb.Entry(o).State = EntityState.Modified;
                }

                _sfDb.SaveChanges();

                return Json(new { value = "success", status = status }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }

        [TokenAuthorize]
        public ActionResult Read_OrderStatuses()
        {
            try
            {
                List<OrderStatus> statuses = new List<OrderStatus>();

                if (_site.IsVendor)
                {
                    //statuses = GlobalConstants.OrderStatuses.Where(os => os.Code == "RP" || os.Code == "SH" || os.Code == "PH" || os.Code == "IP")
                    statuses = GlobalConstants.OrderStatuses.Where(os => os.Code == "OH" || os.Code == "PS" || os.Code == "RP" || os.Code == "PH" || os.Code == "IP")
                            .Select(c => new OrderStatus()
                            {
                                Id = c.Id,
                                Code = c.Code,
                                Desc = c.Desc
                            })
                            .OrderBy(e => e.Code).ToList();
                }
                else
                {
                    statuses = GlobalConstants.OrderStatuses
                            .Select(c => new OrderStatus()
                            {
                                Id = c.Id,
                                Code = c.Code,
                                Desc = c.Desc
                            })
                            .OrderBy(e => e.Code).ToList();
                }

                return Json(statuses, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }

        [TokenAuthorize]
        public ActionResult OrderStatuses_SetSelected(FilterViewModel filter)
        {
            try
            {
                List<OrderStatus> selectedStatuses;
                if (!filter.Archived)
                {
                    if (filter.Status != null && filter.Status.Length > 0)
                    {
                        selectedStatuses = GlobalConstants.OrderStatuses
                            .Select(s => new OrderStatus()
                            {
                                Id = s.Id,
                                Code = s.Code,
                                Desc = s.Desc
                            }).Where(w => filter.Status.Contains(w.Code))
                            .OrderBy(o => o.Code).ToList();
                    }
                    else
                    {
                        selectedStatuses = GlobalConstants.OrderStatuses
                            .Select(s => new OrderStatus()
                            {
                                Id = s.Id,
                                Code = s.Code,
                                Desc = s.Desc
                            }).Where(w => w.Code == "RP" || w.Code == "PS" || w.Code == "OH" || w.Code == "PH" || w.Code == "IP")
                            .OrderBy(o => o.Code).ToList();
                    }
                }
                else
                {
                    selectedStatuses = GlobalConstants.OrderStatuses
                        .Select(s => new OrderStatus()
                        {
                            Id = s.Id,
                            Code = s.Code,
                            Desc = s.Desc
                        }).Where(w => w.Code == "CN" || w.Code == "SH" || w.Code == "RT")
                        .OrderBy(o => o.Code).ToList();
                }

                return Json(selectedStatuses, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }

        }

        [TokenAuthorize]
        //public ActionResult Orders_Read_Shopper([DataSourceRequest] DataSourceRequest request)
        public ActionResult Orders_Read_Shopper([DataSourceRequest] DataSourceRequest request, AspNetUser aspNetUser)
        {
            try
            {
                // Save the Filter to filter the Order Detail
                List<Kendo.Mvc.CompositeFilterDescriptor> savedFiltersComposite = new List<Kendo.Mvc.CompositeFilterDescriptor>();
                if (request.Filters.Any())
                {
                    foreach (var filter in request.Filters)
                    {
                        if (filter is Kendo.Mvc.FilterDescriptor)
                        {
                            var newComposite = new Kendo.Mvc.CompositeFilterDescriptor();
                            newComposite.FilterDescriptors.Add(filter);
                            savedFiltersComposite.Add(newComposite);
                        }
                        if (filter is Kendo.Mvc.CompositeFilterDescriptor)
                        {
                            savedFiltersComposite.Add((Kendo.Mvc.CompositeFilterDescriptor)filter);
                        }
                    }
                    //Session["OrderFilters"] = savedFilters;
                    Session["OrderFiltersComposite"] = savedFiltersComposite;
                }
                else
                {
                    Session["OrderFiltersComposite"] = null;
                }

                string userId;
                if (aspNetUser.SfId == 0)
                {
                    userId = _userSf.Id;
                }
                else
                {
                    userId = _sfDb.AspNetUsers.Where(a => a.SfId == aspNetUser.SfId).FirstOrDefault().Id;                    
                }
                //IQueryable<Order> orders = _sfDb.Orders.Where(o => o.UserId == _userSf.Id);
                IQueryable<Order> orders = _sfDb.Orders.Where(o => o.UserId == userId);
                List<OrderWithSomeDetailViewModel> ordersWithDetail = (from o in orders
                                                                       //join u in _sfDb.AspNetUsers on o.UserId equals u.Id 
                                                                       where o.StoreFrontId == _site.StoreFrontId 
                                                                       //&& u.SfId == aspNetUser.SfId
                                                                       select new OrderWithSomeDetailViewModel()
                                                                       {
                                                                           Id = o.Id,
                                                                           SFOrderNumber = o.SFOrderNumber,
                                                                           PONumber = o.PONumber,
                                                                           OrderStatus = o.OrderStatus,
                                                                           DateCreated = o.DateCreated,
                                                                           ProductCodes = (from p in _sfDb.Products
                                                                                           join od in _sfDb.OrderDetails on o.Id equals od.OrderId
                                                                                           where od.ProductId == p.Id
                                                                                           select p.ProductCode).ToList(),
                                                                           ShortDescs = (from p in _sfDb.Products
                                                                                         join od in _sfDb.OrderDetails on o.Id equals od.OrderId
                                                                                         where od.ProductId == p.Id
                                                                                         select p.ShortDesc).ToList(),
                                                                       }).AsEnumerable()
                                        .Select(o => new OrderWithSomeDetailViewModel()
                                        {
                                            Id = o.Id,
                                            SFOrderNumber = o.SFOrderNumber,
                                            PONumber = o.PONumber,
                                            OrderStatus = o.OrderStatus,
                                            DateCreated = o.DateCreated,
                                            ProductCodeString = string.Join(",", o.ProductCodes.Select(pc => "[" + pc + "]")),
                                            ShortDescString = string.Join(",", o.ShortDescs.Select(sd => "[" + sd + "]")),
                                        }).ToList();

                foreach (OrderWithSomeDetailViewModel o in ordersWithDetail)
                {
                    List<OrderTracking> shipTrackings = _sfDb.OrderTrackings.Where(ot => ot.OrderNumber == o.Id).Select(ot => ot).ToList();

                    o.TrackingData = new List<TrackingViewModel>();

                    foreach (OrderTracking ot in shipTrackings)
                    {
                        List<TrackingDetailViewModel> detail = _sfDb.OrderTrackingDetails.Where(otd => otd.OrderTrackingId == ot.Id).Select(otd => new TrackingDetailViewModel()
                        {

                            TrackingNumber = ot.TrackingNumber,
                            ProductCode = _sfDb.Products.Where(p => p.Id == otd.ProductId).FirstOrDefault().ProductCode,
                            Qty = otd.Qty,
                        }).ToList();

                        o.TrackingData.Add(new TrackingViewModel()
                        {
                            TrackingNumber = ot.TrackingNumber,
                            TrackingDetails = detail,
                        });
                    }

                    o.OrderStatusDesc = GlobalConstants.OrderStatuses.Where(os => os.Code == o.OrderStatus).Select(os => os.Desc).FirstOrDefault();
                    o.TotalPrice = 0;
                    List<OrderDetail> orderDetails = _sfDb.OrderDetails.Where(od => od.OrderId == o.Id).ToList();
                    foreach (OrderDetail od in orderDetails)
                    {
                        o.TotalPrice = o.TotalPrice + (od.Qty * od.Price);
                    }
                }

                //DataSourceResult result = ordersWithDetail.ToDataSourceResult(request, orderWd => new
                //{
                //    Id = orderWd.Id,
                //    StoreFrontId = 0,
                //    CustomerId = 0,
                //    ShipBillType = 0,
                //    OnHold = 0,
                //    SFOrderNumber = orderWd.SFOrderNumber,
                //    DateCreated = orderWd.DateCreated,
                //    OrderStatus = orderWd.OrderStatus,
                //    DateShipped = orderWd.DateShipped,
                //    TrackingNumbers = orderWd.TrackingNumbers,
                //    OrderStatusDesc = orderWd.OrderStatusDesc,
                //    ProductCodeString = orderWd.ProductCodeString,
                //    ShortDescString = orderWd.ShortDescString,
                //    TotalPrice = 0
                //});
                DataSourceResult result = ordersWithDetail.ToDataSourceResult(request, orderWd => new OrderWithSomeDetailViewModel()
                {
                    Id = orderWd.Id,
                    SFOrderNumber = orderWd.SFOrderNumber,
                    PONumber = orderWd.PONumber,
                    DateCreated = orderWd.DateCreated,
                    OrderStatus = orderWd.OrderStatus,
                    DateShipped = orderWd.DateShipped,
                    TrackingNumbers = orderWd.TrackingNumbers,
                    TrackingData = orderWd.TrackingData,
                    OrderStatusDesc = orderWd.OrderStatusDesc,
                    ProductCodeString = orderWd.ProductCodeString,
                    ShortDescString = orderWd.ShortDescString,
                    //TotalPrice = 0,
                    TotalPrice = orderWd.TotalPrice,
                });

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }

        #region Vendor Orders

        [TokenAuthorize]
        public ActionResult Orders_Read_Vendor([DataSourceRequest] DataSourceRequest request)
        {
            try
            {
                // Save the Filter to filter the Order Detail
                List<Kendo.Mvc.CompositeFilterDescriptor> savedFiltersComposite = new List<Kendo.Mvc.CompositeFilterDescriptor>();
                if (request.Filters.Any())
                {
                    foreach (var filter in request.Filters)
                    {
                        if (filter is Kendo.Mvc.FilterDescriptor)
                        {
                            var newComposite = new Kendo.Mvc.CompositeFilterDescriptor();
                            newComposite.FilterDescriptors.Add(filter);
                            savedFiltersComposite.Add(newComposite);
                        }
                        if (filter is Kendo.Mvc.CompositeFilterDescriptor)
                        {
                            savedFiltersComposite.Add((Kendo.Mvc.CompositeFilterDescriptor)filter);
                        }
                    }
                    //Session["OrderFilters"] = savedFilters;
                    Session["OrderFiltersComposite"] = savedFiltersComposite;
                }
                else
                {
                    Session["OrderFiltersComposite"] = null;
                }

                Vendor vendor = _sfDb.Vendors.FirstOrDefault(v => v.AspNetUserId == _userSf.Id);
                List<int> vendorOrders = _sfDb.OrderDetails.Where(od => od.IsFulfilledByVendor == 1 && od.VendorId == vendor.Id).Select(od => od.OrderId).ToList();

                List<OrderWithSomeDetailViewModel> ordersWithDetail = (from o in _sfDb.Orders
                                                                       where o.StoreFrontId == _site.StoreFrontId && vendorOrders.Contains(o.Id)
                                                                       select new OrderWithSomeDetailViewModel()
                                                                       {
                                                                           Id = o.Id,
                                                                           SFOrderNumber = o.SFOrderNumber,
                                                                           PONumber = o.PONumber,
                                                                           OrderStatus = o.OrderStatus,
                                                                           DateCreated = o.DateCreated,
                                                                           ProductCodes = (from p in _sfDb.Products
                                                                                           join od in _sfDb.OrderDetails on o.Id equals od.OrderId
                                                                                           where od.ProductId == p.Id
                                                                                           select p.ProductCode).ToList(),
                                                                           ShortDescs = (from p in _sfDb.Products
                                                                                         join od in _sfDb.OrderDetails on o.Id equals od.OrderId
                                                                                         where od.ProductId == p.Id
                                                                                         select p.ShortDesc).ToList(),
                                                                       }).AsEnumerable()
                                        .Select(o => new OrderWithSomeDetailViewModel()
                                        {
                                            Id = o.Id,
                                            SFOrderNumber = o.SFOrderNumber,
                                            PONumber = o.PONumber,
                                            OrderStatus = o.OrderStatus,
                                            DateCreated = o.DateCreated,
                                            ProductCodeString = string.Join(",", o.ProductCodes.Select(pc => "[" + pc + "]")),
                                            ShortDescString = string.Join(",", o.ShortDescs.Select(sd => "[" + sd + "]")),
                                        }).ToList();

                foreach (OrderWithSomeDetailViewModel o in ordersWithDetail)
                {
                    List<OrderTracking> shipTrackings = _sfDb.OrderTrackings.Where(ot => ot.OrderNumber == o.Id).Select(ot => ot).ToList();

                    o.TrackingData = new List<TrackingViewModel>();

                    foreach (OrderTracking ot in shipTrackings)
                    {
                        List<TrackingDetailViewModel> detail = _sfDb.OrderTrackingDetails.Where(otd => otd.OrderTrackingId == ot.Id).Select(otd => new TrackingDetailViewModel()
                        {

                            TrackingNumber = ot.TrackingNumber,
                            ProductCode = _sfDb.Products.Where(p => p.Id == otd.ProductId).FirstOrDefault().ProductCode,
                            Qty = otd.Qty,
                        }).ToList();

                        o.TrackingData.Add(new TrackingViewModel()
                        {
                            TrackingNumber = ot.TrackingNumber,
                            TrackingDetails = detail,
                        });
                    }

                    o.OrderStatusDesc = GlobalConstants.OrderStatuses.Where(os => os.Code == o.OrderStatus).Select(os => os.Desc).FirstOrDefault();
                    o.TotalPrice = 0;
                    List<OrderDetail> orderDetails = _sfDb.OrderDetails.Where(od => od.OrderId == o.Id).ToList();
                    foreach (OrderDetail od in orderDetails)
                    {
                        o.TotalPrice = o.TotalPrice + (od.Qty * od.Price);
                    }
                }

                //DataSourceResult result = ordersWithDetail.ToDataSourceResult(request, orderWd => new
                //{
                //    Id = orderWd.Id,
                //    StoreFrontId = 0,
                //    CustomerId = 0,
                //    ShipBillType = 0,
                //    OnHold = 0,
                //    SFOrderNumber = orderWd.SFOrderNumber,
                //    DateCreated = orderWd.DateCreated,
                //    OrderStatus = orderWd.OrderStatus,
                //    DateShipped = orderWd.DateShipped,
                //    TrackingNumbers = orderWd.TrackingNumbers,
                //    OrderStatusDesc = orderWd.OrderStatusDesc,
                //    ProductCodeString = orderWd.ProductCodeString,
                //    ShortDescString = orderWd.ShortDescString,
                //    TotalPrice = 0
                //});
                DataSourceResult result = ordersWithDetail.ToDataSourceResult(request, orderWd => new OrderWithSomeDetailViewModel()
                {
                    Id = orderWd.Id,
                    SFOrderNumber = orderWd.SFOrderNumber,
                    PONumber = orderWd.PONumber,
                    DateCreated = orderWd.DateCreated,
                    OrderStatus = orderWd.OrderStatus,
                    DateShipped = orderWd.DateShipped,
                    TrackingNumbers = orderWd.TrackingNumbers,
                    TrackingData = orderWd.TrackingData,
                    OrderStatusDesc = orderWd.OrderStatusDesc,
                    ProductCodeString = orderWd.ProductCodeString,
                    ShortDescString = orderWd.ShortDescString,
                    //TotalPrice = 0,
                    TotalPrice = orderWd.TotalPrice,
                });

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }

        [TokenAuthorize]
        public ActionResult Order_Details_Read_Vendor([DataSourceRequest] DataSourceRequest request, int orderId)
        {
            try
            {
                Vendor vendor = _sfDb.Vendors.FirstOrDefault(v => v.AspNetUserId == _userSf.Id);
                List<OrderDetail> orderDetails = _sfDb.OrderDetails.Where(od => od.OrderId == orderId && od.IsFulfilledByVendor == 1 && od.VendorId == vendor.Id).ToList();

                foreach (OrderDetail orderdetail in orderDetails)
                {
                    // has all been shipped?
                    List<OrderTrackingDetail> orderTrackingDetails = _sfDb.OrderTrackingDetails.Where(otd => otd.OrderDetailId == orderdetail.Id).ToList();
                    int qtyShipped = 0;
                    foreach (OrderTrackingDetail otd in orderTrackingDetails)
                    {
                        qtyShipped += otd.Qty;
                    }
                    orderdetail.Qty -= qtyShipped;
                }

                DataSourceResult result = orderDetails.ToDataSourceResult(request, od => new OrderDetailViewModel()
                {
                    Id = od.Id,
                    ProductId = od.ProductId,
                    ProductCode = _sfDb.Products.FirstOrDefault(p => p.Id == od.ProductId).ProductCode,
                    Qty = od.Qty,
                    Selected = false,
                });

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }

        // GET: Manage/VendorOrder_Update
        [TokenAuthorize]
        [HttpPost]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult VendorOrder_Update([DataSourceRequest] DataSourceRequest request, OrderViewModel order)
        {
            bool noerror = true;
            try
            {
                if (order.ProductQtys == null)
                {
                    ModelState.AddModelError("", "Please Select Item to Ship");
                }

                if (order != null && ModelState.IsValid)
                {
                    // Save the tracking number and the items shipped for that tracking
                    string[] trackingNumbers = order.TrackingNumbers.Split(',');
                    string[] productIds = order.ProductIds.Split(',');
                    string[] productQtys = order.ProductQtys.Split(',');
                    string[] productIdQtys = order.ProductQtys.Split(';');

                    foreach (string trackingNumber in trackingNumbers)
                    {
                        List<OrderTrackingDetail> orderTrackingDetails = new List<OrderTrackingDetail>();
                        for (int i = 0; i < productIdQtys.Length; i++)
                        {
                            int productIdInt = Convert.ToInt32(productIdQtys[i].Split(',')[0]);
                            int productQtyInt = Convert.ToInt32(productIdQtys[i].Split(',')[1]);
                            OrderDetail orderDetail = _sfDb.OrderDetails.Where(od => od.OrderId == order.Id && od.ProductId == productIdInt).FirstOrDefault();
                            orderTrackingDetails.Add(new OrderTrackingDetail()
                            {
                                OrderDetailId = orderDetail != null ? orderDetail.Id : 0,
                                ProductId = productIdInt,
                                Qty = productQtyInt,
                                UserId = _userSf.Id,
                                UserName = _userSf.UserName,
                                DateCreated = DateTime.Now,
                            });
                        }

                        int carrierId = _sfDb.ShipMethods.Where(sm => sm.Code == order.ShipMethodCode).FirstOrDefault().CarrierId;
                        string carrier = _sfDb.ShipCarriers.Where(sc => sc.Id == carrierId).FirstOrDefault().Name;
                        OrderTracking orderTracking = new OrderTracking()
                        {
                            TrackingNumber = trackingNumber,
                            OrderNumber = order.Id,
                            Carrier = carrier,
                            DateCreated = DateTime.Now,
                            ShipMethod = order.ShipMethodCode,
                            OrderTrackingDetails = orderTrackingDetails
                        };
                        _sfDb.OrderTrackings.Add(orderTracking);
                    }
                    _sfDb.SaveChanges();

                    // change status
                    Order selectedOrder = _sfDb.Orders.FirstOrDefault(o => o.Id == order.Id);

                    List<OrderDetail> orderDetails = _sfDb.OrderDetails.Where(od => od.OrderId == order.Id).ToList();
                    string orderStatus = "SH";
                    foreach (OrderDetail od in orderDetails)
                    {
                        // if any are not complete, it is partial shipment
                        var trackingDetails = _sfDb.OrderTrackingDetails
                            .Where(otd => otd.OrderDetailId == od.Id)
                            .GroupBy(otd => otd.OrderDetailId)
                            .Select(otd => new
                            {
                                Qty = otd.Sum(s => s.Qty),
                            }).FirstOrDefault();
                        if (trackingDetails == null)
                        {
                            orderStatus = "PH";
                        };
                        if (orderStatus != "PH" && trackingDetails.Qty < od.Qty) orderStatus = "PH";

                        if (orderStatus == "PH") break;
                    }

                    if (selectedOrder != null)
                    {
                        selectedOrder.OrderStatus = orderStatus;
                        _sfDb.Entry(selectedOrder).State = EntityState.Modified;
                    }
                    else
                    {
                        noerror = false;
                    }

                    if (noerror)
                    {
                        _sfDb.SaveChanges();
                        GlobalFunctions.Log(_site.StoreFrontId, _userSf.Id, "", "Tracking Added", order.TrackingNumbers);
                    }
                    else
                    {
                        return Json(new { result = "Error", errors = "Missing Order Record" });
                    }
                }
                else
                {
                    return Json(new[] { order }.ToDataSourceResult(request, ModelState));
                }
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", errors = ex.Message });
            }

            return Json(new[] { order }.ToDataSourceResult(request, ModelState));
        }
        #endregion Vendor Orders


        // Helper kendo grid filter parsing function 
        private Kendo.Mvc.FilterDescriptor FindFilterDescriptor(IEnumerable<object> filters, string selectedMember)
        {
            Kendo.Mvc.FilterDescriptor selectedFilter = new Kendo.Mvc.FilterDescriptor();
            try
            {
                if (filters.Any())
                {
                    foreach (var filter in filters)
                    {
                        var descriptor = filter as Kendo.Mvc.FilterDescriptor;
                        if (descriptor != null && descriptor.Member.Contains(selectedMember))
                        {
                            selectedFilter = descriptor;
                            break;
                        }
                        else if (filter is Kendo.Mvc.CompositeFilterDescriptor)
                        {
                            var descriptor2 = filter as Kendo.Mvc.CompositeFilterDescriptor;
                            IEnumerable<object> nextFilter = descriptor2.FilterDescriptors;
                            selectedFilter = FindFilterDescriptor(nextFilter, selectedMember);
                            if (selectedFilter.Member.Contains(selectedMember)) break;
                        }
                    }
                }
                return selectedFilter;
            }
            catch (Exception ex)
            {
                selectedFilter.Member = ex.Message;
                return selectedFilter;
            }

        }

        // This is secondary read when user expand the order to reveal details
        [TokenAuthorize]
        public ActionResult OrderDetails_Read([DataSourceRequest] DataSourceRequest request, Order order)
        {
            try
            {
                // Retrieve the Filter to filter the Order Detail
                List<Kendo.Mvc.CompositeFilterDescriptor> filters = (List<Kendo.Mvc.CompositeFilterDescriptor>)Session["OrderFiltersComposite"];

                if (filters != null)
                {
                    Kendo.Mvc.FilterDescriptor newFilter;
                    newFilter = FindFilterDescriptor(filters, "ProductCode");
                    if (newFilter != null && newFilter.Member.Length > 0)
                    {
                        request.Filters.Add(new Kendo.Mvc.FilterDescriptor()
                        {
                            Member = "ProductCode",
                            Operator = Kendo.Mvc.FilterOperator.Contains,
                            Value = newFilter.Value ?? ""
                        });
                    }
                    newFilter = FindFilterDescriptor(filters, "ShortDesc");
                    if (newFilter != null && newFilter.Member.Length > 0)
                    {
                        request.Filters.Add(new Kendo.Mvc.FilterDescriptor()
                        {
                            Member = "ShortDesc",
                            Operator = Kendo.Mvc.FilterOperator.Contains,
                            Value = newFilter.Value ?? ""
                        });
                    }
                }
                else
                {
                    request.Filters.Clear();
                }

                List<OrderDetailViewModel> orderDetails = (from od in _sfDb.OrderDetails
                                                           join p in _sfDb.Products on od.ProductId equals p.Id
                                                           where od.OrderId == order.Id
                                                           select new OrderDetailViewModel()
                                                           {
                                                               Id = od.Id,
                                                               OrderId = od.OrderId,
                                                               SFOrderNumber = od.SFOrderNumber,
                                                               ProductId = od.ProductId,
                                                               ProductCode = p.ProductCode,
                                                               ShortDesc = p.ShortDesc,
                                                               LongDesc = p.LongDesc,
                                                               Qty = od.Qty,
                                                               Status = od.Status,
                                                               Price = od.Price,
                                                               Total = od.Price * od.Qty,
                                                               ImageRelativePath = _sfDb.ProductImages.Where(pi => pi.ProductId == p.Id).Select(pif => pif.RelativePath).FirstOrDefault() ?? "Content/" + _site.StoreFrontName + "/Images/default.png",
                                                           }).ToList();

                foreach (OrderDetailViewModel odvm in orderDetails)
                {
                    var trackingDetails = _sfDb.OrderTrackingDetails
                        .Where(otd => otd.OrderDetailId == odvm.Id)
                        .GroupBy(otd => otd.OrderDetailId)
                        .Select(otd => new
                        {
                            Qty = otd.Sum(s => s.Qty),
                        }).FirstOrDefault();
                    if (trackingDetails != null)
                    {
                        odvm.QtyShipped = trackingDetails.Qty;
                    };
                }

                var retList = Json(orderDetails.ToDataSourceResult(request));

                return retList;
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }

        // This is secondary read when user expand the order to reveal details
        [TokenAuthorize]
        public ActionResult OrderDetailsVendor_Read([DataSourceRequest] DataSourceRequest request, Order order)
        {
            try
            {
                // Retrieve the Filter to filter the Order Detail
                List<Kendo.Mvc.CompositeFilterDescriptor> filters = (List<Kendo.Mvc.CompositeFilterDescriptor>)Session["OrderFiltersComposite"];

                if (filters != null)
                {
                    Kendo.Mvc.FilterDescriptor newFilter;
                    newFilter = FindFilterDescriptor(filters, "ProductCode");
                    if (newFilter != null && newFilter.Member.Length > 0)
                    {
                        request.Filters.Add(new Kendo.Mvc.FilterDescriptor()
                        {
                            Member = "ProductCode",
                            Operator = Kendo.Mvc.FilterOperator.Contains,
                            Value = newFilter.Value ?? ""
                        });
                    }
                    newFilter = FindFilterDescriptor(filters, "ShortDesc");
                    if (newFilter != null && newFilter.Member.Length > 0)
                    {
                        request.Filters.Add(new Kendo.Mvc.FilterDescriptor()
                        {
                            Member = "ShortDesc",
                            Operator = Kendo.Mvc.FilterOperator.Contains,
                            Value = newFilter.Value ?? ""
                        });
                    }
                }
                else
                {
                    request.Filters.Clear();
                }

                Vendor vendor = _sfDb.Vendors.Where(v => v.AspNetUserId == _userSf.Id).FirstOrDefault();
                List<OrderDetailViewModel> orderDetails = (from od in _sfDb.OrderDetails
                                                           join p in _sfDb.Products on od.ProductId equals p.Id
                                                           where od.OrderId == order.Id && od.IsFulfilledByVendor == 1 && od.VendorId == vendor.Id
                                                           select new OrderDetailViewModel()
                                                           {
                                                               Id = od.Id,
                                                               OrderId = od.OrderId,
                                                               SFOrderNumber = od.SFOrderNumber,
                                                               ProductId = od.ProductId,
                                                               ProductCode = p.ProductCode,
                                                               ShortDesc = p.ShortDesc,
                                                               LongDesc = p.LongDesc,
                                                               Qty = od.Qty,
                                                               Status = od.Status,
                                                               Price = od.Price,
                                                               Total = od.Price * od.Qty,
                                                               ImageRelativePath = _sfDb.ProductImages.Where(pi => pi.ProductId == p.Id).Select(pif => pif.RelativePath).FirstOrDefault() ?? "Content/" + _site.StoreFrontName + "/Images/default.png",
                                                           }).ToList();

                var retList = Json(orderDetails.ToDataSourceResult(request));

                return retList;
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }

        // This is secondary read when user expand the order to reveal details
        [TokenAuthorize]
        public ActionResult OrderDetailShipment_Read([DataSourceRequest] DataSourceRequest request, Order order)
        {
            try
            {
                // Retrieve the Filter to filter the Order Detail
                request.Filters.Clear();

                List<ShipmentViewModel> orderDetails = (from ot in _sfDb.OrderTrackings
                                                        where ot.OrderNumber == order.Id
                                                        select new ShipmentViewModel()
                                                        {
                                                            Id = ot.Id,
                                                            OrderId = ot.OrderNumber ?? 0,
                                                            DateShipped = ot.DateCreated ?? new DateTime(1, 1, 1),
                                                            TrackId = ot.Id,
                                                            TrackingNumber = ot.TrackingNumber,
                                                            Carrier = ot.Carrier,
                                                            Status = ot.DeliveryStatus ?? 0,
                                                        }).ToList();

                foreach (ShipmentViewModel t in orderDetails)
                {
                    string trackingDetail = t.TrackingNumber;
                    switch (t.Carrier.ToLower())
                    {
                        case "fedex":
                            trackingDetail = "<a href='http://www.fedex.com/apps/fedextrack/?action=track&trackingnumber=" + t.TrackingNumber + "' target='_blank' style = 'color:blue; text-decoration: underline'>" + t.TrackingNumber + "</a><br>";
                            break;
                        case "ups":
                            trackingDetail = "<a href='http://wwwapps.ups.com/WebTracking/processInputRequest?HTMLVersion=5.0&loc=en_US&Requester=UPSHome&tracknum=" + t.TrackingNumber + "&AgreeToTermsAndConditions=yes&ignore=&track.x=26&track.y=15' target='_blank' style = 'color:blue; text-decoration: underline'>" + t.TrackingNumber + "</a><br>";
                            break;
                        case "usps":
                            trackingDetail = "<a href='http://trkcnfrm1.smi.usps.com/PTSInternetWeb/InterLabelInquiry.do?CAMEFROM=OK&strOrigTrackNum=" + t.TrackingNumber + "' target='_blank' style = 'color:blue; text-decoration: underline'>" + t.TrackingNumber + "</a><br>";
                            break;
                        default:
                            break;
                    }

                    t.TrackingNumber = trackingDetail;
                }

                var retList = Json(orderDetails.ToDataSourceResult(request));

                return retList;
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }

        // This is secondary read when user expand the order to reveal details
        [TokenAuthorize]
        public ActionResult TrackingDetails_Read([DataSourceRequest] DataSourceRequest request, TrackingDetailViewModel tracking)
        {
            try
            {
                List<TrackingDetailViewModel> trackingDetails = (from otd in _sfDb.OrderTrackingDetails
                                                                 join p in _sfDb.Products on otd.ProductId equals p.Id
                                                                 where otd.OrderTrackingId == tracking.OrderTrackingId
                                                                 select new TrackingDetailViewModel()
                                                                 {
                                                                     OrderDetailId = otd.OrderDetailId,
                                                                     ProductId = otd.ProductId,
                                                                     ProductCode = p.ProductCode,
                                                                     Qty = otd.Qty,
                                                                 }).ToList();

                var retList = Json(trackingDetails.ToDataSourceResult(request));

                return retList;
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }

        [TokenAuthorize]
        public ActionResult ProductList(CategoryViewModel categorySelected, string searchText, string searchTextDescription)
        {
            try
            {
                CategoryViewModel category = null;
                if (categorySelected.Id != 0)
                {
                    string name = (from c in _sfDb.Categories
                                   where c.Id == categorySelected.Id
                                   select c.Name).FirstOrDefault();
                    category = categorySelected;
                    ViewBag.CategoryId = category.Id;
                    ViewBag.CategoryName = name;
                }
                else
                {
                    ViewBag.CategoryId = 0;
                    ViewBag.CategoryName = "";
                }

                if (searchText != null && searchText.Length > 0)
                    ViewBag.SearchText = searchText;
                else
                    ViewBag.SearchText = "";

                if (searchTextDescription != null && searchTextDescription.Length > 0)
                    ViewBag.SearchTextDescription = searchTextDescription;
                else
                    ViewBag.SearchTextDescription = "";

                return View();
            }
            catch (Exception ex)
            {
                return View("Error", new HandleErrorInfo(ex, "Order", "Index"));
            }
        }

        [TokenAuthorize]
        public ActionResult ProductDetail(int? id, string categoryIdsSelected, string searchText, string searchTextDescription)
        {
            try
            {
                ViewBag.CategoryIdsSelected = categoryIdsSelected;

                ProductViewModel product = _sfDb.Products.Where(p => p.Id == id).Select(f => new ProductViewModel()
                {
                    Id = f.Id,
                    ProductCode = f.ProductCode,
                    Upc = f.Upc,
                    ShortDesc = f.ShortDesc,
                    LongDesc = f.LongDesc,
                    SellPrice = f.SellPrice,
                    SellPriceCAD = f.SellPriceCAD,
                    EnableMinQty = f.EnableMinQty == 1,
                    MinQty = f.MinQty,
                    EnableMaxQty = f.EnableMaxQty == 1,
                    MaxQty = f.MaxQty,
                    EMSQty = f.EMSQty,
                    InStock = f.EMSQty > 0,
                    EstRestockDate = f.EstRestockDate,
                    Uom = f.Uom,
                    ImageRelativePath = _sfDb.ProductImages.Where(pi => pi.ProductId == id && (pi.DisplayOrder ?? 1) == 1).Select(pif => pif.RelativePath).FirstOrDefault() ?? "Content/" + _site.StoreFrontName + "/Images/default.png",
                    FileRelativePath = _sfDb.ProductFiles.Where(pf => pf.ProductId == id).Select(pf => pf.RelativePath).FirstOrDefault() ?? "Content/Defaults/Web/default.png",
                }).FirstOrDefault();

                if (product != null)
                {
                    SystemSetting systemSetting = _sfDb.SystemSettings.Where(ss => ss.StoreFrontId == _site.StoreFrontId).FirstOrDefault();
                    if (systemSetting.TurnOnProductMinMaxLevels == 1 && systemSetting.TurnOnProductMinMaxLevelsFor == "Enforce Per Group")
                    {
                        var selectedProducts = (from ugp in _sfDb.UserGroupProducts
                                                join ugu in _sfDb.UserGroupUsers on ugp.UserGroupId equals ugu.UserGroupId
                                                join p in _sfDb.Products on ugp.ProductId equals p.Id
                                                where p.Id == id && ugp.StoreFrontId == _site.StoreFrontId && ugu.AspNetUserId == _site.AspNetUserId
                                                select new UserGroupProductsVM()
                                                {
                                                    Id = ugp.Id,
                                                    ProductId = ugp.ProductId,
                                                    MinQty = ugp.MinQty,
                                                    MaxQty = ugp.MaxQty
                                                }).ToList();
                        if (selectedProducts != null && selectedProducts.Count > 0)
                        {
                            foreach (var p in selectedProducts)
                            {
                                product.MinQty = p.MinQty;
                                product.MaxQty = p.MaxQty;
                                product.EnableMinQty = true;
                                product.EnableMaxQty = true;
                            }
                        }
                    }

                    product.ProductImages = (from pi in _sfDb.ProductImages
                                             where pi.ProductId == product.Id
                                             select new ProductImageVM()
                                             {
                                                 Id = pi.Id,
                                                 RelativePath = pi.RelativePath == null ? "Content/" + _site.StoreFrontName + "/Images/default.png" : pi.RelativePath,
                                                 DateCreated = pi.DateCreated,
                                                 UserName = pi.UserName,
                                             }).ToList();
                }

                // Retrieve Categories for this product
                List<CategoryViewModel> categories = (from pc in _sfDb.ProductCategories
                                                      join c in _sfDb.Categories on pc.CategoryId equals c.Id
                                                      where pc.ProductId == product.Id
                                                      select new CategoryViewModel { Id = c.Id, Name = c.Name, Desc = c.Desc }).ToList();

                ViewBag.Categories = categories;
                ViewBag.SearchText = searchText;
                ViewBag.SearchTextDescription = searchTextDescription;

                return View(product);
            }
            catch (Exception ex)
            {
                return View("Error", new HandleErrorInfo(ex, "Order", "Index"));
            }
        }

        // (Not used currently)
        public ActionResult Categories_Read([DataSourceRequest] DataSourceRequest request)
        {
            IEnumerable<Category> data = GetAllCategories(_site.StoreFrontId);

            return Json(data.ToDataSourceResult(request), JsonRequestBehavior.AllowGet);
        }

        // (Not used currently)
        public List<Category> GetAllCategories(int storeFrontId)
        {
            return _sfDb.Categories.Where(c => c.StoreFrontId == storeFrontId).OrderBy(o => o.Name).ToList();
        }

        public ActionResult Products_Specials()
        {
            try
            {
                IEnumerable<ProductViewModel> data = GetProductsSpecials().Select(s => new ProductViewModel()
                {
                    Id = s.Id,
                    EmsProductId = s.EMSProductId,
                    ProductCode = s.ProductCode,
                    PickPackCode = s.PickPackCode,
                    Upc = s.Upc,
                    ShortDesc = s.ShortDesc,
                    LongDesc = s.LongDesc,
                    Weight = s.Weight,
                    Length = s.Length,
                    Width = s.Width,
                    Height = s.Height,
                    Restricted = s.Restricted == 1 ? true : false,
                    DefaultValue = s.DefaultValue,
                    SellPrice = s.SellPrice,
                    LowLevel = s.LowLevel,
                    CreatedBy = s.CreatedBy,
                    DateCreated = s.DateCreated,
                    UserId = s.UserId,
                    UserName = s.UserName,
                    ItemValue = s.ItemValue,
                    EnableMinQty = s.EnableMinQty == 1,
                    MinQty = s.MinQty,
                    EnableMaxQty = s.EnableMaxQty == 1,
                    MaxQty = s.MaxQty,
                    OrderQty = s.LowLevel,
                    DisplayOrder = s.DisplayOrder,
                    Status = s.Status,
                    IsSpecial = s.IsSpecial,
                    ImageRelativePath = GetProductDefaultImage(s.Id) ?? "Content/" + _site.StoreFrontName + "/Images/default.png",
                    Categories = GetCategoriesForProduct(s.Id).Select(c => new CategoryViewModel()
                    {
                        Id = c.Id,
                        Name = c.Name,
                        Desc = c.Desc
                    }).ToList()
                });

                return Json(data, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }

        // (Not used currently)
        [TokenAuthorize]
        public ActionResult Product_Update([DataSourceRequest] DataSourceRequest request, ProductViewModel product)
        {
            return Json(ModelState.ToDataSourceResult());
        }

        [TokenAuthorize]
        public ActionResult Read_Categories()
        {
            try
            {
                List<CategoryViewModel> categories = new List<CategoryViewModel>();

                // get permissions
                // where uc.AspNetUserId == _userSf.Id && c.StoreFrontId == _site.StoreFrontId
                if (_site.SiteAuth.InventoryRestrictCategory == 1)
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
                else
                {
                    categories = (from c in _sfDb.Categories
                                  where c.StoreFrontId == _site.StoreFrontId
                                  orderby c.Name
                                  select new CategoryViewModel
                                  {
                                      Id = c.Id,
                                      Name = c.Name
                                  }).ToList();
                }

                return Json(categories, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }

        [TokenAuthorize]
        public ActionResult Products_Read([DataSourceRequest] DataSourceRequest request)
        {
            try
            {
                List<int> userCategoryList = new List<int>();

                if (_site.SiteAuth.InventoryRestrictCategory == 1)
                    userCategoryList = (from uc in _sfDb.UserCategories where uc.AspNetUserId == _userSf.Id select uc.CategoryId).ToList();
                else
                    userCategoryList = (from c in _sfDb.Categories where c.StoreFrontId == _site.StoreFrontId select c.Id).ToList();

                var productList = (from p in _sfDb.Products
                                   join pi in _sfDb.ProductImages on p.Id equals pi.ProductId into subpi
                                   from pi in subpi.DefaultIfEmpty()
                                   where p.Status == 1 && p.StoreFrontId == _site.StoreFrontId && (pi.DisplayOrder ?? 1) == 1
                                   orderby p.ProductCode
                                   select new ProductViewModel()
                                   {
                                       Id = p.Id,
                                       EmsProductId = p.EMSProductId,
                                       ProductCode = p.ProductCode,
                                       PickPackCode = p.PickPackCode,
                                       Upc = p.Upc,
                                       ShortDesc = p.ShortDesc,
                                       LongDesc = p.LongDesc,
                                       SellPrice = p.SellPrice,
                                       SellPriceCAD = p.SellPriceCAD,
                                       LowLevel = p.LowLevel,
                                       CreatedBy = p.CreatedBy,
                                       DateCreated = p.DateCreated,
                                       UserId = p.UserId,
                                       UserName = p.UserName,
                                       EnableMinQty = p.EnableMinQty == 1,
                                       MinQty = p.MinQty,
                                       EnableMaxQty = p.EnableMaxQty == 1,
                                       MaxQty = p.MaxQty,
                                       EMSQty = p.EMSQty,
                                       InStock = p.EMSQty > 0,
                                       EstRestockDate = p.EstRestockDate,
                                       OrderQty = p.MinQty,
                                       Status = p.Status,
                                       ImageRelativePath = pi.RelativePath ?? "Content/" + _site.StoreFrontName + "/Images/default.png",
                                       Categories = (from c in _sfDb.Categories
                                                     join pc in _sfDb.ProductCategories on c.Id equals pc.CategoryId
                                                     where pc.ProductId == p.Id
                                                     select new CategoryViewModel { Id = c.Id, Desc = c.Desc }).ToList(),
                                   }).ToList();

                SystemSetting systemSetting = _sfDb.SystemSettings.Where(ss => ss.StoreFrontId == _site.StoreFrontId).FirstOrDefault();
                if (systemSetting.TurnOnProductMinMaxLevels == 1 && systemSetting.TurnOnProductMinMaxLevelsFor == "Enforce Per Group")
                {
                    foreach (var prod in productList)
                    {
                        var selectedProducts = (from ugp in _sfDb.UserGroupProducts
                                                join ugu in _sfDb.UserGroupUsers on ugp.UserGroupId equals ugu.UserGroupId
                                                join p in _sfDb.Products on ugp.ProductId equals p.Id
                                                where p.Id == prod.Id && ugp.StoreFrontId == _site.StoreFrontId && ugu.AspNetUserId == _site.AspNetUserId
                                                select new UserGroupProductsVM()
                                                {
                                                    Id = ugp.Id,
                                                    ProductId = ugp.ProductId,
                                                    MinQty = ugp.MinQty,
                                                    MaxQty = ugp.MaxQty
                                                }).ToList();
                        if (selectedProducts != null && selectedProducts.Count > 0)
                        {
                            foreach (var p in selectedProducts)
                            {
                                prod.MinQty = p.MinQty;
                                prod.MaxQty = p.MaxQty;
                                prod.EnableMinQty = true;
                                prod.EnableMaxQty = true;
                            }
                        }
                    }
                }

                if (_site.SiteAuth.InventoryRestrictCategory == 1)
                {
                    // remove those category not in the list
                    foreach (ProductViewModel pvm in productList)
                    {
                        bool removeMe = true;
                        foreach (CategoryViewModel c in pvm.Categories)
                        {
                            if (userCategoryList.Contains(c.Id))
                            {
                                removeMe = false;
                                break;
                            }
                        }
                        if (removeMe)
                        {
                            pvm.Status = 0;
                        }
                    }
                    productList.RemoveAll(pl => pl.Status == 0);
                }

                productList = productList.Select(p => new ProductViewModel()
                {
                    Id = p.Id,
                    EmsProductId = p.EmsProductId,
                    ProductCode = p.ProductCode,
                    PickPackCode = p.PickPackCode,
                    Upc = p.Upc,
                    ShortDesc = p.ShortDesc,
                    LongDesc = p.LongDesc,
                    Weight = p.Weight,
                    Length = p.Length,
                    Width = p.Width,
                    Height = p.Height,
                    Restricted = p.Restricted,
                    DefaultValue = p.DefaultValue,
                    SellPrice = p.SellPrice,
                    SellPriceCAD = p.SellPriceCAD,
                    LowLevel = p.LowLevel,
                    CreatedBy = p.CreatedBy,
                    DateCreated = p.DateCreated,
                    UserId = p.UserId,
                    UserName = p.UserName,
                    ItemValue = p.ItemValue,
                    EnableMinQty = p.EnableMinQty,
                    MinQty = p.MinQty,
                    EnableMaxQty = p.EnableMaxQty,
                    MaxQty = p.MaxQty,
                    EMSQty = p.EMSQty,
                    InStock = p.EMSQty > 0,
                    EstRestockDate = p.EstRestockDate,
                    Status = p.Status,
                    ImageRelativePath = p.ImageRelativePath ?? "Content/" + _site.StoreFrontName + "/Images/default.png",
                    Categories = p.Categories,
                    CategoriesString = string.Join(",", p.Categories.Select(c => "[" + c.Id.ToString() + "]"))
                }).ToList();

                //join pc in _sfDb.ProductCategories on p.Id equals pc.ProductId into subpc
                //from pc in subpc.DefaultIfEmpty()
                // where && userCategoryList.Contains(pc.CategoryId)

                DataSourceResult result = productList.ToDataSourceResult(request, productvm => new ProductViewModel
                {
                    Id = productvm.Id,
                    //EMSProductId = productvm.EmsProductId,
                    ProductCode = productvm.ProductCode,
                    PickPackCode = productvm.PickPackCode,
                    Upc = productvm.Upc,
                    ShortDesc = productvm.ShortDesc,
                    LongDesc = productvm.LongDesc,
                    Weight = productvm.Weight,
                    Length = productvm.Length,
                    Width = productvm.Width,
                    Height = productvm.Height,
                    Restricted = productvm.Restricted,
                    DefaultValue = productvm.DefaultValue,
                    SellPrice = productvm.SellPrice,
                    SellPriceCAD = productvm.SellPriceCAD,
                    LowLevel = productvm.LowLevel,
                    CreatedBy = productvm.CreatedBy,
                    DateCreated = productvm.DateCreated,
                    UserName = productvm.UserName,
                    ItemValue = productvm.ItemValue,
                    EnableMinQty = productvm.EnableMinQty,
                    MinQty = productvm.MinQty,
                    EnableMaxQty = productvm.EnableMaxQty,
                    MaxQty = productvm.MaxQty,
                    EMSQty = productvm.EMSQty,
                    InStock = productvm.EMSQty > 0,
                    EstRestockDate = productvm.EstRestockDate,
                    OrderQty = productvm.OrderQty,
                    DisplayOrder = productvm.DisplayOrder,
                    Status = productvm.Status,
                    ImageRelativePath = productvm.ImageRelativePath ?? "Content/" + _site.StoreFrontName + "/Images/default.png",
                    Categories = productvm.Categories,
                    CategoriesString = productvm.CategoriesString
                });

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }

        [TokenAuthorize]
        public ActionResult CartDisplay(Cart cartItem)
        {
            try
            {
                var cartItemList = (from c in _sfDb.Carts
                                    join p in _sfDb.Products on c.ProductId equals p.Id
                                    join pi in _sfDb.ProductImages on p.Id equals pi.ProductId into subpi
                                    from pi in subpi.DefaultIfEmpty()
                                    where c.StoreFrontId == _site.StoreFrontId &&
                                          c.UserId == _userSf.Id &&
                                          p.Status == 1 && p.StoreFrontId == _site.StoreFrontId
                                    orderby c.Id
                                    select new CartViewModel()
                                    {
                                        Id = c.Id,
                                        CartId = c.CartId,
                                        ProductId = c.ProductId,
                                        PickPackCode = p.PickPackCode,
                                        ShortDesc = p.ShortDesc,
                                        SellPrice = p.SellPrice,
                                        SellPriceCAD = p.SellPriceCAD,
                                        Count = c.Count,
                                        DateCreated = c.DateCreated,
                                        UserId = c.UserId,
                                        StoreFrontId = c.StoreFrontId,
                                        ImageRelativePath = pi.RelativePath ?? "Content/" + _site.StoreFrontName + "/Images/default.png",
                                        DisplayOrder = pi.DisplayOrder ?? 0
                                    }).AsEnumerable();

                return View(cartItemList);
            }
            catch (Exception ex)
            {
                return View("Error", new HandleErrorInfo(ex, "Order", "ProductList"));
            }
        }

        [HttpPost]
        [TokenAuthorize]
        public ActionResult Cart_AddItem(int qtyOrdered, int productId)
        {
            string feedbackMessage = "";
            try
            {
                qtyOrdered = qtyOrdered == 0 ? 1 : qtyOrdered;

                // Create Cart ID
                string cartId = CreateCartId();

                // Get the product info
                Product product = (from p in _sfDb.Products
                                   where p.Id == productId
                                   select p).FirstOrDefault();

                SystemSetting systemSetting = _sfDb.SystemSettings.Where(ss => ss.StoreFrontId == _site.StoreFrontId).FirstOrDefault();
                if (systemSetting.TurnOnProductMinMaxLevels == 1 && systemSetting.TurnOnProductMinMaxLevelsFor == "Enforce Per Group")
                {
                    if (product != null)
                    {
                        var selectedProducts = (from ugp in _sfDb.UserGroupProducts
                                                join ugu in _sfDb.UserGroupUsers on ugp.UserGroupId equals ugu.UserGroupId
                                                join p in _sfDb.Products on ugp.ProductId equals p.Id
                                                where p.Id == product.Id && ugp.StoreFrontId == _site.StoreFrontId && ugu.AspNetUserId == _site.AspNetUserId
                                                select new UserGroupProductsVM()
                                                {
                                                    Id = ugp.Id,
                                                    ProductId = ugp.ProductId,
                                                    MinQty = ugp.MinQty,
                                                    MaxQty = ugp.MaxQty
                                                }).ToList();
                        if (selectedProducts != null && selectedProducts.Count > 0)
                        {
                            foreach (var p in selectedProducts)
                            {
                                product.MinQty = p.MinQty;
                                product.MaxQty = p.MaxQty;
                                product.EnableMinQty = 1;
                                product.EnableMaxQty = 1;
                            }
                        }
                    }
                }

                Cart cartItem = new Cart()
                {
                    StoreFrontId = _site.StoreFrontId,
                    UserId = _userSf.Id,
                    CartId = cartId,
                    Count = qtyOrdered,
                    Price = product.SellPrice,
                    PriceCAD = product.SellPriceCAD,
                    ProductId = productId
                };

                if (Session["Cart"] == null)
                {
                    // Save in session data
                    List<Cart> cartItemList = new List<Cart>();

                    cartItemList.Add(cartItem);
                    Session["Cart"] = cartItemList;
                    ViewBag.CartItemCount = cartItemList.Count();

                    Session["CartItemCount"] = 1;
                }
                else
                {
                    // Save in session data
                    List<Cart> cartItemList = (List<Cart>)Session["Cart"];
                    cartItemList.Add(cartItem);
                    Session["Cart"] = cartItemList;
                    ViewBag.CartItemCount = cartItemList.Count();
                    Session["CartItemCount"] = Convert.ToInt32(Session["CartItemCount"]) + 1;
                }

                // Save in database
                Cart selectedCartItem = cartItem;
                selectedCartItem.DateCreated = DateTime.Now;

                // Update or Add?
                Cart existingCartItem = (from ci in _sfDb.Carts
                                         where ci.CartId == cartId
                                             && ci.StoreFrontId == _site.StoreFrontId
                                             && ci.UserId == _userSf.Id
                                             && ci.ProductId == selectedCartItem.ProductId
                                         select ci).FirstOrDefault();

                if (existingCartItem != null)
                {
                    // First check the quantity
                    int newqty = existingCartItem.Count + selectedCartItem.Count;
                    if (newqty > product.MaxQty && product.EnableMaxQty == 1)
                    {
                        // cannot order more than max
                        existingCartItem.Count = product.MaxQty;
                        _sfDb.Entry(existingCartItem).State = EntityState.Modified;
                        _sfDb.SaveChanges();
                        feedbackMessage = "The total qty for " + product.ProductCode + " exceeds the maximum qty allowed (" + product.MaxQty.ToString() + ") per order for this item. Qty has been adjusted.";
                        return Json(new { result = "Error", message = feedbackMessage, productcode = product.ProductCode, maxqty = product.MaxQty });
                    }
                    else
                    {
                        existingCartItem.Count = existingCartItem.Count + selectedCartItem.Count;
                    }
                    _sfDb.Entry(existingCartItem).State = EntityState.Modified;
                    _sfDb.SaveChanges();
                }
                else
                {
                    int newqty = selectedCartItem.Count;
                    if (newqty > product.MaxQty && product.EnableMaxQty == 1)
                    {
                        // cannot order more than max
                        selectedCartItem.Count = product.MaxQty;
                        feedbackMessage = "The total qty for for " + product.ProductCode + " exceeds the maximum qty allowed (" + product.MaxQty.ToString() + ") per order for this item. Qty has been adjusted.";
                        //feedbackMessage = "Qty ordered for " + product.ProductCode + " is more than maximum allowed (" + product.MaxQty.ToString() + ")";
                        //return Json(new { result = "Error", message = feedbackMessage, productcode = product.ProductCode, maxqty = product.MaxQty });
                    }
                    else if (newqty < product.MinQty && product.EnableMinQty == 1)
                    {
                        selectedCartItem.Count = product.MinQty;
                        feedbackMessage = "Qty ordered for " + product.ProductCode + " is set to minimum required (" + product.MinQty.ToString() + ") " + "Item added to cart";
                        //return Json(new { result = "Warning", message = feedbackMessage, productcode = product.ProductCode, minqty = product.MinQty });
                    }
                    _sfDb.Carts.Add(selectedCartItem);
                    _sfDb.SaveChanges();
                }

                var totItemsList = from ci in _sfDb.Carts
                                   where ci.CartId == cartId
                                       && ci.StoreFrontId == _site.StoreFrontId
                                       && ci.UserId == _userSf.Id
                                       && ci.Count > 0
                                   select ci;

                return Json(new { result = feedbackMessage.Length > 0 ? "Warning" : "Success", message = feedbackMessage, productcode = product.ProductCode, totItemsInCart = totItemsList.Count() });
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }

        private string CreateCartId()
        {
            string cartId = "";
            //SystemLog lastLog = _sfDb.SystemLogs
            //    .Where(sl => sl.StoreFrontId == _site.StoreFrontId && sl.LogType == "AutoLogin" && sl.Description == "BuyerCookie" && sl.CreatedById == _userSf.Id)
            //    .OrderByDescending(sl => sl.DateCreated)
            //    .Take(1).FirstOrDefault();

            if (Session["Cart"] != null)
            {
                // Use the first item's cart id for default cart id
                List<Cart> cartItemList = (List<Cart>)Session["Cart"];
                if (cartItemList.Count > 0)
                {
                    cartId = cartItemList[0].CartId;
                }
                else
                {
                    if (_site.BuyerCookie != null)
                        cartId = _site.BuyerCookie;
                    else
                        cartId = DateTime.Now.ToString("yyyyMMddHHmmss");
                }
            }
            else
            {
                if (_site.BuyerCookie != null)
                    cartId = _site.BuyerCookie;
                else
                    cartId = DateTime.Now.ToString("yyyyMMddHHmmss");
            }

            return cartId;
        }

        [HttpPost]
        [TokenAuthorize]
        public ActionResult Cart_Reorder(OrderViewModel order)
        {
            string feedbackMessage = "";
            try
            {
                // Only if there's parameter
                if (order == null) return Json(new { result = "Error", message = "No order selected" });

                // Create Cart ID
                string cartId = CreateCartId();

                // Messages collection
                StringBuilder returnMessages = new StringBuilder();

                // Get the order data
                List<OrderDetail> lastOrderDetails = _sfDb.OrderDetails.Where(od => od.OrderId == order.Id).ToList();
                if (lastOrderDetails.Count > 0)
                {
                    foreach (OrderDetail od in lastOrderDetails)
                    {
                        // Get the product info
                        Product product = (from p in _sfDb.Products
                                           where p.Id == od.ProductId
                                           select p).FirstOrDefault();

                        SystemSetting systemSetting = _sfDb.SystemSettings.Where(ss => ss.StoreFrontId == _site.StoreFrontId).FirstOrDefault();
                        if (systemSetting.TurnOnProductMinMaxLevels == 1 && systemSetting.TurnOnProductMinMaxLevelsFor == "Enforce Per Group")
                        {
                            if (product != null)
                            {
                                var selectedProducts = (from ugp in _sfDb.UserGroupProducts
                                                        join ugu in _sfDb.UserGroupUsers on ugp.UserGroupId equals ugu.UserGroupId
                                                        join p in _sfDb.Products on ugp.ProductId equals p.Id
                                                        where p.Id == product.Id && ugp.StoreFrontId == _site.StoreFrontId && ugu.AspNetUserId == _site.AspNetUserId
                                                        select new UserGroupProductsVM()
                                                        {
                                                            Id = ugp.Id,
                                                            ProductId = ugp.ProductId,
                                                            MinQty = ugp.MinQty,
                                                            MaxQty = ugp.MaxQty
                                                        }).ToList();
                                if (selectedProducts != null && selectedProducts.Count > 0)
                                {
                                    foreach (var p in selectedProducts)
                                    {
                                        product.MinQty = p.MinQty;
                                        product.MaxQty = p.MaxQty;
                                        product.EnableMinQty = 1;
                                        product.EnableMaxQty = 1;
                                    }
                                }
                            }
                        }

                        Cart selectedCartItem = new Cart()
                        {
                            StoreFrontId = _site.StoreFrontId,
                            UserId = _userSf.Id,
                            CartId = cartId,
                            ProductId = od.ProductId,
                            Count = od.Qty,
                            Price = product.SellPrice,
                            PriceCAD = product.SellPriceCAD,
                            DateCreated = DateTime.Now,
                        };
                        selectedCartItem.DateCreated = DateTime.Now;

                        // Update or Add?
                        Cart existingCartItem = (from ci in _sfDb.Carts
                                                 where ci.StoreFrontId == _site.StoreFrontId
                                                     && ci.UserId == _userSf.Id
                                                     && ci.ProductId == selectedCartItem.ProductId
                                                 select ci).FirstOrDefault();

                        if (existingCartItem != null)
                        {
                            // First check the quantity
                            int newqty = existingCartItem.Count + selectedCartItem.Count;
                            if (newqty > product.MaxQty && product.EnableMaxQty == 1)
                            {
                                // cannot order more than max
                                selectedCartItem.Count = product.MaxQty - existingCartItem.Count;
                                existingCartItem.Count = product.MaxQty;
                                _sfDb.Entry(existingCartItem).State = EntityState.Modified;
                            }
                            else
                            {
                                existingCartItem.Count = existingCartItem.Count + selectedCartItem.Count;
                            }
                            feedbackMessage = "* " + selectedCartItem.Count.ToString() + " of " + product.ProductCode.TrimEnd() + " added to cart";
                            _sfDb.Entry(existingCartItem).State = EntityState.Modified;
                        }
                        else
                        {
                            int newqty = selectedCartItem.Count;
                            if (newqty > product.MaxQty && product.EnableMaxQty == 1)
                            {
                                // cannot order more than max
                                selectedCartItem.Count = product.MaxQty;
                            }
                            else if (newqty < product.MinQty && product.EnableMinQty == 1)
                            {
                                selectedCartItem.Count = product.MinQty;
                            }
                            feedbackMessage = "* " + selectedCartItem.Count.ToString() + " of " + product.ProductCode.TrimEnd() + " added to cart";
                            _sfDb.Carts.Add(selectedCartItem);
                        }

                        returnMessages.AppendLine(feedbackMessage);
                    }
                    _sfDb.SaveChanges();
                    feedbackMessage = returnMessages.ToString();
                }

                var totItemsList = from ci in _sfDb.Carts
                                   where ci.CartId == cartId
                                       && ci.StoreFrontId == _site.StoreFrontId
                                       && ci.UserId == _userSf.Id
                                       && ci.Count > 0
                                   select ci;

                return Json(new { result = "Success", message = feedbackMessage, totItemsInCart = totItemsList.Count() });
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }


        [TokenAuthorize]
        public ActionResult Cart_ReadItems([DataSourceRequest] DataSourceRequest request)
        {
            try
            {
                var cartItemList = (from c in _sfDb.Carts
                                    join p in _sfDb.Products on c.ProductId equals p.Id
                                    where c.StoreFrontId == _site.StoreFrontId &&
                                          c.UserId == _userSf.Id &&
                                          p.Status == 1 && p.StoreFrontId == _site.StoreFrontId
                                    orderby c.Id
                                    select new CartViewModel()
                                    {
                                        Id = c.Id,
                                        CartId = c.CartId,
                                        ProductId = c.ProductId,
                                        PickPackCode = p.PickPackCode,
                                        ShortDesc = p.ShortDesc,
                                        Count = c.Count,
                                        SellPrice = c.Price,
                                        SellPriceCAD = c.PriceCAD,
                                        DateCreated = c.DateCreated,
                                        UserId = c.UserId,
                                        StoreFrontId = c.StoreFrontId
                                    }).AsEnumerable();

                DataSourceResult result = cartItemList.ToDataSourceResult(request);

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }

        [TokenAuthorize]
        [HttpPost]
        public ActionResult Cart_UpdateItem(CartViewModel cart)
        {
            try
            {
                if (ModelState.ContainsKey("VendorId") && ModelState["VendorId"].Errors.Count > 0) ModelState["VendorId"].Errors.Clear();
                if (ModelState.IsValid)
                {
                    // Get the product info
                    Product product = (from p in _sfDb.Products
                                       where p.Id == cart.ProductId
                                       select p).FirstOrDefault();

                    var entity = (from c in _sfDb.Carts
                                  where c.Id == cart.Id
                                  select c).FirstOrDefault();

                    entity.IsFulfilledByVendor = cart.IsFulfilledByVendor ? 1 : 0;

                    if (product.IsFulfilledByVendor == 1)
                    {
                        VendorProduct vendorProduct = _sfDb.VendorProducts.FirstOrDefault(vp => vp.ProductId == cart.ProductId);
                        if (vendorProduct == null)
                        {
                            return Json(new { result = "Error", message = "Vendor cannot fulfill item" });
                        }
                        else
                        {
                            entity.VendorId = vendorProduct.VendorId;
                        }
                    }

                    // First check the quantity
                    if (cart.Count > product.MaxQty && product.EnableMaxQty == 1)
                    {
                        // cannot order more than max
                        entity.Count = product.MaxQty;
                        _sfDb.Entry(entity).State = EntityState.Modified;
                        _sfDb.SaveChanges();
                        return Json(new { result = "ErrorMax", message = "Qty exceeding " + product.MaxQty.ToString(), productcode = product.ProductCode, maxqty = product.MaxQty });
                    }
                    else if (cart.Count < product.MinQty && product.EnableMinQty == 1)
                    {
                        // cannot order less than min
                        entity.Count = product.MinQty;
                        _sfDb.Entry(entity).State = EntityState.Modified;
                        _sfDb.SaveChanges();
                        return Json(new { result = "ErrorMin", message = "Qty less than minimum " + product.MinQty.ToString(), productcode = product.ProductCode, minqty = product.MinQty });
                    }
                    else
                    {
                        entity.Count = cart.Count;
                        _sfDb.Entry(entity).State = EntityState.Modified;
                        _sfDb.SaveChanges();
                    }

                    //_sfDb.Carts.Attach(entity);

                    return Json(new { result = "Success", message = "Qty updated" });
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
                    return Json(new { result = "Error", message = errorMessage });
                }

            }
            catch (Exception ex)
            {
                return Json(new { result = "Error", message = ex.Message });
            }
        }

        [TokenAuthorize]
        [HttpPost]
        public ActionResult Cart_RemoveItem(Cart cartItem)
        {
            try
            {
                // Remove from session
                List<Cart> cartItemList = (List<Cart>)Session["Cart"];
                cartItemList.RemoveAll(i => i.Id == cartItem.Id);
                Session["Cart"] = cartItemList;
                Session["CartItemCount"] = Convert.ToInt32(Session["CartItemCount"]) - 1;

                // Remove from database
                var selectedCartItem = (from ci in _sfDb.Carts
                                        where ci.Id == cartItem.Id
                                        select ci).FirstOrDefault();

                _sfDb.Carts.Remove(selectedCartItem);
                _sfDb.SaveChanges();

                return RedirectToAction("CartDisplay", "Order");
            }
            catch (Exception ex)
            {
                return View("Error", new HandleErrorInfo(ex, "Order", "CartDisplay"));
            }
        }

        [TokenAuthorize]
        [HttpPost]
        public async Task<ActionResult> Cart_SubmitOrderAsync(CartViewModel cartItemParam)
        {
            try
            {
                /*if (_site.AspNetUserId == null || _site.StoreFrontId <= 0) {
                    return RedirectToAction("Account", "Login");
                };*/

                if ((cartItemParam.FirstName ?? "").Length == 0) ModelState.AddModelError("FirstName", "Ship To Firstname Is Required");
                if ((cartItemParam.LastName ?? "").Length == 0) ModelState.AddModelError("LastName", "Ship To Lastname Is Required");
                if ((cartItemParam.Address1 ?? "").Length == 0) ModelState.AddModelError("Address1", "Ship To Address First Line Is Required");
                if ((cartItemParam.City ?? "").Length == 0) ModelState.AddModelError("City", "Ship To City Is Required");
                if ((cartItemParam.State ?? "").Length == 0) ModelState.AddModelError("State", "Ship To State Is Required");
                if ((cartItemParam.Zip ?? "").Length == 0) ModelState.AddModelError("Zip", "Ship To Zip Code Is Required");
                if ((cartItemParam.Country ?? "").Length == 0) ModelState.AddModelError("Country", "Ship To Country Is Required");
                if ((cartItemParam.Email ?? "").Length == 0) ModelState.AddModelError("Email", "Ship To Email Is Required");

                if (ModelState.IsValid)
                {
                    var cartId = cartItemParam.CartId;

                    List<Cart> cartItems = (from c in _sfDb.Carts
                                            where c.CartId == cartId
                                            select c).ToList();

                    // Get next order number
                    var sfOrderNumberQuery = from o in _sfDb.Orders
                                             where o.StoreFrontId == _site.StoreFrontId
                                             select o;
                    int sfOrderNumber = 0;
                    if (sfOrderNumberQuery.Count() > 0)
                    {
                        sfOrderNumber = sfOrderNumberQuery.Max(x => x.SFOrderNumber) + 1;
                    }
                    else
                    {
                        sfOrderNumber = 1;
                    }

                    // Save order detail values
                    decimal totalPrice = 0;
                    List<OrderDetail> selectedOrderDetailList = new List<OrderDetail>();
                    foreach (var c in cartItems)
                    {
                        Product selectedProduct = _sfDb.Products.Where(p => p.Id == c.ProductId).FirstOrDefault();
                        int? vendorId = _sfDb.VendorProducts.Where(vp => vp.ProductId == c.ProductId).Select(vp => vp.VendorId).FirstOrDefault();

                        OrderDetail selectedOrderDetail = new OrderDetail()
                        {
                            SFOrderNumber = sfOrderNumber,
                            ProductId = c.ProductId,
                            Qty = c.Count,
                            Price = c.Price,
                            Status = "",
                            ShipWithItem = 0,
                            IsFulfilledByVendor = selectedProduct.IsFulfilledByVendor,
                            VendorId = vendorId ?? 0,
                            UserId = _userSf.Id,
                            Currency = "USD",
                            UserName = _userSf.UserName,
                            DateCreated = DateTime.Now,
                            Product = selectedProduct,
                        };
                        totalPrice += c.Count * c.Price;
                        selectedOrderDetailList.Add(selectedOrderDetail);
                        selectedOrderDetail = null;

                        // Update product inventory available qty
                        selectedProduct.EMSQty = selectedProduct.EMSQty - c.Count;
                        if (selectedProduct.EMSQty < 0) selectedProduct.EMSQty = 0;
                        _sfDb.Entry(selectedProduct).State = EntityState.Modified;

                    }

                    // Also increase the user budget tally
                    SystemSetting systemSetting = _sfDb.SystemSettings.Where(ss => ss.StoreFrontId == _site.StoreFrontId).FirstOrDefault();
                    //UserSetting userSetting = _sfDb.UserSettings.Where(us => us.AspNetUserId == _site.AspNetUserId && us.StoreFrontId == _site.StoreFrontId).FirstOrDefault();
                    UserSetting userSetting = _sfDb.UserSettings.Where(us => us.AspNetUserId == _userSf.Id && us.StoreFrontId == _site.StoreFrontId).FirstOrDefault();
                    //UserGroupUser userGroupBudget = _sfDb.UserGroupUsers.Join(from y in _sfDb.UserGroups on y).Where(ug => ug.AspNetUserId == _site.AspNetUserId).FirstOrDefault();

                    if (userSetting.BudgetIgnore == 1)
                    {
                        //skip Budget
                    }
                    else
                    {                 
                        var model = (from post in _sfDb.UserGroups
                                     join ugu in _sfDb.UserGroupUsers on post.Id equals ugu.UserGroupId
                                     where ugu.AspNetUserId == _site.AspNetUserId && post.StoreFrontId == _site.StoreFrontId
                                     select post).FirstOrDefault();
                        /*
                        if (model != null)
                        {
                            if (systemSetting.BudgetEnforce == 1)
                            {
                                if (systemSetting.BudgetType == "Group Based Budget")
                                {
                                    if (totalPrice > model.CurrentBudgetLeft)
                                    {
                                        //return Json(new { result = "Error", message = "Total Cumulative Amount " + string.Format("{0:#.00}", userSetting.BudgetCurrentTotal) + " Exceeds User Budget Limit" });
                                        return Json(new { result = "Error", message = "We're sorry you don't currently have enough available budget to complete this purchase. Please contact your administrator and try again." });
                                    }
                                    else
                                    {
                                        UserGroup userGroupSettings = _sfDb.UserGroups.Where(us => us.Id == model.Id && us.StoreFrontId == _site.StoreFrontId).FirstOrDefault();
                                        userGroupSettings.CurrentBudgetLeft = userGroupSettings.CurrentBudgetLeft - totalPrice;
                                        //  if (userSetting.BudgetLastResetDate.Year == 1) userSetting.BudgetLastResetDate = DateTime.Now.Date; // set the initial oder date
                                        _sfDb.Entry(userGroupSettings).State = EntityState.Modified;
                                    }
                                }
                                else if (systemSetting.BudgetType == "User Based Budget")
                                {
                                    //if (totalPrice > userSetting.BudgetCurrentTotal - userSetting.BudgetLimit)
                                    if (totalPrice > userSetting.BudgetLimit - userSetting.BudgetCurrentTotal)
                                    {
                                        return Json(new { result = "Error", message = "We're sorry you don't currently have enough available budget to complete this purchase. Please contact your administrator and try again." });
                                    }
                                    else if (userSetting != null)
                                    {
                                        userSetting.BudgetCurrentTotal += totalPrice;
                                        if (userSetting.BudgetLastResetDate.Year == 1) userSetting.BudgetLastResetDate = DateTime.Now.Date; // set the initial oder date
                                        _sfDb.Entry(userSetting).State = EntityState.Modified;
                                    }
                                }
                            }

                        }
                        */
                        if (systemSetting.BudgetEnforce == 1)
                        {
                            if (systemSetting.BudgetType == "Group Based Budget")
                            {
                                if (model != null)
                                {
                                    if (totalPrice > model.CurrentBudgetLeft)
                                    {
                                        //return Json(new { result = "Error", message = "Total Cumulative Amount " + string.Format("{0:#.00}", userSetting.BudgetCurrentTotal) + " Exceeds User Budget Limit" });
                                        return Json(new { result = "Error", message = "We're sorry you don't currently have enough available budget to complete this purchase. Please contact your administrator and try again." });
                                    }
                                    else
                                    {
                                        UserGroup userGroupSettings = _sfDb.UserGroups.Where(us => us.Id == model.Id && us.StoreFrontId == _site.StoreFrontId).FirstOrDefault();
                                        userGroupSettings.CurrentBudgetLeft = userGroupSettings.CurrentBudgetLeft - totalPrice;
                                        //if (userSetting.BudgetLastResetDate.Year == 1) userSetting.BudgetLastResetDate = DateTime.Now.Date; // set the initial oder date
                                        _sfDb.Entry(userGroupSettings).State = EntityState.Modified;
                                    }
                                }
                            }
                            else if (systemSetting.BudgetType == "User Based Budget" && userSetting != null)
                            {
                                if (totalPrice > userSetting.BudgetLimit - userSetting.BudgetCurrentTotal)
                                {
                                    return Json(new { result = "Error", message = "We're sorry you don't currently have enough available budget to complete this purchase. Please contact your administrator and try again." });
                                }
                                else if (userSetting != null)
                                {
                                    userSetting.BudgetCurrentTotal += totalPrice;
                                    //if (userSetting.BudgetLastResetDate.Year == 1) userSetting.BudgetLastResetDate = DateTime.Now.Date; // set the initial oder date
                                    _sfDb.Entry(userSetting).State = EntityState.Modified;
                                }
                            }
                        }


                        //if (userSetting == null && model == null)
                        if (userSetting == null)
                        {
                            // this user hasn't been setup with budget, create one
                            userSetting = new UserSetting()
                            {
                                AspNetUserId = _userSf.Id,
                                StoreFrontId = _site.StoreFrontId,
                                BudgetIgnore = 0,
                                BudgetLimit = systemSetting.BudgetLimitDefault,
                                //BudgetResetInterval = systemSetting.BudgetRefreshPeriodDefault,
                                BudgetCurrentTotal = 0,
                                BudgetResetInterval = 0,
                                BudgetLastResetDate = new DateTime(1, 1, 1),
                                BudgetNextResetDate = new DateTime(1, 1, 1),
                                DefaultCurrency = "USD",
                            };
                            if (systemSetting.BudgetEnforce == 1 && systemSetting.BudgetType == "User Based Budget")
                            {
                                userSetting.BudgetCurrentTotal += totalPrice;
                                //if (userSetting.BudgetLastResetDate.Year == 1) userSetting.BudgetLastResetDate = DateTime.Now.Date; // set the initial oder date
                                userSetting.BudgetLastResetDate = DateTime.Now.Date;
                                userSetting.BudgetResetInterval = systemSetting.BudgetRefreshPeriodDefault;
                                userSetting.BudgetNextResetDate = DateTime.Today.AddDays(systemSetting.BudgetRefreshPeriodDefault.Value);
                                _sfDb.UserSettings.Add(userSetting);
                            }

                        }
                        //else if (model == null && userSetting != null && systemSetting.BudgetType == "User Based Budget")
                        //{
                        //    userSetting.BudgetCurrentTotal += totalPrice;
                        //    if (userSetting.BudgetLastResetDate.Year == 1) userSetting.BudgetLastResetDate = DateTime.Now.Date; // set the initial oder date
                        //    _sfDb.Entry(userSetting).State = EntityState.Modified;
                        //}
                    }


                    // Save Ship To Address
                    UserAddress shipTo = _sfDb.UserAddresses.Where(ua => ua.Id == cartItemParam.UserAddressId).FirstOrDefault();

                    if (shipTo != null)
                    {
                        // Any changes to the fields don't save the alias
                        if (!(cartItemParam.Company ?? "").Equals(shipTo.Company ?? "") ||
                            !(cartItemParam.CompanyAlias ?? "").Equals(shipTo.CompanyAlias ?? "") ||
                            !(cartItemParam.FirstName ?? "").Equals(shipTo.FirstName ?? "") ||
                            !(cartItemParam.LastName ?? "").Equals(shipTo.LastName ?? "") ||
                            !(cartItemParam.Address1 ?? "").Equals(shipTo.Address1 ?? "") ||
                            !(cartItemParam.Address2 ?? "").Equals(shipTo.Address2 ?? "") ||
                            !(cartItemParam.City ?? "").Equals(shipTo.City ?? "") ||
                            !(cartItemParam.State ?? "").Equals(shipTo.State ?? "") ||
                            !(cartItemParam.Zip ?? "").Equals(shipTo.Zip ?? "") ||
                            !(cartItemParam.Country ?? "").Equals(shipTo.Country ?? "") ||
                            !(cartItemParam.Phone ?? "").Equals(shipTo.Phone ?? "") ||
                            !(cartItemParam.Email ?? "").Equals(shipTo.Email ?? ""))
                        {
                            cartItemParam.UserAddressId = 0;
                            cartItemParam.AddressAlias = "";
                        }
                    }

                    List<OrderShipTo> newOrderShipToList = new List<OrderShipTo>();
                    OrderShipTo newOrderShipTo = new OrderShipTo()
                    {
                        StoreFrontId = _site.StoreFrontId,
                        UserId = _userSf.Id,
                        ShipToId = cartItemParam.UserAddressId,
                        Alias = cartItemParam.AddressAlias ?? "",
                        Company = cartItemParam.Company ?? "",
                        CompanyAlias = cartItemParam.CompanyAlias ?? "",
                        FirstName = cartItemParam.FirstName ?? "",
                        LastName = cartItemParam.LastName ?? "",
                        Address1 = cartItemParam.Address1 ?? "",
                        Address2 = cartItemParam.Address2 ?? "",
                        City = cartItemParam.City ?? "",
                        State = cartItemParam.State ?? "",
                        Zip = cartItemParam.Zip ?? "",
                        Country = cartItemParam.Country ?? "",
                        Phone = cartItemParam.Phone ?? "",
                        Email = cartItemParam.Email ?? "",
                    };
                    newOrderShipToList.Add(newOrderShipTo);

                    // Changed in default ship to
                    bool newAddressCreated = false;
                    if (cartItemParam.SetAsDefaultShipTo || (cartItemParam.AddressAlias ?? "").Length == 0)
                    {
                        cartItemParam.AddressAlias = cartItemParam.AddressAlias ?? "";

                        if (cartItemParam.AddressAlias.Length > 0)
                        {
                            UserAddress shipToSelected = _sfDb.UserAddresses.Where(ua => ua.AddressAlias == cartItemParam.AddressAlias && ua.StoreFrontId == _site.StoreFrontId && ua.AspNetUserId == _userSf.Id).FirstOrDefault();
                            if (shipToSelected != null)
                            {
                                shipToSelected.DefaultShipTo = cartItemParam.SetAsDefaultShipTo ? 1 : 0;
                                _sfDb.Entry(shipToSelected).State = EntityState.Modified;
                            }
                            else
                                cartItemParam.AddressAlias = "";
                        }

                        if (cartItemParam.AddressAlias.Length == 0)
                        {
                            // any useraddress set as default already?
                            int setMeAsDefault = cartItemParam.SetAsDefaultShipTo ? 1 : 0;
                            if (setMeAsDefault == 1)
                            {
                                List<UserAddress> defaultShipTos = _sfDb.UserAddresses.Where(ua => ua.AddressAlias == cartItemParam.AddressAlias && ua.StoreFrontId == _site.StoreFrontId && ua.AspNetUserId == _userSf.Id && ua.DefaultShipTo == 1).ToList();
                                if (defaultShipTos.Count > 0)
                                {
                                    // clear all default, user want the new shipto as default
                                    foreach (UserAddress defaultShipTo in defaultShipTos)
                                    {
                                        defaultShipTo.DefaultShipTo = 0;
                                        _sfDb.Entry(defaultShipTo).State = EntityState.Modified;
                                    }
                                }
                            }

                            UserAddress newUserAddress = new UserAddress()
                            {
                                StoreFrontId = _site.StoreFrontId,
                                AspNetUserId = _userSf.Id,
                                AddressAlias = cartItemParam.CartId,
                                Company = cartItemParam.Company ?? "",
                                CompanyAlias = cartItemParam.CompanyAlias ?? "",
                                FirstName = cartItemParam.FirstName ?? "",
                                LastName = cartItemParam.LastName ?? "",
                                Address1 = cartItemParam.Address1 ?? "",
                                Address2 = cartItemParam.Address2 ?? "",
                                City = cartItemParam.City ?? "",
                                State = cartItemParam.State ?? "",
                                Zip = cartItemParam.Zip ?? "",
                                Country = cartItemParam.Country ?? "",
                                Phone = cartItemParam.Phone ?? "",
                                Email = cartItemParam.Email ?? "",
                                DefaultShipTo = setMeAsDefault,
                            };
                            _sfDb.UserAddresses.Add(newUserAddress);

                            newAddressCreated = true;
                        }
                    }

                    // Save order note record
                    List<OrderNote> selectedOrderNoteList = new List<OrderNote>();
                    OrderNote selectedOrderNote = new OrderNote()
                    {
                        SFOrderNumber = sfOrderNumber,
                        Note = cartItemParam.CartNote,
                        UserId = _userSf.Id,
                        UserName = _userSf.UserName,
                        DateCreated = DateTime.Now
                    };
                    selectedOrderNoteList.Add(selectedOrderNote);

                    // Check user credit hold
                    bool creditHold = _userSf.OnHold == 1 ? true : false;
                    bool allOrdersOnHold = systemSetting.AllOrdersOnHold == 1 ? true : false;
                    string poNumber = cartItemParam.PONumber;

                    // Save order header values
                    Order selectedOrder = new Order()
                    {
                        StoreFrontId = _site.StoreFrontId,
                        FacilityId = _userSf.FacilityId,
                        SFOrderNumber = sfOrderNumber,
                        UserId = _userSf.Id,
                        UserName = _userSf.UserName,
                        CustomerId = 0,
                        DateCreated = DateTime.Now,
                        OrderStatus = creditHold || allOrdersOnHold ? "OH" : "RP",
                        PONumber = poNumber,
                        ShipType = 0,
                        ShipAccount = "",
                        ShipBillType = 0,
                        OnHold = 0,
                        EditLock = 0,
                        LockTime = null,
                        OrderUrgency = null,
                        FutureReleaseDate = null,
                        Exported = 0,
                        ShipMethodId = cartItemParam.ShipMethodId,
                        CustomShipMessage = "",
                        OrderReference1 = "",
                        OrderReference2 = "",
                        OrderReference3 = "",
                        OrderReference4 = "",
                        OrderDetails = selectedOrderDetailList,
                        OrderShipTos = newOrderShipToList,
                        OrderNotes = selectedOrderNoteList
                    };

                    _sfDb.Orders.Add(selectedOrder);

                    // remove items in cart
                    foreach (var cartItem in cartItems)
                    {
                        _sfDb.Carts.Remove(cartItem);
                    }



                    _sfDb.SaveChanges();

                    // if a new shipto address is created, make sure the ordershipto has the correct useraddress info
                    if (newAddressCreated)
                    {
                        newOrderShipTo.Alias = cartItemParam.CartId;
                        UserAddress newUserAddress = _sfDb.UserAddresses.Where(ua => ua.StoreFrontId == _site.StoreFrontId && ua.AspNetUserId == _userSf.Id && ua.AddressAlias == cartItemParam.CartId).FirstOrDefault();
                        if (newUserAddress != null) newOrderShipTo.ShipToId = newUserAddress.Id;
                        _sfDb.Entry(newOrderShipTo).State = EntityState.Modified;
                        _sfDb.SaveChanges();
                    }

                    // Check system alert
                    SystemSetting sysData = (from ss in _sfDb.SystemSettings
                                             where ss.StoreFrontId == _site.StoreFrontId
                                             select ss).FirstOrDefault();
                    if (sysData != null && sysData.AlertOrderReceived == 1)
                    {
                        List<string> emailSentList = new List<string>();

                        // Also alert users subscribing for this alert

                        //All Orders
                        //For Orders Placed By Me Only	
                        //For Orders Placed By My Group Members Only
                        List<AspNetUser> usersNeedAlert1 = (from u in _sfDb.AspNetUsers
                                                           join us in _sfDb.UserSettings on u.Id equals us.AspNetUserId
                                                           where us.AlertOrderReceived == 1
                                                           && us.StoreFrontId == _site.StoreFrontId
                                                           && u.Id == _userSf.Id
                                                           select u).ToList();
                        List<AspNetUser> usersNeedAlert2 = (from u in _sfDb.AspNetUsers
                                                            join us in _sfDb.UserSettings on u.Id equals us.AspNetUserId
                                                            where us.AlertOrderReceived == 1
                                                            && us.StoreFrontId == _site.StoreFrontId
                                                            && us.AlertOrderReceivedFor == "All Orders"
                                                            && u.Id != _userSf.Id
                                                            select u).ToList();
                        List<AspNetUser> usersNeedAlert3 = (from u in _sfDb.AspNetUsers
                                                            join us in _sfDb.UserSettings on u.Id equals us.AspNetUserId
                                                            join ugu in _sfDb.UserGroupUsers on us.AspNetUserId equals ugu.AspNetUserId
                                                            join ug in _sfDb.UserGroups on ugu.UserGroupId equals ug.Id
                                                            where us.AlertOrderReceived == 1
                                                            && us.StoreFrontId == _site.StoreFrontId
                                                            && ugu.StoreFrontId == _site.StoreFrontId
                                                            && ug.StoreFrontId == _site.StoreFrontId
                                                            && us.AlertOrderReceivedFor == "For Orders Placed By My Group Members Only"
                                                            && u.Id != _userSf.Id
                                                            select u).ToList();

                        string baseUrl = string.Format("{0}://{1}", Request.Url.Scheme, Request.Url.Authority);
                        string strDeepLink = baseUrl + "/Order/OrderDetails/" + selectedOrder.Id.ToString();

                        // create lineitem html
                        string orderDetailHtml = "";
                        orderDetailHtml += "" +
                            "    <tr>" +
                            "        <td width=120 style='width:1.25in;background:lightgoldenrodyellow;padding:.75pt .75pt .75pt .75pt'>" +
                            "            <p class=MsoNormal align=center style='text-align:center'>" +
                            "                <b>" +
                            @"                    <span style='font-size:7.5pt;font-family:""Verdana"",""sans-serif""'>Product Code</span>" +
                            "                </b>" +
                            "            </p>" +
                            "        </td>" +
                            "        <td width=120 style='width:1.25in;background:lightgoldenrodyellow;padding:.75pt .75pt .75pt .75pt'>" +
                            "            <p class=MsoNormal align=center style='text-align:center'>" +
                            "                <b>" +
                            @"                    <span style='font-size:7.5pt;font-family:""Verdana"",""sans-serif""'>Short Description</span>" +
                            "                </b>" +
                            "            </p>" +
                            "        </td>" +
                            "        <td width=120 style='width:1.25in;background:lightgoldenrodyellow;padding:.75pt .75pt .75pt .75pt'>" +
                            "            <p class=MsoNormal align=center style='text-align:center'>" +
                            "                <b>" +
                            @"                    <span style='font-size:7.5pt;font-family:""Verdana"",""sans-serif""'>Qty Ordered</span>" +
                            "                </b>" +
                            "            </p>" +
                            "        </td>" +
                            "    </tr>";

                        foreach (var detail in selectedOrder.OrderDetails)
                        {
                            orderDetailHtml += "" +
                            "    <tr>" +
                            "        <td valign=top style='padding:.75pt .75pt .75pt .75pt;height:15.0pt'> " +
                            "            <p class=MsoNormal align=center style='text-align:center'>" +
                            @"                <span style='font-size:7.5pt;font-family:""Verdana"",""sans-serif""'>" + detail.Product.ProductCode + "</span>" +
                            "            </p>" +
                            "        </td>" +
                            "        <td valign=top style='padding:.75pt .75pt .75pt .75pt;height:15.0pt'>" +
                            "            <p class=MsoNormal align=center style='text-align:center'>" +
                            @"                <span style='font-size:7.5pt;font-family:""Verdana"",""sans-serif""'> " +
                                                detail.Product.ShortDesc +
                            "                </span><o:p></o:p>" +
                            "            </p>" +
                            "        </td>" +
                            "        <td valign=top style='padding:.75pt .75pt .75pt .75pt;height:15.0pt'>" +
                            "            <p class=MsoNormal align=center style='text-align:center'>" +
                            @"                <span style='font-size:7.5pt;font-family:""Verdana"",""sans-serif""'>" + detail.Qty.ToString() + "</span><o:p></o:p>" +
                            "            </p>" +
                            "        </td>" +
                            "    </tr>" +
                            "    <tr>" +
                            "        <td colspan=62 style='background:silver;padding:.75pt .75pt .75pt .75pt'>" +
                            "            <p class=MsoNormal>" +
                            @"                <img width=1 height=1 id=""_x0000_i1025""" +
                            @"                        src="" %20../Images/1pt.gif"">" +
                            "            </p>" +
                            "        </td>" +
                            "    </tr>" +
                            "";
                        }

                        orderDetailHtml = "<table class=MsoNormalTable border=0 cellpadding=0>" + orderDetailHtml + "</table>";

                        OrderShipTo orderShipTo = _sfDb.OrderShipTos.Where(ost => ost.OrderId == selectedOrder.Id).FirstOrDefault();

                        // email the users
                        //Microsoft.AspNet.Identity.IdentityMessage messageObject = new Microsoft.AspNet.Identity.IdentityMessage()
                        //{
                        string Subject = "";
                        string Body = "";
                        //Subject = "Order# " + selectedOrder.Id.ToString() + " has been received - Order No: " + selectedOrder.Id.ToString(),
                        Subject = "Order# " + selectedOrder.Id.ToString() + " has been received - Order No: " + selectedOrder.Id.ToString();
                        Body = "Good News! Your order has been received and will be processed for shipment as soon as possible. <br />" +
                        "<br />" +
                        "Order Details - " +
                        "<br />" +
                        "Order Date: " + (selectedOrder.DateCreated ?? new DateTime(1, 1, 1)).ToString("MM/dd/yyyy") + "<br />" +
                        "Order No: " + selectedOrder.Id.ToString() + "<br />" +
                        "Alt Order No: " + "<br />" +
                        "PO: " + selectedOrder.PONumber + "<br />" +
                        "<br />" +
                        "Ship To:" + "<br />" +
                        //((_userSf.Company ?? "").Length + (_userSf.CompanyAlias ?? "").Length > 0 ? _userSf.Company + " " + _userSf.CompanyAlias : "") + "<br />" +
                        //((_userSf.FirstName ?? "").Length + (_userSf.LastName ?? "").Length > 0 ? _userSf.FirstName + " " + _userSf.LastName : "") + "<br />" +
                        //((_userSf.Address1 ?? "").Length > 0 ? _userSf.Address1 : "") + "<br />" +
                        //((_userSf.Address2 ?? "").Length > 0 ? _userSf.Address2 : "") + "<br />" +
                        //_userSf.City + "," + _userSf.State + " " + _userSf.Zip + " " + _userSf.Country + "<br />" +
                        ((orderShipTo.Company ?? "").Length + (orderShipTo.CompanyAlias ?? "").Length > 0 ? orderShipTo.Company + " " + orderShipTo.CompanyAlias : "") + "<br />" +
                        ((orderShipTo.FirstName ?? "").Length + (orderShipTo.LastName ?? "").Length > 0 ? orderShipTo.FirstName + " " + orderShipTo.LastName : "") + "<br />" +
                        ((orderShipTo.Address1 ?? "").Length > 0 ? orderShipTo.Address1 : "") + "<br />" +
                        ((orderShipTo.Address2 ?? "").Length > 0 ? orderShipTo.Address2 : "") + "<br />" +
                        orderShipTo.City + "," + orderShipTo.State + " " + orderShipTo.Zip + " " + orderShipTo.Country + "<br />" +
                        "<br />" +
                        orderDetailHtml +
                        "<p class=MsoNormal><br>" +
                        "If you have access to our online management portal you may check on the status of you order by clicking the link below:<br>" +
                        "<br>" +
                        "Order No: " + selectedOrder.Id.ToString() + @" - <a href=""" + strDeepLink + @""">CLICK HERE</a><br>" +
                        "<br>" +
                        "**** PLEASE NOTE: THIS IS AN AUTOMATED EMAIL - PLEASE DO NOT REPLY ****</p>";
                        //"**** PLEASE NOTE: THIS IS AN AUTOMATED EMAIL - PLEASE DO NOT REPLY ****</p>",
                        //};

                        //string emailFrom = _sfDb.SystemSettings.Where(ss => ss.StoreFrontId == _site.StoreFrontId).FirstOrDefault().NotifyFromEmail;
                        /*foreach (AspNetUser u in usersNeedAlert)
                        {
                            if ((u.Email ?? "").Length > 0)
                            {
                                messageObject.Destination = u.Email.ToLower();
                                if (!emailSentList.Contains(messageObject.Destination)) await SmtpEmailService.SendAsync(messageObject, emailFrom);
                                emailSentList.Add(messageObject.Destination);
                                await Task.Delay(200);
                            }
                        }*/

                        //var sendMail = new SendMail();
                        //sendMail.From = systemSetting.AlertFromEmail;
                        //sendMail.To = "kenchengpa@gmail.com";
                        //sendMail.CC = "kenchengjap@gmail.com,kenchengwa@gmail.com";
                        //sendMail.Subject = Subject;
                        //sendMail.Body = Body;

                        MailMessage mail = new MailMessage();
                        //mail.To.Add(sendMail.To);
                        //mail.CC.Add(sendMail.CC);
                        mail.From = new MailAddress(systemSetting.AlertFromEmail);
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
                        foreach (AspNetUser u in usersNeedAlert1)
                        {
                            mail.To.Add(u.Email);
                            //smtp.Send(mail);
                            //await Task.Delay(200);
                        }
                        //mail.To.Clear();
                        foreach (AspNetUser u in usersNeedAlert2)
                        {
                            mail.To.Add(u.Email);
                            //smtp.Send(mail);
                            //await Task.Delay(200);
                        }
                        //mail.To.Clear();
                        foreach (AspNetUser u in usersNeedAlert3)
                        {
                            mail.To.Add(u.Email);
                            //smtp.Send(mail);
                            //await Task.Delay(200);
                        }

                        if (mail.To.Count >= 1)
                        {
                            smtp.Send(mail);
                            await Task.Delay(200);
                        }
                    }

                    int orderid = (from o in _sfDb.Orders where o.SFOrderNumber == sfOrderNumber select o).FirstOrDefault().Id;

                    return Json(new { result = "Success", message = "Order created", ordernumber = orderid.ToString() });
                    //return RedirectToAction("ProductList", "Order");
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
                    return Json(new { result = "Error", message = errorMessage });
                }
            }
            catch (Exception ex)
            {
                //return View("Error", new HandleErrorInfo(ex, "Order", "CartDisplay"));
                string errorMessage = "";
                /*foreach (Exception exception in ex.GetInnerExceptions())
                {
                    errorMessage += exception.Message + "\r\n";
                }*/
                return Json(new { result = "Error", message = errorMessage });
            }
        }

        [TokenAuthorize]
        [HttpPost]
        public async Task<ActionResult> Cart_SubmitPunchOutOrderAsync(CartViewModel cartItemParam)
        {
            try
            {

                if (ModelState.IsValid)
                {
                    string cartId = cartItemParam.CartId;
                    string buyerCookie = "BuyerCookie";
                    List<Cart> cartItems = (from c in _sfDb.Carts
                                            where c.CartId == cartId
                                            select c).ToList();

                    // Get next order number
                    var sfOrderNumberQuery = from o in _sfDb.Orders
                                             where o.StoreFrontId == _site.StoreFrontId
                                             select o;
                    int sfOrderNumber = 0;
                    if (sfOrderNumberQuery.Count() > 0)
                    {
                        sfOrderNumber = sfOrderNumberQuery.Max(x => x.SFOrderNumber) + 1;
                    }
                    else
                    {
                        sfOrderNumber = 1;
                    }

                    // Get User settings
                    // Get punchOutBrowserFormPost from User Setting
                    string BASWARE_REST_URL = "";
                    string defaultCurrency = "USD";
                    UserSetting selectedUserSetting = _sfDb.UserSettings.Where(us => us.StoreFrontId == _site.StoreFrontId && us.AspNetUserId == _userSf.Id).FirstOrDefault();
                    if (selectedUserSetting != null)
                    {
                        BASWARE_REST_URL = selectedUserSetting.PunchOutBrowserPostUrl;
                        defaultCurrency = selectedUserSetting.DefaultCurrency;
                    }

                    if (_site.CurrencyFlag == 1) defaultCurrency = "USD"; else defaultCurrency = "CAD";

                    // Start with empty XElement
                    string timeStamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssK");

                    XElement header =
                        new XElement("Header",
                            new XElement("From",
                                new XElement("Credential", new XAttribute("domain", "NETWORKID"),
                                    new XElement("Identity", "Basware" + ((_site.CurrencyFlag == 1) ? "-US" : "-CA")))),
                            new XElement("To",
                                new XElement("Credential", new XAttribute("domain", "DUNS"),
                                    new XElement("Identity", "876897831"))),
                            new XElement("Sender",
                                new XElement("Credential", new XAttribute("domain", "NETWORKID"),
                                    new XElement("Identity", "Basware"),
                                    new XElement("SharedSecret", "eef7e4aa-430a-4570-bb5d-8691a88fa02d")),
                                new XElement("UserAgent", "Verian"))
                        );

                    // Save order detail values as ItemIn element
                    bool noerror = true;
                    string errorMessage = "";
                    XElement totalAmount = new XElement("empty", "empty");
                    List<XElement> allItemIn = new List<XElement>();
                    XElement itemIn = new XElement("empty", "empty");
                    decimal totalPrice = 0;
                    List<OrderDetail> selectedOrderDetailList = new List<OrderDetail>();
                    foreach (var c in cartItems)
                    {
                        Product selectedProduct = _sfDb.Products.Where(p => p.Id == c.ProductId).FirstOrDefault();
                        int? vendorId = _sfDb.VendorProducts.Where(vp => vp.ProductId == c.ProductId).Select(vp => vp.VendorId).FirstOrDefault();

                        OrderDetail selectedOrderDetail = new OrderDetail()
                        {
                            SFOrderNumber = sfOrderNumber,
                            ProductId = c.ProductId,
                            Qty = c.Count,
                            Price = c.Price,
                            Status = "",
                            ShipWithItem = 0,
                            IsFulfilledByVendor = selectedProduct.IsFulfilledByVendor,
                            VendorId = vendorId ?? 0,
                            UserId = _userSf.Id,
                            UserName = _userSf.UserName,
                            DateCreated = DateTime.Now,
                            Product = selectedProduct,
                        };
                        totalPrice += c.Count * c.Price;
                        selectedOrderDetailList.Add(selectedOrderDetail);
                        selectedOrderDetail = null;

                        itemIn = new XElement("ItemIn", new XAttribute("quantity", c.Count.ToString()),
                                                            new XElement("ItemID",
                                                                new XElement("SupplierPartID", c.ProductId)),
                                                            new XElement("ItemDetail",
                                                                new XElement("UnitPrice",
                                                                    new XElement("Money", new XAttribute("currency", defaultCurrency), String.Format("{0:0.00}", c.Price))),
                                                                new XElement("Description", new XAttribute(XNamespace.Xmlns + "lang", "en-US"), selectedProduct.ShortDesc),
                                                                new XElement("UnitOfMeasure", "EA"),
                                                                new XElement("Classification", new XAttribute("domain", ""), ""),
                                                                new XElement("Extrinsic", new XAttribute("name", buyerCookie), cartId)
                                                                ));
                        allItemIn.Add(itemIn);
                    }
                    totalAmount = new XElement("Money", new XAttribute("currency", defaultCurrency), String.Format("{0:0.00}", totalPrice));


                    // Troubleshooting : need to send the cart info to the main user? With the main user currency setup?
                    if (_site.CurrencyFlag == 1)
                        cartId = cartItemParam.CartId;
                    else
                        cartId = cartItemParam.CartId.Replace("1316725", "1317854");

                    XElement responseRoot = new XElement("empty", "empty");
                    try
                    {
                        XElement message =
                            new XElement("Message",
                                new XElement("PunchOutOrderMessage",
                                    new XElement("BuyerCookie", cartId),
                                    new XElement("PunchOutOrderMessageHeader", new XAttribute("operationAllowed", "edit"),
                                        new XElement("Total", totalAmount)),
                                    allItemIn
                                )
                            );

                        responseRoot =
                            new XElement("cXML",
                                new XAttribute("timestamp", timeStamp),
                                new XAttribute("payloadID", timeStamp + "-SfDyn_PunchOut"),
                                    header,
                                    message
                            );
                    }
                    catch (Exception ex)
                    {
                        noerror = false;
                        errorMessage = ex.Message;
                    }

                    if (noerror)
                    {
                        // Send the PunchOut Order
                        StringWriter writer;
                        XDocument responseDocument = new XDocument(
                                                            new XDeclaration("1.0", "utf-8", "true"),
                                                            new XDocumentType("cXML", null, "http://xml.cxml.org/schemas/cXML/1.1.010/cXML.dtd", null));
                        responseDocument.Add(responseRoot);

                        writer = new Utf8StringWriter();
                        responseDocument.Save(writer, SaveOptions.None);
                        string xmlBody = writer.ToString();

                        // Setup client
                        RestClient client = new RestClient();
                        client.BaseUrl = new Uri(BASWARE_REST_URL);

                        // setup request
                        RestRequest punchOutResponse = new RestRequest();
                        punchOutResponse.Resource = BASWARE_REST_URL;
                        punchOutResponse.Method = Method.POST;
                        punchOutResponse.AddHeader("Content-Type", "text/xml");
                        //punchOutResponse.AddXmlBody(writer.ToString());
                        punchOutResponse.AddParameter("text/xml", xmlBody, ParameterType.RequestBody);

                        //GlobalFunctions.Log(_site.StoreFrontId, _userSf.Id, "Cart_SubmitPunchOutOrderAsync", "Xmldata", writer.ToString());
                        GlobalFunctions.Log(_site.StoreFrontId, _userSf.Id, "Cart_SubmitPunchOutOrderAsync", cartId, xmlBody);

                        try
                        {
                            IRestResponse response = client.Execute(punchOutResponse);

                            if (response.ErrorException != null)
                            {
                                GlobalFunctions.Log(_site.StoreFrontId, _userSf.Id, "Error Cart_SubmitPunchOutOrderAsync Response", "- Error in punchout response : " + response.ErrorMessage, xmlBody);
                            }
                            else if (response.StatusCode != HttpStatusCode.OK)
                            {
                                GlobalFunctions.Log(_site.StoreFrontId, _userSf.Id, "Error Cart_SubmitPunchOutOrderAsync Response", "- " + response.StatusCode.ToString() + " " + response.Content.ToString(), xmlBody);
                            }
                            else
                            {
                                // Check if there are text in the 2 possible sections of html
                                HtmlDocument htmlDoc = new HtmlDocument();
                                htmlDoc.LoadHtml(response.Content);
                                var pageContentFrame = htmlDoc.DocumentNode.SelectSingleNode("//div[@id='PageContentFrame']");
                                var errMsgDiv = htmlDoc.DocumentNode.SelectSingleNode("//div[@id='errMsgDiv']");
                                var errorMsgH3 = pageContentFrame.Descendants("h3").FirstOrDefault();
                                string errorMsgPageContent = "";

                                if (errorMsgH3 != null)
                                {
                                    errorMsgPageContent = errorMsgH3.WriteTo();
                                    GlobalFunctions.Log(_site.StoreFrontId, _userSf.Id, "Error Cart_SubmitPunchOutOrderAsync Response", "Error : " + errorMsgPageContent, response.Content);
                                    return Json(new { result = "Error", message = "Punchout server error : " + errorMsgPageContent });
                                }

                                if (pageContentFrame.InnerText.ToLower().Contains("error"))
                                {
                                    errorMsgPageContent = pageContentFrame.InnerText.Replace(Environment.NewLine, "").Trim(); //  String.Concat(pageContentFrame.InnerText.Where(c => !Char.IsWhiteSpace(c)));
                                    errorMsgPageContent = errorMsgPageContent.Replace("Received Shopping Cart Transmission", "");
                                    errorMsgPageContent = errorMsgPageContent.Trim();
                                    GlobalFunctions.Log(_site.StoreFrontId, _userSf.Id, "Error Cart_SubmitPunchOutOrderAsync Response", "Error : " + errorMsgPageContent, response.Content);
                                    return Json(new { result = "Error", message = "Punchout server error : " + errorMsgPageContent });
                                }

                                string errorMsgDiv = errMsgDiv.WriteTo();
                                if (errorMsgDiv.ToLower().Contains("error"))
                                {
                                    GlobalFunctions.Log(_site.StoreFrontId, _userSf.Id, "Error Cart_SubmitPunchOutOrderAsync Response", "Error : " + errorMsgDiv, response.Content);
                                    return Json(new { result = "Error", message = "Punchout server error : " + errorMsgDiv });
                                }

                                GlobalFunctions.Log(_site.StoreFrontId, _userSf.Id, "Cart_SubmitPunchOutOrderAsync Response", cartId, response.Content);

                                //  var cartId = cartItemParam.CartId;

                                //   List<Cart> cartItems = (from c in _sfDb.Carts
                                //       where c.CartId == cartId
                                //       select c).ToList();

                                // Get next order number
                                //  var sfOrderNumberQuery = from o in _sfDb.Orders
                                //                 where o.StoreFrontId == _site.StoreFrontId
                                //              select o;
                                // int sfOrderNumber = 0;
                                if (sfOrderNumberQuery.Count() > 0)
                                {
                                    sfOrderNumber = sfOrderNumberQuery.Max(x => x.SFOrderNumber) + 1;
                                }
                                else
                                {
                                    sfOrderNumber = 1;
                                }

                                // Save order detail values
                                //   decimal totalPrice = 0;
                                //   List<OrderDetail> selectedOrderDetailList = new List<OrderDetail>();
                                selectedOrderDetailList.Clear();
                                foreach (var c in cartItems)
                                {
                                    Product selectedProduct = _sfDb.Products.Where(p => p.Id == c.ProductId).FirstOrDefault();
                                    int? vendorId = _sfDb.VendorProducts.Where(vp => vp.ProductId == c.ProductId).Select(vp => vp.VendorId).FirstOrDefault();

                                    OrderDetail selectedOrderDetail = new OrderDetail()
                                    {
                                        SFOrderNumber = sfOrderNumber,
                                        ProductId = c.ProductId,
                                        Qty = c.Count,
                                        Price = c.Price,
                                        Status = "",
                                        ShipWithItem = 0,
                                        IsFulfilledByVendor = selectedProduct.IsFulfilledByVendor,
                                        VendorId = vendorId ?? 0,
                                        UserId = _userSf.Id,
                                        Currency = defaultCurrency,
                                        UserName = _userSf.UserName,
                                        DateCreated = DateTime.Now,
                                        Product = selectedProduct,
                                    };
                                    totalPrice += c.Count * c.Price;
                                    selectedOrderDetailList.Add(selectedOrderDetail);
                                    selectedOrderDetail = null;
                                }

                                // Save Ship To Address
                                UserAddress shipTo = _sfDb.UserAddresses.Where(ua => ua.Id == cartItemParam.UserAddressId).FirstOrDefault();

                                if (shipTo != null)
                                {
                                    // Any changes to the fields don't save the alias
                                    if (!(cartItemParam.Company ?? "").Equals(shipTo.Company ?? "") ||
                                        !(cartItemParam.CompanyAlias ?? "").Equals(shipTo.CompanyAlias ?? "") ||
                                        !(cartItemParam.FirstName ?? "").Equals(shipTo.FirstName ?? "") ||
                                        !(cartItemParam.LastName ?? "").Equals(shipTo.LastName ?? "") ||
                                        !(cartItemParam.Address1 ?? "").Equals(shipTo.Address1 ?? "") ||
                                        !(cartItemParam.Address2 ?? "").Equals(shipTo.Address2 ?? "") ||
                                        !(cartItemParam.City ?? "").Equals(shipTo.City ?? "") ||
                                        !(cartItemParam.State ?? "").Equals(shipTo.State ?? "") ||
                                        !(cartItemParam.Zip ?? "").Equals(shipTo.Zip ?? "") ||
                                        !(cartItemParam.Country ?? "").Equals(shipTo.Country ?? "") ||
                                        !(cartItemParam.Phone ?? "").Equals(shipTo.Phone ?? "") ||
                                        !(cartItemParam.Email ?? "").Equals(shipTo.Email ?? ""))
                                    {
                                        cartItemParam.UserAddressId = 0;
                                        cartItemParam.AddressAlias = "";
                                    }
                                }

                                List<OrderShipTo> newOrderShipToList = new List<OrderShipTo>();
                                OrderShipTo newOrderShipTo = new OrderShipTo()
                                {
                                    StoreFrontId = _site.StoreFrontId,
                                    UserId = _userSf.Id,
                                    ShipToId = cartItemParam.UserAddressId,
                                    Alias = cartItemParam.AddressAlias ?? "",
                                    Company = cartItemParam.Company ?? "",
                                    CompanyAlias = cartItemParam.CompanyAlias ?? "",
                                    FirstName = cartItemParam.FirstName ?? "",
                                    LastName = cartItemParam.LastName ?? "",
                                    Address1 = cartItemParam.Address1 ?? "",
                                    Address2 = cartItemParam.Address2 ?? "",
                                    City = cartItemParam.City ?? "",
                                    State = cartItemParam.State ?? "",
                                    Zip = cartItemParam.Zip ?? "",
                                    Country = cartItemParam.Country ?? "",
                                    Phone = cartItemParam.Phone ?? "",
                                    Email = cartItemParam.Email ?? "",
                                };
                                newOrderShipToList.Add(newOrderShipTo);

                                // Changed in default ship to
                                bool newAddressCreated = false;
                                if (cartItemParam.SetAsDefaultShipTo || (cartItemParam.AddressAlias ?? "").Length == 0)
                                {
                                    cartItemParam.AddressAlias = cartItemParam.AddressAlias ?? "";

                                    if (cartItemParam.AddressAlias.Length > 0)
                                    {
                                        UserAddress shipToSelected = _sfDb.UserAddresses.Where(ua => ua.AddressAlias == cartItemParam.AddressAlias && ua.StoreFrontId == _site.StoreFrontId && ua.AspNetUserId == _userSf.Id).FirstOrDefault();
                                        if (shipToSelected != null)
                                        {
                                            shipToSelected.DefaultShipTo = cartItemParam.SetAsDefaultShipTo ? 1 : 0;
                                            _sfDb.Entry(shipToSelected).State = EntityState.Modified;
                                        }
                                        else
                                            cartItemParam.AddressAlias = "";
                                    }

                                    if (cartItemParam.AddressAlias.Length == 0)
                                    {
                                        // any useraddress set as default already?
                                        int setMeAsDefault = cartItemParam.SetAsDefaultShipTo ? 1 : 0;
                                        if (setMeAsDefault == 1)
                                        {
                                            List<UserAddress> defaultShipTos = _sfDb.UserAddresses.Where(ua => ua.AddressAlias == cartItemParam.AddressAlias && ua.StoreFrontId == _site.StoreFrontId && ua.AspNetUserId == _userSf.Id && ua.DefaultShipTo == 1).ToList();
                                            if (defaultShipTos.Count > 0)
                                            {
                                                // clear all default, user want the new shipto as default
                                                foreach (UserAddress defaultShipTo in defaultShipTos)
                                                {
                                                    defaultShipTo.DefaultShipTo = 0;
                                                    _sfDb.Entry(defaultShipTo).State = EntityState.Modified;
                                                }
                                            }
                                        }

                                        UserAddress newUserAddress = new UserAddress()
                                        {
                                            StoreFrontId = _site.StoreFrontId,
                                            AspNetUserId = _userSf.Id,
                                            AddressAlias = cartItemParam.CartId,
                                            Company = cartItemParam.Company ?? "",
                                            CompanyAlias = cartItemParam.CompanyAlias ?? "",
                                            FirstName = cartItemParam.FirstName ?? "",
                                            LastName = cartItemParam.LastName ?? "",
                                            Address1 = cartItemParam.Address1 ?? "",
                                            Address2 = cartItemParam.Address2 ?? "",
                                            City = cartItemParam.City ?? "",
                                            State = cartItemParam.State ?? "",
                                            Zip = cartItemParam.Zip ?? "",
                                            Country = cartItemParam.Country ?? "",
                                            Phone = cartItemParam.Phone ?? "",
                                            Email = cartItemParam.Email ?? "",
                                            DefaultShipTo = setMeAsDefault,
                                        };
                                        _sfDb.UserAddresses.Add(newUserAddress);

                                        newAddressCreated = true;
                                    }
                                }

                                // Save order note record
                                List<OrderNote> selectedOrderNoteList = new List<OrderNote>();
                                OrderNote selectedOrderNote = new OrderNote()
                                {
                                    SFOrderNumber = sfOrderNumber,
                                    Note = cartItemParam.CartNote,
                                    UserId = _userSf.Id,
                                    UserName = _userSf.UserName,
                                    DateCreated = DateTime.Now
                                };
                                selectedOrderNoteList.Add(selectedOrderNote);
                                SystemSetting systemSetting = _sfDb.SystemSettings.Where(ss => ss.StoreFrontId == _site.StoreFrontId).FirstOrDefault();
                                // Check user credit hold
                                bool creditHold = _userSf.OnHold == 1 ? true : false;
                                bool allOrdersOnHold = systemSetting.AllOrdersOnHold == 1 ? true : false;
                                string poNumber = cartItemParam.PONumber;

                                // Save order header values
                                Order selectedOrder = new Order()
                                {
                                    StoreFrontId = _site.StoreFrontId,
                                    FacilityId = _userSf.FacilityId,
                                    SFOrderNumber = sfOrderNumber,
                                    UserId = _userSf.Id,
                                    UserName = _userSf.UserName,
                                    CustomerId = 0,
                                    DateCreated = DateTime.Now,
                                    OrderStatus = "OH",
                                    PONumber = poNumber,
                                    ShipType = 0,
                                    ShipAccount = "",
                                    ShipBillType = 0,
                                    OnHold = 0,
                                    EditLock = 0,
                                    LockTime = null,
                                    OrderUrgency = null,
                                    FutureReleaseDate = null,
                                    Exported = 0,
                                    ShipMethodId = 95,
                                    CustomShipMessage = "",
                                    OrderReference1 = cartId,
                                    OrderReference2 = "",
                                    OrderReference3 = "",
                                    OrderReference4 = "",
                                    OrderDetails = selectedOrderDetailList,
                                    OrderShipTos = newOrderShipToList,
                                    OrderNotes = selectedOrderNoteList
                                };

                                _sfDb.Orders.Add(selectedOrder);

                                // Clear cart
                                // remove items in cart
                                foreach (var cartItem in cartItems)
                                {
                                    _sfDb.Carts.Remove(cartItem);
                                }
                                _sfDb.SaveChanges();

                                return Json(new { result = "Success", message = "Punchout order sent", ordernumber = "" });
                            }
                        }
                        catch (Exception error)
                        {
                            GlobalFunctions.Log(_site.StoreFrontId, _userSf.Id, "Error Cart_SubmitPunchOutOrderAsync response", "Error : " + error.Message, null);
                        }
                    }
                    else
                    {
                        return Json(new { result = "Error", message = "Cannot Respond for Punchout" + errorMessage });
                    }
                    return Json(new { result = "Error", message = "Cannot Respond for Punchout" + errorMessage });
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
                    return Json(new { result = "Error", message = errorMessage });
                }
            }
            catch (Exception ex)
            {
                //return View("Error", new HandleErrorInfo(ex, "Order", "CartDisplay"));
                string errorMessage = "";
                /*foreach (Exception exception in ex.GetInnerExceptions())
                {
                    errorMessage += exception.Message + "\r\n";
                }*/
                return Json(new { result = "Error", message = errorMessage });
            }
        }

        [TokenAuthorize]
        public ActionResult Cart_CheckoutProcess(string cartId)
        {
            List<CartViewModel> cartItemList = (from c in _sfDb.Carts
                                                join p in _sfDb.Products on c.ProductId equals p.Id
                                                join pi in _sfDb.ProductImages on p.Id equals pi.ProductId into subpi
                                                from pi in subpi.DefaultIfEmpty()
                                                where c.StoreFrontId == _site.StoreFrontId &&
                                                      c.UserId == _userSf.Id &&
                                                      c.CartId == cartId &&
                                                      p.Status == 1 && p.StoreFrontId == _site.StoreFrontId
                                                orderby c.Id
                                                select new CartViewModel()
                                                {
                                                    Id = c.Id,
                                                    CartId = c.CartId,
                                                    ProductId = c.ProductId,
                                                    PickPackCode = p.PickPackCode,
                                                    ShortDesc = p.ShortDesc,
                                                    Count = c.Count,
                                                    SellPrice = c.Price,
                                                    SellPriceCAD = c.PriceCAD,
                                                    IsFulfilledByVendor = c.IsFulfilledByVendor == 1,
                                                    VendorId = c.VendorId,
                                                    DateCreated = c.DateCreated,
                                                    UserId = c.UserId,
                                                    StoreFrontId = c.StoreFrontId,
                                                    ImageRelativePath = pi.RelativePath ?? "Content/" + _site.StoreFrontName + "/Images/default.png",
                                                    DisplayOrder = pi.DisplayOrder ?? 0
                                                }).ToList();

            List<ShipMethod> allowedShipMethods = new List<ShipMethod>();
            if (_site.SiteAuth.OrderRestrictShipMethod == 0)
            {
                allowedShipMethods = (from sm in _sfDb.ShipMethods
                                      join sc in _sfDb.ShipCarriers on sm.CarrierId equals sc.Id
                                      where sm.Enabled == 1 && sc.StoreFrontId == _site.StoreFrontId
                                      select sm).ToList();
            }
            else
            {
                allowedShipMethods = (from usm in _sfDb.UserShipMethods
                                      join sm in _sfDb.ShipMethods on usm.ShipMethodId equals sm.Id
                                      join sc in _sfDb.ShipCarriers on sm.CarrierId equals sc.Id
                                      where sm.Enabled == 1 && usm.AspNetUserId == _userSf.Id && sc.StoreFrontId == _site.StoreFrontId
                                      select sm).ToList();
            }

            ViewBag.AllowedShipMethods = new SelectList(allowedShipMethods, "Id", "MethodName");
            ViewBag.Countries = new SelectList(_sfDb.Countries.ToList(), "Code", "Name");

            UserAddress defaultShipTo = _sfDb.UserAddresses.Where(ua => ua.StoreFrontId == _site.StoreFrontId && ua.AspNetUserId == _userSf.Id && ua.DefaultShipTo == 1).FirstOrDefault();
            if (defaultShipTo != null)
            {
                foreach (CartViewModel c in cartItemList)
                {
                    c.UserAddressId = defaultShipTo.Id;
                    c.AddressAlias = defaultShipTo.AddressAlias;
                    c.Company = defaultShipTo.Company;
                    c.CompanyAlias = defaultShipTo.CompanyAlias;
                    c.FirstName = defaultShipTo.FirstName;
                    c.LastName = defaultShipTo.LastName;
                    c.Address1 = defaultShipTo.Address1;
                    c.Address2 = defaultShipTo.Address2;
                    c.City = defaultShipTo.City;
                    c.State = defaultShipTo.State;
                    c.Zip = defaultShipTo.Zip;
                    c.Country = defaultShipTo.Country;
                    c.Email = defaultShipTo.Email;
                    c.Phone = defaultShipTo.Phone;
                    c.SetAsDefaultShipTo = true;
                    break;
                }
            }
            else
            {
                // There's no default ship to setup, use the last used address
                List<OrderShipTo> lastShipTos = _sfDb.OrderShipTos.Where(ost => ost.StoreFrontId == _site.StoreFrontId && ost.UserId == _userSf.Id).OrderByDescending(ost => ost.DateCreated).Take(1).ToList();
                if (lastShipTos.Count > 0)
                {
                    foreach (CartViewModel c in cartItemList)
                    {
                        c.UserAddressId = lastShipTos[0].ShipToId;
                        c.AddressAlias = lastShipTos[0].Alias;
                        c.Company = lastShipTos[0].Company;
                        c.CompanyAlias = lastShipTos[0].CompanyAlias;
                        c.FirstName = lastShipTos[0].FirstName;
                        c.LastName = lastShipTos[0].LastName;
                        c.Address1 = lastShipTos[0].Address1;
                        c.Address2 = lastShipTos[0].Address2;
                        c.City = lastShipTos[0].City;
                        c.State = lastShipTos[0].State;
                        c.Zip = lastShipTos[0].Zip;
                        c.Country = lastShipTos[0].Country;
                        c.Email = lastShipTos[0].Email;
                        c.Phone = lastShipTos[0].Phone;
                        break;
                    }
                }
                // Otherwise, use the profile info
                else
                {
                    foreach (CartViewModel c in cartItemList)
                    {
                        c.UserAddressId = 0;
                        c.AddressAlias = "";
                        c.Company = _userSf.Company;
                        c.CompanyAlias = _userSf.CompanyAlias;
                        c.FirstName = _userSf.FirstName;
                        c.LastName = _userSf.LastName;
                        c.Address1 = _userSf.Address1;
                        c.Address2 = _userSf.Address2;
                        c.City = _userSf.City;
                        c.State = _userSf.State;
                        c.Zip = _userSf.Zip;
                        c.Country = _userSf.Country;
                        c.Email = _userSf.Email;
                        c.Phone = _userSf.Phone;
                        break;
                    }
                }
            }


            if (cartItemList.Count > 0 && (cartItemList[0].Country ?? "").Length == 0) cartItemList[0].Country = "US";

            return View(cartItemList);
        }

        // GET: Manage/GetCountries
        [TokenAuthorize]
        public ActionResult GetLastTenShipTos_Read([DataSourceRequest] DataSourceRequest request)
        {
            List<UserAddressViewModel> lastShipTos = _sfDb.OrderShipTos.Where(ost => ost.StoreFrontId == _site.StoreFrontId && ost.UserId == _userSf.Id)
                .OrderByDescending(ost => ost.DateCreated)
                .Take(10)
                .Select(ost => new UserAddressViewModel()
                {
                    StoreFrontId = _site.StoreFrontId,
                    AspNetUserId = _userSf.Id,
                    Id = ost.ShipToId,
                    AddressAlias = ost.Alias,
                    Company = ost.Company ?? "",
                    CompanyAlias = ost.CompanyAlias ?? "",
                    FirstName = ost.FirstName ?? "",
                    LastName = ost.LastName ?? "",
                    Address1 = ost.Address1 ?? "",
                    Address2 = ost.Address2 ?? "",
                    City = ost.City ?? "",
                    State = ost.State ?? "",
                    Zip = ost.Zip ?? "",
                    Country = ost.Country ?? "",
                    Phone = ost.Phone ?? "",
                    Email = ost.Email ?? "",
                })
                .ToList();

            foreach (UserAddressViewModel uavm in lastShipTos)
            {
                UserAddress prevShipTo = _sfDb.UserAddresses.Where(ua => ua.Id == uavm.Id).FirstOrDefault();
                if (prevShipTo != null)
                {
                    uavm.DefaultShipTo = prevShipTo.DefaultShipTo == 1;
                }
            }

            DataSourceResult modelList = lastShipTos.ToDataSourceResult(request);

            return Json(modelList, JsonRequestBehavior.AllowGet);
        }

        // (Not used currently)
        public List<Product> GetAllProducts()
        {
            return _sfDb.Products.ToList();
        }

        public string GetProductDefaultImage(int id)
        {
            string imagePath;
            imagePath = _sfDb.ProductImages.Where(pi => pi.ProductId == id).Select(path => path.RelativePath).FirstOrDefault();
            imagePath = imagePath.Replace("/Content", "Content"); // this bandaid change to remove the double slashes in the home page
            return imagePath;
        }

        public List<Category> GetCategoriesForProduct(int productId)
        {
            try
            {
                Category category = new Category();

                var retList = (from c in _sfDb.Categories
                               join pc in _sfDb.ProductCategories on c.Id equals pc.CategoryId
                               where pc.ProductId == productId
                               orderby c.Name
                               select c).ToList();

                return retList;
            }
            catch
            {
                return new List<Category>();
            }

        }

        public List<Product> GetProductsSpecials()
        {
            var Products = (from product in _sfDb.Products select product).Where(p => p.IsSpecial == true).Take(3).ToList();
            return Products;
        }

        protected override void Dispose(bool disposing)
        {
            _sfDb.Dispose();
            base.Dispose(disposing);
        }
    }

}

