using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using WarehouseManagementSystem.DataBase;

namespace WarehouseManagementSystem.Repositories;

public class AuthRepository(WmsDbContext wmsDbContext, IConfiguration configuration) : IAuthRepository
{
    private static readonly TimeSpan TokeLifetime = TimeSpan.FromHours(8);
    private const int keySize = 64;
    private const int iterations = 350000;
    private HashAlgorithmName hashAlgorithm = HashAlgorithmName.SHA512;
    public string? RegisterNewUser(string email, string userName, string password)
    {
        //fix salt
        var hash = HashPassword(password, out var salt);
        var newUser = new Models.User()
        {
            UserName = userName,
            Email = email,
            Password = hash,
            Salt = Convert.ToHexString(salt)
        };

        var userFormDb = wmsDbContext.Users.FirstOrDefault(u => u.Email == email);
        if (userFormDb != null)
            return null;

        wmsDbContext.Users.Add(newUser);
        wmsDbContext.SaveChanges();
        
        var jwt = GenerateToken(email);
        return jwt;
    }

    public string? LogIn(string email, string password)
    {
        var userFromDb = wmsDbContext.Users.FirstOrDefault(u => u.Email == email);
        
        return !VerifyPassword(password, userFromDb.Password, Convert.FromHexString(userFromDb.Salt)) ? null : GenerateToken(email);
    }

    private string HashPassword(string password,out byte[] salt )
    {
        salt = RandomNumberGenerator.GetBytes(keySize);

        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            hashAlgorithm,
            keySize
        );
        return Convert.ToHexString(hash);
    }

    private bool VerifyPassword(string password, string hash, byte[] salt)
    {
        var hashToCompere = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, hashAlgorithm, keySize);
        return CryptographicOperations.FixedTimeEquals(hashToCompere, Convert.FromHexString(hash));
    }
    private string GenerateToken(string email)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(configuration["JwtSettings:Key"]!);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Email, email)
        };
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.Add(TokeLifetime),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var jwt = tokenHandler.WriteToken(token);
        return jwt;
    }
    
    
}