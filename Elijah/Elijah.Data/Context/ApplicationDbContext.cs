using System.Linq.Expressions;
using Elijah.Domain.Entities;
using Elijah.Domain.Entities.Abstract;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Query;

namespace Elijah.Data.Context
{
    public class ApplicationDbContext : DbContext
    {
        // expose each table
        public DbSet<Device> Devices { get; set; }
        public DbSet<ConfiguredReporting> ConfiguredReportings { get; set; }
        public DbSet<DeviceFilter> DeviceFilters { get; set; }
        public DbSet<DeviceTemplate> DeviceTemplates { get; set; }
        public DbSet<Option> Options { get; set; }

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
            modelBuilder.Entity<DeviceTemplate>();

            // -----------------------
            // Unique indexes
            // -----------------------
            modelBuilder.Entity<Device>(entity =>
            {
                entity.HasIndex(x => x.Address).IsUnique();
                entity.HasIndex(x => x.Name).IsUnique();
            });

            modelBuilder.Entity<DeviceTemplate>(entity =>
            {
                entity.HasIndex(x => x.Name).IsUnique();
                entity.HasIndex(x => x.ModelId).IsUnique();
            });

            modelBuilder
                .Entity<DeviceFilter>()
                .HasIndex(x => new { x.DeviceId, x.FilterType })
                .IsUnique();

            modelBuilder.Entity<Option>().HasIndex(x => new { x.DeviceId, x.Property }).IsUnique();

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
