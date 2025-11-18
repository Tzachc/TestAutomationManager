using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace TestAutomationManager.Dialogs
{
    /// <summary>
    /// Dialog for displaying FREE tests with navigation
    /// </summary>
    public partial class FreeTestsDialog : Window
    {
        /// <summary>
        /// Test ID that user selected to navigate to
        /// </summary>
        public int? SelectedTestId { get; private set; }

        public FreeTestsDialog(List<FreeTestInfo> freeTests)
        {
            InitializeComponent();

            // Set subtitle
            SubtitleText.Text = $"{freeTests.Count} test{(freeTests.Count != 1 ? "s" : "")} found";

            // Bind data
            FreeTestsList.ItemsSource = freeTests;
        }

        /// <summary>
        /// Handle "Take Me" button click
        /// </summary>
        private void TakeMeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int testId)
            {
                SelectedTestId = testId;
                DialogResult = true;
                Close();
            }
        }

        /// <summary>
        /// Handle close button click
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    /// <summary>
    /// Information about a FREE test
    /// </summary>
    public class FreeTestInfo
    {
        public int TestId { get; set; }
        public string TestName { get; set; }
        public string Status { get; set; }
    }
}
