using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using TestAutomationManager.Models;
using TestAutomationManager.Data;
using TestAutomationManager.Data.Schema;

namespace TestAutomationManager.Data
{
    /// <summary>
    /// Database context for Test Automation Manager
    /// Manages connection and operations with SQL Server database
    /// </summary>
    public class TestAutomationDbContext : DbContext
    {
        // ================================================
        // DbSets - Represent tables in database
        // ================================================

        /// <summary>
        /// Tests table
        /// </summary>
        public DbSet<Test> Tests { get; set; }

        /// <summary>
        /// Processes table
        /// </summary>
        public DbSet<Process> Processes { get; set; }

        /// <summary>
        /// Functions table
        /// </summary>
        public DbSet<Function> Functions { get; set; }

        // ================================================
        // Configuration
        // ================================================

        /// <summary>
        /// Configure database connection
        /// </summary>
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // Get connection string from configuration
                string connectionString = DbConnectionConfig.GetConnectionString();

                // Configure SQL Server connection
                optionsBuilder.UseSqlServer(connectionString);

                // Optional: Enable detailed logging (useful for development)
#if DEBUG
                optionsBuilder.EnableSensitiveDataLogging();
                optionsBuilder.LogTo(System.Console.WriteLine);
#endif
            }
        }

        /// <summary>
        /// Configure entity relationships and mappings
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            var schema = SchemaManager.Current;

            ConfigureTestEntity(modelBuilder, schema);
            ConfigureProcessEntity(modelBuilder, schema);
            ConfigureFunctionEntity(modelBuilder, schema);
        }

        private static void ConfigureTestEntity(ModelBuilder modelBuilder, SchemaDefinition schema)
        {
            var entity = modelBuilder.Entity<Test>();
            entity.ToTable(schema.GetTableName("tests"), schema.DatabaseSchema);

            entity.HasKey(e => e.Id);

            var columns = schema.GetColumnCandidates("tests");

            entity.Property(e => e.Id).HasColumnName(GetColumnName(columns, "Id", "Id"));
            entity.Property(e => e.Name).HasColumnName(GetColumnName(columns, "Name", "Name")).HasMaxLength(200);
            entity.Property(e => e.Description).HasColumnName(GetColumnName(columns, "Description", "Description")).HasMaxLength(500);
            entity.Property(e => e.Category).HasColumnName(GetColumnName(columns, "Category", "Category")).HasMaxLength(100);
            entity.Property(e => e.Status).HasColumnName(GetColumnName(columns, "Status", "Status")).HasMaxLength(50);
            entity.Property(e => e.IsActive).HasColumnName(GetColumnName(columns, "IsActive", "IsActive")).HasDefaultValue(true);
            entity.Property(e => e.LastRun).HasColumnName(GetColumnName(columns, "LastRun", "LastRun"));

            entity.HasMany(t => t.Processes)
                .WithOne()
                .HasForeignKey(nameof(Process.TestId))
                .OnDelete(DeleteBehavior.Cascade);

            entity.Ignore(e => e.IsExpanded);
        }

        private static void ConfigureProcessEntity(ModelBuilder modelBuilder, SchemaDefinition schema)
        {
            var entity = modelBuilder.Entity<Process>();
            entity.ToTable(schema.GetTableName("processes"), schema.DatabaseSchema);

            entity.HasKey(e => e.Id);

            var columns = schema.GetColumnCandidates("processes");

            entity.Property(e => e.Id).HasColumnName(GetColumnName(columns, "Id", "Id"));
            entity.Property(e => e.TestId).HasColumnName(GetColumnName(columns, "TestId", "TestId"));
            entity.Property(e => e.Name).HasColumnName(GetColumnName(columns, "Name", "Name")).HasMaxLength(200);
            entity.Property(e => e.Description).HasColumnName(GetColumnName(columns, "Description", "Description")).HasMaxLength(500);
            entity.Property(e => e.Sequence).HasColumnName(GetColumnName(columns, "Sequence", "Sequence"));
            entity.Property(e => e.IsCritical).HasColumnName(GetColumnName(columns, "IsCritical", "IsCritical")).HasDefaultValue(false);
            entity.Property(e => e.Timeout).HasColumnName(GetColumnName(columns, "Timeout", "Timeout")).HasDefaultValue(30);

            entity.HasMany(p => p.Functions)
                .WithOne()
                .HasForeignKey(nameof(Function.ProcessId))
                .OnDelete(DeleteBehavior.Cascade);

            entity.Ignore(e => e.IsExpanded);
        }

        private static void ConfigureFunctionEntity(ModelBuilder modelBuilder, SchemaDefinition schema)
        {
            var entity = modelBuilder.Entity<Function>();
            entity.ToTable(schema.GetTableName("functions"), schema.DatabaseSchema);

            entity.HasKey(e => e.Id);

            var columns = schema.GetColumnCandidates("functions");

            entity.Property(e => e.Id).HasColumnName(GetColumnName(columns, "Id", "Id"));
            entity.Property(e => e.ProcessId).HasColumnName(GetColumnName(columns, "ProcessId", "ProcessId"));
            entity.Property(e => e.Name).HasColumnName(GetColumnName(columns, "Name", "Name")).HasMaxLength(200);
            entity.Property(e => e.MethodName).HasColumnName(GetColumnName(columns, "MethodName", "MethodName")).HasMaxLength(200);
            entity.Property(e => e.Parameters).HasColumnName(GetColumnName(columns, "Parameters", "Parameters")).HasMaxLength(1000);
            entity.Property(e => e.ExpectedResult).HasColumnName(GetColumnName(columns, "ExpectedResult", "ExpectedResult")).HasMaxLength(500);
            entity.Property(e => e.Sequence).HasColumnName(GetColumnName(columns, "Sequence", "Sequence"));
        }

        private static string GetColumnName(Dictionary<string, List<string>> candidates, string property, string fallback)
        {
            if (candidates.TryGetValue(property, out var options) && options.Count > 0)
            {
                return options[0];
            }

            return fallback;
        }
    }
}