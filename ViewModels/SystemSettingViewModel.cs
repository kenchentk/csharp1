using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace StoreFront2.ViewModels
{
    public class SystemSettingViewModel
    {
        // These are fields from aspnetuser

        public int StoreFrontId { get; set; }

        // These are fields from systemsettings
        //[Display(Name = "Phone Number")]
        public string FromCompany { get; set; }
        public string FromFirstName { get; set; }
        public string FromLastName { get; set; }
        public string FromAddress1 { get; set; }
        public string FromAddress2 { get; set; }
        public string FromCity { get; set; }
        public string FromState { get; set; }
        public string FromZip { get; set; }
        public string FromCountry { get; set; }
        public string FromPhone { get; set; }

        public string ShipCompany { get; set; }
        public string ShipFirstName { get; set; }
        public string ShipLastName { get; set; }
        public string ShipAddress1 { get; set; }
        public string ShipAddress2 { get; set; }
        public string ShipCity { get; set; }
        public string ShipState { get; set; }
        public string ShipZip { get; set; }
        public string ShipCountry { get; set; }
        public string ShipPhone { get; set; }

        [DisplayName("Display Product Prices")]
        public bool DisplayProductPrices { get; set; }

        [DisplayName("Display Order Values")]
        public bool DisplayOrderValues { get; set; }

        public string DisplayOrderValuesFor { get; set; }

        [DisplayName("Display Inventory Availability")]
        public bool DisplayInventoryAvailability { get; set; }

        public string DisplayInventoryAvailabilityFor { get; set; }

        [DisplayName("Turn On/Off Product Min/Max Levels")]
        public bool TurnOnProductMinMaxLevels { get; set; }

        public string TurnOnProductMinMaxLevelsFor { get; set; }         

        [DisplayName("Disable Out of Stock Ordering")]
        public bool DisableOutOfStockOrdering { get; set; }

        [DisplayName("Restrict Categories")]
        public bool RestrictCategories { get; set; }

        [DisplayName("RECEIVE AN ALERT FOR ALL ORDERS RECEIVED")]
        public bool AlertOrderReceived { get; set; }

        [DisplayName("RECEIVE AN ALERT FOR ALL ORDERS SHIPPED")]
        public bool AlertOrderShipped { get; set; }

        [DisplayName("SEND ALERTS WHEN BUDGETS REFRESHED")]
        public bool AlertBudgetRefreshed { get; set; }

        [DisplayName("SEND ALERTS WHEN ITEMS ARE OUT OF STOCK")]
        public bool AlertItemOutOfStock { get; set; }

        [DisplayName("ENFORCE USER BUDGETS")]
        public bool BudgetEnforce { get; set; }

        [DisplayName("SYSTEM WIDE BUDGET REFRESH")]
        public bool BudgetRefreshSystemWide { get; set; }

        [DisplayName("PER USER BUDGET REFRESH")]
        public bool BudgetRefreshPerUser { get; set; }

        [DisplayName("Default Budget Refresh Period For New Users")] 
        [Range(0,365, ErrorMessage = "Value for {0} must be between {1} and {2}.")]
        public string BudgetRefreshPeriodDefault { get; set; }

        [DisplayName("Budget Refresh Starting On")]
        public DateTime BudgetRefreshStartDate { get; set; }

        [DisplayName("Default Budget For New Users")]
        [DataType(DataType.Currency)]
        [DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{0:c}")]
        public decimal BudgetLimitDefault { get; set; }

        public string LogoPath { get; set; }
        public DateTime LogoUploadDate { get; set; }

        [DisplayName("Place All Orders On Hold")]
        public bool OnHold { get; set; }

        [DisplayName("Weekly Budget Refresh Day")]
        public string BudgetRefreshDayOfTheWeek { get; set; }

        [DisplayName("ENFORCE USER BUDGETS")]
        public string BudgetType { get; set; }

        [DisplayName("Monthly Budget Refresh Day")]
        public string BudgetRefreshDayOfTheMonth { get; set; }

        [DisplayName("DEFAULY BUDGET REFRESH FREQUENCY")]
        public string DefaultBudgetRefreshFrequency { get; set; }

        [DisplayName("NEXT DATE FOR BUDGET REFRESH")]
        public DateTime BudgetNextRefreshDate { get; set; }
        
    }
}
