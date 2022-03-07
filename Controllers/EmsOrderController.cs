using Microsoft.AspNet.Identity;
using StoreFront2.Data;
using StoreFront2.Helpers;
using StoreFront2.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Net.Configuration;
using System.Net.Http;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Results;

namespace StoreFront2.Controllers
{
    [Authorize]
    public class EmsOrderController : ApiController
    {

        private StoreFront2Entities _sfDb = new StoreFront2Entities();

        // GET api/<controller>/5
        [Authorize]
        [HttpGet]
        [Route("api/EmsOrderAll/{storeFrontId}")]
        public List<EmsOrderExportModel> Get(int storeFrontId)
        {
            // 8/3/21 : PS and PH are obsolete 
            List<EmsOrderExportModel> orderHeader = (from o in _sfDb.Orders
                                                     join u in _sfDb.AspNetUsers on o.UserId equals u.Id
                                                     where o.StoreFrontId == storeFrontId && o.OnHold == 0 && "RP,IP".Contains(o.OrderStatus)
                                                     select new EmsOrderExportModel()
                                                     {
                                                         Id = o.Id,
                                                         StoreFrontOrder = 1,
                                                         UserId = u.SfId,
                                                         CustomerId = o.CustomerId,
                                                         DateCreated = o.DateCreated,
                                                         OrderStatus = o.OrderStatus,
                                                         OrderUrgency = o.OrderUrgency,
                                                         PONumber = o.PONumber,
                                                         ShipAccount = o.ShipAccount,
                                                         ShipBillType = o.ShipBillType,
                                                         OnHold = o.OnHold,
                                                         FutureReleaseDate = o.FutureReleaseDate,
                                                         Exported = o.Exported,
                                                         CustomShipMessage = o.CustomShipMessage,
                                                         OrderReference1 = o.OrderReference1,
                                                         OrderReference2 = o.OrderReference2,
                                                         OrderReference3 = o.OrderReference3,
                                                         OrderReference4 = o.OrderReference4,
                                                         ShipMethod = (from sm in _sfDb.ShipMethods where sm.Id == o.ShipMethodId select sm).FirstOrDefault(),
                                                         //OrderShipTos = (from ost in _sfDb.OrderShipTos where ost.OrderId == o.Id select ost).ToList(),
                                                     }).ToList();

            foreach (var order in orderHeader)
            {
                // Get customer info
                var orderCustomer = (from u in _sfDb.AspNetUsers
                                     where u.SfId == order.UserId
                                     select new
                                     {
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
                                         Email = u.Email,

                                     }).FirstOrDefault();

                // Get order details
                List<EmsOrderDetailExportModel> orderDetail = (from od in _sfDb.OrderDetails
                                                               join p in _sfDb.Products on od.ProductId equals p.Id
                                                               where od.OrderId == order.Id && od.Qty > 0
                                                               select new EmsOrderDetailExportModel()
                                                               {
                                                                   OrderDetailId = od.Id,
                                                                   OrderNumber = order.Id,
                                                                   ProductId = p.EMSProductId,
                                                                   ProductCode = p.ProductCode,
                                                                   PickPackCode = p.PickPackCode,
                                                                   Upc = p.Upc,
                                                                   ShipWithItem = od.ShipWithItem,
                                                                   TotalQtyOrdered = od.Qty,
                                                                   ShortDesc = p.ShortDesc,
                                                                   LongDesc = p.LongDesc,
                                                                   UserName = od.UserName,
                                                                   DateCreated = od.DateCreated,
                                                               }).ToList();

                order.OrderDetails = orderDetail;
                order.Customer = new AspNetUser()
                {
                    Company = orderCustomer.Company,
                    CompanyAlias = orderCustomer.CompanyAlias,
                    FirstName = orderCustomer.FirstName,
                    LastName = orderCustomer.LastName,
                    Address1 = orderCustomer.Address1,
                    Address2 = orderCustomer.Address2,
                    City = orderCustomer.City,
                    State = orderCustomer.State,
                    Zip = orderCustomer.Zip,
                    Country = orderCustomer.Country,
                    Phone = orderCustomer.Phone,
                    Email = orderCustomer.Email
                };

                order.OrderShipTo = _sfDb.OrderShipTos.Where(ost => ost.OrderId == order.Id).Select(ost => new EmsOrderShipToExportModel()
                {
                    Alias = ost.Alias,
                    Company = ost.Company,
                    CompanyAlias = ost.CompanyAlias,
                    FirstName = ost.FirstName,
                    LastName = ost.LastName,
                    Address1 = ost.Address1,
                    Address2 = ost.Address2,
                    City = ost.City,
                    State = ost.State,
                    Zip = ost.Zip,
                    Country = ost.Country,
                    Phone = ost.Phone,
                    Email = ost.Email,
                }).FirstOrDefault();

                OrderNote orderNote = _sfDb.OrderNotes.Where(on => on.OrderId == order.Id).FirstOrDefault();
                if (orderNote != null)
                {
                    order.CustomShipMessage = orderNote.Note;
                }

            }

            return orderHeader;
        }

        // POST api/<controller>
        [Route("api/EMSUpdateStoreFront/{storeFrontId}")]
        [HttpPost]
        public async Task<IHttpActionResult> EMSUpdateStoreFront([FromBody] EmsTrackingUpload orderTrackings, int storeFrontId)
        {
            //int updatedCount = 0;

            if (orderTrackings != null)
            {
                int orderId = Convert.ToInt32(orderTrackings.OrderId);

                AspNetUser adminUser = _sfDb.AspNetUsers.Where(u => u.UserName.ToLower() == "c2devgroup").FirstOrDefault();
                SystemSetting systemSetting = _sfDb.SystemSettings.Where(ss => ss.StoreFrontId == storeFrontId).FirstOrDefault();
                Order selectedOrder = (from o in _sfDb.Orders where o.Id == orderId select o).FirstOrDefault();

                bool existsTrackingNumber = false;
                foreach (EmsTracking trackingNumber in orderTrackings.TrackingNumbers)
                {
                    OrderTracking chkorderTrackings = (from t in _sfDb.OrderTrackings where t.TrackingNumber == trackingNumber.TrackingNumber select t).FirstOrDefault();
                    if (chkorderTrackings != null)
                    {
                        existsTrackingNumber = true;
                    }
                }

                if ((selectedOrder != null) && (existsTrackingNumber == false))
                {
                    List<OrderDetail> orderDetails = _sfDb.OrderDetails.Where(od => od.OrderId == selectedOrder.Id && od.IsFulfilledByVendor == 0).ToList();

                    // Create ordertrackingdetail records for those not fulfilled by vendor
                    foreach (EmsTracking trackingNumber in orderTrackings.TrackingNumbers)
                    {
                        //OrderTracking chkorderTrackings = (from t in _sfDb.OrderTrackings where t.TrackingNumber == trackingNumber.TrackingNumber select t).FirstOrDefault();
                        List<OrderTrackingDetail> orderTrackingDetails = new List<OrderTrackingDetail>();
                        foreach (OrderDetail orderDetail in orderDetails)
                        {
                            orderTrackingDetails.Add(new OrderTrackingDetail()
                            {
                                OrderDetailId = orderDetail != null ? orderDetail.Id : 0,
                                ProductId = orderDetail.ProductId,
                                Qty = orderDetail.Qty,
                                UserId = adminUser != null ? adminUser.Id : "",
                                UserName = adminUser != null ? adminUser.UserName : "",
                                DateCreated = DateTime.Now,
                            });
                        }

                        ShipMethod selectedShipMethod = _sfDb.ShipMethods.Where(sm => sm.Id == selectedOrder.ShipMethodId).FirstOrDefault();
                        int carrierId = selectedShipMethod != null ? selectedShipMethod.CarrierId : 0;
                        ShipCarrier selectedShipCarrier = _sfDb.ShipCarriers.Where(sc => sc.Id == carrierId).FirstOrDefault();
                        string carrier = selectedShipCarrier != null ? selectedShipCarrier.Name : "";
                        string selectedShipMethodCode = selectedShipMethod == null ? "" : selectedShipMethod.Code;

                        OrderTracking orderTracking = new OrderTracking()
                        {
                            TrackingNumber = trackingNumber.TrackingNumber,
                            OrderNumber = selectedOrder.Id,
                            Carrier = carrier,
                            DateCreated = DateTime.Now,
                            //ShipMethod = selectedShipMethod.Code,
                            ShipMethod = selectedShipMethodCode,
                            OrderTrackingDetails = orderTrackingDetails
                        };
                        _sfDb.OrderTrackings.Add(orderTracking);
                    }
                    _sfDb.SaveChanges();

                    // Set order status based on if all are shipped complete (SH) or otherwise set as In Process (IP)
                    string orderStatus = orderTrackings.OrderStatus;
                    bool wasSomethingShipped = false;
                    if (orderStatus == "SH")
                    {
                        wasSomethingShipped = true;
                        orderDetails = _sfDb.OrderDetails.Where(od => od.OrderId == selectedOrder.Id).ToList();
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
                            if (trackingDetails == null) orderStatus = "IP";
                            if (orderStatus != "IP" && trackingDetails.Qty < od.Qty) orderStatus = "IP";

                            if (orderStatus == "IP") break;
                        }
                    }

                    // Set status as IP (Order in Process) when EMS are in these status
                    if (orderStatus != "SH") orderStatus = "IP";

                    selectedOrder.OrderStatus = orderStatus;
                    _sfDb.Entry(selectedOrder).State = EntityState.Modified;
                    _sfDb.SaveChanges();

                    // Alert user for shipped orders
                    if (wasSomethingShipped)
                    {
                        // Get the details for the email
                        selectedOrder.OrderDetails = _sfDb.OrderDetails.Where(od => od.OrderId == orderId).ToList();
                        foreach (var od in selectedOrder.OrderDetails)
                        {
                            od.Product = _sfDb.Products.Where(p => p.Id == od.ProductId).FirstOrDefault();
                        }

                        AspNetUser createdBy = _sfDb.AspNetUsers.Where(u => u.Id == selectedOrder.UserId).FirstOrDefault();

                        // Check system alert
                        SystemSetting sysData = (from ss in _sfDb.SystemSettings
                                                 where ss.StoreFrontId == storeFrontId
                                                 select ss).FirstOrDefault();
                        if (sysData != null && sysData.AlertOrderShipped == 1)
                        {
                            List<string> emailSentList = new List<string>();

                            // Also alert users subscribing for this alert
                            /*List<AspNetUser> usersNeedAlert = (from u in _sfDb.AspNetUsers
                                                               join us in _sfDb.UserSettings on u.Id equals us.AspNetUserId
                                                               where u.Id == selectedOrder.UserId && us.StoreFrontId == storeFrontId && us.AlertOrderShipped == 1
                                                               select u).ToList();
                            */
                            List<AspNetUser> usersNeedAlert1 = (from u in _sfDb.AspNetUsers
                                                                join us in _sfDb.UserSettings on u.Id equals us.AspNetUserId
                                                                where us.AlertOrderShipped == 1
                                                                && us.StoreFrontId == storeFrontId
                                                                && u.Id == selectedOrder.UserId
                                                                select u).ToList();
                            List<AspNetUser> usersNeedAlert2 = (from u in _sfDb.AspNetUsers
                                                                join us in _sfDb.UserSettings on u.Id equals us.AspNetUserId
                                                                where us.AlertOrderShipped == 1
                                                                && us.StoreFrontId == storeFrontId
                                                                && us.AlertOrderShippedFor == "All Orders"
                                                                && u.Id != selectedOrder.UserId
                                                                select u).ToList();
                            List<AspNetUser> usersNeedAlert3 = (from u in _sfDb.AspNetUsers
                                                                join us in _sfDb.UserSettings on u.Id equals us.AspNetUserId
                                                                join ugu in _sfDb.UserGroupUsers on us.AspNetUserId equals ugu.AspNetUserId
                                                                join ug in _sfDb.UserGroups on ugu.UserGroupId equals ug.Id
                                                                where us.AlertOrderShipped == 1
                                                                && us.StoreFrontId == storeFrontId
                                                                && ugu.StoreFrontId == storeFrontId
                                                                && ug.StoreFrontId == storeFrontId
                                                                && us.AlertOrderShippedFor == "For Orders Placed By My Group Members Only"
                                                                && u.Id != selectedOrder.UserId
                                                                select u).ToList();


                            string baseUrl = string.Format("{0}://{1}", Request.RequestUri.Scheme, Request.RequestUri.Authority);
                            string strDeepLink = baseUrl + "/Order/OrderDetails/" + selectedOrder.Id.ToString();

                            try
                            {
                           
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

                                string trackingDetail = "";
                                foreach (var trackingData in orderTrackings.TrackingNumbers)
                                {
                                    switch (trackingData.Carrier)
                                    {
                                        case "Fedex":
                                            trackingDetail += "<a href='http://www.fedex.com/apps/fedextrack/?action=track&trackingnumber=" + trackingData.TrackingNumber + "'>" + "Fedex - " + trackingData.TrackingNumber + "</a><br>";
                                            break;
                                        case "Ups":
                                            trackingDetail += "<a href='http://wwwapps.ups.com/WebTracking/processInputRequest?HTMLVersion=5.0&loc=en_US&Requester=UPSHome&tracknum=" + trackingData.TrackingNumber + "&AgreeToTermsAndConditions=yes&ignore=&track.x=26&track.y=15'>" + "UPS - " + trackingData.TrackingNumber + "</a><br>";
                                            break;
                                        case "Usps":
                                            trackingDetail += "<a href='http://trkcnfrm1.smi.usps.com/PTSInternetWeb/InterLabelInquiry.do?CAMEFROM=OK&strOrigTrackNum=" + trackingData.TrackingNumber + "'>" + "USPS - " + trackingData.TrackingNumber + "</a><br>";
                                            break;
                                        default:
                                            break;
                                    }
                                }

                                OrderShipTo orderShipTo = _sfDb.OrderShipTos.Where(ost => ost.OrderId == orderId).FirstOrDefault();

                                // email the users
                                //Microsoft.AspNet.Identity.IdentityMessage messageObject = new Microsoft.AspNet.Identity.IdentityMessage()
                                //{
                                string Subject = "";
                                string Body = "";
                                //Subject = "Your Order Has Been Shipped - Order No: " + selectedOrder.Id.ToString(),
                                Subject = "Your Order Has Been Shipped - Order No: " + selectedOrder.Id.ToString();
                                Body = "" +
                                "<br />" +
                                "<p class=MsoNormal style='margin-bottom:12.0pt'>Great News! Your order has been shipped and your tracking information is below!" + "<br />" +
                                "<br />" +
                                "*** Please allow 24 hours for tracking information to be updated from the carrier. ***<br />" +
                                "<br />" +
                                "Order Date: " + (selectedOrder.DateCreated ?? new DateTime(1, 1, 1)).ToString("MM/dd/yyyy") + "<br />" +
                                "Order No: " + selectedOrder.Id.ToString() + "<br />" +
                                "Alt Order No: " + "<br />" +
                                "PO: " + selectedOrder.PONumber + "<br />" +
                                "<br />" +
                                "Ship To:" + "<br />" +
                                //((createdBy.Company ?? "").Length + (createdBy.CompanyAlias ?? "").Length > 0 ? createdBy.Company + " " + createdBy.CompanyAlias : "") + "<br />" +
                                //((createdBy.FirstName ?? "").Length + (createdBy.LastName ?? "").Length > 0 ? createdBy.FirstName + " " + createdBy.LastName : "") + "<br />" +
                                //createdBy.Address1 + "<br />" +
                                //((createdBy.Address2 ?? "").Length > 0 ? createdBy.Address2 : "") + "<br />" +
                                //createdBy.City + "," + createdBy.State + " " + createdBy.Zip + "<br />" +
                                ((orderShipTo.Company ?? "").Length + (orderShipTo.CompanyAlias ?? "").Length > 0 ? orderShipTo.Company + " " + orderShipTo.CompanyAlias : "") + "<br />" +
                                ((orderShipTo.FirstName ?? "").Length + (orderShipTo.LastName ?? "").Length > 0 ? orderShipTo.FirstName + " " + orderShipTo.LastName : "") + "<br />" +
                                orderShipTo.Address1 + "<br />" +
                                ((orderShipTo.Address2 ?? "").Length > 0 ? orderShipTo.Address2 : "") + "<br />" +
                                orderShipTo.City + "," + orderShipTo.State + " " + orderShipTo.Zip + "<br />" +
                                "<br />" +
                                "<br />" +
                                "Tracking Info: " + "<br />" +
                                trackingDetail + "<br />" +
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

                                //string emailFrom = _sfDb.SystemSettings.Where(ss => ss.StoreFrontId == storeFrontId).FirstOrDefault().NotifyFromEmail;
                                /*foreach (AspNetUser u in usersNeedAlert)
                                {
                                    if ((u.Email ?? "").Length > 0)
                                    {
                                        messageObject.Destination = u.Email.ToLower();
                                        if (!emailSentList.Contains(messageObject.Destination)) await SendEmailAsync(messageObject, emailFrom);
                                        emailSentList.Add(messageObject.Destination);
                                        await Task.Delay(200);
                                    }
                                }*/

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
                                }
                                //mail.To.Clear();
                                foreach (AspNetUser u in usersNeedAlert2)
                                {
                                    mail.To.Add(u.Email);
                                }
                                //mail.To.Clear();
                                foreach (AspNetUser u in usersNeedAlert3)
                                {
                                    mail.To.Add(u.Email);
                                }

                                if (mail.To.Count >= 1)
                                {
                                    smtp.Send(mail);
                                    await Task.Delay(200);
                                }

                            }
                            catch
                            {
                                //error@impdynamics.sgdistribution.com
                            }
                        }

                    }
                }
                else
                {
                    return this.StatusCode(HttpStatusCode.NotFound);
                }
            }
            else
            {
                return this.StatusCode(HttpStatusCode.NotFound);
            }

            return this.StatusCode(HttpStatusCode.OK);
        }

        // GET api/<controller>/5
        [Authorize]
        [HttpGet]
        [Route("api/EMSProductsAll/{storeFrontId}")]
        public List<EmsProductImportModel> EMSProductsAll(int storeFrontId)
        {
            List<EmsProductImportModel> products = (from p in _sfDb.Products
                                                    where p.Status == 1 && p.StoreFrontId == storeFrontId
                                                    select new EmsProductImportModel()
                                                    {
                                                        Id = p.Id,
                                                        SFProductCode = p.ProductCode,
                                                    }).ToList();
            return products;
        }

        //EMSInventoryBulkUpdate
        // POST api/<controller>
        [Route("api/EMSInventoryBulkUpdate/{storeFrontId}")]
        [HttpPost]
        public int EMSInventoryBulkUpdate([FromBody] List<EmsProductImportModel> products, int storeFrontId)
        {
            int updatedCount = 0;

            if (products != null)
            {
                foreach (EmsProductImportModel product in products)
                {
                    Product selectedProduct = _sfDb.Products.Where(p => p.Id == product.Id && p.StoreFrontId == storeFrontId).FirstOrDefault();
                    if (selectedProduct != null)
                    {
                        selectedProduct.EMSQty = product.AvailableQty;
                        if (selectedProduct.EMSQty > 0) selectedProduct.EstRestockDate = null;
                        _sfDb.Entry(selectedProduct).State = EntityState.Modified;
                    }
                }
                _sfDb.SaveChanges();
            }

            return updatedCount;
        }

        // PUT api/<controller>/5
        public void Put(int id, [FromBody] string value)
        {
        }

        // DELETE api/<controller>/5
        public void Delete(int id)
        {
        }

        public Task SendEmailAsync(IdentityMessage message, string emailFrom)
        {
            // Plug in your email service here to send an email.
            var smtp = new SmtpClient();
            var mail = new MailMessage();
            var smtpSection = (SmtpSection)ConfigurationManager.GetSection("system.net/mailSettings/smtp");
            string username = "";

            if (emailFrom != null && emailFrom.Length > 0)
                username = emailFrom;
            else
                username = smtpSection.Network.UserName;

            mail.IsBodyHtml = true;
            mail.From = new MailAddress(username);
            mail.To.Add(message.Destination);
            mail.Subject = message.Subject;
            mail.Body = message.Body;

            smtp.Timeout = 1000;

            var t = Task.Run(() => smtp.SendAsync(mail, null));

            return t;
        }

    }
}