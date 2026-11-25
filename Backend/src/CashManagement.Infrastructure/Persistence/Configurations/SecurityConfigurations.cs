using CashManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CashManagement.Infrastructure.Persistence.Configurations;

// Mapeo de las entidades a las tablas del script Backend/database/01_schema.sql.

public class RolConfiguration : IEntityTypeConfiguration<Rol>
{
    public void Configure(EntityTypeBuilder<Rol> b)
    {
        b.ToTable("rol");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("rolid");
        b.Property(x => x.Name).HasColumnName("rolname").HasMaxLength(50);
    }
}

public class UserStatusConfiguration : IEntityTypeConfiguration<UserStatus>
{
    public void Configure(EntityTypeBuilder<UserStatus> b)
    {
        b.ToTable("userstatus");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("statusid").HasMaxLength(3);
        b.Property(x => x.Description).HasColumnName("description").HasMaxLength(50);
    }
}

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("userid");
        b.Property(x => x.Username).HasColumnName("username").HasMaxLength(20);
        b.Property(x => x.Names).HasColumnName("names").HasMaxLength(50);
        b.Property(x => x.LastNames).HasColumnName("lastnames").HasMaxLength(50);
        b.Property(x => x.Email).HasColumnName("email").HasMaxLength(400);
        b.Property(x => x.EmailHash).HasColumnName("emailhash").HasMaxLength(64).IsFixedLength();
        b.Property(x => x.Identification).HasColumnName("identification").HasMaxLength(400);
        b.Property(x => x.IdentificationHash).HasColumnName("identificationhash").HasMaxLength(64).IsFixedLength();
        b.Property(x => x.Password).HasColumnName("password").HasMaxLength(100);
        b.Property(x => x.RolId).HasColumnName("rol_rolid");
        b.Property(x => x.CreationDate).HasColumnName("creationdate");
        b.Property(x => x.UserCreateId).HasColumnName("usercreate");
        b.Property(x => x.UserApprovalId).HasColumnName("userapproval");
        b.Property(x => x.DateApproval).HasColumnName("dateapproval");
        b.Property(x => x.StatusId).HasColumnName("userstatus_statusid").HasMaxLength(3);
        b.Property(x => x.SessionActive).HasColumnName("sessionactive");
        b.Property(x => x.LastLogin).HasColumnName("lastlogin");
        b.Property(x => x.LastLogout).HasColumnName("lastlogout");
        b.Property(x => x.FailedAttempts).HasColumnName("failedattempts");
        b.Property(x => x.IsDeleted).HasColumnName("isdeleted");
        b.Ignore(x => x.FullName);

        b.HasOne(x => x.Rol).WithMany().HasForeignKey(x => x.RolId);
        b.HasOne(x => x.Status).WithMany().HasForeignKey(x => x.StatusId);
        b.HasOne(x => x.UserCreate).WithMany().HasForeignKey(x => x.UserCreateId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.UserApproval).WithMany().HasForeignKey(x => x.UserApprovalId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class PasswordResetConfiguration : IEntityTypeConfiguration<PasswordReset>
{
    public void Configure(EntityTypeBuilder<PasswordReset> b)
    {
        b.ToTable("passwordreset");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("passwordresetid");
        b.Property(x => x.UserId).HasColumnName("user_userid");
        b.Property(x => x.TokenHash).HasColumnName("tokenhash").HasMaxLength(64).IsFixedLength();
        b.Property(x => x.ExpiresAt).HasColumnName("expiresat");
        b.Property(x => x.Used).HasColumnName("used");
        b.Property(x => x.CreatedAt).HasColumnName("createdat");
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
    }
}

public class MenuConfiguration : IEntityTypeConfiguration<Menu>
{
    public void Configure(EntityTypeBuilder<Menu> b)
    {
        b.ToTable("menu");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("menuid");
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(50);
        b.Property(x => x.Route).HasColumnName("route").HasMaxLength(100);
        b.Property(x => x.Icon).HasColumnName("icon").HasMaxLength(50);
        b.Property(x => x.ParentId).HasColumnName("parentid");
        b.Property(x => x.SortOrder).HasColumnName("sortorder");
        b.Property(x => x.Active).HasColumnName("active");
    }
}

public class RolMenuConfiguration : IEntityTypeConfiguration<RolMenu>
{
    public void Configure(EntityTypeBuilder<RolMenu> b)
    {
        b.ToTable("rolmenu");
        b.HasKey(x => new { x.RolId, x.MenuId });
        b.Property(x => x.RolId).HasColumnName("rol_rolid");
        b.Property(x => x.MenuId).HasColumnName("menu_menuid");
        b.HasOne(x => x.Rol).WithMany(r => r.Menus).HasForeignKey(x => x.RolId);
        b.HasOne(x => x.Menu).WithMany(m => m.Roles).HasForeignKey(x => x.MenuId);
    }
}
