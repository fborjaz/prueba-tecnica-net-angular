using CashManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CashManagement.Application.Interfaces;

/// <summary>Abstracción del contexto de datos que usa la capa de aplicación.</summary>
public interface IApplicationDbContext
{
    DbSet<Rol> Roles { get; }
    DbSet<UserStatus> UserStatuses { get; }
    DbSet<User> Users { get; }
    DbSet<PasswordReset> PasswordResets { get; }
    DbSet<Menu> Menus { get; }
    DbSet<RolMenu> RolMenus { get; }
    DbSet<Cash> Cashes { get; }
    DbSet<UserCash> UserCashes { get; }
    DbSet<CashSession> CashSessions { get; }
    DbSet<Client> Clients { get; }
    DbSet<AttentionType> AttentionTypes { get; }
    DbSet<AttentionStatus> AttentionStatuses { get; }
    DbSet<Turn> Turns { get; }
    DbSet<Attention> Attentions { get; }
    DbSet<Service> Services { get; }
    DbSet<Device> Devices { get; }
    DbSet<MethodPayment> MethodPayments { get; }
    DbSet<StatusContract> StatusContracts { get; }
    DbSet<Contract> Contracts { get; }
    DbSet<Payment> Payments { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
