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
        #region Helper Classes

        private class NavigationMenuItemTag
        {
            public ParameterNavigationHelper.NavigationTarget Target { get; set; }
            public TextBlock TextBlock { get; set; }
        }

        #endregion

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
                    // Create context menu with modern styling
                    var contextMenu = new ContextMenu
                    {
                        PlacementTarget = textBlock,
                        Background = (Brush)Application.Current.Resources["CardBackgroundBrush"],
                        BorderBrush = (Brush)Application.Current.Resources["BorderBrush"],
                        BorderThickness = new Thickness(1),
                        Padding = new Thickness(4),
                        HasDropShadow = true,
                        Effect = new System.Windows.Media.Effects.DropShadowEffect
                        {
                            Color = Colors.Black,
                            BlurRadius = 10,
                            ShadowDepth = 3,
                            Opacity = 0.3
                        }
                    };

                    // Create icon for the menu item
                    var icon = new TextBlock
                    {
                        Text = target.Type == ParameterNavigationHelper.NavigationType.ExtTest ? "📊" : "⚙️",
                        FontSize = 16,
                        Margin = new Thickness(0, 0, 8, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    // Create header with icon and text
                    var headerPanel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal
                    };
                    headerPanel.Children.Add(icon);
                    headerPanel.Children.Add(new TextBlock
                    {
                        Text = ParameterNavigationHelper.GetNavigationDescription(target),
                        FontWeight = FontWeights.Medium,
                        VerticalAlignment = VerticalAlignment.Center
                    });

                    // Add navigation menu item with enhanced styling
                    var menuItem = new MenuItem
                    {
                        Header = headerPanel,
                        Tag = new NavigationMenuItemTag { Target = target, TextBlock = textBlock },
                        FontSize = 13,
                        Padding = new Thickness(12, 8, 12, 8),
                        Foreground = (Brush)Application.Current.Resources["TextPrimaryBrush"]
                    };

                    // Add hover effect style
                    var style = new Style(typeof(MenuItem));
                    var hoverTrigger = new Trigger { Property = MenuItem.IsMouseOverProperty, Value = true };
                    hoverTrigger.Setters.Add(new Setter(MenuItem.BackgroundProperty, new SolidColorBrush(Color.FromArgb(30, 0, 120, 215))));
                    style.Triggers.Add(hoverTrigger);
                    menuItem.Style = style;

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
            if (sender is MenuItem menuItem && menuItem.Tag is NavigationMenuItemTag tag)
            {
                var target = tag.Target;
                var textBlock = tag.TextBlock;

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
