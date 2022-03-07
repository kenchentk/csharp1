using StoreFront2.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNet.Identity;

namespace StoreFront2.Helpers
{
    public class GlobalFunctions
    {
        private static StoreFront2Entities _sfDb = new StoreFront2Entities();

        public static void Log(int storeFrontId, string aspNetUserId, string type, string message, string data)
        {
            var _user = (from u in _sfDb.AspNetUsers
                         where u.Id == aspNetUserId
                         select u).FirstOrDefault();
            if (_user != null && _user.UserName != null && _user.UserName.Length > 0)
            {
                SystemLog entrySystemLog = new SystemLog()
                {
                    StoreFrontId = storeFrontId,
                    CreatedById = aspNetUserId,
                    CreatedBy = _user.UserName,
                    DateCreated = DateTime.Now,
                    LogType = type,
                    Description = message,
                    Data = data,
                };
                _sfDb.SystemLogs.Add(entrySystemLog);
                _sfDb.SaveChanges();
            }

        }

        //public static string GetSettings(string settingName)
        //{
        //    var _systemSettings = (from ss in _sfDb.SystemSettings
        //                           select ss).FirstOrDefault();

        //    string returnValue = "";

        //    if (settingName == GlobalConstants.GlobalMaxLoginAttempts) returnValue = _systemSettings.LoginAttemptsAllowed.ToString();
        //    if (settingName == GlobalConstants.GlobalLockoutHours) returnValue = _systemSettings.HoursToLockoutReset.ToString();

        //    return returnValue;
        //}

        private static string GeneratePassword(PasswordValidator validator)
        {
            if (validator == null)
                return null;

            bool requireNonLetterOrDigit = validator.RequireNonLetterOrDigit;
            bool requireDigit = validator.RequireDigit;
            bool requireLowercase = validator.RequireLowercase;
            bool requireUppercase = validator.RequireUppercase;

            string randomPassword = string.Empty;

            int passwordLength = validator.RequiredLength;

            Random random = new Random();
            while (randomPassword.Length != passwordLength)
            {
                int randomNumber = random.Next(48, 122);  // >= 48 && < 122 
                if (randomNumber == 95 || randomNumber == 96) continue;  // != 95, 96 _'

                char c = Convert.ToChar(randomNumber);

                if (requireDigit)
                    if (char.IsDigit(c))
                        requireDigit = false;

                if (requireLowercase)
                    if (char.IsLower(c))
                        requireLowercase = false;

                if (requireUppercase)
                    if (char.IsUpper(c))
                        requireUppercase = false;

                if (requireNonLetterOrDigit)
                    if (!char.IsLetterOrDigit(c))
                        requireNonLetterOrDigit = false;

                randomPassword += c;
            }

            if (requireDigit)
                randomPassword += Convert.ToChar(random.Next(48, 58));  // 0-9

            if (requireLowercase)
                randomPassword += Convert.ToChar(random.Next(97, 123));  // a-z

            if (requireUppercase)
                randomPassword += Convert.ToChar(random.Next(65, 91));  // A-Z

            if (requireNonLetterOrDigit)
                randomPassword += Convert.ToChar(random.Next(33, 48));  // symbols !"#$%&'()*+,-./

            return randomPassword;
        }
    }
}