using CashManagement.Application.Features.Users;

namespace CashManagement.Tests.Application;

/// <summary>Reglas de validación del enunciado para el registro de usuarios.</summary>
public class CreateUserValidatorTests
{
    private readonly CreateUserRequestValidator _validator = new();

    private static CreateUserRequest Valid() =>
        new("cajero2027", "Rosa Elena", "Quinde Vera", "rosa.quinde@correo.com", "0956789012", "Cajero2027", 3);

    [Fact]
    public void Un_usuario_valido_pasa()
    {
        Assert.True(_validator.Validate(Valid()).IsValid);
    }

    [Theory]
    [InlineData("abc1")]                    // menos de 8 caracteres
    [InlineData("usuariodemasiadolargo123")] // más de 20
    [InlineData("cajero_2027")]             // carácter especial
    [InlineData("cajerosinnumero")]         // sin número
    public void El_nombre_de_usuario_cumple_las_reglas(string username)
    {
        var result = _validator.Validate(Valid() with { Username = username });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateUserRequest.Username));
    }

    [Theory]
    [InlineData("corta1A")]      // menos de 8
    [InlineData("sinnumeroA")]   // sin número
    [InlineData("sinmayuscula1")] // sin mayúscula
    public void La_contrasena_cumple_las_reglas(string password)
    {
        var result = _validator.Validate(Valid() with { Password = password });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateUserRequest.Password));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("09123456AB")]
    public void La_identificacion_solo_admite_de_10_a_13_digitos(string identification)
    {
        var result = _validator.Validate(Valid() with { Identification = identification });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateUserRequest.Identification));
    }

    [Fact]
    public void El_correo_debe_tener_formato_valido()
    {
        var result = _validator.Validate(Valid() with { Email = "correo-invalido" });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateUserRequest.Email));
    }
}
