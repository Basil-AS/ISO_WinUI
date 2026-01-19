using System;
using System.Collections.Generic;
using System.Security.Principal;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ModernDisk.Core.Services;
using Windows.Storage.Pickers;

namespace ModernDisk.UI
{
    public sealed partial class MainWindow : Window
    {
        private readonly VirtualDiskService _virtualDiskService = new();
        private readonly IsoBuilderService _isoBuilderService = new();
        private readonly DiscWriterService _discWriterService = new();

        public MainWindow()
        {
            InitializeComponent();
            RefreshRecorders();
        }

        private static bool IsAdministrator()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private bool EnsureAdmin(TextBlock statusBlock)
        {
            if (IsAdministrator())
                return true;

            statusBlock.Text = "Administrator privileges are required.";
            return false;
        }

        private void InitializePicker(object picker)
        {
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        }

        private async void MountBrowse_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".iso");
            InitializePicker(picker);

            var file = await picker.PickSingleFileAsync();
            if (file != null)
                MountIsoPathBox.Text = file.Path;
        }

        private void MountIso_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureAdmin(MountStatusText))
                return;

            try
            {
                _virtualDiskService.MountIso(MountIsoPathBox.Text);
                MountStatusText.Text = "ISO mounted.";
            }
            catch (Exception ex)
            {
                MountStatusText.Text = $"Error: {ex.Message}";
            }
        }

        private void UnmountIso_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureAdmin(MountStatusText))
                return;

            try
            {
                _virtualDiskService.UnmountIso(MountIsoPathBox.Text);
                MountStatusText.Text = "ISO unmounted.";
            }
            catch (Exception ex)
            {
                MountStatusText.Text = $"Error: {ex.Message}";
            }
        }

        private async void CreateBrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FolderPicker();
            picker.FileTypeFilter.Add("*");
            InitializePicker(picker);

            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
                CreateSourceFolderBox.Text = folder.Path;
        }

        private async void CreateBrowseOutput_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileSavePicker
            {
                SuggestedFileName = "output"
            };

            picker.FileTypeChoices.Add("ISO Image", new List<string> { ".iso" });
            InitializePicker(picker);

            var file = await picker.PickSaveFileAsync();
            if (file != null)
                CreateOutputPathBox.Text = file.Path;
        }

        private async void CreateIso_Click(object sender, RoutedEventArgs e)
        {
            CreateIsoButton.IsEnabled = false;
            CreateProgressBar.IsIndeterminate = true;
            CreateProgressBar.Value = 0;
            CreateStatusText.Text = string.Empty;

            try
            {
                var progress = new Progress<IsoBuilderService.BuildProgress>(p =>
                {
                    if (p.TotalFiles > 0)
                    {
                        CreateProgressBar.IsIndeterminate = false;
                        CreateProgressBar.Maximum = p.TotalFiles;
                        CreateProgressBar.Value = p.ProcessedFiles;
                    }

                    CreateStatusText.Text = $"{p.ProcessedFiles}/{p.TotalFiles} - {p.CurrentFile}";
                });

                string output = await _isoBuilderService.CreateIsoFileAsync(
                    CreateSourceFolderBox.Text,
                    CreateOutputPathBox.Text,
                    CreateVolumeLabelBox.Text,
                    progress);

                CreateStatusText.Text = $"ISO created: {output}";
            }
            catch (Exception ex)
            {
                CreateStatusText.Text = $"Error: {ex.Message}";
            }
            finally
            {
                CreateIsoButton.IsEnabled = true;
                CreateProgressBar.IsIndeterminate = false;
            }
        }

        private async void BurnBrowseIso_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".iso");
            InitializePicker(picker);

            var file = await picker.PickSingleFileAsync();
            if (file != null)
                BurnIsoPathBox.Text = file.Path;
        }

        private void RefreshRecorders_Click(object sender, RoutedEventArgs e)
        {
            RefreshRecorders();
        }

        private void RefreshRecorders()
        {
            var recorders = _discWriterService.GetAvailableRecorders();
            RecorderComboBox.ItemsSource = recorders;
            RecorderComboBox.SelectedIndex = recorders.Count > 0 ? 0 : -1;

            BurnStatusText.Text = recorders.Count > 0
                ? $"Found {recorders.Count} recorder(s)."
                : "No recorders found.";
        }

        private async void BurnIso_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureAdmin(BurnStatusText))
                return;

            if (RecorderComboBox.SelectedItem is not DiscWriterService.RecorderInfo recorder)
            {
                BurnStatusText.Text = "Select a recorder first.";
                return;
            }

            BurnIsoButton.IsEnabled = false;
            BurnProgressBar.IsIndeterminate = true;
            BurnProgressBar.Value = 0;

            try
            {
                var progress = new Progress<DiscWriterService.WriteProgress>(p =>
                {
                    BurnProgressBar.IsIndeterminate = false;
                    BurnProgressBar.Value = p.PercentComplete;
                    BurnStatusText.Text = $"Burning... {p.PercentComplete}%";
                });

                await _discWriterService.BurnIsoAsync(BurnIsoPathBox.Text, recorder.UniqueId, progress);

                BurnProgressBar.IsIndeterminate = false;
                BurnProgressBar.Value = 100;
                BurnStatusText.Text = "Burn completed.";
            }
            catch (Exception ex)
            {
                BurnStatusText.Text = $"Error: {ex.Message}";
            }
            finally
            {
                BurnIsoButton.IsEnabled = true;
                BurnProgressBar.IsIndeterminate = false;
            }
        }
    }
}
