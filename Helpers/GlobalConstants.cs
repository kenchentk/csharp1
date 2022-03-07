using StoreFront2.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace StoreFront2.Helpers
{
    public class GlobalConstants
    {
        public static List<OrderStatus> OrderStatuses;

        static GlobalConstants()
        {
            // Initialize order status
            OrderStatuses = new List<OrderStatus>();
            int index = 0;
            foreach (KeyValuePair<string, string> os in GlobalConstants.OrderStatusDictionary)
            {
                index++;
                OrderStatuses.Add(new OrderStatus() { Id = index, Code = os.Key, Desc = os.Value });
            }
        }

        public static readonly Dictionary<string, string> OrderStatusDictionary = new Dictionary<string, string>()
        {
            {"RP", "Order Received"},
            {"IP", "Order In Process"},
            {"PS", "Order Pending Shipment"},
            {"PH", "Order Partially Shipped"},
            {"SH", "Order Shipped"},
            {"OH", "Order On Hold"},
            {"CN", "Order Canceled"},
            {"RT", "Order Returned"},
        };

        public enum Statuses
        {
            Active,
            Inactive
        }

        // Permission static strings
        public static string AccessAdminUserModify = "AdminUserModify";
        public static string AccessAdminUserGroupModify = "AdminUserGroupModify";
        public static string AccessAdminUserCategoryModify = "AdminUserCategoryModify";
        public static string AccessAdminSettingModify = "AdminSettingModify";
        public static string AccessInventoryItemModify = "InventoryItemModify";
        public static string AccessInventoryRestrictCategory = "InventoryRestrictCategory";
        public static string AccessInventoryCategoryModify = "InventoryCategoryModify";
        public static string AccessOrderRestrictShipMethod = "OrderRestrictShipMethod";
        public static string AccessOrderCreate = "OrderCreate";
        public static string AccessOrderCancel = "OrderCancel";

    }
}