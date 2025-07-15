using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using TableManagement.Core.Entities;

namespace TableManagement.Infrastructure.Data
{
    public class ApplicationDbContext : IdentityDbContext<User, IdentityRole<int>, int>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<CustomTable> CustomTables { get; set; }
        public DbSet<CustomColumn> CustomColumns { get; set; }
        public DbSet<CustomTableData> CustomTableData { get; set; }

        public DbSet<SecurityLog> SecurityLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            

            // SecurityLog entity konfigürasyonu
            builder.Entity<SecurityLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Timestamp).IsRequired();
                entity.Property(e => e.IpAddress).HasMaxLength(45).IsRequired();
                entity.Property(e => e.ThreatType).HasMaxLength(100).IsRequired();
                entity.Property(e => e.RequestPath).HasMaxLength(500).IsRequired();
                entity.Property(e => e.RequestMethod).HasMaxLength(10).IsRequired();
                entity.Property(e => e.UserAgent).HasMaxLength(1000).IsRequired();
                entity.Property(e => e.UserId).HasMaxLength(450);

                // Index'ler
                entity.HasIndex(e => e.Timestamp);
                entity.HasIndex(e => e.IpAddress);
                entity.HasIndex(e => e.ThreatType);
                entity.HasIndex(e => e.IsBlocked);
                entity.HasIndex(e => new { e.IpAddress, e.Timestamp });
            });


            // User Configuration
            builder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FirstName).IsRequired().HasMaxLength(50);
                entity.Property(e => e.LastName).IsRequired().HasMaxLength(50);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.HasQueryFilter(e => !e.IsDeleted);

                entity.HasMany(e => e.CustomTables)
                      .WithOne(e => e.User)
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // CustomTable Configuration
            builder.Entity<CustomTable>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TableName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(500); // Required kaldırıldı - nullable
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.HasQueryFilter(e => !e.IsDeleted);

                entity.HasOne(e => e.User)
                      .WithMany(e => e.CustomTables)
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.Columns)
                      .WithOne(e => e.CustomTable)
                      .HasForeignKey(e => e.CustomTableId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.TableData)
                      .WithOne(e => e.CustomTable)
                      .HasForeignKey(e => e.CustomTableId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => new { e.UserId, e.TableName }).IsUnique();
            });

            // CustomColumn Configuration
            builder.Entity<CustomColumn>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ColumnName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.DataType).IsRequired();
                entity.Property(e => e.DefaultValue).HasMaxLength(255); // Nullable olarak bırakıldı
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.HasQueryFilter(e => !e.IsDeleted);

                entity.HasOne(e => e.CustomTable)
                      .WithMany(e => e.Columns)
                      .HasForeignKey(e => e.CustomTableId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => new { e.CustomTableId, e.ColumnName }).IsUnique();
            });

            // CustomTableData Configuration
            builder.Entity<CustomTableData>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Value).HasMaxLength(1000); // Nullable olarak bırakıldı
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.HasQueryFilter(e => !e.IsDeleted);

                entity.HasOne(e => e.CustomTable)
                      .WithMany(e => e.TableData)
                      .HasForeignKey(e => e.CustomTableId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Column)
                      .WithMany()
                      .HasForeignKey(e => e.ColumnId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => new { e.CustomTableId, e.RowIdentifier, e.ColumnId }).IsUnique();
            });

            // Identity Tables Rename
            builder.Entity<User>().ToTable("Users");
            builder.Entity<IdentityRole<int>>().ToTable("Roles");
            builder.Entity<IdentityUserRole<int>>().ToTable("UserRoles");
            builder.Entity<IdentityUserClaim<int>>().ToTable("UserClaims");
            builder.Entity<IdentityUserLogin<int>>().ToTable("UserLogins");
            builder.Entity<IdentityUserToken<int>>().ToTable("UserTokens");
            builder.Entity<IdentityRoleClaim<int>>().ToTable("RoleClaims");
        }
    }
}