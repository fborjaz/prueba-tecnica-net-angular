using CashManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CashManagement.Infrastructure.Persistence.Configurations;

public class CashConfiguration : IEntityTypeConfiguration<Cash>
{
    public void Configure(EntityTypeBuilder<Cash> b)
    {
        b.ToTable("cash");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("cashid");
        b.Property(x => x.Description).HasColumnName("cashdescription").HasMaxLength(50);
        b.Property(x => x.Active).HasColumnName("active");
        b.Property(x => x.IsDeleted).HasColumnName("isdeleted");
    }
}

public class UserCashConfiguration : IEntityTypeConfiguration<UserCash>
{
    public void Configure(EntityTypeBuilder<UserCash> b)
    {
        b.ToTable("usercash");
        b.HasKey(x => new { x.UserId, x.CashId });
        b.Property(x => x.UserId).HasColumnName("user_userid");
        b.Property(x => x.CashId).HasColumnName("cash_cashid");
        b.Property(x => x.AssignedById).HasColumnName("assignedby");
        b.Property(x => x.AssignedAt).HasColumnName("assignedat");
        b.Property(x => x.Active).HasColumnName("active");
        b.HasOne(x => x.User).WithMany(u => u.Cashes).HasForeignKey(x => x.UserId);
        b.HasOne(x => x.Cash).WithMany(c => c.Users).HasForeignKey(x => x.CashId);
    }
}

public class CashSessionConfiguration : IEntityTypeConfiguration<CashSession>
{
    public void Configure(EntityTypeBuilder<CashSession> b)
    {
        b.ToTable("cashsession");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("cashsessionid");
        b.Property(x => x.CashId).HasColumnName("cash_cashid");
        b.Property(x => x.UserId).HasColumnName("user_userid");
        b.Property(x => x.OpenedAt).HasColumnName("openedat");
        b.Property(x => x.ClosedAt).HasColumnName("closedat");
        b.HasOne(x => x.Cash).WithMany(c => c.Sessions).HasForeignKey(x => x.CashId);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
    }
}

public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> b)
    {
        b.ToTable("client");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("clientid");
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(50);
        b.Property(x => x.LastName).HasColumnName("lastname").HasMaxLength(50);
        b.Property(x => x.Identification).HasColumnName("identification").HasMaxLength(13);
        b.Property(x => x.Email).HasColumnName("email").HasMaxLength(120);
        b.Property(x => x.PhoneNumber).HasColumnName("phonenumber").HasMaxLength(13);
        b.Property(x => x.Address).HasColumnName("address").HasMaxLength(100);
        b.Property(x => x.ReferenceAddress).HasColumnName("referenceaddress").HasMaxLength(100);
        b.Property(x => x.Active).HasColumnName("active");
        b.Property(x => x.CreatedAt).HasColumnName("createdat");
        b.Property(x => x.UserCreateId).HasColumnName("usercreate");
        b.Property(x => x.IsDeleted).HasColumnName("isdeleted");
    }
}

public class AttentionTypeConfiguration : IEntityTypeConfiguration<AttentionType>
{
    public void Configure(EntityTypeBuilder<AttentionType> b)
    {
        b.ToTable("attentiontype");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("attentiontypeid").HasMaxLength(3);
        b.Property(x => x.Description).HasColumnName("description").HasMaxLength(100);
    }
}

public class AttentionStatusConfiguration : IEntityTypeConfiguration<AttentionStatus>
{
    public void Configure(EntityTypeBuilder<AttentionStatus> b)
    {
        b.ToTable("attentionstatus");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("statusid").ValueGeneratedNever();
        b.Property(x => x.Description).HasColumnName("description").HasMaxLength(30);
    }
}

public class TurnConfiguration : IEntityTypeConfiguration<Turn>
{
    public void Configure(EntityTypeBuilder<Turn> b)
    {
        b.ToTable("turn");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("turnid");
        b.Property(x => x.Description).HasColumnName("description").HasMaxLength(6).IsFixedLength();
        b.Property(x => x.Date).HasColumnName("date");
        b.Property(x => x.TurnDate).HasColumnName("turndate");
        b.Property(x => x.CashId).HasColumnName("cash_cashid");
        b.Property(x => x.GestorId).HasColumnName("usergestorid");
        b.Property(x => x.AttentionTypeId).HasColumnName("attentiontype_attentiontypeid").HasMaxLength(3);
        b.Property(x => x.IsDeleted).HasColumnName("isdeleted");
        b.HasOne(x => x.Cash).WithMany().HasForeignKey(x => x.CashId);
        b.HasOne(x => x.Gestor).WithMany().HasForeignKey(x => x.GestorId);
        b.HasOne(x => x.AttentionType).WithMany().HasForeignKey(x => x.AttentionTypeId);
        b.HasOne(x => x.Attention).WithOne(a => a.Turn).HasForeignKey<Attention>(a => a.TurnId);
    }
}

public class AttentionConfiguration : IEntityTypeConfiguration<Attention>
{
    public void Configure(EntityTypeBuilder<Attention> b)
    {
        b.ToTable("attention");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("attentionid");
        b.Property(x => x.TurnId).HasColumnName("turn_turnid");
        b.Property(x => x.ClientId).HasColumnName("client_clientid");
        b.Property(x => x.AttentionTypeId).HasColumnName("attentiontype_attentiontypeid").HasMaxLength(3);
        b.Property(x => x.StatusId).HasColumnName("attentionstatus_statusid");
        b.Property(x => x.UserId).HasColumnName("user_userid");
        b.Property(x => x.AttendedAt).HasColumnName("attendedat");
        b.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(250);
        b.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId);
        b.HasOne(x => x.AttentionType).WithMany().HasForeignKey(x => x.AttentionTypeId);
        b.HasOne(x => x.Status).WithMany().HasForeignKey(x => x.StatusId);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
    }
}

public class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> b)
    {
        b.ToTable("service");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("serviceid");
        b.Property(x => x.Name).HasColumnName("servicename").HasMaxLength(100);
        b.Property(x => x.Description).HasColumnName("servicedescription").HasMaxLength(150);
        b.Property(x => x.SpeedMbps).HasColumnName("speedmbps");
        b.Property(x => x.Price).HasColumnName("price").HasPrecision(10, 2);
        b.Property(x => x.Active).HasColumnName("active");
        b.Property(x => x.IsDeleted).HasColumnName("isdeleted");
    }
}

public class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> b)
    {
        b.ToTable("device");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("deviceid");
        b.Property(x => x.Name).HasColumnName("devicename").HasMaxLength(50);
        b.Property(x => x.ServiceId).HasColumnName("service_serviceid");
        b.Property(x => x.IsDeleted).HasColumnName("isdeleted");
        b.HasOne(x => x.Service).WithMany(s => s.Devices).HasForeignKey(x => x.ServiceId);
    }
}

public class MethodPaymentConfiguration : IEntityTypeConfiguration<MethodPayment>
{
    public void Configure(EntityTypeBuilder<MethodPayment> b)
    {
        b.ToTable("methodpayment");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("methodpaymentid");
        b.Property(x => x.Description).HasColumnName("description").HasMaxLength(50);
        b.Property(x => x.Active).HasColumnName("active");
        b.Property(x => x.IsDeleted).HasColumnName("isdeleted");
    }
}

public class StatusContractConfiguration : IEntityTypeConfiguration<StatusContract>
{
    public void Configure(EntityTypeBuilder<StatusContract> b)
    {
        b.ToTable("statuscontract");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("statusid").HasMaxLength(3);
        b.Property(x => x.Description).HasColumnName("description").HasMaxLength(50);
    }
}

public class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> b)
    {
        b.ToTable("contract");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("contractid");
        b.Property(x => x.StartDate).HasColumnName("startdate");
        b.Property(x => x.EndDate).HasColumnName("enddate");
        b.Property(x => x.ServiceId).HasColumnName("service_serviceid");
        b.Property(x => x.StatusId).HasColumnName("statuscontract_statusid").HasMaxLength(3);
        b.Property(x => x.ClientId).HasColumnName("client_clientid");
        b.Property(x => x.MethodPaymentId).HasColumnName("methodpayment_methodpaymentid");
        b.Property(x => x.PreviousContractId).HasColumnName("previouscontractid");
        b.Property(x => x.CreatedAt).HasColumnName("createdat");
        b.Property(x => x.UserCreateId).HasColumnName("usercreate");
        b.Property(x => x.IsDeleted).HasColumnName("isdeleted");
        b.HasOne(x => x.Service).WithMany().HasForeignKey(x => x.ServiceId);
        b.HasOne(x => x.Status).WithMany().HasForeignKey(x => x.StatusId);
        b.HasOne(x => x.Client).WithMany(c => c.Contracts).HasForeignKey(x => x.ClientId);
        b.HasOne(x => x.MethodPayment).WithMany().HasForeignKey(x => x.MethodPaymentId);
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.ToTable("payments");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("paymentid");
        b.Property(x => x.PaymentDate).HasColumnName("paymentdate");
        b.Property(x => x.ClientId).HasColumnName("client_clientid");
        b.Property(x => x.ContractId).HasColumnName("contract_contractid");
        b.Property(x => x.MethodPaymentId).HasColumnName("methodpayment_methodpaymentid");
        b.Property(x => x.Amount).HasColumnName("amount").HasPrecision(10, 2);
        b.Property(x => x.UserId).HasColumnName("user_userid");
        b.Property(x => x.IsDeleted).HasColumnName("isdeleted");
        b.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId);
        b.HasOne(x => x.Contract).WithMany().HasForeignKey(x => x.ContractId);
        b.HasOne(x => x.MethodPayment).WithMany().HasForeignKey(x => x.MethodPaymentId);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
    }
}
