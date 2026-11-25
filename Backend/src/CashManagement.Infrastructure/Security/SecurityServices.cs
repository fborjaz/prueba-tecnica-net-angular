using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CashManagement.Application.Interfaces;
using CashManagement.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CashManagement.Infrastructure.Security;

public class JwtOptions
{
    public const string Section = "Jwt";

    public string Issuer { get; set; } = "CashManagement.Api";
    public string Audience { get; set; } = "CashManagement.Web";
    /// <summary>Clave de firma (HMAC-SHA256).</summary>
    public string SigningKey { get; set; } = string.Empty;
    /// <summary>Clave de cifrado del token (JWE A256CBC-HS512).</summary>
    public string EncryptionKey { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 60;

    // Las claves se derivan con SHA para obtener siempre el tamaño exacto que exige cada algoritmo.
    public SymmetricSecurityKey GetSigningKey() => new(SHA256.HashData(Encoding.UTF8.GetBytes(SigningKey)));

    public SymmetricSecurityKey GetEncryptionKey() => new(SHA512.HashData(Encoding.UTF8.GetBytes(EncryptionKey)));
}

public class EncryptionOptions
{
    public const string Section = "Encryption";

    /// <summary>Clave AES-256 para correo e identificación de usuarios.</summary>
    public string Key { get; set; } = string.Empty;
    /// <summary>Clave HMAC para los hashes de búsqueda.</summary>
    public string HashKey { get; set; } = string.Empty;
}

/// <summary>
/// JWT firmado (HS256) y cifrado (JWE "dir" + A256CBC-HS512). Las claims (id, usuario, rol)
/// viajan encriptadas: el token no puede leerse sin la clave del servidor.
/// </summary>
public class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
{
    public TokenResult CreateToken(User user, string roleName)
    {
        var jwt = options.Value;
        var expires = DateTimeOffset.UtcNow.AddMinutes(jwt.ExpirationMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = DateTime.UtcNow,
            NotBefore = DateTime.UtcNow,
            Expires = expires.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("role", roleName),
                new Claim("name", user.FullName)
            ]),
            SigningCredentials = new SigningCredentials(jwt.GetSigningKey(), SecurityAlgorithms.HmacSha256),
            EncryptingCredentials = new EncryptingCredentials(
                jwt.GetEncryptionKey(), JwtConstants.DirectKeyUseAlg, SecurityAlgorithms.Aes256CbcHmacSha512)
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new TokenResult(token, expires);
    }
}

public class BCryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11);

    public bool Verify(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }
}

/// <summary>
/// AES-256-CBC con IV aleatorio por valor (el IV se antepone al texto cifrado) y
/// HMAC-SHA256 determinístico para poder buscar y validar unicidad sin descifrar.
/// </summary>
public class AesFieldEncryptor : IFieldEncryptor
{
    private readonly byte[] _key;
    private readonly byte[] _hashKey;

    public AesFieldEncryptor(IOptions<EncryptionOptions> options)
    {
        if (string.IsNullOrWhiteSpace(options.Value.Key) || string.IsNullOrWhiteSpace(options.Value.HashKey))
        {
            throw new InvalidOperationException("Debe configurar Encryption:Key y Encryption:HashKey.");
        }

        _key = SHA256.HashData(Encoding.UTF8.GetBytes(options.Value.Key));
        _hashKey = SHA256.HashData(Encoding.UTF8.GetBytes(options.Value.HashKey));
    }

    public string Encrypt(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();

        var cipher = aes.EncryptCbc(Encoding.UTF8.GetBytes(plainText), aes.IV);
        return Convert.ToBase64String([.. aes.IV, .. cipher]);
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return string.Empty;

        var data = Convert.FromBase64String(cipherText);
        using var aes = Aes.Create();
        aes.Key = _key;

        var iv = data[..16];
        var plain = aes.DecryptCbc(data[16..], iv);
        return Encoding.UTF8.GetString(plain);
    }

    public string Hash(string value) =>
        Convert.ToHexString(HMACSHA256.HashData(_hashKey, Encoding.UTF8.GetBytes(value)));
}
