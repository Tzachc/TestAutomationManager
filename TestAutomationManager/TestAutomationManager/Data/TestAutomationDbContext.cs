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
        /// Maps to the dynamic schema (e.g., PRODUCTION_Selenium)
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Get current schema and table names from SchemaConfigService
            var schemaConfig = TestAutomationManager.Services.SchemaConfigService.Instance;
            string currentSchema = schemaConfig.CurrentSchema;
            string testTableName = schemaConfig.TestTableName;
            string processTableName = schemaConfig.ProcessTableName;
            string functionTableName = schemaConfig.FunctionTableName;

            // ================================================
            // TEST ENTITY CONFIGURATION (Test_WEB3)
            // ================================================
            modelBuilder.Entity<Test>(entity =>
            {
                // Map to schema and table (e.g., [PRODUCTION_Selenium].[Test_WEB3])
                entity.ToTable(testTableName, currentSchema);

                // Primary key
                entity.HasKey(e => e.TestID);

                // Properties - map actual column names
                entity.Property(e => e.TestID)
                    .HasColumnName("TestID");

                entity.Property(e => e.TestName)
                    .HasColumnName("TestName");

                entity.Property(e => e.Bugs)
                    .HasColumnName("Bugs");

                entity.Property(e => e.DisableKillDriver)
                    .HasColumnName("DisableKillDriver");

                entity.Property(e => e.EmailOnFailureOnly)
                    .HasColumnName("EmailOnFailureOnly");

                entity.Property(e => e.ExceptionMessage)
                    .HasColumnName("ExceptionMessage");

                entity.Property(e => e.ExitTestOnFailure)
                    .HasColumnName("ExitTestOnFailure")
                    .HasMaxLength(100);

                entity.Property(e => e.LastRunning)
                    .HasColumnName("LastRunning")
                    .HasMaxLength(50);

                entity.Property(e => e.LastTimePass)
                    .HasColumnName("LastTimePass");

                entity.Property(e => e.RecipientsEmailsList)
                    .HasColumnName("RecipientsEmailsList");

                entity.Property(e => e.RunStatus)
                    .HasColumnName("RunStatus");

                entity.Property(e => e.SendEmailReport)
                    .HasColumnName("SendEmailReport")
                    .HasMaxLength(100);

                entity.Property(e => e.SnapshotMultipleFailure)
                    .HasColumnName("SnapshotMultipleFailure");

                entity.Property(e => e.TestRunAgainTimes)
                    .HasColumnName("TestRunAgainTimes")
                    .HasMaxLength(100);

                // Relationships
                entity.HasMany(t => t.Processes)
                    .WithOne()
                    .HasForeignKey(p => p.TestID)
                    .OnDelete(DeleteBehavior.Cascade);

                // Ignore UI-only properties
                entity.Ignore(e => e.IsActive);
                entity.Ignore(e => e.IsExpanded);
                entity.Ignore(e => e.Id); // Compatibility property
                entity.Ignore(e => e.Name); // Compatibility property
                entity.Ignore(e => e.Status); // Compatibility property
                entity.Ignore(e => e.Category); // Compatibility property
                entity.Ignore(e => e.Description); // Compatibility property
                entity.Ignore(e => e.LastRun); // Compatibility property
            });

            // ================================================
            // PROCESS ENTITY CONFIGURATION (Process_WEB3)
            // ================================================
            modelBuilder.Entity<Process>(entity =>
            {
                // Map to schema and table (e.g., [PRODUCTION_Selenium].[Process_WEB3])
                entity.ToTable(processTableName, currentSchema);

                // Primary key - Index is the unique identifier for each process record
                entity.HasKey(e => e.Index);

                // Alternate key - ProcessID is used for Function relationships
                // Multiple processes can share the same ProcessID (it's a template/definition ID)
                entity.HasAlternateKey(e => e.ProcessID);

                // Properties - map actual column names
                entity.Property(e => e.TestID).HasColumnName("TestID");
                entity.Property(e => e.Comments).HasColumnName("Comments");
                entity.Property(e => e.Index).HasColumnName("Index");
                entity.Property(e => e.LastRunning).HasColumnName("LastRunning");
                entity.Property(e => e.Module).HasColumnName("Module");
                entity.Property(e => e.Pass_Fail_WEB3Operator).HasColumnName("Pass_Fail_WEB3Operator");
                entity.Property(e => e.ProcessID).HasColumnName("ProcessID");
                entity.Property(e => e.ProcessName).HasColumnName("ProcessName");
                entity.Property(e => e.ProcessPosition).HasColumnName("ProcessPosition");
                entity.Property(e => e.Repeat).HasColumnName("Repeat");
                entity.Property(e => e.TempParam).HasColumnName("TempParam");
                entity.Property(e => e.WEB3Operator).HasColumnName("WEB3Operator");

                // Param columns (Param1-Param46) are mapped automatically by convention

                // Map TempParam columns
                entity.Property(e => e.TempParam1).HasColumnName("TempParam1");
                entity.Property(e => e.TempParam11).HasColumnName("TempParam11");
                entity.Property(e => e.TempParam111).HasColumnName("TempParam111");
                entity.Property(e => e.TempParam1111).HasColumnName("TempParam1111");
                entity.Property(e => e.TempParam11111).HasColumnName("TempParam11111");
                entity.Property(e => e.TempParam111111).HasColumnName("TempParam111111");

                // Relationships
                // Functions link to Process via ProcessID (not via the primary key Index)
                entity.HasMany(p => p.Functions)
                    .WithOne()
                    .HasForeignKey(f => f.ProcessID)
                    .HasPrincipalKey(p => p.ProcessID)  // Explicitly use ProcessID, not Index
                    .OnDelete(DeleteBehavior.Cascade);

                // Ignore UI-only and compatibility properties
                entity.Ignore(e => e.IsExpanded);
                entity.Ignore(e => e.Id);
                entity.Ignore(e => e.TestId);
                entity.Ignore(e => e.Name);
                entity.Ignore(e => e.Description);
                entity.Ignore(e => e.Sequence);
                entity.Ignore(e => e.IsCritical);
                entity.Ignore(e => e.Timeout);
            });

            // ================================================
            // FUNCTION ENTITY CONFIGURATION (Function_WEB3)
            // ================================================
            modelBuilder.Entity<Function>(entity =>
            {
                // Map to schema and table (e.g., [PRODUCTION_Selenium].[Function_WEB3])
                entity.ToTable(functionTableName, currentSchema);

                // Primary key (using Index column)
                entity.HasKey(e => e.Index);

                // Properties - map actual column names
                entity.Property(e => e.ActualValue).HasColumnName("ActualValue");
                entity.Property(e => e.BreakPoint).HasColumnName("BreakPoint").HasMaxLength(50);
                entity.Property(e => e.Comments).HasColumnName("Comments").HasMaxLength(50);
                entity.Property(e => e.FunctionDescription).HasColumnName("FunctionDescription");
                entity.Property(e => e.FunctionName).HasColumnName("FunctionName");
                entity.Property(e => e.FunctionPosition).HasColumnName("FunctionPosition");
                entity.Property(e => e.Index).HasColumnName("Index");
                entity.Property(e => e.Pass_Fail_WEB3Operator).HasColumnName("Pass_Fail_WEB3Operator");
                entity.Property(e => e.ProcessID).HasColumnName("ProcessID");
                entity.Property(e => e.WEB3Operator).HasColumnName("WEB3Operator");

                // Param columns (Param1-Param30) are mapped automatically by convention

                // Ignore compatibility properties
                entity.Ignore(e => e.Id);
                entity.Ignore(e => e.ProcessId);
                entity.Ignore(e => e.Name);
                entity.Ignore(e => e.MethodName);
                entity.Ignore(e => e.Parameters);
                entity.Ignore(e => e.ExpectedResult);
                entity.Ignore(e => e.Sequence);
            });
        }
    }
}