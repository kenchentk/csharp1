using StoreFront2.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace StoreFront2.Helpers
{
    public static class Extensions  
    {
        public static IEnumerable<SelectListItem> ToSelectListItems(this IEnumerable<StoreFront> storeFronts, int? selectedId)
        {
            return storeFronts.OrderBy(s => s.StoreFrontName).Select(s => new SelectListItem { Selected = (s.Id == selectedId), Text = s.StoreFrontName, Value = s.Id.ToString() });
        }

        public static IEnumerable<SelectListItem> ToSelectListItems(this IEnumerable<AspNetUser> users, int? selectedId)
        {
            return users.OrderBy(s => s.UserName).Select(s => new SelectListItem { Selected = (s.SfId == selectedId), Text = s.UserName, Value = s.SfId.ToString() });
        }

        public static IEnumerable<Exception> GetInnerExceptions(this Exception ex)
        {
            if (ex == null)
            {
                throw new ArgumentNullException("ex");
            }

            var innerException = ex;
            do
            {
                yield return innerException;
                innerException = innerException.InnerException;
            }
            while (innerException != null);
        }
    }

}