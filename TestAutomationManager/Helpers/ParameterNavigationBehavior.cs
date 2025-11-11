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
                    // Create modern, compact context menu with rounded corners
                    var contextMenu = new ContextMenu
                    {
                        PlacementTarget = textBlock,
                        Background = (Brush)Application.Current.Resources["CardBackgroundBrush"],
                        BorderBrush = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)),
                        BorderThickness = new Thickness(0.5),
                        Padding = new Thickness(2),
                        HasDropShadow = true,
                        Effect = new System.Windows.Media.Effects.DropShadowEffect
                        {
                            Color = Colors.Black,
                            BlurRadius = 8,
                            ShadowDepth = 2,
                            Opacity = 0.2
                        }
                    };

                    // Apply rounded corner template
                    var menuBorder = new Border
                    {
                        CornerRadius = new CornerRadius(6),
                        Background = (Brush)Application.Current.Resources["CardBackgroundBrush"]
                    };

                    // Create compact icon
                    var icon = new TextBlock
                    {
                        Text = target.Type == ParameterNavigationHelper.NavigationType.ExtTest ? "📊" : "⚙️",
                        FontSize = 13,
                        Margin = new Thickness(0, 0, 6, 0),
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
                        FontSize = 12,
                        FontWeight = FontWeights.Normal,
                        VerticalAlignment = VerticalAlignment.Center
                    });

                    // Add compact navigation menu item
                    var menuItem = new MenuItem
                    {
                        Header = headerPanel,
                        Tag = new NavigationMenuItemTag { Target = target, TextBlock = textBlock },
                        Padding = new Thickness(8, 5, 8, 5),
                        Foreground = (Brush)Application.Current.Resources["TextPrimaryBrush"]
                    };

                    // Create modern style with rounded corners and smooth hover
                    var style = new Style(typeof(MenuItem));
                    style.Setters.Add(new Setter(MenuItem.BackgroundProperty, Brushes.Transparent));

                    var hoverTrigger = new Trigger { Property = MenuItem.IsMouseOverProperty, Value = true };
                    hoverTrigger.Setters.Add(new Setter(MenuItem.BackgroundProperty, new SolidColorBrush(Color.FromArgb(20, 0, 120, 215))));
                    hoverTrigger.Setters.Add(new Setter(MenuItem.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(40, 0, 120, 215))));
                    hoverTrigger.Setters.Add(new Setter(MenuItem.BorderThicknessProperty, new Thickness(0)));

                    style.Triggers.Add(hoverTrigger);
                    menuItem.Style = style;

                    menuItem.Click += MenuItem_Navigate_Click;

                    contextMenu.Items.Add(menuItem);

                    // Apply rounded corners to context menu
                    contextMenu.Resources.Add(typeof(Border), new Style(typeof(Border))
                    {
                        Setters =
                        {
                            new Setter(Border.CornerRadiusProperty, new CornerRadius(6))
                        }
                    });

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
