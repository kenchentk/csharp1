using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNet.Identity;
using Microsoft.Owin.Security;
using System.ComponentModel;

namespace StoreFront2.Models
{
    public class IndexViewModel
    {
        public string Id { get; set; }
        public int SfId { get; set; }
        public int StoreFrontId { get; set; }
        public bool HasPassword { get; set; }
        public IList<UserLoginInfo> Logins { get; set; }
        public string PhoneNumber { get; set; }
        public bool TwoFactor { get; set; }
        public bool BrowserRemembered { get; set; }

        [Display(Name = "User Name")]
        [Required(ErrorMessage = "User Name is Required")]
        public string UserName { get; set; }

        [Display(Name = "Company")]
        public string Company { get; set; }

        [Display(Name = "Company Alias")]
        public string CompanyAlias { get; set; }

        [Display(Name = "First Name")]
        [Required(ErrorMessage = "First Name is Required")]
        public string FirstName { get; set; }

        [Display(Name = "Last Name")]
        [Required(ErrorMessage = "Last Name is Required")]
        public string LastName { get; set; }

        [Display(Name = "Address Line 1")]
        [Required(ErrorMessage = "First Address Line is Required")]
        public string Address1 { get; set; }

        [Display(Name = "Address Line 2")]
        public string Address2 { get; set; }

        [Display(Name = "City")]
        [Required(ErrorMessage = "City is Required")]
        public string City { get; set; }

        [Display(Name = "State")]
        [Required(ErrorMessage = "State is Required")]
        public string State { get; set; }

        [Display(Name = "Zip")]
        [Required(ErrorMessage = "Zip is Required")]
        public string Zip { get; set; }

        [Display(Name ="Phone Number")]
        [Required(ErrorMessage = "Phone number is Required")]
        [RegularExpression(@"^\(?([0-9]{3})\)?[-. ]?([0-9]{3})[-. ]?([0-9]{4})$", ErrorMessage = "Not a valid phone number")]
        [DataType(DataType.PhoneNumber)]
        public string Phone { get; set; }

        [DisplayName("Orders are placed")]
        public bool AlertOrderReceived { get; set; }

        [DisplayName("Orders are shipped")]
        public bool AlertOrderShipped { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Current password")]
        public string OldPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "New password")]
        public string NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm new password")]
        public string ConfirmPassword { get; set; }

        [DisplayName("Current Budget Limit")]
        [DataType(DataType.Currency)]
        [DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{0:c}")]
        public decimal BudgetLimit { get; set; }

        [DisplayName("Budget Used")]
        [DataType(DataType.Currency)]
        [DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{0:c}")]
        public decimal BudgetCurrentTotal { get; set; }

        [DisplayName("No. of Orders Counting Against Budget")]
        public int OrdersCountingAgainstBudget { get; set; }

        [DisplayName("Current Available Budget")]
        [DataType(DataType.Currency)]
        [DisplayFormat(DataFormatString = "{0:c}")]
        public decimal BudgetCurrentAvailable { get; set; }

        [DisplayName("Days Until Budget Refresh")]
        [DataType(DataType.Text)]
        public int BudgetDaysUntilRefresh { get; set; }

    }

    public class ManageLoginsViewModel
    {
        public IList<UserLoginInfo> CurrentLogins { get; set; }
        public IList<AuthenticationDescription> OtherLogins { get; set; }
    }

    public class FactorViewModel
    {
        public string Purpose { get; set; }
    }

    public class SetPasswordViewModel
    {
        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "New password")]
        public string NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm new password")]
        [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }
    }

    public class ChangePasswordViewModel
    {
        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Current password")]
        public string OldPassword { get; set; }

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "New password")]
        public string NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm new password")]
        [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }
    }

    public class AddPhoneNumberViewModel
    {
        [Required]
        [Phone]
        [Display(Name = "Phone Number")]
        public string Number { get; set; }
    }

    public class VerifyPhoneNumberViewModel
    {
        [Required]
        [Display(Name = "Code")]
        public string Code { get; set; }

        [Required]
        [Phone]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; }
    }

    public class ConfigureTwoFactorViewModel
    {
        public string SelectedProvider { get; set; }
        public ICollection<System.Web.Mvc.SelectListItem> Providers { get; set; }
    }
}