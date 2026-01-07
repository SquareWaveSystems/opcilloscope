using Terminal.Gui;
using Opcilloscope.App.Themes;
using Opcilloscope.Configuration;
using AppThemeManager = Opcilloscope.App.Themes.ThemeManager;

namespace Opcilloscope.App.Dialogs;

/// <summary>
/// Custom save dialog for configuration files that preserves the filename
/// when the user navigates to different directories.
/// </summary>
public class SaveConfigDialog : Dialog
{
    private readonly TextField _directoryField;
    private readonly TextField _filenameField;
    private string _currentDirectory;
    private string _currentFilename;
    private bool _confirmed;

    /// <summary>
    /// Gets the full path to save the file (directory + filename with .cfg extension).
    /// </summary>
    public string FilePath => Path.Combine(_currentDirectory,
        ConfigurationService.EnsureConfigExtension(_currentFilename));

    /// <summary>
    /// Gets whether the user confirmed the save operation.
    /// </summary>
    public bool Confirmed => _confirmed;

    /// <summary>
    /// Creates a new SaveConfigDialog with the specified default directory and filename.
    /// </summary>
    /// <param name="defaultDirectory">The default directory to save to.</param>
    /// <param name="defaultFilename">The default filename (with or without .cfg extension).</param>
    public SaveConfigDialog(string defaultDirectory, string defaultFilename)
    {
        var theme = AppThemeManager.Current;

        _currentDirectory = string.IsNullOrEmpty(defaultDirectory)
            ? ConfigurationService.GetDefaultConfigDirectory()
            : defaultDirectory;
        _currentFilename = string.IsNullOrEmpty(defaultFilename)
            ? ConfigurationService.GenerateDefaultFilename(null)
            : defaultFilename;

        Title = " Save Configuration ";
        Width = 70;
        Height = 14;

        // Apply theme styling
        ColorScheme = theme.DialogColorScheme;
        BorderStyle = LineStyle.Double;
        if (Border != null)
        {
            Border.ColorScheme = theme.BorderColorScheme;
        }

        // Directory section
        var directoryLabel = new Label
        {
            X = 1,
            Y = 1,
            Text = "Save in:"
        };

        _directoryField = new TextField
        {
            X = 1,
            Y = 2,
            Width = Dim.Fill(12),
            Text = _currentDirectory,
            ReadOnly = false
        };
        _directoryField.TextChanged += (_, _) =>
        {
            _currentDirectory = _directoryField.Text ?? _currentDirectory;
        };

        var browseButton = new Button
        {
            X = Pos.Right(_directoryField) + 1,
            Y = 2,
            Text = "Browse...",
            ColorScheme = theme.ButtonColorScheme
        };
        browseButton.Accepting += OnBrowseDirectory;

        // Filename section
        var filenameLabel = new Label
        {
            X = 1,
            Y = 4,
            Text = "File name:"
        };

        _filenameField = new TextField
        {
            X = 1,
            Y = 5,
            Width = Dim.Fill(1),
            Text = _currentFilename
        };
        _filenameField.TextChanged += (_, _) =>
        {
            _currentFilename = _filenameField.Text ?? _currentFilename;
        };

        // File type hint
        var typeLabel = new Label
        {
            X = 1,
            Y = 6,
            Text = $"Save as type: Opcilloscope Config (*{ConfigurationService.ConfigFileExtension})",
            ColorScheme = theme.MainColorScheme
        };

        // Info hint about preserving filename
        var hintLabel = new Label
        {
            X = 1,
            Y = 7,
            Text = "Tip: Use Browse to change folder - filename is preserved",
            ColorScheme = theme.MainColorScheme
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
            Y = 9,
            Text = $"{theme.ButtonPrefix}Save{theme.ButtonSuffix}",
            IsDefault = true,
            ColorScheme = defaultButtonScheme
        };
        saveButton.Accepting += OnSave;

        var cancelButton = new Button
        {
            X = Pos.Center() + 3,
            Y = 9,
            Text = $"{theme.ButtonPrefix}Cancel{theme.ButtonSuffix}",
            ColorScheme = theme.ButtonColorScheme
        };
        cancelButton.Accepting += OnCancel;

        Add(directoryLabel, _directoryField, browseButton,
            filenameLabel, _filenameField, typeLabel, hintLabel,
            saveButton, cancelButton);

        _filenameField.SetFocus();
    }

    /// <summary>
    /// Handles the Browse button click to select a save location.
    /// Opens a file dialog that allows the user to navigate directories.
    /// The selected directory is extracted while preserving the current filename.
    /// </summary>
    private void OnBrowseDirectory(object? sender, CommandEventArgs e)
    {
        using var dialog = new OpenDialog
        {
            Title = "Browse for Save Location",
            AllowedTypes = new List<IAllowedType>
            {
                new AllowedType("Opcilloscope Config", ConfigurationService.ConfigFileExtension)
            },
            Path = _currentDirectory,
            // We'll let user navigate to any directory and extract the directory path
        };

        Application.Run(dialog);

        if (!dialog.Canceled && dialog.Path != null)
        {
            var selectedPath = dialog.Path.ToString()!;

            // If user selected a file, get its directory
            // If user selected a directory, use it directly
            if (File.Exists(selectedPath))
            {
                _currentDirectory = Path.GetDirectoryName(selectedPath) ?? _currentDirectory;
            }
            else if (Directory.Exists(selectedPath))
            {
                _currentDirectory = selectedPath;
            }
            else
            {
                // Path doesn't exist yet - assume it's a directory path
                var dirPart = Path.GetDirectoryName(selectedPath);
                if (!string.IsNullOrEmpty(dirPart) && Directory.Exists(dirPart))
                {
                    _currentDirectory = dirPart;
                }
            }

            _directoryField.Text = _currentDirectory;
            // Note: _currentFilename is preserved - this is the key feature!
        }
    }

    private void OnSave(object? sender, CommandEventArgs e)
    {
        if (!ValidateSave())
            return;

        _confirmed = true;
        Application.RequestStop();
    }

    private void OnCancel(object? sender, CommandEventArgs e)
    {
        _confirmed = false;
        Application.RequestStop();
    }

    private bool ValidateSave()
    {
        // Validate filename
        var filename = _currentFilename.Trim();
        if (string.IsNullOrEmpty(filename))
        {
            MessageBox.ErrorQuery("Error", "Please enter a filename", "OK");
            return false;
        }

        // Check for invalid characters in filename
        var invalidChars = Path.GetInvalidFileNameChars();
        if (filename.IndexOfAny(invalidChars) >= 0)
        {
            MessageBox.ErrorQuery("Error", "Filename contains invalid characters", "OK");
            return false;
        }

        // Validate directory
        var directory = _currentDirectory.Trim();
        if (string.IsNullOrEmpty(directory))
        {
            MessageBox.ErrorQuery("Error", "Please specify a directory", "OK");
            return false;
        }

        // Create directory if it doesn't exist
        try
        {
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Error", $"Cannot create directory: {ex.Message}", "OK");
            return false;
        }

        // Check if file exists and prompt for overwrite
        var fullPath = FilePath;
        if (File.Exists(fullPath))
        {
            var result = MessageBox.Query("Confirm Overwrite",
                $"File '{Path.GetFileName(fullPath)}' already exists.\nDo you want to replace it?",
                "Yes", "No");
            if (result != 0) // "No" selected
            {
                return false;
            }
        }

        _currentDirectory = directory;
        _currentFilename = filename;
        return true;
    }
}
