using CashManagement.Application.Common;
using CashManagement.Application.Features.Users;
using CashManagement.Application.Interfaces;
using CashManagement.Domain.Entities;
using CashManagement.Domain.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CashManagement.Application.Features.Clients;

public record ClientDto(
    int Id,
    string Name,
    string LastName,
    string FullName,
    string Identification,
    string Email,
    string PhoneNumber,
    string Address,
    string ReferenceAddress,
    bool Active,
    DateTimeOffset CreatedAt,
    int ActiveContracts);

public record ClientQuery : PageQuery
{
    public bool? Active { get; init; }
}

public record SaveClientRequest(
    string Name,
    string LastName,
    string Identification,
    string Email,
    string PhoneNumber,
    string Address,
    string ReferenceAddress);

public record ChangeClientStatusRequest(bool Active);

public class SaveClientRequestValidator : AbstractValidator<SaveClientRequest>
{
    public SaveClientRequestValidator()
    {
        RuleFor(x => x.Name).ApplyPersonNameRules("Los nombres");
        RuleFor(x => x.LastName).ApplyPersonNameRules("Los apellidos");
        RuleFor(x => x.Identification).ApplyIdentificationRules();
        RuleFor(x => x.Email).ApplyEmailRules(120);

        RuleFor(x => x.PhoneNumber).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("El teléfono es obligatorio.")
            .Matches(ValidationRules.Phone)
            .WithMessage("El teléfono debe tener solo números, al menos 10 dígitos y empezar con 09.");

        RuleFor(x => x.Address).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("La dirección es obligatoria.")
            .Must(v => v.Trim().Length is >= 20 and <= 100)
            .WithMessage("La dirección debe tener entre 20 y 100 caracteres.");

        RuleFor(x => x.ReferenceAddress).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("La referencia de la dirección es obligatoria.")
            .Must(v => v.Trim().Length is >= 20 and <= 100)
            .WithMessage("La referencia de la dirección debe tener entre 20 y 100 caracteres.");
    }
}

public interface IClientService
{
    Task<PagedResult<ClientDto>> GetAllAsync(ClientQuery query, CancellationToken ct = default);
    Task<ClientDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<ClientDto> GetByIdentificationAsync(string identification, CancellationToken ct = default);
    Task<ClientDto> CreateAsync(SaveClientRequest request, CancellationToken ct = default);
    Task<ClientDto> UpdateAsync(int id, SaveClientRequest request, CancellationToken ct = default);
    Task<ClientDto> ChangeStatusAsync(int id, ChangeClientStatusRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task<BulkUploadResult> BulkCreateAsync(Stream file, string fileName, CancellationToken ct = default);
}

public class ClientService(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    ISpreadsheetReader spreadsheetReader,
    IValidator<SaveClientRequest> validator) : IClientService
{
    public async Task<PagedResult<ClientDto>> GetAllAsync(ClientQuery query, CancellationToken ct = default)
    {
        var clients = db.Clients.AsNoTracking();

        if (query.Active is not null) clients = clients.Where(c => c.Active == query.Active);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            clients = clients.Where(c =>
                c.Identification.Contains(term) ||
                c.Name.Contains(term) ||
                c.LastName.Contains(term) ||
                c.Email.Contains(term));
        }

        var total = await clients.CountAsync(ct);
        var items = await Project(clients.OrderBy(c => c.LastName).ThenBy(c => c.Name)
                .Skip((query.SafePage - 1) * query.SafePageSize)
                .Take(query.SafePageSize))
            .ToListAsync(ct);

        return new PagedResult<ClientDto>(items, total, query.SafePage, query.SafePageSize);
    }

    public async Task<ClientDto> GetByIdAsync(int id, CancellationToken ct = default) =>
        await Project(db.Clients.AsNoTracking().Where(c => c.Id == id)).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException($"No existe el cliente con id {id}.");

    public async Task<ClientDto> GetByIdentificationAsync(string identification, CancellationToken ct = default)
    {
        var value = identification.Trim();
        return await Project(db.Clients.AsNoTracking().Where(c => c.Identification == value)).FirstOrDefaultAsync(ct)
               ?? throw new NotFoundException($"No existe un cliente registrado con la identificación {value}.");
    }

    public async Task<ClientDto> CreateAsync(SaveClientRequest request, CancellationToken ct = default)
    {
        await validator.EnsureValidAsync(request, ct);
        var client = await BuildNewClientAsync(request, ct);

        db.Clients.Add(client);
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(client.Id, ct);
    }

    public async Task<ClientDto> UpdateAsync(int id, SaveClientRequest request, CancellationToken ct = default)
    {
        await validator.EnsureValidAsync(request, ct);
        var client = await FindAsync(id, ct);

        var identification = request.Identification.Trim();
        if (await db.Clients.IgnoreQueryFilters().AnyAsync(c => c.Identification == identification && c.Id != id, ct))
        {
            throw new ConflictException($"La identificación {identification} ya está registrada por otro cliente.");
        }

        Map(request, client);
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<ClientDto> ChangeStatusAsync(int id, ChangeClientStatusRequest request, CancellationToken ct = default)
    {
        var client = await FindAsync(id, ct);
        client.Active = request.Active;
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var client = await FindAsync(id, ct);

        if (await db.Contracts.AnyAsync(c => c.ClientId == id && (c.StatusId == "VIG" || c.StatusId == "REN"), ct))
        {
            throw new ConflictException("El cliente tiene contratos vigentes. Debe cancelarlos antes de eliminarlo.");
        }

        db.Clients.Remove(client); // eliminación lógica
        await db.SaveChangesAsync(ct);
    }

    public async Task<BulkUploadResult> BulkCreateAsync(Stream file, string fileName, CancellationToken ct = default)
    {
        var rows = spreadsheetReader.Read(file, fileName);
        var errors = new List<BulkRowError>();
        var created = 0;

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var request = new SaveClientRequest(
                row.Value("name", "nombres", "nombre"),
                row.Value("lastname", "apellidos", "apellido"),
                row.Value("identification", "identificacion", "identificación"),
                row.Value("email", "correo"),
                row.Value("phonenumber", "phone", "telefono", "teléfono"),
                row.Value("address", "direccion", "dirección"),
                row.Value("referenceaddress", "referencia", "referencia de la direccion", "referencia de la dirección"));

            var messages = (await validator.CollectErrorsAsync(request, ct)).ToList();
            if (messages.Count == 0)
            {
                try
                {
                    db.Clients.Add(await BuildNewClientAsync(request, ct));
                    await db.SaveChangesAsync(ct);
                    created++;
                    continue;
                }
                catch (ConflictException ex)
                {
                    messages.Add(ex.Message);
                }
            }

            errors.Add(new BulkRowError(i + 2, request.Identification, messages));
        }

        return new BulkUploadResult(rows.Count, created, errors.Count, errors);
    }

    private async Task<Client> BuildNewClientAsync(SaveClientRequest request, CancellationToken ct)
    {
        var identification = request.Identification.Trim();

        // Un cliente solo puede registrarse una vez (validado por identificación).
        if (await db.Clients.IgnoreQueryFilters().AnyAsync(c => c.Identification == identification, ct))
        {
            throw new ConflictException($"El cliente con identificación {identification} ya se encuentra registrado.");
        }

        var client = new Client
        {
            CreatedAt = DateTimeOffset.UtcNow,
            UserCreateId = currentUser.UserId,
            Active = true
        };
        Map(request, client);
        return client;
    }

    private static void Map(SaveClientRequest request, Client client)
    {
        client.Name = request.Name.Trim();
        client.LastName = request.LastName.Trim();
        client.Identification = request.Identification.Trim();
        client.Email = request.Email.Trim().ToLowerInvariant();
        client.PhoneNumber = request.PhoneNumber.Trim();
        client.Address = request.Address.Trim();
        client.ReferenceAddress = request.ReferenceAddress.Trim();
    }

    private async Task<Client> FindAsync(int id, CancellationToken ct) =>
        await db.Clients.FirstOrDefaultAsync(c => c.Id == id, ct)
        ?? throw new NotFoundException($"No existe el cliente con id {id}.");

    private static IQueryable<ClientDto> Project(IQueryable<Client> query) => query.Select(c => new ClientDto(
        c.Id,
        c.Name,
        c.LastName,
        c.Name + " " + c.LastName,
        c.Identification,
        c.Email,
        c.PhoneNumber,
        c.Address,
        c.ReferenceAddress,
        c.Active,
        c.CreatedAt,
        c.Contracts.Count(x => !x.IsDeleted && (x.StatusId == "VIG" || x.StatusId == "REN"))));
}
