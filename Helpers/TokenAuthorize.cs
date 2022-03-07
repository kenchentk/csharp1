using StoreFront2.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IdentityModel.Tokens;
using System.Linq;
using System.Security;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Web;
using System.Web.Configuration;
using System.Web.Mvc;

namespace StoreFront2.Helpers
{
    public class TokenAuthorize : AuthorizeAttribute
    {
        private const string SecurityToken = "token";

        StoreFront2Entities StoreFrontDb = new StoreFront2Entities();

        public override void OnAuthorization(AuthorizationContext filterContext)
        {
            if (Authorize(filterContext))
            {
                return;
            }

            HandleUnauthorizedRequest(filterContext);
        }

        private bool Authorize(AuthorizationContext actionContext)
        {
            try
            {
                HttpContextBase context = actionContext.RequestContext.HttpContext;
                bool isAuthorized = false;

                if (context.Request.IsAuthenticated) isAuthorized = true;

                if (!isAuthorized)
                {
                    isAuthorized = AuthorizeCore(context);
                }

                if (isAuthorized)
                {
                    //string userEmail = HttpContext.Current.Request.Params["auth_user"];
                    string _userName = HttpContext.Current.Request.Params["auth_user"];

                    string aspNetUserId = "";
                    AspNetUser user = StoreFrontDb.AspNetUsers.Where(u => u.UserName == _userName).FirstOrDefault();
                    if (user != null) aspNetUserId = user.Id;

                    int storeFrontId = 0;
                    string baseUrl = HttpContext.Current.Request.Url.AbsoluteUri;

                    var referrerObject = HttpContext.Current.Request.UrlReferrer;
                    string referrerUrl = (referrerObject == null) ? "localhost" : referrerObject.AbsoluteUri;


                    StoreFront sf = StoreFrontDb.StoreFronts.Where(s => s.BaseUrl == baseUrl).FirstOrDefault();
                    if (sf != null) storeFrontId = sf.Id;

                    // Store in log
                    UserLoginLog log = new UserLoginLog()
                    {
                        UserName = _userName,
                        Email = _userName,
                        Token = "", //token,
                        LoginTimeStamp = DateTime.Now,
                        Status = 1,
                        TokenExpireTime = DateTime.Now,
                        AspNetUserId = aspNetUserId,
                        StoreFrontId = storeFrontId
                    };
                    StoreFrontDb.UserLoginLogs.Add(log);
                    StoreFrontDb.SaveChanges();

                }

                return isAuthorized;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error : " + ex.Message);
            }
            return false;
        }

        private bool IsTokenValid(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var validationParameters = GetValidationParameters();

                var jsonToken = tokenHandler.ReadToken(token) as JwtSecurityToken;


                SecurityToken validatedToken;
                IPrincipal principal = tokenHandler.ValidateToken(token, validationParameters, out validatedToken);

                Thread.CurrentPrincipal = principal;
                HttpContext.Current.User = principal;

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error : " + ex.Message);
            }
            return false;
        }

        private static TokenValidationParameters GetValidationParameters()
        {
            return new TokenValidationParameters
            {
                //IssuerSigningToken = new System.ServiceModel.Security.Tokens.BinarySecretSecurityToken(symmetricKey), //Key used for token generation
                //ValidIssuer = issuerName,
                //ValidAudience = allowedAudience,
                ValidateIssuerSigningKey = true,
                ValidateIssuer = true,
                ValidateAudience = true
            };
        }

        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            bool isAuthorized = false;
            
            // Authorize by Token, check expiration on UserLoginLogs table
            isAuthorized = HasValidToken();

            // Run MVC regular authorization
            if (!isAuthorized)
            {
                isAuthorized = base.AuthorizeCore(httpContext);
            }

            return isAuthorized;
        }

        private bool HasValidToken()
        {
            string token = string.Empty;
            token = HttpContext.Current.Request.Params["token"];
            if (token != null)
            {
                var tokenHistory = StoreFrontDb.UserLoginLogs.Where(l => l.TokenExpireTime > DateTime.Now).FirstOrDefault();
                if (tokenHistory != null)
                {
                    // Update login time stamp
                    tokenHistory.LoginTimeStamp = DateTime.Now;
                    //StoreFrontDb.SaveChanges(); not correct need to save to the correct user storefront and login stamp
                    return true;
                }
                else
                {
                    // No token for this user
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

    }
}