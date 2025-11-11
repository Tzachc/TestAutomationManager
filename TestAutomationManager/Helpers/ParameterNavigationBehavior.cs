using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TestAutomationManager.Models;

namespace TestAutomationManager.Helpers
{
    /// <summary>
    /// Attached behavior that enables parameter navigation via context menu
    /// Detects From_ExtTest_ and From_Process_ patterns and shows navigation options
    /// </summary>
    public static class ParameterNavigationBehavior
    {
        #region Attached Properties

        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(ParameterNavigationBehavior),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static bool GetIsEnabled(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsEnabledProperty);
        }

        public static void SetIsEnabled(DependencyObject obj, bool value)
        {
            obj.SetValue(IsEnabledProperty, value);
        }

        #endregion

        #region Event Handlers

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBlock textBlock)
            {
                if ((bool)e.NewValue)
                {
                    textBlock.ContextMenuOpening += TextBlock_ContextMenuOpening;
                    textBlock.MouseRightButtonDown += TextBlock_MouseRightButtonDown;
                }
                else
                {
                    textBlock.ContextMenuOpening -= TextBlock_ContextMenuOpening;
                    textBlock.MouseRightButtonDown -= TextBlock_MouseRightButtonDown;
                }
            }
        }

        private static void TextBlock_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Mark as handled to prevent default context menu behavior
            if (sender is TextBlock textBlock)
            {
                var paramValue = textBlock.Text;
                if (ParameterNavigationHelper.IsNavigationPattern(paramValue))
                {
                    e.Handled = true;
                }
            }
        }

        private static void TextBlock_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (sender is TextBlock textBlock)
            {
                var paramValue = textBlock.Text;
                var target = ParameterNavigationHelper.ParseNavigationTarget(paramValue);

                if (target != null)
                {
                    // Create context menu
                    var contextMenu = new ContextMenu
                    {
                        PlacementTarget = textBlock,
                        Background = (Brush)Application.Current.Resources["CardBackgroundBrush"],
                        BorderBrush = (Brush)Application.Current.Resources["BorderBrush"],
                        BorderThickness = new Thickness(1),
                        Padding = new Thickness(4)
                    };

                    // Add navigation menu item
                    var menuItem = new MenuItem
                    {
                        Header = ParameterNavigationHelper.GetNavigationDescription(target),
                        Tag = new { Target = target, TextBlock = textBlock }
                    };
                    menuItem.Click += MenuItem_Navigate_Click;

                    contextMenu.Items.Add(menuItem);

                    // Set and show context menu
                    textBlock.ContextMenu = contextMenu;
                    contextMenu.IsOpen = true;
                }
                else
                {
                    // No navigation pattern - cancel context menu
                    e.Handled = true;
                }
            }
        }

        private static void MenuItem_Navigate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is dynamic tag)
            {
                var target = tag.Target as ParameterNavigationHelper.NavigationTarget;
                var textBlock = tag.TextBlock as TextBlock;

                if (target != null && textBlock != null)
                {
                    // Find the TestsView parent
                    var testsView = FindParent<Views.TestsView>(textBlock);
                    if (testsView != null)
                    {
                        // Get the data context (Process or Function)
                        var dataContext = textBlock.DataContext;

                        switch (target.Type)
                        {
                            case ParameterNavigationHelper.NavigationType.Process:
                                HandleProcessNavigation(testsView, dataContext, target.TargetName);
                                break;

                            case ParameterNavigationHelper.NavigationType.ExtTest:
                                HandleExtTestNavigation(testsView, dataContext, target.TargetName);
                                break;
                        }
                    }
                }
            }
        }

        #endregion

        #region Navigation Handlers

        private static void HandleProcessNavigation(Views.TestsView testsView, object dataContext, string paramName)
        {
            Process? targetProcess = null;
            Test? parentTest = null;

            // Determine the target process based on the data context
            if (dataContext is Function function)
            {
                // Navigation from Function to Process
                targetProcess = function.ParentProcess;
                parentTest = targetProcess?.ParentTest;
            }
            else if (dataContext is Process process)
            {
                // Navigation within Process (same process)
                targetProcess = process;
                parentTest = process.ParentTest;
            }

            if (targetProcess != null && parentTest != null)
            {
                // Ensure the test is expanded
                if (!parentTest.IsExpanded)
                {
                    parentTest.IsExpanded = true;
                }

                // Scroll to the target parameter in the process
                testsView.ScrollToProcessParameter(parentTest, targetProcess, paramName);
            }
        }

        private static void HandleExtTestNavigation(Views.TestsView testsView, object dataContext, string columnName)
        {
            Test? test = null;

            // Determine the test based on the data context
            if (dataContext is Function function)
            {
                test = function.ParentProcess?.ParentTest;
            }
            else if (dataContext is Process process)
            {
                test = process.ParentTest;
            }
            else if (dataContext is Test t)
            {
                test = t;
            }

            if (test != null && test.TestID.HasValue)
            {
                var testId = (int)test.TestID.Value;
                testsView.NavigateToExtTest(testId, columnName);
            }
        }

        #endregion

        #region Helper Methods

        private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parent = VisualTreeHelper.GetParent(child);

            while (parent != null)
            {
                if (parent is T typedParent)
                    return typedParent;

                parent = VisualTreeHelper.GetParent(parent);
            }

            return null;
        }

        #endregion
    }
}
