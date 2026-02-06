using Terminal.Gui;
using Opcilloscope.App.Themes;
using Opcilloscope.Configuration;
using AppThemeManager = Opcilloscope.App.Themes.ThemeManager;

namespace Opcilloscope.App.Dialogs;

/// <summary>
/// Custom open dialog for configuration files that lists files
/// sorted by last modified time (newest first).
/// </summary>
public class OpenConfigDialog : Dialog
{
    private readonly ListView _fileListView;
    private readonly Label _directoryLabel;
    private readonly List<FileInfo> _files = new();
    private bool _confirmed;

    /// <summary>
    /// Gets the full path to the selected file.
    /// </summary>
    public string? SelectedFilePath { get; private set; }

    /// <summary>
    /// Gets whether the user confirmed the selection.
    /// </summary>
    public bool Confirmed => _confirmed;

    public OpenConfigDialog()
    {
        var theme = AppThemeManager.Current;
        var configDir = ConfigurationService.GetDefaultConfigDirectory();

        Title = " Open Configuration ";
        Width = 70;
        Height = Dim.Fill(2);

        ColorScheme = theme.DialogColorScheme;
        BorderStyle = LineStyle.Double;
        if (Border != null)
        {
            Border.ColorScheme = theme.BorderColorScheme;
        }

        _directoryLabel = new Label
        {
            X = 1,
            Y = 0,
            Width = Dim.Fill(1),
            Text = configDir,
            ColorScheme = theme.MainColorScheme
        };

        // Scan config directory for files sorted by last modified (newest first)
        LoadFiles(configDir);

        var displayNames = _files.Select(f =>
        {
            var modified = f.LastWriteTime.ToString("yyyy-MM-dd HH:mm");
            return $"{f.Name,-40} {modified}";
        }).ToList();

        _fileListView = new ListView
        {
            X = 1,
            Y = 2,
            Width = Dim.Fill(1),
            Height = Dim.Fill(3),
            ColorScheme = theme.MainColorScheme
        };
        _fileListView.SetSource(new System.Collections.ObjectModel.ObservableCollection<string>(displayNames));
        _fileListView.OpenSelectedItem += (_, _) => Confirm();

        var openButton = new Button
        {
            X = Pos.Center() - 12,
            Y = Pos.AnchorEnd(1),
            Text = $"{theme.ButtonPrefix}Open{theme.ButtonSuffix}",
            IsDefault = true,
            ColorScheme = new ColorScheme
            {
                Normal = new Terminal.Gui.Attribute(theme.Accent, theme.Background),
                Focus = new Terminal.Gui.Attribute(theme.AccentBright, theme.Background),
                HotNormal = new Terminal.Gui.Attribute(theme.Accent, theme.Background),
                HotFocus = new Terminal.Gui.Attribute(theme.AccentBright, theme.Background),
                Disabled = new Terminal.Gui.Attribute(theme.MutedText, theme.Background)
            }
        };
        openButton.Accepting += (_, _) => Confirm();

        var browseButton = new Button
        {
            X = Pos.Center() - 1,
            Y = Pos.AnchorEnd(1),
            Text = $"{theme.ButtonPrefix}Browse...{theme.ButtonSuffix}",
            ColorScheme = theme.ButtonColorScheme
        };
        browseButton.Accepting += OnBrowse;

        var cancelButton = new Button
        {
            X = Pos.Center() + 12,
            Y = Pos.AnchorEnd(1),
            Text = $"{theme.ButtonPrefix}Cancel{theme.ButtonSuffix}",
            ColorScheme = theme.ButtonColorScheme
        };
        cancelButton.Accepting += (_, _) =>
        {
            _confirmed = false;
            Application.RequestStop();
        };

        Add(_directoryLabel, _fileListView, openButton, browseButton, cancelButton);
        _fileListView.SetFocus();
    }

    private void LoadFiles(string directory)
    {
        _files.Clear();

        if (!Directory.Exists(directory))
            return;

        var extensions = new[] { ConfigurationService.ConfigFileExtension, ".opcilloscope", ".json" };

        var files = extensions
            .SelectMany(ext => Directory.GetFiles(directory, $"*{ext}"))
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTime)
            .ToList();

        _files.AddRange(files);
    }

    private void Confirm()
    {
        if (_fileListView.SelectedItem >= 0 && _fileListView.SelectedItem < _files.Count)
        {
            SelectedFilePath = _files[_fileListView.SelectedItem].FullName;
            _confirmed = true;
            Application.RequestStop();
        }
    }

    private void OnBrowse(object? sender, CommandEventArgs e)
    {
        using var dialog = new OpenDialog
        {
            Title = "Open Configuration",
            AllowedTypes = new List<IAllowedType>
            {
                new AllowedType("opcilloscope Config", ConfigurationService.ConfigFileExtension),
                new AllowedType("Legacy opcilloscope Config", ".opcilloscope"),
                new AllowedType("JSON Files", ".json")
            },
            Path = ConfigurationService.GetDefaultConfigDirectory()
        };

        Application.Run(dialog);

        if (!dialog.Canceled && dialog.Path != null)
        {
            SelectedFilePath = dialog.Path.ToString()!;
            _confirmed = true;
            Application.RequestStop();
        }
    }
}
