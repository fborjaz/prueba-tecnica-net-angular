using CashManagement.Application.Common;
using CashManagement.Application.Interfaces;
using CashManagement.Domain.Constants;
using CashManagement.Domain.Entities;
using CashManagement.Domain.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CashManagement.Application.Features.Users;

public interface IUserService
{
    Task<PagedResult<UserDto>> GetAllAsync(UserQuery query, CancellationToken ct = default);
    Task<UserDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<UserDto>> GetPendingApprovalAsync(CancellationToken ct = default);
    Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<UserDto> UpdateAsync(int id, UpdateUserRequest request, CancellationToken ct = default);
    Task<UserDto> ChangeStatusAsync(int id, ChangeUserStatusRequest request, CancellationToken ct = default);
    Task<UserDto> ApproveAsync(int id, CancellationToken ct = default);
    Task<UserDto> RejectAsync(int id, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task<BulkUploadResult> BulkCreateAsync(Stream file, string fileName, CancellationToken ct = default);
}

public class UserService(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IPasswordHasher hasher,
    IFieldEncryptor encryptor,
    ISpreadsheetReader spreadsheetReader,
    IValidator<CreateUserRequest> createValidator,
    IValidator<UpdateUserRequest> updateValidator,
    IValidator<ChangeUserStatusRequest> statusValidator) : IUserService
{
    public async Task<PagedResult<UserDto>> GetAllAsync(UserQuery query, CancellationToken ct = default)
    {
        var users = BaseQuery();

        // El gestor no administra administradores.
        if (currentUser.Role == Roles.Manager)
        {
            users = users.Where(u => u.RolId != Roles.AdministratorId);
        }

        if (query.RolId is not null) users = users.Where(u => u.RolId == query.RolId);
        if (!string.IsNullOrWhiteSpace(query.StatusId)) users = users.Where(u => u.StatusId == query.StatusId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            // Correo e identificación están cifrados: se buscan por su hash exacto.
            var hash = encryptor.Hash(term.ToLowerInvariant());
            users = users.Where(u =>
                u.Username.Contains(term) ||
                u.Names.Contains(term) ||
                u.LastNames.Contains(term) ||
                u.EmailHash == hash ||
                u.IdentificationHash == hash);
        }

        var total = await users.CountAsync(ct);
        var items = await users
            .OrderByDescending(u => u.CreationDate)
            .Skip((query.SafePage - 1) * query.SafePageSize)
            .Take(query.SafePageSize)
            .ToListAsync(ct);

        return new PagedResult<UserDto>(items.Select(ToDto).ToList(), total, query.SafePage, query.SafePageSize);
    }

    public async Task<UserDto> GetByIdAsync(int id, CancellationToken ct = default) =>
        ToDto(await FindAsync(id, ct));

    public async Task<IReadOnlyList<UserDto>> GetPendingApprovalAsync(CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var query = BaseQuery().Where(u => u.StatusId == UserStatuses.Pending);

        // El gestor ve los usuarios que él creó; el administrador ve todos.
        if (currentUser.Role == Roles.Manager)
        {
            query = query.Where(u => u.UserCreateId == userId);
        }

        var users = await query.OrderBy(u => u.CreationDate).ToListAsync(ct);
        return users.Select(ToDto).ToList();
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        await createValidator.EnsureValidAsync(request, ct);
        var user = await BuildNewUserAsync(request, ct);

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(user.Id, ct);
    }

    public async Task<UserDto> UpdateAsync(int id, UpdateUserRequest request, CancellationToken ct = default)
    {
        await updateValidator.EnsureValidAsync(request, ct);
        var user = await FindAsync(id, ct);
        EnsureManageable(user);

        var emailHash = encryptor.Hash(request.Email.Trim().ToLowerInvariant());
        var identificationHash = encryptor.Hash(request.Identification.Trim());
        await EnsureUniqueAsync(null, emailHash, identificationHash, user.Id, ct);

        user.Names = request.Names.Trim();
        user.LastNames = request.LastNames.Trim();
        user.Email = encryptor.Encrypt(request.Email.Trim().ToLowerInvariant());
        user.EmailHash = emailHash;
        user.Identification = encryptor.Encrypt(request.Identification.Trim());
        user.IdentificationHash = identificationHash;
        user.RolId = request.RolId;

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            user.Password = hasher.Hash(request.Password);
        }

        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<UserDto> ChangeStatusAsync(int id, ChangeUserStatusRequest request, CancellationToken ct = default)
    {
        await statusValidator.EnsureValidAsync(request, ct);
        var user = await FindAsync(id, ct);
        EnsureManageable(user);

        if (user.StatusId == UserStatuses.Pending)
        {
            throw new BusinessException("El usuario está pendiente de aprobación. Use la opción aprobar o rechazar.");
        }

        user.StatusId = request.StatusId;
        if (request.StatusId == UserStatuses.Active)
        {
            user.FailedAttempts = 0;
        }
        else
        {
            user.SessionActive = false; // fuerza el cierre de sesión
        }

        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<UserDto> ApproveAsync(int id, CancellationToken ct = default) =>
        await ResolvePendingAsync(id, UserStatuses.Active, ct);

    public async Task<UserDto> RejectAsync(int id, CancellationToken ct = default) =>
        await ResolvePendingAsync(id, UserStatuses.Inactive, ct);

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var user = await FindAsync(id, ct);
        EnsureManageable(user);

        if (user.Id == currentUser.RequireUserId())
        {
            throw new BusinessException("No puede eliminar su propio usuario.");
        }

        if (await db.CashSessions.AnyAsync(s => s.UserId == id && s.ClosedAt == null, ct))
        {
            throw new ConflictException("El usuario tiene una caja abierta. Debe cerrarla antes de eliminarlo.");
        }

        // Eliminación lógica: el DbContext convierte el borrado en IsDeleted = true.
        db.Users.Remove(user);

        var assignments = await db.UserCashes.Where(uc => uc.UserId == id && uc.Active).ToListAsync(ct);
        assignments.ForEach(a => a.Active = false);

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
            var rowNumber = i + 2; // fila 1 = cabecera
            var request = new CreateUserRequest(
                row.Value("username", "usuario"),
                row.Value("names", "nombres"),
                row.Value("lastnames", "apellidos"),
                row.Value("email", "correo"),
                row.Value("identification", "identificacion", "identificación"),
                row.Value("password", "contrasena", "contraseña"),
                ParseRole(row.Value("role", "rol")));

            var messages = (await createValidator.CollectErrorsAsync(request, ct)).ToList();
            if (messages.Count == 0)
            {
                try
                {
                    db.Users.Add(await BuildNewUserAsync(request, ct));
                    await db.SaveChangesAsync(ct);
                    created++;
                    continue;
                }
                catch (Exception ex) when (ex is ConflictException or BusinessException)
                {
                    messages.Add(ex.Message);
                }
            }

            errors.Add(new BulkRowError(rowNumber, request.Username, messages));
        }

        return new BulkUploadResult(rows.Count, created, errors.Count, errors);
    }

    // ----------------------------------------------------------------- helpers

    private async Task<User> BuildNewUserAsync(CreateUserRequest request, CancellationToken ct)
    {
        var creatorId = currentUser.RequireUserId();
        var creatorRole = currentUser.Role;

        if (creatorRole is not (Roles.Administrator or Roles.Manager))
        {
            throw new ForbiddenException("Solo administradores y gestores pueden crear usuarios.");
        }

        var username = request.Username.Trim();
        var emailHash = encryptor.Hash(request.Email.Trim().ToLowerInvariant());
        var identificationHash = encryptor.Hash(request.Identification.Trim());
        await EnsureUniqueAsync(username, emailHash, identificationHash, null, ct);

        // Creado por administrador: activo. Creado por gestor: requiere aprobación del administrador.
        var approvedByAdmin = creatorRole == Roles.Administrator;

        return new User
        {
            Username = username,
            Names = request.Names.Trim(),
            LastNames = request.LastNames.Trim(),
            Email = encryptor.Encrypt(request.Email.Trim().ToLowerInvariant()),
            EmailHash = emailHash,
            Identification = encryptor.Encrypt(request.Identification.Trim()),
            IdentificationHash = identificationHash,
            Password = hasher.Hash(request.Password),
            RolId = request.RolId,
            CreationDate = DateTimeOffset.UtcNow,
            UserCreateId = creatorId,
            StatusId = approvedByAdmin ? UserStatuses.Active : UserStatuses.Pending,
            UserApprovalId = approvedByAdmin ? creatorId : null,
            DateApproval = approvedByAdmin ? DateTimeOffset.UtcNow : null
        };
    }

    private async Task EnsureUniqueAsync(string? username, string emailHash, string identificationHash, int? excludeId, CancellationToken ct)
    {
        // Se consideran también los eliminados lógicamente: los índices únicos de la BD los incluyen.
        var others = db.Users.IgnoreQueryFilters().Where(u => excludeId == null || u.Id != excludeId);

        if (username is not null && await others.AnyAsync(u => u.Username == username, ct))
            throw new ConflictException($"El nombre de usuario '{username}' ya está registrado.");

        if (await others.AnyAsync(u => u.EmailHash == emailHash, ct))
            throw new ConflictException("El correo ya está registrado por otro usuario.");

        if (await others.AnyAsync(u => u.IdentificationHash == identificationHash, ct))
            throw new ConflictException("La identificación ya está registrada por otro usuario.");
    }

    private async Task<UserDto> ResolvePendingAsync(int id, string newStatus, CancellationToken ct)
    {
        var user = await FindAsync(id, ct);
        if (user.StatusId != UserStatuses.Pending)
        {
            throw new BusinessException("El usuario no está pendiente de aprobación.");
        }

        user.StatusId = newStatus;
        user.UserApprovalId = currentUser.RequireUserId();
        user.DateApproval = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(id, ct);
    }

    private static void EnsureManageable(User user)
    {
        if (!Roles.ManageableRoleIds.Contains(user.RolId))
        {
            throw new ForbiddenException("Solo se pueden administrar usuarios gestores y cajeros.");
        }
    }

    private static int ParseRole(string value) => value.Trim().ToUpperInvariant() switch
    {
        "GESTOR" or "2" => Roles.ManagerId,
        "CAJERO" or "3" => Roles.CashierId,
        _ => 0
    };

    private IQueryable<User> BaseQuery() => db.Users.Where(u => !u.IsDeleted)
        .Include(u => u.Rol)
        .Include(u => u.Status)
        .Include(u => u.UserCreate)
        .Include(u => u.UserApproval)
        .AsNoTracking();

    private async Task<User> FindAsync(int id, CancellationToken ct) =>
        await db.Users.Include(u => u.Rol).Include(u => u.Status).Include(u => u.UserCreate).Include(u => u.UserApproval)
            .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted, ct)
        ?? throw new NotFoundException($"No existe el usuario con id {id}.");

    private UserDto ToDto(User u) => new(
        u.Id,
        u.Username,
        u.Names,
        u.LastNames,
        u.FullName,
        encryptor.Decrypt(u.Email),
        encryptor.Decrypt(u.Identification),
        u.RolId,
        u.Rol?.Name ?? Roles.NameOf(u.RolId),
        u.StatusId,
        u.Status?.Description ?? u.StatusId,
        u.CreationDate,
        u.UserCreate?.Username,
        u.UserApproval?.Username,
        u.DateApproval,
        u.SessionActive,
        u.LastLogin,
        u.FailedAttempts);
}
