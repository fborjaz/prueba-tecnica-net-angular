using CashManagement.Domain.Common;

namespace CashManagement.Domain.Entities;

public class Service : ISoftDelete
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SpeedMbps { get; set; }
    public decimal Price { get; set; }
    public bool Active { get; set; } = true;
    public bool IsDeleted { get; set; }

    public ICollection<Device> Devices { get; set; } = [];
}

public class Device : ISoftDelete
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ServiceId { get; set; }
    public Service? Service { get; set; }
    public bool IsDeleted { get; set; }
}

public class MethodPayment : ISoftDelete
{
    public int Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool Active { get; set; } = true;
    public bool IsDeleted { get; set; }
}

public class StatusContract
{
    public string Id { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class Contract : ISoftDelete
{
    public int Id { get; set; }
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    public int ServiceId { get; set; }
    public Service? Service { get; set; }
    public string StatusId { get; set; } = string.Empty;
    public StatusContract? Status { get; set; }
    public int ClientId { get; set; }
    public Client? Client { get; set; }
    public int MethodPaymentId { get; set; }
    public MethodPayment? MethodPayment { get; set; }
    public int? PreviousContractId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int? UserCreateId { get; set; }
    public bool IsDeleted { get; set; }
}

public class Payment : ISoftDelete
{
    public int Id { get; set; }
    public DateTimeOffset PaymentDate { get; set; }
    public int ClientId { get; set; }
    public Client? Client { get; set; }
    public int ContractId { get; set; }
    public Contract? Contract { get; set; }
    public int MethodPaymentId { get; set; }
    public MethodPayment? MethodPayment { get; set; }
    public decimal Amount { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }
    public bool IsDeleted { get; set; }
}
