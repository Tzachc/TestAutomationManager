using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace TestAutomationManager.Services
{
    public static class VisualSearchHighlighter
    {
        private static readonly Brush HighlightBrush = new SolidColorBrush(Color.FromArgb(100, 255, 255, 0));

        public static void ClearHighlights(DependencyObject parent)
        {
            if (parent == null) return;

            foreach (var tb in FindVisualChildren<TextBlock>(parent))
            {
                ClearTextBlockHighlight(tb);
            }

            foreach (var tb in FindVisualChildren<TextBox>(parent))
            {
                tb.Background = Brushes.Transparent;
            }
        }

        public static (int matches, FrameworkElement firstMatch) HighlightText(DependencyObject parent, string query, bool exactMatch)
        {
            if (string.IsNullOrWhiteSpace(query) || parent == null)
                return (0, null);

            int matchCount = 0;
            FrameworkElement first = null;

            foreach (var tb in FindVisualChildren<TextBlock>(parent))
            {
                int m = HighlightInTextBlock(tb, query, exactMatch);
                if (m > 0 && first == null)
                    first = tb;
                matchCount += m;
            }

            foreach (var tb in FindVisualChildren<TextBox>(parent))
            {
                bool isMatch = exactMatch
                    ? string.Equals(tb.Text, query, StringComparison.OrdinalIgnoreCase)
                    : tb.Text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

                if (isMatch)
                {
                    tb.Background = HighlightBrush;
                    if (first == null)
                        first = tb;
                    matchCount++;
                }
            }

            return (matchCount, first);
        }

        private static void ClearTextBlockHighlight(TextBlock tb)
        {
            if (tb.Inlines.Count > 0)
            {
                string original = new TextRange(tb.ContentStart, tb.ContentEnd).Text;
                tb.Text = original.TrimEnd('\r', '\n');
            }
        }

        private static int HighlightInTextBlock(TextBlock tb, string query, bool exactMatch)
        {
            string text = new TextRange(tb.ContentStart, tb.ContentEnd).Text;
            int matches = 0;

            if (exactMatch)
            {
                if (string.Equals(text.Trim(), query, StringComparison.OrdinalIgnoreCase))
                {
                    tb.Background = HighlightBrush;
                    matches = 1;
                }
                return matches;
            }

            int index = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                return 0;

            tb.Inlines.Clear();
            int pos = 0;

            while (index >= 0)
            {
                string before = text.Substring(pos, index - pos);
                if (!string.IsNullOrEmpty(before))
                    tb.Inlines.Add(before);

                var run = new Run(text.Substring(index, query.Length))
                {
                    Background = HighlightBrush,
                    Foreground = Brushes.Black
                };
                tb.Inlines.Add(run);
                matches++;

                pos = index + query.Length;
                if (pos >= text.Length) break;

                index = text.IndexOf(query, pos, StringComparison.OrdinalIgnoreCase);
            }

            if (pos < text.Length)
                tb.Inlines.Add(text.Substring(pos));

            return matches;
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent)
            where T : DependencyObject
        {
            if (parent == null) yield break;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T tChild)
                    yield return tChild;

                foreach (var sub in FindVisualChildren<T>(child))
                    yield return sub;
            }
        }
    }
}
