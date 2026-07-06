using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Contracts.DTOs.User;
using ErrorOr;
using Domain.Errors;

namespace SampleCkWebApp.Application.Users
{
    public class AuthValidator
    {
        public ErrorOr<Success> ValidateRegistration(RegisterUserDto request)
        {
            var errors = new List<Error>();

            //  Validate Name (full name)
            if (string.IsNullOrWhiteSpace(request.Username))
                errors.Add(UserErrors.NameRequired);
            else if (request.Username.Length > 50)
                errors.Add(UserErrors.InvalidName);

            //  Validate Email
            if (string.IsNullOrWhiteSpace(request.Email))
                errors.Add(UserErrors.EmailRequired);
            else if (!IsValidEmail(request.Email))
                errors.Add(UserErrors.InvalidEmail);

            //  Validate Password
            if (string.IsNullOrWhiteSpace(request.Password))
                errors.Add(UserErrors.PasswordRequired);
            else if (request.Password.Length < 8)
                errors.Add(UserErrors.InvalidPassword);
            
            //  Validate CurrencyId
            if (request.CurrencyId <= 0)
                errors.Add(UserErrors.InvalidCurrency);
            

            return errors.Count > 0 ? errors : Result.Success;
        }

        public ErrorOr<Success> ValidateLogin(LoginUserDto request)
        {
            var errors = new List<Error>();

            //  Validate Email
            if (string.IsNullOrWhiteSpace(request.Email))
                errors.Add(UserErrors.EmailRequired);
            else if (!IsValidEmail(request.Email))
                errors.Add(UserErrors.InvalidEmail);

            //  Validate Password
            if (string.IsNullOrWhiteSpace(request.Password))
                errors.Add(UserErrors.PasswordRequired);
            else if (request.Password.Length < 8)
                errors.Add(UserErrors.InvalidPassword);

            return errors.Count > 0 ? errors : Result.Success;
        }

        public ErrorOr<Success> ValidateChangePassword(ChangePasswordDto request)
        {
            var errors = new List<Error>();

            // Validate current password
            if (string.IsNullOrWhiteSpace(request.CurrentPassword))
                errors.Add(Error.Validation("ChangePassword.CurrentPasswordEmpty", "Current password is required"));

            // Validate new password
            if (string.IsNullOrWhiteSpace(request.NewPassword))
                errors.Add(Error.Validation("ChangePassword.NewPasswordEmpty", "New password is required"));

            if (request.NewPassword?.Length < 6)
                errors.Add(Error.Validation("ChangePassword.NewPasswordTooShort", "New password must be at least 6 characters"));

            if (request.NewPassword?.Length > 100)
                errors.Add(Error.Validation("ChangePassword.NewPasswordTooLong", "New password must not exceed 100 characters"));

            // Validate confirm password
            if (string.IsNullOrWhiteSpace(request.ConfirmPassword))
                errors.Add(Error.Validation("ChangePassword.ConfirmPasswordEmpty", "Confirm password is required"));

            // Check if passwords match
            if (request.NewPassword != request.ConfirmPassword)
                errors.Add(Error.Validation("ChangePassword.PasswordsMismatch", "New password and confirm password do not match"));

            // Check if new password is different from current
            if (request.CurrentPassword == request.NewPassword)
                errors.Add(Error.Validation("ChangePassword.SamePassword", "New password must be different from current password"));

            return errors.Count > 0 ? errors : Result.Success;
        }
        

        private static bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }
}
