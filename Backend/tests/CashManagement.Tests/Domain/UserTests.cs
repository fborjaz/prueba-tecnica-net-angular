using CashManagement.Domain.Constants;
using CashManagement.Domain.Entities;
using CashManagement.Domain.Exceptions;

namespace CashManagement.Tests.Domain;

public class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 9, 0, 0, TimeSpan.Zero);

    private static User ActiveUser() => new() { Username = "cajero01", StatusId = UserStatuses.Active };

    [Fact]
    public void Se_bloquea_al_tercer_intento_fallido()
    {
        var user = ActiveUser();

        Assert.Equal(2, user.RegisterFailedLogin());
        Assert.Equal(1, user.RegisterFailedLogin());
        Assert.False(user.IsBlocked);

        Assert.Equal(0, user.RegisterFailedLogin());
        Assert.True(user.IsBlocked);
        Assert.Equal(UserStatuses.Blocked, user.StatusId);
    }

    [Fact]
    public void Un_login_correcto_reinicia_los_intentos_y_abre_la_sesion()
    {
        var user = ActiveUser();
        user.RegisterFailedLogin();
        user.RegisterFailedLogin();

        user.RegisterSuccessfulLogin(Now);

        Assert.Equal(0, user.FailedAttempts);
        Assert.True(user.SessionActive);
        Assert.Equal(Now, user.LastLogin);
    }

    [Fact]
    public void Cerrar_sesion_marca_la_sesion_inactiva()
    {
        var user = ActiveUser();
        user.RegisterSuccessfulLogin(Now);

        user.Logout(Now.AddHours(1));

        Assert.False(user.SessionActive);
        Assert.Equal(Now.AddHours(1), user.LastLogout);
    }

    [Fact]
    public void Recuperar_la_contrasena_desbloquea_al_usuario()
    {
        var user = ActiveUser();
        for (var i = 0; i < SecurityPolicy.MaxFailedAttempts; i++) user.RegisterFailedLogin();

        user.ResetPassword("nuevo-hash");

        Assert.Equal(UserStatuses.Active, user.StatusId);
        Assert.Equal(0, user.FailedAttempts);
        Assert.Equal("nuevo-hash", user.Password);
    }

    [Theory]
    [InlineData(UserStatuses.Inactive)]
    [InlineData(UserStatuses.Blocked)]
    public void Inactivar_o_bloquear_cierra_la_sesion(string status)
    {
        var user = ActiveUser();
        user.RegisterSuccessfulLogin(Now);

        user.ChangeStatus(status);

        Assert.Equal(status, user.StatusId);
        Assert.False(user.SessionActive);
    }

    [Fact]
    public void No_se_cambia_el_estado_de_un_usuario_pendiente()
    {
        var user = new User { StatusId = UserStatuses.Pending };

        Assert.Throws<BusinessException>(() => user.ChangeStatus(UserStatuses.Active));
    }

    [Fact]
    public void Aprobar_un_pendiente_lo_activa_y_registra_quien_aprobo()
    {
        var user = new User { StatusId = UserStatuses.Pending };

        user.Approve(approverId: 1, Now);

        Assert.Equal(UserStatuses.Active, user.StatusId);
        Assert.Equal(1, user.UserApprovalId);
        Assert.Equal(Now, user.DateApproval);
    }

    [Fact]
    public void Rechazar_un_pendiente_lo_deja_inactivo()
    {
        var user = new User { StatusId = UserStatuses.Pending };

        user.Reject(approverId: 1, Now);

        Assert.Equal(UserStatuses.Inactive, user.StatusId);
    }

    [Fact]
    public void Solo_se_aprueba_un_usuario_pendiente()
    {
        Assert.Throws<BusinessException>(() => ActiveUser().Approve(1, Now));
    }
}
