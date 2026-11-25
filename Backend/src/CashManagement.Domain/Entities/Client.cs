using CashManagement.Domain.Common;

namespace CashManagement.Domain.Entities;

public class Client : ISoftDelete
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Identification { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string ReferenceAddress { get; set; } = string.Empty;
    public bool Active { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public int? UserCreateId { get; set; }
    public bool IsDeleted { get; set; }

    public ICollection<Contract> Contracts { get; set; } = [];
}
