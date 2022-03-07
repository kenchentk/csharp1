using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web.Http;


namespace StoreFront2.Controllers
{
    public class WixController : ApiController
    {

        [HttpPost]
        [Route("api/WixAuthorized/")]
        public IHttpActionResult WixAuthorized([FromBody] object requestBody)
        {
            File.WriteAllText(@"d:\websites\storefront2\bin\Debug.txt", "WixAuthorized called");
            var model = requestBody;

            string json = JsonConvert.SerializeObject(requestBody);

            File.WriteAllText(@"d:\websites\storefront2\bin\JsonPayLoad1.txt", json);

            //open file stream
            using (StreamWriter file = File.CreateText(@"d:\websites\storefront2\bin\JsonPayLoad2.txt"))
            {
                JsonSerializer serializer = new JsonSerializer();
                //serialize object directly into file stream
                serializer.Serialize(file, requestBody);
            }
            return Ok(model);
        }

        [HttpPost]
        [Route("api/ProcessWebhookEvent/")]
        public System.Web.Mvc.HttpStatusCodeResult ProcessWebhookEvent(HttpRequestMessage request, [FromBody] string requestBody, string vendorId, string eventType = "Unknown")
        {
            var model = new { result = "Received" };
            return new System.Web.Mvc.HttpStatusCodeResult(HttpStatusCode.OK);
        }

        [HttpPost]
        [Route("api/WixCartCompleted/")]
        public IHttpActionResult WixCartCompleted([FromBody] object requestBody)
        {
            File.WriteAllText(@"c:\websites\storefront2\bin\Debug2.txt", "WixCartCompleted called");

            var headersJson = JsonConvert.SerializeObject(Request.Headers);
            File.WriteAllText(@"c:\websites\storefront2\bin\Headers.json", headersJson);

            // Get the authentication from the header
            //var encoding = Encoding.GetEncoding("UTF-8");
            //var authValue = encoding.GetString(Convert.FromBase64String(Request.Headers.FirstOrDefault().ToString()));
            //File.WriteAllText(@"c:\websites\storefront2\bin\Debug1.txt", authValue);
            ////var validAuthorization = ConfigurationManager.AppSettings["ValidKey"];

            //string wixPublicKey = "-----BEGIN PUBLIC KEY-----MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA5JMgZcaWhCOlMjQxpyhu1uzLuZXrT / UNbzNCsj5tpMFv5XHrbXiODIeTxqC / q06aoQnDfSmvtb9b0KONS0DGCNXRzzkfxvWoy + hAaKPGCLMGNJi0bU + Xv9R67bypSSDQ2NWJz7hoH + kypsUWe6TgxE8n9sy / 8aJkNngePPkubR1RMwcF46dLptLhWVIEAvkm / RSK5nmoDbnkMuzmiKj0zzWDzADnJgDak0OFAdg2osGQTGcE7Q41XLh4KkpP + vFIeWfaa5 / 4eu8gWzk9krVrHnsAdWTZBfwYlQ9ThiLl5Ti2LsT2JSRLZ64Yh64sq7ZgohXOKzXUdUBzxXOUIreN+ QIDAQAB---- - END PUBLIC KEY-----";



            var model = new { result = "Received" };
            return Ok(model);
        }


    }
}
