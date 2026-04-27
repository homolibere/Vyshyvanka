using FlowForge.Engine.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace FlowForge.Engine.Persistence;

/// <summary>
/// EF Core database context for FlowForge persistence.
/// </summary>
public class FlowForgeDbContext : DbContext
{
    /// <summary>Workflows.</summary>
    public DbSet<WorkflowEntity> Workflows => Set<WorkflowEntity>();
    
    /// <summary>Workflow executions.</summary>
    public DbSet<ExecutionEntity> Executions => Set<ExecutionEntity>();
    
    /// <summary>Node executions.</summary>
    public DbSet<NodeExecutionEntity> NodeExecutions => Set<NodeExecutionEntity>();
    
    /// <summary>Credentials.</summary>
    public DbSet<CredentialEntity> Credentials => Set<CredentialEntity>();
    
    /// <summary>Users.</summary>
    public DbSet<UserEntity> Users => Set<UserEntity>();
    
    /// <summary>API keys.</summary>
    public DbSet<ApiKeyEntity> ApiKeys => Set<ApiKeyEntity>();
    
    /// <summary>Audit logs.</summary>
    public DbSet<AuditLogEntity> AuditLogs => Set<AuditLogEntity>();

    public FlowForgeDbContext(DbContextOptions<FlowForgeDbContext> options) 
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<WorkflowEntity>(entity =>
        {
            entity.ToTable("Workflows");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.CreatedBy);
            entity.HasIndex(e => e.UpdatedAt);
            
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.NodesJson).IsRequired();
            entity.Property(e => e.ConnectionsJson).IsRequired();
        });

        modelBuilder.Entity<ExecutionEntity>(entity =>
        {
            entity.ToTable("Executions");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.WorkflowId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.StartedAt);
            entity.HasIndex(e => new { e.WorkflowId, e.Status });
            
            entity.HasMany(e => e.NodeExecutions)
                .WithOne(ne => ne.Execution)
                .HasForeignKey(ne => ne.ExecutionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NodeExecutionEntity>(entity =>
        {
            entity.ToTable("NodeExecutions");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ExecutionId);
            entity.HasIndex(e => new { e.ExecutionId, e.NodeId });
        });
        
        modelBuilder.Entity<CredentialEntity>(entity =>
        {
            entity.ToTable("Credentials");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.OwnerId);
            entity.HasIndex(e => new { e.OwnerId, e.Name });
            
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.EncryptedData).IsRequired();
        });
        
        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.IsActive);
            
            entity.Property(e => e.Email).HasMaxLength(256).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(200);
            entity.Property(e => e.PasswordHash).HasMaxLength(500).IsRequired();
            entity.Property(e => e.RefreshToken).HasMaxLength(500);
            
            entity.HasMany(e => e.ApiKeys)
                .WithOne(a => a.User)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        
        modelBuilder.Entity<ApiKeyEntity>(entity =>
        {
            entity.ToTable("ApiKeys");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.KeyHash).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.IsActive });
            
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.KeyHash).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Scopes).HasMaxLength(1000);
        });
        
        modelBuilder.Entity<AuditLogEntity>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.EventType);
            entity.HasIndex(e => new { e.ResourceType, e.ResourceId });
            entity.HasIndex(e => new { e.UserId, e.Timestamp });
            
            entity.Property(e => e.Action).HasMaxLength(200).IsRequired();
            entity.Property(e => e.UserEmail).HasMaxLength(256);
            entity.Property(e => e.IpAddress).HasMaxLength(50);
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            entity.Property(e => e.ResourceType).HasMaxLength(100);
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
        });
    }
}
