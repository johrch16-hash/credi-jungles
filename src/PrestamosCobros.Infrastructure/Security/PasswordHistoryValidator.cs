using Microsoft.AspNetCore.Identity;
using PrestamosCobros.DAL.Entities;

namespace PrestamosCobros.Infrastructure.Security;

public class PasswordHistoryValidator<TUser> : IPasswordValidator<TUser> where TUser : Usuario
{
    public async Task<IdentityResult> ValidateAsync(UserManager<TUser> manager, TUser user, string? password)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(user.PasswordHash)) 
            return IdentityResult.Success;

        var verify = manager.PasswordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verify == PasswordVerificationResult.Success)
        {
            return IdentityResult.Failed(new IdentityError 
            { 
                Code = "PasswordHistory", 
                Description = "La nueva contraseña no puede ser igual a la actual." 
            });
        }
        
        return IdentityResult.Success;
    }
}
