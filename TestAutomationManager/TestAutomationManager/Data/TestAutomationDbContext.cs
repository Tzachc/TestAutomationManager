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
                entity.HasKey(e => e.Id);

                // Properties
                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(e => e.Description)
                    .HasMaxLength(500);

                entity.Property(e => e.Category)
                    .HasMaxLength(100);

                entity.Property(e => e.Status)
                    .HasMaxLength(50);

                entity.Property(e => e.IsActive)
                    .HasDefaultValue(true);

                // Relationships
                entity.HasMany(t => t.Processes)
                    .WithOne()
                    .HasForeignKey("TestId")
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
                entity.HasKey(e => e.Id);

                // Properties
                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(e => e.Description)
                    .HasMaxLength(500);

                entity.Property(e => e.IsCritical)
                    .HasDefaultValue(false);

                entity.Property(e => e.Timeout)
                    .HasDefaultValue(30);

                // Relationships
                entity.HasMany(p => p.Functions)
                    .WithOne()
                    .HasForeignKey("ProcessId")
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

                // Primary key
                entity.HasKey(e => e.Id);

                // Properties
                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(e => e.MethodName)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(e => e.Parameters)
                    .HasMaxLength(1000);

                entity.Property(e => e.ExpectedResult)
                    .HasMaxLength(500);
            });
        }
    }
}