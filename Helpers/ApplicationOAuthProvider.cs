using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Owin.Security.OAuth;
using Microsoft.AspNet.Identity.Owin;
using System.Security.Claims;
using Microsoft.Owin.Security.Cookies;
using Microsoft.Owin.Security;
using System.Collections.Generic;
using StoreFront2.Data;
using System.Diagnostics;
using Microsoft.AspNet.Identity;

namespace StoreFront2.Helpers
{
    public class ApplicationOAuthProvider : OAuthAuthorizationServerProvider
    {
        private readonly string _publicClientId;

        private StoreFront2Entities StoreFrontDb = new StoreFront2Entities();

        public ApplicationOAuthProvider(string publicClientId)
        {
            if (publicClientId == null)
            {
                throw new ArgumentNullException("publicClientId");
            }

            _publicClientId = publicClientId;
        }

        public override async Task GrantResourceOwnerCredentials(OAuthGrantResourceOwnerCredentialsContext context)
        {
            //var userManager = context.OwinContext.GetUserManager<ApplicationUserManager>();
            //var user = await userManager.FindAsync(context.UserName, context.Password);

            try
            {
                string usernameVal = context.UserName;
                string passwordVal = context.Password;

                var user = StoreFrontDb.AspNetUsers.Where(u => u.UserName == context.UserName).FirstOrDefault();

                if (user == null)
                {
                    context.SetError("Invalid Grant", "The username not found");
                    return;
                }
                else
                {
                    var checkPassword = new PasswordHasher();

                    if (checkPassword.VerifyHashedPassword(user.PasswordHash, passwordVal) == PasswordVerificationResult.Failed) 
                    {
                        context.SetError("Invalid Grant", "The password is invalid");
                    }
                }

                if (user.EnforcePasswordChange == 1)
                {
                    if (user.LastPasswordChangedDate != null)
                    {
                        DateTime lastDate = user.LastPasswordChangedDate ?? DateTime.Now.Date.AddDays(-30);
                        if (lastDate.AddDays(30) < DateTime.Now.Date)
                        {
                            context.SetError("Invalid Grant", "Password expired");
                        }
                    }
                }

                // Initialization
                var claims = new List<Claim>();
                claims.Add(new Claim(ClaimTypes.Name, user.UserName));
                
                // Setting Claim Identities for OAUTH 2 protocol.
                ClaimsIdentity oAuthIdentity = new ClaimsIdentity(claims, OAuthDefaults.AuthenticationType);
                oAuthIdentity.AddClaim(new Claim("token", context.UserName));
                ClaimsIdentity cookiesIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationType);
                
                AuthenticationProperties properties = CreateProperties(user.UserName);
                AuthenticationTicket ticket = new AuthenticationTicket(oAuthIdentity, properties);

                context.Validated(ticket);
                context.Request.Context.Authentication.SignIn(cookiesIdentity);

            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error : " + ex.Message);
            }


            return;
        }

        public override Task TokenEndpoint(OAuthTokenEndpointContext context)
        {
            foreach (KeyValuePair<string, string> property in context.Properties.Dictionary)
            {
                context.AdditionalResponseParameters.Add(property.Key, property.Value);
            }

            return Task.FromResult<object>(null);
        }

        public override Task ValidateClientRedirectUri(OAuthValidateClientRedirectUriContext context)
        {
            if (context.ClientId == _publicClientId)
            {
                Uri expectedRootUri = new Uri(context.Request.Uri, "/");

                if (expectedRootUri.AbsoluteUri == context.RedirectUri)
                {
                    context.Validated();
                }
                else if (context.ClientId == "web")
                {
                    var expectedUri = new Uri(context.Request.Uri, "/");
                    context.Validated(expectedUri.AbsoluteUri);
                }
            }

            return Task.FromResult<object>(null);
        }

        public override Task ValidateClientAuthentication(OAuthValidateClientAuthenticationContext context)
        {
            // Resource owner password credentials does not provide a client ID.
            if (context.ClientId == null)
            {
                context.Validated();
            }

            return Task.FromResult<object>(null);
        }

        private AuthenticationProperties CreateProperties(string userName)
        {
            IDictionary<string, string> data = new Dictionary<string,string>
            {
                { "userName", userName }
            };
            return new AuthenticationProperties(data);
        }
    }
}