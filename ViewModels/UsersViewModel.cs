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
    public class UsersViewModel
    {
        [Key]
        public int Id { get; set; }

        [HiddenInput]
        public string AspNetUserId { get; set; }

        //[Required]
        [Display(Name = "User Role")]
        public string UserRole { get; set; }

        [Display(Name = "Allow Admin Access")]
        public bool AllowAdminAccess { get; set; }

        [Display(Name = "Store Currency Default")]
        public string DefaultCurrency { get; set; }
        
        [Required]
        [Display(Name = "User Name")]
        public string UserName { get; set; }

        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }

        //[Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [System.ComponentModel.DataAnnotations.Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }

        [DisplayName("Company")]
        public string Company { get; set; }

        [DisplayName("Company Alias")]
        public string CompanyAlias { get; set; }

        [Required]
        [DisplayName("First Name")]
        public string FirstName { get; set; }

        [Required]
        [DisplayName("Last Name")]
        public string LastName { get; set; }

        [Required]
        [DisplayName("Address Line 1")]
        public string Address1 { get; set; }

        [DisplayName("Address Line 2")]
        public string Address2 { get; set; }

        [Required]
        public string City { get; set; }
        [Required]
        public string State { get; set; }
        [Required]
        public string Zip { get; set; }
        [Required]
        public string Country { get; set; }
        public string Phone { get; set; }

        [DisplayName("Access Restricted")]
        public bool AccessRestricted { get; set; }

        public short Status { get; set; }

        public int StoreFrontId { get; set; }

        [DisplayName("Facility Id")]
        public int FacilityId { get; set; }

        [DisplayName("Projects are submitted")]
        public bool AlertProjectSubmitted { get; set; }

        [DisplayName("Projects are approved")]
        public bool AlertProjectApproved { get; set; }

        [DisplayName("RECEIVE AN ALERT FOR ALL ORDERS RECEIVED")]
        public bool AlertOrderReceived { get; set; }

        [DisplayName("RECEIVE AN ALERT FOR ALL ORDERS SHIPPED")]
        public bool AlertOrderShipped { get; set; }

        [DisplayName("RECEIVE AN ALERT ON BUDGET REFRESH REQUEST")]
        public bool AlertOnBudgetRefreshRequest { get; set; }

        public string AlertOrderReceivedFor { get; set; }
        public string AlertOrderShippedFor { get; set; }

        [DisplayName("Reset Budget After")]
        [Range(0, 365, ErrorMessage = "Value for {0} must be between {1} and {2}.")]
        public string BudgetRefreshPeriod { get; set; }

        [DisplayName("Current Budget Limit")]
        [DataType(DataType.Currency)]
        [DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{0:c}")]
        public decimal BudgetLimit { get; set; }

        [DisplayName("Budget Used")]
        [DataType(DataType.Currency)]
        [DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{0:c}")]
        public decimal BudgetCurrentTotal { get; set; }

        [DisplayName("Days Until Refresh")]
        [DataType(DataType.Text)]
        public int BudgetDaysUntilRefresh { get; set; }

        [DisplayName("Place New Order On Hold")]
        public bool OnHold { get; set; }

        [DisplayName("Current Budget Method")]
        [DataType(DataType.Text)]
        public string BudgetMethod { get; set; }

        public bool BudgetEnforce { get; set; }        

        [DisplayName("System Default Budget")]
        [DataType(DataType.Currency)]
        public decimal DefaultBudget { get; set; }

        [DisplayName("Budget Remaning")]
        [DataType(DataType.Currency)]
        public decimal BudgetRemaning { get; set; }

        [DisplayName("Override Budget")]
        [DataType(DataType.Currency)]
        public decimal AdditionalBudgetLimit { get; set; }

        public IEnumerable<SelectListItem> Users { get; set; }

        public List<StoreFront> StoreFronts { get; set; }
        public UserPermission Permission { get; set; }
        public UserSetting Settings { get; set; }

        public List<UserCategoriesVM> UserCategories { get; set; }

        public string UserGroupsList { get; set; }
    }

    public class UserCategoriesVM
    {
        public int Id { get; set; }
        public string AspNetUserId { get; set; }
        public int CategoryId { get; set; }
        public string Name { get; set; }
        public string Desc { get; set; }
    }

}
