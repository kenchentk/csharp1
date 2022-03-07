using StoreFront2.Data;
using StoreFront2.Helpers;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using System.Xml;
using System.Xml.Linq;

namespace StoreFront2.Controllers
{
    public class PunchOutController : ApiController
    {

        private StoreFront2Entities _sfDb = new StoreFront2Entities();

        // POST SetupRequest
        [HttpPost]
        [Route("SetupRequest/{storeFrontId}")]
        public async Task<HttpResponseMessage> SetupRequestAsync(HttpRequestMessage requestMessage, int storeFrontId)
        {
            XElement response = new XElement("empty", "empty");
            string aspNetUserId = "";
            try
            {
                string baseUrl = Url.Content("~/");

                bool isAuthenticated = false;

                // Convert content as string
                Stream incomingXmlStream = requestMessage.Content.ReadAsStreamAsync().Result;
                StreamReader reader = new StreamReader(incomingXmlStream);
                string streamText = await reader.ReadToEndAsync();

                streamText = streamText.Replace(@"<!DOCTYPE cXML SYSTEM ""http://xml.cXML.org/schemas/cXML/1.2.008/cXML.dtd"">", "");
                streamText = streamText.Replace(@"<?xml version=""1.0"" encoding=""UTF-8""?>", "");
                streamText = streamText.Replace(System.Environment.NewLine, string.Empty);

                GlobalFunctions.Log(storeFrontId, "a9d00d8e-f98d-43b3-93cf-6aec9536d14e", "SetupRequestAsync RawData", "Xmldata", streamText);

                XElement cXml = XElement.Parse(streamText);
                XNamespace xmlNs = "xml";
                string payloadId = cXml.Attribute("payloadID").Value;
                XElement header = cXml.Descendants("Header").FirstOrDefault();
                XElement from = header.Descendants("From").FirstOrDefault();
                string fromIdentity = from.Descendants("Identity").Select(e => (string)e).FirstOrDefault();
                XElement sender = cXml.Descendants("Sender").FirstOrDefault();
                string senderIdentity = sender.Descendants("Identity").Select(e => (string)e).FirstOrDefault();
                string sharedSecret = sender.Descendants("SharedSecret").Select(e => (string)e).FirstOrDefault();
                XElement to = cXml.Descendants("To").FirstOrDefault();
                string toIdentity = to.Descendants("Identity").Select(e => (string)e).FirstOrDefault();

                if (sharedSecret == "eef7e4aa-430a-4570-bb5d-8691a88fa02d")
                {
                    isAuthenticated = true;
                }

                string timeStamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssK");
                payloadId = timeStamp + "-SfDyn_PunchOut";

                if (isAuthenticated)
                {
                    XElement request = cXml.Descendants("Request").FirstOrDefault();
                    XElement punchOutSetupRequest = cXml.Descendants("PunchOutSetupRequest").Where(e => (string)e.Attribute("operation") == "create").FirstOrDefault();
                    string buyerCookie = punchOutSetupRequest.Descendants("BuyerCookie").Select(e => (string)e).FirstOrDefault();
                    string username = punchOutSetupRequest.Descendants("Extrinsic").Where(e => (string)e.Attribute("name") == "User").Select(e => (string)e).FirstOrDefault();
                    string email = punchOutSetupRequest.Descendants("Extrinsic").Where(e => (string)e.Attribute("name") == "email").Select(e => (string)e).FirstOrDefault();



                    // 2/9/21 : adjustment to where they store the username
                    username = fromIdentity;

                    //username = punchOutSetupRequest.Descendants("ShipTo").Descendants("Address").Descendants("PostalAddress").Descendants("DeliverTo").Select(e => (string)e).FirstOrDefault();
                    //email = punchOutSetupRequest.Descendants("ShipTo").Descendants("Address").Descendants("Email").Select(e => (string)e).FirstOrDefault();
                    string defaultCurrency = ""; //punchOutSetupRequest.Descendants("ShipTo").Descendants("Address").Descendants("Name").Select(e => (string)e).FirstOrDefault();

                    string punchOutOrderBrowserFormPost = punchOutSetupRequest.Descendants("BrowserFormPost").Descendants("URL").Select(e => (string)e).FirstOrDefault();

                    string key = "";

                    // 2/9/21 : adjustment to where they store the username, remove email from the requirement
                    AspNetUser userSf = _sfDb.AspNetUsers.Where(u => u.UserName == username).FirstOrDefault();
                    if (userSf != null) { key = userSf.Id; aspNetUserId = userSf.Id; }

                    GlobalFunctions.Log(storeFrontId, key, "SetupRequestAsync", "Xmldata", streamText);

                    // Send response
                    string url = "";
                    string currencyFlag = "1";
                    if (key.Length > 0)
                    {
                        // save the punchout browser form post
                        UserSetting selectedUserSetting = _sfDb.UserSettings.Where(us => us.AspNetUserId == key && us.StoreFrontId == storeFrontId).FirstOrDefault();
                        if (selectedUserSetting != null)
                        {
                            selectedUserSetting.PunchOutBrowserPostUrl = punchOutOrderBrowserFormPost;
                            //defaultCurrency = defaultCurrency.Substring(defaultCurrency.IndexOf('(') + 1);
                            //defaultCurrency = defaultCurrency.Substring(0, defaultCurrency.IndexOf(')'));

                            defaultCurrency = "USD";
                            if (fromIdentity == "Basware-CA")
                            {
                                defaultCurrency = "CAD"; // 12/17/20 : Can't use this since one user can have several punchoutusers with different currencies. We'll use currencyFlag instead
                                currencyFlag = "2"; // Currency flag will be stored in the site object in [session]
                            }

                            selectedUserSetting.DefaultCurrency = defaultCurrency;
                            _sfDb.SaveChanges();
                        }

                        url = baseUrl + "Account/AutoLogin?key=" + currencyFlag + key + @"~" + buyerCookie;

                        response =
                            new XElement("cXML", new XAttribute("version", "1.2.011"), new XAttribute(XNamespace.Xmlns + "lang", "en-US"), new XAttribute("timestamp", timeStamp), new XAttribute("payloadID", payloadId),
                                new XElement("Response",
                                    new XElement("Status", new XAttribute("code", "200"), new XAttribute("text", "OK")),
                                    new XElement("PunchOutSetupResponse",
                                        new XElement("StartPage",
                                            new XElement("URL", url)
                                        )
                                    )
                                )
                            );
                    }
                    else
                    {
                        response =
                            new XElement("cXML", new XAttribute("version", "1.1.010"), new XAttribute(XNamespace.Xmlns + "lang", "en-US"), new XAttribute("timestamp", timeStamp), new XAttribute("payloadID", payloadId),
                                new XElement("Response",
                                    new XElement("Status", new XAttribute("code", "403"), new XAttribute("text", "Error"), "Forbidden")
                                )
                            );
                    }
                }
                else
                {
                    response =
                        new XElement("cXML", new XAttribute("version", "1.1.010"), new XAttribute(XNamespace.Xmlns + "lang", "en-US"), new XAttribute("timestamp", timeStamp), new XAttribute("payloadID", timeStamp + "-SfDyn_PunchOut"),
                            new XElement("Response",
                                new XElement("Status", new XAttribute("code", "403"), new XAttribute("text", "Error"), "Forbidden")
                            )
                        );
                }

            }
            catch (Exception ex)
            {
                GlobalFunctions.Log(storeFrontId, aspNetUserId, "", "Error", ex.Message);
            }


            XDocument responseDocument = new XDocument(
                new XDeclaration("1.0", "utf-8", "true"),
                new XDocumentType("cXML", null, "http://xml.cxml.org/schemas/cXML/1.1.010/cXML.dtd", null),
                response);

            StringWriter writer = new Utf8StringWriter();
            responseDocument.Save(writer, SaveOptions.None);

            return new HttpResponseMessage() { Content = new StringContent(writer.ToString()) };

        }

        // POST PunchOutReceiver
        [HttpPost]
        [Route("PunchOutReceiver/{storeFrontId}")]
        public async Task<IHttpActionResult> PunchOutReceiverAsync(HttpRequestMessage requestMessage, int storeFrontId)
        {
            // Convert content as string
            Stream incomingXmlStream = requestMessage.Content.ReadAsStreamAsync().Result;
            StreamReader reader = new StreamReader(incomingXmlStream);
            string streamText = await reader.ReadToEndAsync();

            GlobalFunctions.Log(storeFrontId, "PunchOutReceiverAsync", "Log", "Xmldata", streamText);

            return Ok();
        }


        // POST POReceiver
        [HttpPost]
        [Route("POReceiver/{storeFrontId}")]
        public async Task<HttpResponseMessage> POReceiverAsync(HttpRequestMessage requestMessage, int storeFrontId)
        {
            StringWriter writer;
            XDocument responseDocument = new XDocument(
                                                new XDeclaration("1.0", "utf-8", "true"),
                                                new XDocumentType("cXML", null, "http://xml.cxml.org/schemas/cXML/1.1.010/cXML.dtd", null));

            XElement response = new XElement("empty", "empty");
            string aspNetUserId = "";
            try
            {
                string baseUrl = Url.Content("~/");

                bool isAuthenticated = false;

                // Convert content as string
                Stream incomingXmlStream = requestMessage.Content.ReadAsStreamAsync().Result;
                StreamReader reader = new StreamReader(incomingXmlStream);
                string streamText = await reader.ReadToEndAsync();

                GlobalFunctions.Log(storeFrontId, "a9d00d8e-f98d-43b3-93cf-6aec9536d14e", "POReceiverAsync", "Xmldata", streamText);

                streamText = streamText.Replace(@"<!DOCTYPE cXML SYSTEM ""http://xml.cXML.org/schemas/cXML/1.2.008/cXML.dtd"">", "");
                streamText = streamText.Replace(@"<?xml version=""1.0"" encoding=""UTF-8""?>", "");
                streamText = streamText.Replace(System.Environment.NewLine, string.Empty);

                XElement cXml = XElement.Parse(streamText);
                XNamespace xmlNs = "xml";
                string payloadId = cXml.Attribute("payloadID").Value;
                XElement header = cXml.Descendants("Header").FirstOrDefault();
                XElement from = header.Descendants("From").FirstOrDefault();

                string fromIdentity = from.Descendants("Identity").Select(e => (string)e).FirstOrDefault();
                XElement sender = cXml.Descendants("Sender").FirstOrDefault();
                string senderIdentity = sender.Descendants("Identity").Select(e => (string)e).FirstOrDefault();
                string sharedSecret = sender.Descendants("SharedSecret").Select(e => (string)e).FirstOrDefault();
                XElement to = cXml.Descendants("To").FirstOrDefault();
                string toIdentity = to.Descendants("Identity").Select(e => (string)e).FirstOrDefault();

                if (sharedSecret == "eef7e4aa-430a-4570-bb5d-8691a88fa02d")
                {
                    isAuthenticated = true;
                }

                string timeStamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssK");
                payloadId = timeStamp + "-SfDyn_PunchOut";

                AspNetUser _sfUser;

                if (isAuthenticated)
                {
                    XElement request = cXml.Descendants("Request").FirstOrDefault();
                    string orderDate = request.Descendants("OrderRequest").Descendants("OrderRequestHeader").Attributes("orderDate").FirstOrDefault().Value;

                    XElement shipTo = request.Descendants("OrderRequest").Descendants("OrderRequestHeader").Descendants("ShipTo").FirstOrDefault();
                    string buyerCookie = "";
                    string addressId = shipTo.Descendants("Address").FirstOrDefault().Attribute("addressID").Value;
                    string orderNumber = request.Descendants("OrderRequest").Descendants("OrderRequestHeader").Attributes("orderID").FirstOrDefault().Value;
                    string company = shipTo.Descendants("Address").Descendants("Name").Select(e => (string)e).FirstOrDefault();
                    XElement postalAddress = shipTo.Descendants("Address").Descendants("PostalAddress").FirstOrDefault();
                    string firstName = postalAddress.Descendants("DeliverTo").Select(e => (string)e).FirstOrDefault();

                    string lastName = firstName;
                    List<string> streets = postalAddress.Descendants("Street").Select(e => (string)e).ToList();
                    string address1 = streets[0];
                    string address2 = (streets.Count > 1) ? streets[1] : "";
                    string city = postalAddress.Descendants("City").Select(e => (string)e).FirstOrDefault();
                    string state = postalAddress.Descendants("State").Select(e => (string)e).FirstOrDefault();
                    string postalCode = postalAddress.Descendants("PostalCode").Select(e => (string)e).FirstOrDefault();
                    string country = postalAddress.Descendants("Country").Select(e => (string)e).FirstOrDefault();
                    string email = shipTo.Descendants("Address").Descendants("Email").Select(e => (string)e).FirstOrDefault();

                    string phonecountrycode = shipTo.Descendants("Address").Descendants("Phone").FirstOrDefault().Descendants("TelephoneNumber").Descendants("CountryCode").Select(e => (string)e).FirstOrDefault();
                    string phone = "+" + phonecountrycode + " " + shipTo.Descendants("Address").Descendants("Phone").FirstOrDefault().Descendants("TelephoneNumber").Descendants("Number").Select(e => (string)e).FirstOrDefault();

                    // Get the userid that initiate this order
                    if (firstName == "Admin (117)") firstName = "Admin  117";
                    _sfUser = _sfDb.AspNetUsers.Where(u => u.UserName == fromIdentity).FirstOrDefault();

                    XElement shipping = request.Descendants("OrderRequest").Descendants("OrderRequestHeader").Descendants("Shipping").FirstOrDefault();
                    string shipMethod = shipping.Attribute("trackingDomain").Value;

                    string comments = request.Descendants("OrderRequest").Descendants("OrderRequestHeader").Descendants("Comments").Select(e => (string)e).FirstOrDefault();

                    List<XElement> itemOuts = request.Descendants("OrderRequest").Descendants("ItemOut").ToList();

                    // Get all order ids matching this buyerCookie                    
                    List<int> selectedOrderIds = new List<int>();

                    // Check user credit hold
                    SystemSetting systemSetting = _sfDb.SystemSettings.Where(ss => ss.StoreFrontId == storeFrontId).FirstOrDefault();
                    bool creditHold = _sfUser.OnHold == 1 ? true : false;
                    bool allOrderOnHold = systemSetting.AllOrdersOnHold == 1;

                    int shipMethodId = (from sm in _sfDb.ShipMethods
                                        join sc in _sfDb.ShipCarriers on sm.CarrierId equals sc.Id
                                        where sc.StoreFrontId == storeFrontId && sm.Code == shipMethod
                                        select sm.Id).FirstOrDefault();

                    // Set to default
                    if (shipMethodId == 0)
                    {
                        shipMethodId = (from sm in _sfDb.ShipMethods
                                        join sc in _sfDb.ShipCarriers on sm.CarrierId equals sc.Id
                                        where sc.StoreFrontId == storeFrontId && sm.Code == "UPSG"
                                        select sm.Id).FirstOrDefault();
                    }

                    decimal totalPrice = 0;
                    List<OrderDetail> selectedOrderDetailList = new List<OrderDetail>();

                    // Set all itemOut as cart item
                    var cartId = DateTime.Now.ToString("yyyyMMddHHmmss");
                    foreach (XElement itemOut in itemOuts)
                    {
                        string supplierPartId = itemOut.Descendants("ItemID").Descendants("SupplierPartID").Select(e => (string)e).FirstOrDefault();
                        string quantity = itemOut.Attribute("quantity").Value;
                        string price = itemOut.Descendants("ItemDetail").Descendants("UnitPrice").Descendants("Money").Select(e => (string)e).FirstOrDefault();
                        string currency = itemOut.Descendants("ItemDetail").Descendants("UnitPrice").Descendants("Money").Attributes("currency").FirstOrDefault().Value;
                        buyerCookie = itemOut.Descendants("ItemDetail").Descendants("Extrinsic").Select(e => (string)e).FirstOrDefault();
                        Cart c = new Cart()
                        {
                            CartId = cartId,
                            ProductId = Convert.ToInt32(supplierPartId),
                            Count = Convert.ToInt32(quantity),
                            Price = currency == "USD" ? Convert.ToDecimal(price) : 0,
                            PriceCAD = currency == "CAD" ? Convert.ToDecimal(price) : 0,
                            DateCreated = DateTime.Parse(orderDate),
                            UserId = _sfUser.Id,
                            StoreFrontId = storeFrontId,
                            CartNote = comments,
                        };

                        Product selectedProduct = _sfDb.Products.Where(p => p.Id == c.ProductId).FirstOrDefault();
                        int? vendorId = _sfDb.VendorProducts.Where(vp => vp.ProductId == c.ProductId).Select(vp => vp.VendorId).FirstOrDefault();

                        List<int> orderIds = (from o in _sfDb.Orders where o.OrderReference1 == buyerCookie select o.Id).ToList();
                        foreach (int ids in orderIds) if (!selectedOrderIds.Contains(ids)) selectedOrderIds.Add(ids);

                        OrderDetail selectedOrderDetail = _sfDb.OrderDetails.Where(od => selectedOrderIds.Contains(od.OrderId)
                                                                                        && od.ProductId == c.ProductId
                                                                                        && od.Qty == c.Count
                                                                                        && od.Status != "RP").FirstOrDefault();

                        if (selectedOrderDetail != null)
                        {
                            selectedOrderDetail.Price = currency == "USD" ? c.Price : 0;
                            selectedOrderDetail.PriceCAD = currency == "CAD" ? c.Price : 0;
                            selectedOrderDetail.Currency = currency;
                            selectedOrderDetail.Status = "RP";
                            selectedOrderDetail.VendorId = vendorId ?? 0;
                            _sfDb.Entry(selectedOrderDetail).State = EntityState.Modified;

                            Order selectedOrder = _sfDb.Orders.Where(o => o.Id == selectedOrderDetail.OrderId).FirstOrDefault();

                            selectedOrder.PONumber = orderNumber;
                            selectedOrder.OrderStatus = creditHold || allOrderOnHold ? "OH" : "RP";
                            selectedOrder.ShipMethodId = shipMethodId;
                            _sfDb.Entry(selectedOrder).State = EntityState.Modified;

                            totalPrice += c.Count * (currency == "USD" ? c.Price : c.PriceCAD);
                        }

                        //selectedOrderDetailList.Add(selectedOrderDetail);
                        selectedOrderDetail = null;
                    }

                    // Save Ship To Address
                    UserAddress shipToAddress;
                    int addressIdInt = 0;
                    if (addressId.Length > 0)
                    {
                        addressIdInt = Convert.ToInt32(addressId);
                        shipToAddress = _sfDb.UserAddresses.Where(ua => ua.Id == addressIdInt).FirstOrDefault();
                    }

                    List<OrderShipTo> selectedOrderShipToList = _sfDb.OrderShipTos.Where(ost => selectedOrderIds.Contains(ost.OrderId)).ToList();
                    foreach (OrderShipTo ost in selectedOrderShipToList)
                    {
                        ost.ShipToId = addressIdInt;
                        ost.Alias = addressId;
                        ost.Company = company;
                        ost.FirstName = firstName;
                        ost.LastName = lastName;
                        ost.Address1 = address1;
                        ost.Address2 = address2;
                        ost.City = city;
                        ost.State = state;
                        ost.Zip = postalCode;
                        ost.Country = country;
                        ost.Phone = phone;
                        ost.Email = email;

                        _sfDb.Entry(ost).State = EntityState.Modified;
                    }

                    // Save order note record
                    List<OrderNote> selectedOrderNoteList = _sfDb.OrderNotes.Where(on => selectedOrderIds.Contains(on.OrderId)).ToList();
                    foreach (OrderNote on in selectedOrderNoteList)
                    {
                        on.Note = ((on.Note == null || comments.Length == 0) ? "" : Environment.NewLine) + comments;
                    }

                    // Also increase the user budget tally
                    UserSetting userSetting = _sfDb.UserSettings.Where(us => us.AspNetUserId == _sfUser.Id && us.StoreFrontId == storeFrontId).FirstOrDefault();
                    if (userSetting == null)
                    {
                        // this user hasn't been setup with budget, create one
                        userSetting = new UserSetting()
                        {
                            AspNetUserId = _sfUser.Id,
                            StoreFrontId = storeFrontId,
                            BudgetIgnore = 0,
                            BudgetLimit = systemSetting.BudgetLimitDefault,
                            BudgetCurrentTotal = 0,
                            BudgetResetInterval = systemSetting.BudgetRefreshPeriodDefault,
                            BudgetLastResetDate = new DateTime(1, 1, 1),
                            BudgetNextResetDate = new DateTime(1, 1, 1),
                        };

                        userSetting.BudgetCurrentTotal += totalPrice;
                        if (userSetting.BudgetLastResetDate.Year == 1) userSetting.BudgetLastResetDate = DateTime.Now.Date; // set the initial oder date
                        _sfDb.UserSettings.Add(userSetting);
                    }
                    else
                    {
                        userSetting.BudgetCurrentTotal += totalPrice;
                        if (userSetting.BudgetLastResetDate.Year == 1) userSetting.BudgetLastResetDate = DateTime.Now.Date; // set the initial oder date
                        _sfDb.Entry(userSetting).State = EntityState.Modified;
                    }

                    if (systemSetting.BudgetEnforce == 1)
                    {
                        if (userSetting.BudgetIgnore == 0)
                        {
                            if (userSetting.BudgetCurrentTotal > userSetting.BudgetLimit)
                            {
                                response =
                                    new XElement("cXML", new XAttribute("version", "1.1.010"), new XAttribute(XNamespace.Xmlns + "lang", "en-US"), new XAttribute("timestamp", timeStamp), new XAttribute("payloadID", payloadId),
                                        new XElement("Response",
                                            new XElement("Status", new XAttribute("code", "403"), new XAttribute("text", "Error"), "Forbidden")
                                        )
                                    );

                                responseDocument.Add(response);

                                writer = new Utf8StringWriter();
                                responseDocument.Save(writer, SaveOptions.None);

                                return new HttpResponseMessage() { Content = new StringContent(writer.ToString()) };
                            }
                        }
                    }

                    _sfDb.SaveChanges();

                    // Also move the order qty to QtyCancelled for those not approved
                    foreach (int orderId in selectedOrderIds)
                    {
                        List<OrderDetail> remainingOrderDetails = _sfDb.OrderDetails.Where(od => od.OrderId == orderId
                                                                                                && od.Qty != 0
                                                                                                && od.Status != "RP").ToList();
                        foreach (OrderDetail lineItemNotApproved in remainingOrderDetails)
                        {
                            lineItemNotApproved.QtyCancelled = lineItemNotApproved.Qty;
                            lineItemNotApproved.Qty = 0;
                            lineItemNotApproved.Status = "CN";
                            _sfDb.Entry(lineItemNotApproved).State = EntityState.Modified;
                        }
                    }

                    _sfDb.SaveChanges();

                    // If all items in the orderdetail has been cancelled, set the header to cancelled also
                    foreach (int orderId in selectedOrderIds)
                    {
                        List<OrderDetail> remainingOrderDetails = _sfDb.OrderDetails.Where(od => od.OrderId == orderId
                                                                                                && od.Qty != 0).ToList();
                        if (remainingOrderDetails == null || remainingOrderDetails.Count() == 0)
                        {
                            Order cancelOrder = _sfDb.Orders.Where(o => o.Id == orderId).FirstOrDefault();
                            cancelOrder.OrderStatus = "CN";
                            _sfDb.Entry(cancelOrder).State = EntityState.Modified;
                        }
                    }

                    _sfDb.SaveChanges();

                    // Check system alert
                    if (systemSetting != null && systemSetting.AlertOrderReceived == 1)
                    {
                        List<string> emailSentList = new List<string>();

                        // Also alert users subscribing for this alert
                        List<AspNetUser> usersNeedAlert = (from u in _sfDb.AspNetUsers
                                                           join us in _sfDb.UserSettings on u.Id equals us.AspNetUserId
                                                           where us.AlertOrderReceived == 1 && u.Id == _sfUser.Id && us.StoreFrontId == storeFrontId
                                                           select u).ToList();

                        string strDeepLinks = "";
                        foreach (int orderId in selectedOrderIds)
                        {
                            string strDeepLink = baseUrl + "/Order/OrderDetails/" + orderId.ToString();
                            strDeepLinks += "Order No: " + orderId.ToString() + @" - <a href=""" + strDeepLink + @""">CLICK HERE</a><br>";
                        }

                        // Create the email message body
                        string messageBody = "Good News! Your order has been received and will be processed for shipment as soon as possible. <br />" +
                            "<br />" +
                            "Order Details - " +
                            "<br />";

                        // create lineitem html
                        string orderDetailHtmlHeader = "";
                        orderDetailHtmlHeader += "" +
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

                        foreach (int orderId in selectedOrderIds)
                        {
                            Order selectedOrder = _sfDb.Orders.Where(o => o.Id == orderId).FirstOrDefault();
                            messageBody += "" +
                                "Order Date: " + (selectedOrder.DateCreated ?? new DateTime(1, 1, 1)).ToString("MM/dd/yyyy") + "<br />" +
                                "Order No: " + selectedOrder.Id.ToString() + "<br />" +
                                "Alt Order No: " + "<br />" +
                                "PO: " + selectedOrder.PONumber + "<br />" +
                                "<br />" +
                                "Ship To:" + "<br />" +
                                ((firstName ?? "").Length + (lastName ?? "").Length > 0 ? firstName + " " + lastName : "") + "<br />" +
                                ((address1 ?? "").Length > 0 ? address1 : "") + "<br />" +
                                ((address2 ?? "").Length > 0 ? address2 : "") + "<br />" +
                                city + "," + state + " " + postalCode + " " + country + "<br />" +
                                "<br />";

                            string orderDetailHtml = "";
                            foreach (var detail in selectedOrder.OrderDetails)
                            {
                                if (detail.Status != "RP") continue; // Only process approved line items
                                orderDetailHtml +=
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

                            orderDetailHtml = "<table class=MsoNormalTable border=0 cellpadding=0>" + orderDetailHtmlHeader + orderDetailHtml + "</table>";

                            messageBody += orderDetailHtml + "<br/><br/>";
                        }

                        // email the users
                        Microsoft.AspNet.Identity.IdentityMessage messageObject = new Microsoft.AspNet.Identity.IdentityMessage()
                        {
                            Subject = "Order# [" +
                            string.Join(", ", selectedOrderIds.Select(oid => oid.ToString())) +
                            "] has been received - Order No: " +
                            string.Join(", ", selectedOrderIds.Select(oid => oid.ToString())),
                            Body = messageBody +
                            "<p class=MsoNormal><br>" +
                            "If you have access to our online management portal you may check on the status of you order by clicking the link below:<br>" +
                            "<br>" +
                            strDeepLinks +
                            "<br>" +
                            "**** PLEASE NOTE: THIS IS AN AUTOMATED EMAIL - PLEASE DO NOT REPLY ****</p>",
                        };

                        string emailFrom = _sfDb.SystemSettings.Where(ss => ss.StoreFrontId == storeFrontId).FirstOrDefault().NotifyFromEmail;

                        foreach (AspNetUser u in usersNeedAlert)
                        {
                            if ((u.Email ?? "").Length > 0)
                            {
                                messageObject.Destination = u.Email.ToLower();
                                if (!emailSentList.Contains(messageObject.Destination)) await SmtpEmailService.SendAsync(messageObject, emailFrom);
                                emailSentList.Add(messageObject.Destination);
                                await Task.Delay(200);
                            }
                        }

                    }

                    // int orderid = (from o in _sfDb.Orders where o.SFOrderNumber == sfOrderNumber select o).FirstOrDefault().Id;

                    response = new XElement("cXML", new XAttribute("version", "1.1.010"), new XAttribute(XNamespace.Xmlns + "lang", "en-US"), new XAttribute("timestamp", timeStamp), new XAttribute("payloadID", payloadId),
                                        new XElement("Response",
                                            new XElement("Status", new XAttribute("code", "200"), new XAttribute("text", "Success"), "Success")
                                        )
                                    );
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
                    response =
                        new XElement("cXML", new XAttribute("version", "1.1.010"), new XAttribute(XNamespace.Xmlns + "lang", "en-US"), new XAttribute("timestamp", timeStamp), new XAttribute("payloadID", payloadId),
                            new XElement("Response",
                                new XElement("Status", new XAttribute("code", "403"), new XAttribute("text", "Error"), "Forbidden")
                            )
                        );
                }

            }
            catch (Exception ex)
            {
                GlobalFunctions.Log(storeFrontId, "a9d00d8e-f98d-43b3-93cf-6aec9536d14e", "", "Error", ex.Message);
                response = new XElement("Error", ex.Message);
            }

            responseDocument.Add(response);

            writer = new Utf8StringWriter();
            responseDocument.Save(writer, SaveOptions.None);

            GlobalFunctions.Log(storeFrontId, "a9d00d8e-f98d-43b3-93cf-6aec9536d14e", "POReceiverAsync Response", "Xmldata", writer.ToString());

            return new HttpResponseMessage() { Content = new StringContent(writer.ToString()) };
        }

        // PUT api/<controller>/5
        public void Put(int id, [FromBody] string value)
        {
        }

        // DELETE api/<controller>/5
        public void Delete(int id)
        {
        }
    }
}
