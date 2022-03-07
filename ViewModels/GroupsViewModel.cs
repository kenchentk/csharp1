using StoreFront2.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using StoreFront2.Data;
using System.Web.Mvc;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;

namespace StoreFront2.ViewModels
{
    public class GroupsViewModel
    {
        [Key]
        public int Id { get; set; }

        [HiddenInput]
        public int StoreFrontId { get; set; }

        [Required]
        [Display(Name = "Name Group")]
        public string Name { get; set; }

        [Display(Name = "Description")]
        public string Desc { get; set; }

        [HiddenInput]
        public Nullable<System.DateTime> DateCreated { get; set; }

        [HiddenInput]
        public string UserId { get; set; }

        [HiddenInput]
        public string UserName { get; set; }
        [Required]
        [Display(Name = "Budget")]
        public decimal PriceLimit { get; set; }

        [Required]
        [Display(Name = "Date Of Next Budget Refresh")]
        public Nullable<System.DateTime> TimeRefresh { get; set; }

        public int?  DefaultGroupId { get; set; }

        public int? TotalNumberOfMembers { get; set; }

        [Display(Name = "Current Budget Left")]
        public decimal CurrentBudgetLeft { get; set; }

        public List<UserGroupProductsVM> UserGroupProducts { get; set; }

    }

    public class UserGroupProductsVM
    {
        public int Id { get; set; }
        public int UserGroupId { get; set; }
        public int ProductId { get; set; }
        public int StoreFrontId { get; set; }
        public int EnableMinQty { get; set; }
        public int MinQty { get; set; }
        public int EnableMaxQty { get; set; }
        public int MaxQty { get; set; }
        public string ProductCode { get; set; }

    }

}