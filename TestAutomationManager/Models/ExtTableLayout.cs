using System;
using System.Collections.Generic;

namespace TestAutomationManager.Models
{
    /// <summary>
    /// Model representing the layout configuration for an ExtTable
    /// Stores column widths and row heights
    /// </summary>
    public class ExtTableLayout
    {
        /// <summary>
        /// Name of the ExtTable (e.g., "ExtTable1")
        /// </summary>
        public string TableName { get; set; }

        /// <summary>
        /// Dictionary of column names to widths
        /// Key: Column name (e.g., "IterationName")
        /// Value: Width in pixels
        /// </summary>
        public Dictionary<string, double> ColumnWidths { get; set; }

        /// <summary>
        /// Dictionary of row indices to heights
        /// Key: Row index (0-based)
        /// Value: Height in pixels
        /// </summary>
        public Dictionary<int, double> RowHeights { get; set; }

        /// <summary>
        /// When this layout was last modified
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// User who last modified this layout
        /// </summary>
        public string ModifiedBy { get; set; }

        public ExtTableLayout()
        {
            ColumnWidths = new Dictionary<string, double>();
            RowHeights = new Dictionary<int, double>();
        }
    }
}