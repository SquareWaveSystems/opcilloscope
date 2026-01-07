using System.Collections.ObjectModel;
using Terminal.Gui;
using Opcilloscope.App.Themes;
using Opcilloscope.Utilities;
using ThemeManager = Opcilloscope.App.Themes.ThemeManager;

namespace Opcilloscope.App.Dialogs;

/// <summary>
/// Custom save dialog for CSV recordings that preserves the filename when navigating directories.
/// </summary>
public class SaveRecordingDialog : Dialog
{
    private readonly TextField _directoryField;
    private readonly TextField _filenameField;
    private readonly ListView _fileListView;
    private readonly ObservableCollection<string> _fileListItems = new();
    private string _currentDirectory;
    private bool _confirmed;

    /// <summary>
    /// Gets the full path to save the recording.
    /// </summary>
    public string? FilePath { get; private set; }

    /// <summary>
    /// Gets whether the user confirmed the save.
    /// </summary>
    public bool Confirmed => _confirmed;

    /// <summary>
    /// Creates a new SaveRecordingDialog.
    /// </summary>
    /// <param name="defaultDirectory">The initial directory to show.</param>
    /// <param name="defaultFilename">The initial filename (without path).</param>
    public SaveRecordingDialog(string defaultDirectory, string defaultFilename)
    {
        var theme = ThemeManager.Current;

        Title = " Save Recording ";
        Width = Dim.Percent(80);
        Height = Dim.Percent(80);

        // Apply theme styling
        ColorScheme = theme.DialogColorScheme;
        BorderStyle = LineStyle.Double;
        if (Border != null)
        {
            Border.ColorScheme = theme.BorderColorScheme;
        }

        _currentDirectory = defaultDirectory;

        // Directory label and field
        var directoryLabel = new Label
        {
            X = 1,
            Y = 1,
            Text = "Directory:"
        };

        _directoryField = new TextField
        {
            X = 1,
            Y = 2,
            Width = Dim.Fill(1),
            Text = _currentDirectory,
            ReadOnly = true
        };

        // File list for browsing
        var fileListLabel = new Label
        {
            X = 1,
            Y = 4,
            Text = "Files (↑↓ navigate, Enter to open folder, Backspace for parent):"
        };

        _fileListView = new ListView
        {
            X = 1,
            Y = 5,
            Width = Dim.Fill(1),
            Height = Dim.Fill(6),
            ColorScheme = theme.MainColorScheme
        };

        _fileListView.OpenSelectedItem += OnFileListOpenSelected;
        _fileListView.KeyDown += OnFileListKeyDown;

        // Filename label and field
        var filenameLabel = new Label
        {
            X = 1,
            Y = Pos.AnchorEnd(4),
            Text = "Filename:"
        };

        _filenameField = new TextField
        {
            X = 1,
            Y = Pos.AnchorEnd(3),
            Width = Dim.Fill(1),
            Text = defaultFilename
        };

        // Buttons
        var defaultButtonScheme = new ColorScheme
        {
            Normal = new Terminal.Gui.Attribute(theme.Accent, theme.Background),
            Focus = new Terminal.Gui.Attribute(theme.AccentBright, theme.Background),
            HotNormal = new Terminal.Gui.Attribute(theme.Accent, theme.Background),
            HotFocus = new Terminal.Gui.Attribute(theme.AccentBright, theme.Background),
            Disabled = new Terminal.Gui.Attribute(theme.MutedText, theme.Background)
        };

        var saveButton = new Button
        {
            X = Pos.Center() - 10,
            Y = Pos.AnchorEnd(1),
            Text = $"{theme.ButtonPrefix}Save{theme.ButtonSuffix}",
            IsDefault = true,
            ColorScheme = defaultButtonScheme
        };

        saveButton.Accepting += (_, _) =>
        {
            if (ValidateAndSetPath())
            {
                _confirmed = true;
                Application.RequestStop();
            }
        };

        var cancelButton = new Button
        {
            X = Pos.Center() + 4,
            Y = Pos.AnchorEnd(1),
            Text = $"{theme.ButtonPrefix}Cancel{theme.ButtonSuffix}",
            ColorScheme = theme.ButtonColorScheme
        };

        cancelButton.Accepting += (_, _) =>
        {
            _confirmed = false;
            Application.RequestStop();
        };

        Add(directoryLabel, _directoryField, fileListLabel, _fileListView,
            filenameLabel, _filenameField, saveButton, cancelButton);

        // Load initial directory contents
        LoadDirectory(_currentDirectory);

        _filenameField.SetFocus();
    }

    private void LoadDirectory(string directory)
    {
        _fileListItems.Clear();

        try
        {
            // Ensure directory exists
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _currentDirectory = directory;
            _directoryField.Text = directory;

            // Add parent directory option
            var parent = Directory.GetParent(directory);
            if (parent != null)
            {
                _fileListItems.Add("..");
            }

            // Add subdirectories
            foreach (var dir in Directory.GetDirectories(directory).OrderBy(d => d))
            {
                var name = Path.GetFileName(dir);
                _fileListItems.Add($"[{name}]");
            }

            // Add CSV files
            foreach (var file in Directory.GetFiles(directory, "*.csv").OrderBy(f => f))
            {
                var name = Path.GetFileName(file);
                _fileListItems.Add(name);
            }

            _fileListView.SetSource(_fileListItems);
            _fileListView.SelectedItem = 0;
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Error", $"Cannot access directory:\n{ex.Message}", "OK");
        }
    }

    private void OnFileListOpenSelected(object? sender, ListViewItemEventArgs e)
    {
        NavigateToSelected();
    }

    private void OnFileListKeyDown(object? sender, Key e)
    {
        if (e == Key.Backspace)
        {
            NavigateToParent();
            e.Handled = true;
        }
    }

    private void NavigateToSelected()
    {
        if (_fileListView.SelectedItem < 0 || _fileListView.SelectedItem >= _fileListItems.Count)
            return;

        var selected = _fileListItems[_fileListView.SelectedItem];

        if (selected == "..")
        {
            NavigateToParent();
        }
        else if (selected.StartsWith("[") && selected.EndsWith("]"))
        {
            // Directory - navigate into it
            var dirName = selected.Substring(1, selected.Length - 2);
            var newPath = Path.Combine(_currentDirectory, dirName);
            LoadDirectory(newPath);
        }
        else
        {
            // File - populate filename field
            _filenameField.Text = selected;
            _filenameField.SetFocus();
        }
    }

    private void NavigateToParent()
    {
        var parent = Directory.GetParent(_currentDirectory);
        if (parent != null)
        {
            LoadDirectory(parent.FullName);
        }
    }

    private bool ValidateAndSetPath()
    {
        var filename = _filenameField.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(filename))
        {
            MessageBox.ErrorQuery("Error", "Please enter a filename", "OK");
            return false;
        }

        // Ensure .csv extension
        filename = CsvRecordingManager.EnsureRecordingExtension(filename);

        // Check for invalid filename characters
        var invalidChars = Path.GetInvalidFileNameChars();
        if (filename.IndexOfAny(invalidChars) >= 0)
        {
            MessageBox.ErrorQuery("Error", "Filename contains invalid characters", "OK");
            return false;
        }

        var fullPath = Path.Combine(_currentDirectory, filename);

        // Check if file already exists
        if (File.Exists(fullPath))
        {
            var result = MessageBox.Query("Confirm Overwrite",
                $"File already exists:\n{filename}\n\nOverwrite?",
                "Yes", "No");
            if (result != 0)
            {
                return false;
            }
        }

        FilePath = fullPath;
        return true;
    }
}
