using Microsoft.EntityFrameworkCore;
using TestAutomationManager.Models;
using TestAutomationManager.Data;

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

            // ================================================
            // TEST ENTITY CONFIGURATION
            // ================================================
            modelBuilder.Entity<Test>(entity =>
            {
                // Table name
                entity.ToTable("Tests");

                // Primary key
                entity.HasKey(e => e.TestID);

                // Properties
                entity.Property(e => e.TestName)
                    .HasMaxLength(500);

                entity.Property(e => e.RunStatus)
                    .HasMaxLength(50);

                entity.Property(e => e.Bugs)
                    .HasMaxLength(1000);

                entity.Property(e => e.RecipientsEmailsList)
                    .HasMaxLength(2000);

                entity.Property(e => e.ExceptionMessage)
                    .HasMaxLength(4000);

                // Relationships
                entity.HasMany(t => t.Processes)
                    .WithOne()
                    .HasForeignKey("TestID")
                    .OnDelete(DeleteBehavior.Cascade);

                // Ignore properties that don't exist in database
                entity.Ignore(e => e.IsExpanded);
            });

            // ================================================
            // PROCESS ENTITY CONFIGURATION
            // ================================================
            modelBuilder.Entity<Process>(entity =>
            {
                // Table name
                entity.ToTable("Processes");

                // Primary key
                entity.HasKey(e => e.ProcessID);

                // Properties
                entity.Property(e => e.ProcessName)
                    .HasMaxLength(500);

                entity.Property(e => e.WEB3Operator)
                    .HasMaxLength(200);

                entity.Property(e => e.Pass_Fail)
                    .HasMaxLength(50);

                // Relationships
                entity.HasMany(p => p.Functions)
                    .WithOne()
                    .HasForeignKey("ProcessID")
                    .OnDelete(DeleteBehavior.Cascade);

                // Ignore properties that don't exist in database
                entity.Ignore(e => e.IsExpanded);
            });

            // ================================================
            // FUNCTION ENTITY CONFIGURATION
            // ================================================
            modelBuilder.Entity<Function>(entity =>
            {
                // Table name
                entity.ToTable("Functions");

                // Composite primary key or unique index (ProcessID + FunctionPosition)
                // Note: If your database has a different primary key setup, adjust accordingly
                entity.HasKey(e => new { e.ProcessID, e.FunctionPosition });

                // Properties
                entity.Property(e => e.FunctionName)
                    .HasMaxLength(500);

                entity.Property(e => e.FunctionDescription)
                    .HasMaxLength(1000);

                entity.Property(e => e.WEB3Operator)
                    .HasMaxLength(200);

                entity.Property(e => e.Pass_Fail)
                    .HasMaxLength(50);

                entity.Property(e => e.Comments)
                    .HasMaxLength(2000);
            });
        }
    }
}