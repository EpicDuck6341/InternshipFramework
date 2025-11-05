using System.Linq.Expressions;
using Elijah.Domain.Entities;
using Elijah.Domain.Entities.Abstract;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Query;

namespace Elijah.Data
{
    public class ApplicationDbContext : DbContext
    {
        // ---- expose each table ----
        public DbSet<Device> Devices { get; set; }
        public DbSet<ConfiguredReporting> ConfiguredReportings { get; set; }
        public DbSet<DeviceFilter> DeviceFilters { get; set; }
        public DbSet<DeviceTemplate> DeviceTemplates { get; set; }
        public DbSet<Option> Options { get; set; }
        public DbSet<ReportTemplate> ReportTemplates { get; set; }


        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
            ChangeTracker.Tracked += HandleTracked;
            ChangeTracker.StateChanged += HandleStateChanged;
        }

        /// <summary>
        /// Handles new entities being tracked by DbContext. It sets the `SysCreated`
        /// property of an entity inheriting from `BaseType` to the current UTC time if
        /// the entity is in the `Added` state and was not fetched from a query.
        /// </summary>
        private static void HandleTracked(object? _, EntityTrackedEventArgs eventArgs)
        {
            if (
                eventArgs.FromQuery
                || eventArgs.Entry.State != EntityState.Added
                || eventArgs.Entry.Entity is not BaseType baseTableEntity
            )
            {
                return;
            }

            baseTableEntity.SysCreated = DateTimeOffset.UtcNow;
        }

        /// <summary>
        /// Handles changes in the state of entities tracked by the DbContext.
        /// It updates the `SysModified` property of an entity inheriting from `BaseType`
        /// to the current UTC time when the entity transitions to the `Modified` state.
        /// </summary>
        private static void HandleStateChanged(object? _, EntityStateChangedEventArgs eventArgs)
        {
            if (
                eventArgs.NewState != EntityState.Modified
                || eventArgs.Entry.Entity is not BaseType baseTableEntity
            )
            {
                return;
            }

            baseTableEntity.SysModified = DateTimeOffset.UtcNow;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // -----------------------
            // Entity configurations
            // -----------------------
            modelBuilder.Entity<ConfiguredReporting>();
            modelBuilder.Entity<Device>().HasIndex(x => x.Address).IsUnique();
            modelBuilder.Entity<DeviceFilter>();
            modelBuilder.Entity<DeviceTemplate>();
            modelBuilder.Entity<Option>();
            modelBuilder.Entity<ReportTemplate>();

            // -----------------------
            // Unique indexes
            // -----------------------
            modelBuilder.Entity<DeviceFilter>()
                .HasIndex(x => new { x.Address})
                .IsUnique();

            modelBuilder.Entity<ConfiguredReporting>()
                .HasIndex(x => new { x.Id})
                .IsUnique();

            modelBuilder.Entity<Option>()
                .HasIndex(x => new { x.Id})
                .IsUnique();
            
            modelBuilder.Entity<Device>()
                .HasIndex(x => new { x.Address, x.Name })
                .IsUnique();
            
            modelBuilder.Entity<DeviceTemplate>()
                .HasIndex(x => new { x.ModelId })
                .IsUnique();
            
            modelBuilder.Entity<ReportTemplate>()
                .HasIndex(x => new { x.Id})
                .IsUnique();

            // -----------------------
            // Relationships
            // -----------------------
            modelBuilder.Entity<Device>()
                .HasOne(d => d.DeviceTemplate)
                .WithMany(t => t.Devices)
                .HasForeignKey(d => d.ModelId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DeviceFilter>()
                .HasOne(df => df.Device)
                .WithMany(d => d.DeviceFilters)
                .HasForeignKey(df => df.Address)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ConfiguredReporting>()
                .HasOne(cr => cr.Device)
                .WithMany(d => d.ConfiguredReportings)
                .HasForeignKey(cr => cr.Address)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Option>()
                .HasOne(o => o.Device)
                .WithMany(d => d.Options)
                .HasForeignKey(o => o.Address)
                .OnDelete(DeleteBehavior.Cascade);
            
            modelBuilder.Entity<ReportTemplate>()
                .HasOne(rt => rt.DeviceTemplate)
                .WithMany(d => d.ReportTemplates)
                .HasForeignKey(rt => rt.ModelId)
                .OnDelete(DeleteBehavior.Cascade);

            // -----------------------
            // Soft delete filter
            // -----------------------
            SetDefaultSoftDeleteFilter(modelBuilder);
        }

        /// <summary>
        /// Sets the default soft delete filter for entities inheriting from `BaseType`.
        /// This ensures that only entities where the `SysRemoved` property is not set
        /// to true are included in query results.
        /// </summary>
        private static void SetDefaultSoftDeleteFilter(ModelBuilder modelBuilder)
        {
            Expression<Func<BaseType, bool>> filterExpression = baseTable => !baseTable.SysRemoved;

            foreach (var mutableEntityType in modelBuilder.Model.GetEntityTypes())
            {
                if (!typeof(BaseType).IsAssignableFrom(mutableEntityType.ClrType))
                {
                    continue;
                }

                // modify expression to handle correct child type
                var parameterExpression = Expression.Parameter(mutableEntityType.ClrType);
                var expression = ReplacingExpressionVisitor.Replace(
                    filterExpression.Parameters.First(),
                    parameterExpression,
                    filterExpression.Body
                );
                var lambdaExpression = Expression.Lambda(expression, parameterExpression);

                // set filter
                mutableEntityType.SetQueryFilter(lambdaExpression);
            }
        }
    }
}