using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNet.Identity;
using Microsoft.Owin.Security;
using System.ComponentModel;
using StoreFront2.Models;
using System;

namespace StoreFront2.ViewModels
{
    public class MyWindowViewModel
    {
        public string Id { get; set; }

        public OrderStatusTotal OrderStatus { get; set; }

        public int TotalMonthlyOrder { get; set; }

        public int TotalMonthlyShip { get; set; }

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
        public DateTime BudgetLastResetDate { get; set; }
        public DateTime BudgetNextResetDate { get; set; }

        [DisplayName("Current Available Budget")]
        [DataType(DataType.Currency)]
        [DisplayFormat(DataFormatString = "{0:c}")]
        public decimal BudgetCurrentAvailable { get; set; }

        [DisplayName("Days Until Budget Refresh")]
        [DataType(DataType.Text)]
        public int BudgetDaysUntilRefresh { get; set; }
    }
}
