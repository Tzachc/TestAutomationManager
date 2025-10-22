using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TestAutomationManager.Services
{
    public static class ViewportScroller
    {
        /// <summary>
        /// Tries to scroll the UI so that 'element' is visible (both horizontally and vertically).
        /// Handles DataGrid/ListBox/TreeView (ScrollIntoView) and raw ScrollViewer parents.
        /// Returns true if a strategy was applied.
        /// </summary>
        public static bool ScrollToElement(FrameworkElement element)
        {
            if (element == null) return false;

            // 1) If inside a DataGrid/ListBox/TreeView/ItemsControl, prefer ScrollIntoView
            var itemsControl = FindAncestor<ItemsControl>(element);
            if (itemsControl != null)
            {
                var item = element.DataContext;
                if (item != null)
                {
                    // Special-case DataGrid
                    if (itemsControl is DataGrid dg)
                    {
                        // queue to next layout pass to ensure rows are generated
                        element.Dispatcher.InvokeAsync(() =>
                        {
                            dg.UpdateLayout();
                            dg.ScrollIntoView(item);
                            dg.UpdateLayout();

                            // Try to bring the row fully into view (optional)
                            var row = (DataGridRow)dg.ItemContainerGenerator.ContainerFromItem(item);
                            if (row != null)
                            {
                                row.BringIntoView();
                                row.Focus();

                                // Handle horizontal scrolling for DataGrid
                                EnsureElementInHorizontalView(dg, row);
                            }
                        }, System.Windows.Threading.DispatcherPriority.Background);

                        return true;
                    }

                    // Generic ItemsControl (ListBox, ListView, TreeView, ItemsControl with containers)
                    element.Dispatcher.InvokeAsync(() =>
                    {
                        itemsControl.UpdateLayout();
                        ScrollIntoViewIfPossible(itemsControl, item);
                        itemsControl.UpdateLayout();

                        // Make sure the element itself is visible
                        element.BringIntoView();
                        element.Focus();

                        // Handle horizontal scrolling
                        EnsureElementInHorizontalView(itemsControl, element);
                    }, System.Windows.Threading.DispatcherPriority.Background);

                    return true;
                }
            }

            // 2) If there is a ScrollViewer ancestor, compute both horizontal and vertical offset
            var sv = FindAncestor<ScrollViewer>(element);
            if (sv != null)
            {
                element.Dispatcher.InvokeAsync(() =>
                {
                    sv.UpdateLayout();

                    try
                    {
                        GeneralTransform t = element.TransformToAncestor(sv);
                        var rect = t.TransformBounds(new Rect(new Point(0, 0), element.RenderSize));

                        // Vertical scrolling - nudge a little above the element
                        double targetVertical = sv.VerticalOffset + rect.Top - 20;
                        if (targetVertical < 0) targetVertical = 0;
                        sv.ScrollToVerticalOffset(targetVertical);

                        // Horizontal scrolling - center the element if it's outside the viewport
                        double viewportWidth = sv.ViewportWidth;
                        double elementLeft = rect.Left;
                        double elementRight = rect.Right;
                        double currentHorizontalOffset = sv.HorizontalOffset;

                        // Check if element is outside the viewport horizontally
                        if (elementLeft < currentHorizontalOffset)
                        {
                            // Element is to the left of viewport - scroll left
                            double targetHorizontal = elementLeft - 20; // Add small margin
                            if (targetHorizontal < 0) targetHorizontal = 0;
                            sv.ScrollToHorizontalOffset(targetHorizontal);
                        }
                        else if (elementRight > currentHorizontalOffset + viewportWidth)
                        {
                            // Element is to the right of viewport - scroll right
                            double targetHorizontal = elementRight - viewportWidth + 20; // Add small margin
                            if (targetHorizontal < 0) targetHorizontal = 0;
                            sv.ScrollToHorizontalOffset(targetHorizontal);
                        }
                    }
                    catch
                    {
                        // fallback
                        element.BringIntoView();
                    }

                    element.Focus();
                }, System.Windows.Threading.DispatcherPriority.Background);

                return true;
            }

            // 3) Fallback: bring into view on next tick
            element.Dispatcher.InvokeAsync(() =>
            {
                element.UpdateLayout();
                element.BringIntoView();
                element.Focus();
            }, System.Windows.Threading.DispatcherPriority.Background);

            return true;
        }

        /// <summary>
        /// Ensures an element is visible horizontally within its ScrollViewer parent
        /// </summary>
        private static void EnsureElementInHorizontalView(DependencyObject parent, FrameworkElement element)
        {
            var sv = FindAncestor<ScrollViewer>(parent as DependencyObject ?? element);
            if (sv == null) return;

            try
            {
                GeneralTransform t = element.TransformToAncestor(sv);
                var rect = t.TransformBounds(new Rect(new Point(0, 0), element.RenderSize));

                double viewportWidth = sv.ViewportWidth;
                double elementLeft = rect.Left;
                double elementRight = rect.Right;
                double currentHorizontalOffset = sv.HorizontalOffset;

                // Check if element is outside the viewport horizontally
                if (elementLeft < currentHorizontalOffset)
                {
                    // Element is to the left of viewport - scroll left
                    double targetHorizontal = elementLeft - 20; // Add small margin
                    if (targetHorizontal < 0) targetHorizontal = 0;
                    sv.ScrollToHorizontalOffset(targetHorizontal);
                }
                else if (elementRight > currentHorizontalOffset + viewportWidth)
                {
                    // Element is to the right of viewport - scroll right
                    double targetHorizontal = elementRight - viewportWidth + 20; // Add small margin
                    if (targetHorizontal < 0) targetHorizontal = 0;
                    sv.ScrollToHorizontalOffset(targetHorizontal);
                }
            }
            catch
            {
                // Silently fail
            }
        }

        // ---------- helpers ----------

        private static T FindAncestor<T>(DependencyObject start) where T : DependencyObject
        {
            var p = VisualTreeHelper.GetParent(start);
            while (p != null && p is not T)
            {
                p = VisualTreeHelper.GetParent(p);
            }
            return p as T;
        }

        private static void ScrollIntoViewIfPossible(ItemsControl itemsControl, object item)
        {
            // Handle ListView first (it inherits from ListBox)
            if (itemsControl is ListView lv)
            {
                lv.ScrollIntoView(item);
                lv.SelectedItem = item;
            }
            else if (itemsControl is ListBox lb)
            {
                lb.ScrollIntoView(item);
                lb.SelectedItem = item;
            }
            else if (itemsControl is TreeView tv)
            {
                var tvi = (TreeViewItem)tv.ItemContainerGenerator.ContainerFromItem(item);
                tvi?.BringIntoView();
                if (tvi != null) tvi.IsSelected = true;
            }
            else
            {
                // Generic ItemsControl fallback
                var container = itemsControl.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
                container?.BringIntoView();
            }
        }
    }
}