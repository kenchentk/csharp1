using StoreFront2.Data;
using StoreFront2.ViewModels;
using StoreFront2.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNet.Identity;

namespace StoreFront2.Helpers
{
    public class BudgetFunctions
    {
       
        private static StoreFront2Entities _sfDb =  new StoreFront2Entities();
           
        public static double GetSystemWideDaysRemaining(int storeFrontId, int BudgetRefreshSystemWide, DateTime BudgetNextRefreshDate)
        {
            double systemWideDaysRemaining = 0;

            //SystemSetting systemSetting = _sfDb.SystemSettings.Where(ss => ss.StoreFrontId == storeFrontId).FirstOrDefault();

            //if (systemSetting.BudgetRefreshSystemWide == 1)
            if (BudgetRefreshSystemWide == 1)
            {
                //DateTime systemWideNextBudgetRefresh = new DateTime(DateTime.Now.Year, systemSetting.BudgetRefreshStartDate.Month, systemSetting.BudgetRefreshStartDate.Day);
                //DateTime systemWideNextBudgetRefresh = new DateTime(systemSetting.BudgetNextRefreshDate.Year, systemSetting.BudgetNextRefreshDate.Month, systemSetting.BudgetNextRefreshDate.Day);
                DateTime systemWideNextBudgetRefresh = new DateTime(BudgetNextRefreshDate.Year, BudgetNextRefreshDate.Month, BudgetNextRefreshDate.Day);

                if (DateTime.Compare(DateTime.Now.Date, systemWideNextBudgetRefresh.Date) < 0)
                {
                    systemWideDaysRemaining = systemWideNextBudgetRefresh.Subtract(DateTime.Now.Date).TotalDays;
                }
                else
                {
                    systemWideNextBudgetRefresh = new DateTime(DateTime.Now.Year + 1, systemWideNextBudgetRefresh.Month, systemWideNextBudgetRefresh.Day);
                    systemWideDaysRemaining = systemWideNextBudgetRefresh.Subtract(DateTime.Now.Date).TotalDays;
                }
            }
            return systemWideDaysRemaining;
        }

        public static double GetUserBudgetResetDaysRemaining(int storeFrontId, string aspNetUserId, DateTime BudgetNextResetDate)
        {
            double userBudgetResetDaysRemaining = 0;

            //SystemSetting systemSetting = _sfDb.SystemSettings.Where(ss => ss.StoreFrontId == storeFrontId).FirstOrDefault();
            //UserSetting userSetting = _sfDb.UserSettings.Where(us => us.StoreFrontId == storeFrontId && us.AspNetUserId == aspNetUserId).FirstOrDefault();

            //DateTime userNextBudgetRefreshDate = userSetting.BudgetLastResetDate.AddDays(Convert.ToDouble(userSetting.BudgetResetInterval)).Date;
            //DateTime userNextBudgetRefreshDate = userSetting.BudgetNextResetDate;
            DateTime userNextBudgetRefreshDate = BudgetNextResetDate;
            if (DateTime.Compare(DateTime.Now.Date, userNextBudgetRefreshDate.Date) < 0)
            {
                userBudgetResetDaysRemaining = userNextBudgetRefreshDate.Subtract(DateTime.Now.Date).TotalDays;
            }
            return userBudgetResetDaysRemaining;
        }

        public static int GetRemainingBudgetDaysUntilRefresh(int storeFrontId, string aspNetUserId, int BudgetRefreshSystemWide, DateTime BudgetNextRefreshDate, DateTime BudgetNextResetDate)
        {
            double systemWideDaysRemaining = 0;
            double userBudgetResetDaysRemaining = 0;
            int budgetDaysUntilRefresh = 0;

            systemWideDaysRemaining = BudgetFunctions.GetSystemWideDaysRemaining(storeFrontId, BudgetRefreshSystemWide, BudgetNextRefreshDate);
            userBudgetResetDaysRemaining = BudgetFunctions.GetUserBudgetResetDaysRemaining(storeFrontId, aspNetUserId, BudgetNextResetDate);

            //SystemSetting systemSetting = _sfDb.SystemSettings.Where(ss => ss.StoreFrontId == storeFrontId).FirstOrDefault();

            if (BudgetRefreshSystemWide == 1)
            {
                //budgetDaysUntilRefresh = Convert.ToInt32(systemWideDaysRemaining < userBudgetResetDaysRemaining ? systemWideDaysRemaining : userBudgetResetDaysRemaining);
                budgetDaysUntilRefresh = Convert.ToInt32(systemWideDaysRemaining);
            }
            else
            {
                budgetDaysUntilRefresh = Convert.ToInt32(userBudgetResetDaysRemaining);
            }

            return budgetDaysUntilRefresh;
        }


        //public static int GetOrdersCountedAgainstBudget(int storeFrontId, string aspNetUserId)
        //{
        //    int numberOfOrdersCountedAgainstBudget = 0;
        //    DateTime lastRefreshDate = DateTime.Now.Date;

        //    UserSetting userSetting = _sfDb.UserSettings.Where(us => us.StoreFrontId == storeFrontId && us.AspNetUserId == aspNetUserId).FirstOrDefault();
        //    lastRefreshDate = userSetting.BudgetLastResetDate.Date;

        //    List<Order> ordersCounted = _sfDb.Orders.Where(o => o.DateCreated >= lastRefreshDate && o.UserId == aspNetUserId && o.StoreFrontId == storeFrontId).ToList();
        //    numberOfOrdersCountedAgainstBudget = ordersCounted.Count();

        //    return numberOfOrdersCountedAgainstBudget;
        //}

        //public static int GetBudgetRemaining(int storeFrontId, string aspNetUserId, UserGroupUser userGroupUser)
        public static int GetBudgetRemaining(int storeFrontId, string aspNetUserId)
        {
            int totalBudgetRemaining = 0;
            UserGroupUser userGroupUser = _sfDb.UserGroupUsers.Where(y => y.AspNetUserId == aspNetUserId && y.StoreFrontId == storeFrontId).FirstOrDefault();
            
            if (userGroupUser != null)
            {
                totalBudgetRemaining = Convert.ToInt32(_sfDb.UserGroups.Where(x => x.Id == userGroupUser.UserGroupId && x.StoreFrontId == storeFrontId).FirstOrDefault().CurrentBudgetLeft);
            }
            else
            {
                totalBudgetRemaining = 0;
            }
            //UserGroupUsers userGroupUser _sfDb.UserGroupUsers.Where(y => y.AspNetUserId == _site.AspNetUserId && x.StoreFrontId == _site.StoreFrontId).FirstOrDefault().UserGroupId

            return totalBudgetRemaining;
        }

        //public static int GetTotalGroupBudget(int storeFrontId, string aspNetUserId, UserGroupUser userGroupUser)
        public static int GetTotalGroupBudget(int storeFrontId, string aspNetUserId)
        {
            int totalBudgetRemaining = 0;
            UserGroupUser userGroupUser = _sfDb.UserGroupUsers.Where(y => y.AspNetUserId == aspNetUserId && y.StoreFrontId == storeFrontId).FirstOrDefault();

            if (userGroupUser != null)
            {
                totalBudgetRemaining = Convert.ToInt32(_sfDb.UserGroups.Where(x => x.Id == userGroupUser.UserGroupId && x.StoreFrontId == storeFrontId).FirstOrDefault().PriceLimit);
            }
            else
            {
                totalBudgetRemaining = 0;
            }
            //UserGroupUsers userGroupUser _sfDb.UserGroupUsers.Where(y => y.AspNetUserId == _site.AspNetUserId && x.StoreFrontId == _site.StoreFrontId).FirstOrDefault().UserGroupId

            return totalBudgetRemaining;
        }

        public static string UserGroupsList(int storeFrontId, string aspNetUserId)
        {
            string strUserGroupsList = "";
            //UserGroupUser userGroupUser = _sfDb.UserGroupUsers.Where(y => y.AspNetUserId == aspNetUserId && y.StoreFrontId == storeFrontId).FirstOrDefault();   
            //List<Order> ordersCounted = _sfDb.Orders.Where(o => o.DateCreated >= lastRefreshDate && o.UserId == aspNetUserId && o.StoreFrontId == storeFrontId).ToList();
            List<GroupsViewModel> GroupsAssigned = (from g in _sfDb.UserGroups
                                              join ugu in _sfDb.UserGroupUsers on g.Id equals ugu.UserGroupId
                                              where ugu.AspNetUserId == aspNetUserId && ugu.StoreFrontId == storeFrontId
                                              select new GroupsViewModel()
                                              {
                                                  Name = g.Name,
                                                  Desc = g.Desc
                                              }).ToList();

            foreach (var c in GroupsAssigned)
            {
                strUserGroupsList = strUserGroupsList + c.Name + " | ";
            }

            //strUserGroupsList = strUserGroupsList.Substring(0, strUserGroupsList.Length - 2);
            return strUserGroupsList;
        }

    }
}
