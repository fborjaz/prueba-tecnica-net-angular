using CashManagement.Domain.Constants;
using CashManagement.Domain.Entities;
using CashManagement.Domain.Exceptions;

namespace CashManagement.Tests.Domain;

public class ContractTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Un_contrato_nuevo_queda_vigente_por_los_meses_indicados()
    {
        var contract = Contract.Create(clientId: 1, serviceId: 2, methodPaymentId: 1, months: 12, Now, createdBy: 3);

        Assert.Equal(ContractStatuses.Active, contract.StatusId);
        Assert.Equal(Now.AddMonths(12), contract.EndDate);
        Assert.True(contract.IsInForce);
    }

    [Fact]
    public void Un_contrato_debe_durar_al_menos_un_mes()
    {
        Assert.Throws<BusinessException>(() => Contract.Create(1, 2, 1, months: 0, Now, null));
    }

    [Fact]
    public void Cancelar_cambia_el_estado_a_CAN_y_la_fecha_fin_a_hoy()
    {
        var contract = Contract.Create(1, 2, 1, 12, Now, null);

        contract.Cancel(Now.AddDays(10));

        Assert.Equal(ContractStatuses.Cancelled, contract.StatusId);
        Assert.Equal(Now.AddDays(10), contract.EndDate);
        Assert.False(contract.IsInForce);
    }

    [Theory]
    [InlineData(ContractStatuses.Cancelled)]
    [InlineData(ContractStatuses.Replaced)]
    public void No_se_cancela_un_contrato_que_no_esta_vigente(string status)
    {
        var contract = new Contract { StatusId = status };

        Assert.Throws<BusinessException>(() => contract.Cancel(Now));
    }

    [Fact]
    public void La_nueva_forma_de_pago_debe_ser_distinta()
    {
        var contract = Contract.Create(1, 2, methodPaymentId: 1, 12, Now, null);

        Assert.Throws<BusinessException>(() => contract.ChangePaymentMethod(1));

        contract.ChangePaymentMethod(4);
        Assert.Equal(4, contract.MethodPaymentId);
    }
}

public class AttentionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 9, 0, 0, TimeSpan.Zero);

    private static Attention Pending() => new() { StatusId = AttentionStatuses.Pending };

    [Fact]
    public void El_flujo_normal_es_pendiente_en_atencion_atendido()
    {
        var attention = Pending();

        attention.Start(cashierId: 3);
        Assert.Equal(AttentionStatuses.InProgress, attention.StatusId);

        attention.Complete(cashierId: 3, clientId: 1, notes: "  Pago mensual  ", Now);
        Assert.Equal(AttentionStatuses.Attended, attention.StatusId);
        Assert.Equal("Pago mensual", attention.Notes);
        Assert.Equal(Now, attention.AttendedAt);
    }

    [Fact]
    public void No_se_llama_dos_veces_el_mismo_turno()
    {
        var attention = Pending();
        attention.Start(3);

        Assert.Throws<BusinessException>(() => attention.Start(4));
    }

    [Fact]
    public void Solo_finaliza_el_cajero_que_tiene_el_turno()
    {
        var attention = Pending();
        attention.Start(cashierId: 3);

        Assert.Throws<BusinessException>(() => attention.Complete(cashierId: 4, clientId: 1, null, Now));
    }

    [Fact]
    public void Anular_sin_observacion_registra_que_el_cliente_no_se_presento()
    {
        var attention = Pending();

        attention.Cancel(cashierId: 3, notes: null, Now);

        Assert.Equal(AttentionStatuses.Cancelled, attention.StatusId);
        Assert.Equal("Cliente no se presentó.", attention.Notes);
    }

    [Fact]
    public void No_se_anula_un_turno_ya_atendido()
    {
        var attention = Pending();
        attention.Start(3);
        attention.Complete(3, 1, null, Now);

        Assert.Throws<BusinessException>(() => attention.Cancel(3, null, Now));
    }
}

public class CashTests
{
    [Fact]
    public void Una_caja_admite_como_maximo_dos_cajeros()
    {
        Cash.EnsureRoomForCashier(0);
        Cash.EnsureRoomForCashier(1);

        Assert.Throws<ConflictException>(() => Cash.EnsureRoomForCashier(2));
    }

    [Fact]
    public void No_se_cierra_dos_veces_la_misma_sesion_de_caja()
    {
        var session = new CashSession { OpenedAt = DateTimeOffset.UtcNow };

        session.Close(DateTimeOffset.UtcNow);

        Assert.False(session.IsOpen);
        Assert.Throws<BusinessException>(() => session.Close(DateTimeOffset.UtcNow));
    }
}
