using Microsoft.EntityFrameworkCore;
using ChatApp.Models;

namespace ChatApp.Data;

public class ChatDbContext : DbContext
{
    public ChatDbContext(DbContextOptions<ChatDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<UserSession> UserSessions { get; set; }
    public DbSet<BlockedUser> BlockedUsers { get; set; }
    public DbSet<UserReport> UserReports { get; set; }
    public DbSet<Country> Countries { get; set; }
    public DbSet<City> Cities { get; set; }
    public DbSet<Group> Groups { get; set; }
    public DbSet<GroupMember> GroupMembers { get; set; }
    public DbSet<GroupMessage> GroupMessages { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Suppress pending model changes warning temporarily
        optionsBuilder.ConfigureWarnings(warnings => 
            warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User entity configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username)
                .IsRequired()
                .HasMaxLength(50);
            entity.Property(e => e.PasswordHash)
                .IsRequired()
                .HasMaxLength(255);
            entity.Property(e => e.Email)
                .HasMaxLength(100);
            entity.Property(e => e.DisplayName)
                .HasMaxLength(100);
            entity.Property(e => e.ProfileImageUrl)
                .HasMaxLength(255);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            // Username unique constraint
            entity.HasIndex(e => e.Username)
                .IsUnique();

            // 🔥 Seed Data - Test Kullanıcıları + Sistem Kullanıcıları
            entity.HasData(
                new User
                {
                    Id = 999,
                    Username = "SYSTEM_GENERAL_CHAT",
                    PasswordHash = "$2a$11$SYSTEM.GENERAL.CHAT.HASH.NOT.USABLE", // Sabit sistem şifresi
                    Email = "system@chat.com",
                    DisplayName = "Genel Chat",
                    ProfileImageUrl = null,
                     // Sistem kullanıcısı, aktif değil
                    IsOnline = false,
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) // Sabit tarih
                },
                new User
                {
                    Id = 1,
                    Username = "admin",
                    PasswordHash = "$2a$11$BGXDCIJFEJLeo8k86adupeC5tjKYyrCjJk2Tg6Ho/CjKWEBsYojby", // 123456
                    Email = "admin@chat.com",
                    DisplayName = "Yönetici",
                    ProfileImageUrl = null,
                    
                    IsOnline = false,
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new User
                {
                    Id = 2,
                    Username = "user1",
                    PasswordHash = "$2a$11$PvX.Oqlprh401TRWoocb3OoROql20nUHOUQGfS5hsqj2VcIpuVyE.", // user123
                    Email = "user1@chat.com",
                    DisplayName = "Kullanıcı 1",
                    ProfileImageUrl = null,
                    
                    IsOnline = false,
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new User
                {
                    Id = 3,
                    Username = "test",
                    PasswordHash = "$2a$11$iGrTy4z1K1K.8z9j3vJ9..123456.hash.example", // test123
                    Email = "test@chat.com",
                    DisplayName = "Test Kullanıcısı",
                    ProfileImageUrl = null,
                    
                    IsOnline = false,
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            );
        });

        // ChatMessage entity configuration
        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.From)
                .IsRequired()
                .HasMaxLength(50);
            entity.Property(e => e.To)
                .HasMaxLength(50);
            entity.Property(e => e.Message)
                .IsRequired()
                .HasMaxLength(1000);
            entity.Property(e => e.MessageType)
                .HasMaxLength(20)
                .HasDefaultValue("private");
            entity.Property(e => e.Timestamp)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Indexes for better performance
            entity.HasIndex(e => e.FromUserId);
            entity.HasIndex(e => e.ToUserId);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.MessageType);
        });

        // UserSession entity configuration
        modelBuilder.Entity<UserSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.ConnectionId)
                .HasMaxLength(100);
            entity.Property(e => e.IpAddress)
                .HasMaxLength(45);
            entity.Property(e => e.UserAgent)
                .HasMaxLength(500);
            entity.Property(e => e.LoginTime)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.IsOnline)
                .HasDefaultValue(true);

            // Indexes for active sessions
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.IsOnline);
        });

        // BlockedUser entity configuration
        modelBuilder.Entity<BlockedUser>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Reason)
                .HasMaxLength(500);
            
            entity.Property(e => e.BlockedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Unique constraint - bir kullanıcı aynı kullanıcıyı birden fazla kez engelleyemez
            entity.HasIndex(e => new { e.UserId, e.BlockedUserId })
                .IsUnique();

            // Navigation properties (şimdilik disable)
            // entity.HasOne(e => e.User)
            //     .WithMany()
            //     .HasForeignKey(e => e.UserId)
            //     .OnDelete(DeleteBehavior.Cascade);
            
            // entity.HasOne(e => e.BlockedUserNavigation)
            //     .WithMany()
            //     .HasForeignKey(e => e.BlockedUserId)
            //     .OnDelete(DeleteBehavior.Restrict);
        });

        // UserReport entity configuration
        modelBuilder.Entity<UserReport>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Reason)
                .IsRequired()
                .HasMaxLength(500);
            
            entity.Property(e => e.Category)
                .HasMaxLength(50)
                .HasDefaultValue("General");
            
            entity.Property(e => e.ReportedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            entity.Property(e => e.IsResolved)
                .HasDefaultValue(false);

            entity.Property(e => e.AdminNotes)
                .HasMaxLength(1000);

            // Indexes
            entity.HasIndex(e => e.ReporterId);
            entity.HasIndex(e => e.ReportedUserId);
            entity.HasIndex(e => e.IsResolved);
            entity.HasIndex(e => e.ReportedAt);

            // Navigation properties (şimdilik disable)
            // entity.HasOne(e => e.Reporter)
            //     .WithMany()
            //     .HasForeignKey(e => e.ReporterId)
            //     .OnDelete(DeleteBehavior.Cascade);
            
            // entity.HasOne(e => e.ReportedUser)
            //     .WithMany()
            //     .HasForeignKey(e => e.ReportedUserId)
            //     .OnDelete(DeleteBehavior.Restrict);
        });

        // Country entity configuration
        modelBuilder.Entity<Country>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(100);
            
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Unique constraints
            entity.HasIndex(e => e.Name).IsUnique();
        });

        // City entity configuration
        modelBuilder.Entity<City>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(100);
            
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Foreign key
            entity.HasOne(e => e.Country)
                .WithMany(c => c.Cities)
                .HasForeignKey(e => e.CountryId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            entity.HasIndex(e => e.CountryId);
            entity.HasIndex(e => new { e.Name, e.CountryId }).IsUnique(); // Aynı ülkede şehir adı unique
        });

        // Group entity configuration
        modelBuilder.Entity<Group>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            entity.HasOne(e => e.CreatedByUser)
                .WithMany()
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
                
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.CreatedByUserId);
        });

        // GroupMember entity configuration
        modelBuilder.Entity<GroupMember>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.JoinedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            entity.HasOne(e => e.Group)
                .WithMany(g => g.GroupMembers)
                .HasForeignKey(e => e.GroupId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasIndex(e => new { e.GroupId, e.UserId }).IsUnique();
        });

        // GroupMessage entity configuration
        modelBuilder.Entity<GroupMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Message).IsRequired();
            entity.Property(e => e.Timestamp).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.MessageType).HasDefaultValue("TEXT");
            
            entity.HasOne(e => e.Group)
                .WithMany(g => g.Messages)
                .HasForeignKey(e => e.GroupId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.FromUser)
                .WithMany()
                .HasForeignKey(e => e.FromUserId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasIndex(e => e.GroupId);
            entity.HasIndex(e => e.FromUserId);
            entity.HasIndex(e => e.Timestamp);
        });
    }
}
