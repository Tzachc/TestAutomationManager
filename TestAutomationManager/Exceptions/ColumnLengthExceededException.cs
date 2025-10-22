using System;

namespace TestAutomationManager.Exceptions
{
    /// <summary>
    /// Exception thrown when text exceeds column length limit
    /// </summary>
    public class ColumnLengthExceededException : Exception
    {
        public string ColumnName { get; set; }
        public int MaxLength { get; set; }
        public int ActualLength { get; set; }
        public int ExcessCharacters => ActualLength - MaxLength;

        public ColumnLengthExceededException(string columnName, int maxLength, int actualLength)
            : base($"Text too long for column '{columnName}'")
        {
            ColumnName = columnName;
            MaxLength = maxLength;
            ActualLength = actualLength;
        }

        /// <summary>
        /// Get user-friendly message with all details
        /// </summary>
        public string GetDetailedMessage()
        {
            return $"Text is too long for column '{ColumnName}'.\n\n" +
                   $"Maximum length: {MaxLength} characters\n" +
                   $"Your text length: {ActualLength} characters\n" +
                   $"Excess: {ExcessCharacters} characters\n\n" +
                   $"Please shorten your text by {ExcessCharacters} characters, " +
                   $"or contact your administrator to increase the column size.";
        }
    }
}