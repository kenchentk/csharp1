using StoreFront2.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace StoreFront2.Controllers
{
    [RoutePrefix("api/Authentication")]
    public class AuthenticationController : ApiController
    {
        // (Not used currently)
        [HttpGet]
        [Route("AuthenticatedUser")]
        [Authorize]
        public IHttpActionResult AuthenticatedUser()
        {
            AspNetUser user = new AspNetUser();

            return Ok();
        }
    }
}
