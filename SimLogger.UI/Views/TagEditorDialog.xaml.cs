using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace SimLogger.UI.Views;

public partial class TagEditorDialog : Window
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    private readonly ObservableCollection<string> _currentTags;
    private readonly List<string> _allExistingTags;

    public List<string> ResultTags { get; private set; } = new();

    public TagEditorDialog(List<string> currentTags, List<string> allExistingTags)
    {
        InitializeComponent();

        SourceInitialized += (s, e) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int value = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
        };

        _currentTags = new ObservableCollection<string>(currentTags);
        _allExistingTags = allExistingTags;

        CurrentTagsList.ItemsSource = _currentTags;
        RefreshExistingTags();
    }

    private void RefreshExistingTags()
    {
        var available = _allExistingTags
            .Where(t => !_currentTags.Contains(t))
            .ToList();

        ExistingTagsList.ItemsSource = available;
        ExistingTagsPanel.Visibility = available.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AddTag(string tagName)
    {
        var trimmed = tagName.Trim();
        if (string.IsNullOrEmpty(trimmed)) return;
        if (_currentTags.Contains(trimmed)) return;

        _currentTags.Add(trimmed);
        NewTagTextBox.Text = string.Empty;
        NewTagTextBox.Focus();
        RefreshExistingTags();
    }

    private void AddTagButton_Click(object sender, RoutedEventArgs e)
    {
        AddTag(NewTagTextBox.Text);
    }

    private void NewTagTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            AddTag(NewTagTextBox.Text);
            e.Handled = true;
        }
    }

    private void RemoveTag_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is string tag)
        {
            _currentTags.Remove(tag);
            RefreshExistingTags();
        }
    }

    private void ExistingTag_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is string tag)
        {
            AddTag(tag);
        }
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        ResultTags = _currentTags.ToList();
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
