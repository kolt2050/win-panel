using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WinPanel.Models;
using WinPanel.Services;

namespace WinPanel
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<ShortcutItem> _shortcuts = new();
        private readonly ConfigService _configService = new();
        private ShortcutItem? _contextMenuTarget;
        private bool _isVertical = false;
        private double _opacity = 80;
        private double _scale = 100;
        private bool _isLoaded = false;

        public MainWindow()
        {
            InitializeComponent();
            ShortcutsPanel.ItemsSource = _shortcuts;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Load saved configuration
            var config = _configService.Load();
            
            // Restore window position
            if (config.WindowLeft >= 0 && config.WindowTop >= 0)
            {
                Left = config.WindowLeft;
                Top = config.WindowTop;
            }
            else
            {
                // Center on screen
                Left = (SystemParameters.PrimaryScreenWidth - ActualWidth) / 2;
                Top = 50;
            }
            
            // Restore opacity
            _opacity = config.Opacity;
            UpdateOpacityVisual();
            
            // Restore layout orientation
            _isVertical = config.IsVertical;
            ApplyLayout();
            
            // Restore scale
            _scale = config.Scale;
            ApplyScale();
            
            // Load shortcuts
            foreach (var path in config.ShortcutPaths)
            {
                if (File.Exists(path))
                {
                    AddShortcut(path);
                }
            }
            
            _isLoaded = true;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
                SaveConfig();
            }
        }

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (var file in files)
                {
                    var ext = Path.GetExtension(file).ToLower();
                    if (ext == ".exe" || ext == ".lnk" || ext == ".url")
                    {
                        // Check if already added
                        bool exists = false;
                        foreach (var s in _shortcuts)
                        {
                            if (s.Path.Equals(file, StringComparison.OrdinalIgnoreCase))
                            {
                                exists = true;
                                break;
                            }
                        }
                        
                        if (!exists)
                        {
                            AddShortcut(file);
                        }
                    }
                }
                SaveConfig();
            }
        }

        private void AddShortcut(string path)
        {
            var icon = IconExtractor.GetIcon(path);
            var name = Path.GetFileNameWithoutExtension(path);
            
            _shortcuts.Add(new ShortcutItem
            {
                Name = name,
                Path = path,
                Icon = icon
            });
        }

        private void Shortcut_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ShortcutItem shortcut)
            {
                LaunchShortcut(shortcut);
            }
        }

        private void Shortcut_RightClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is Button button && button.Tag is ShortcutItem shortcut)
            {
                _contextMenuTarget = shortcut;
                var contextMenu = (ContextMenu)FindResource("ShortcutContextMenu");
                contextMenu.PlacementTarget = button;
                contextMenu.IsOpen = true;
                e.Handled = true;
            }
        }

        private void Panel_RightClick(object sender, MouseButtonEventArgs e)
        {
            // Only show panel menu if not clicking on a shortcut button
            var source = e.OriginalSource as DependencyObject;
            while (source != null && source != MainBorder)
            {
                if (source is Button)
                {
                    return; // Clicked on a button, don't show panel menu
                }
                source = System.Windows.Media.VisualTreeHelper.GetParent(source);
            }
            
            var contextMenu = (ContextMenu)FindResource("PanelContextMenu");
            contextMenu.PlacementTarget = MainBorder;
            contextMenu.IsOpen = true;
            e.Handled = true;
        }

        private void LaunchShortcut(ShortcutItem shortcut)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = shortcut.Path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось запустить: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ContextMenu_Launch(object sender, RoutedEventArgs e)
        {
            if (_contextMenuTarget != null)
            {
                LaunchShortcut(_contextMenuTarget);
            }
        }

        private void ContextMenu_OpenLocation(object sender, RoutedEventArgs e)
        {
            if (_contextMenuTarget != null)
            {
                try
                {
                    var folder = Path.GetDirectoryName(_contextMenuTarget.Path);
                    if (folder != null)
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "explorer.exe",
                            Arguments = $"/select,\"{_contextMenuTarget.Path}\"",
                            UseShellExecute = true
                        });
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ContextMenu_Delete(object sender, RoutedEventArgs e)
        {
            if (_contextMenuTarget != null)
            {
                _shortcuts.Remove(_contextMenuTarget);
                SaveConfig();
            }
        }

        private void Layout_Horizontal(object sender, RoutedEventArgs e)
        {
            _isVertical = false;
            ApplyLayout();
            SaveConfig();
        }

        private void Layout_Vertical(object sender, RoutedEventArgs e)
        {
            _isVertical = true;
            ApplyLayout();
            SaveConfig();
        }

        private void ApplyLayout()
        {
            // Update ItemsPanel orientation
            ShortcutsPanel.ItemsPanel = CreateItemsPanelTemplate(_isVertical ? Orientation.Vertical : Orientation.Horizontal);
        }

        private ItemsPanelTemplate CreateItemsPanelTemplate(Orientation orientation)
        {
            var template = new ItemsPanelTemplate();
            var factory = new FrameworkElementFactory(typeof(StackPanel));
            factory.SetValue(StackPanel.OrientationProperty, orientation);
            template.VisualTree = factory;
            template.Seal();
            return template;
        }

        private void Menu_Close(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isLoaded) return; // Prevent reset during menu initialization
            
            _opacity = e.NewValue;
            UpdateOpacityVisual();
            
            // Update text in the MenuItem
            if (sender is Slider slider)
            {
                var parent = slider.Parent as StackPanel;
                if (parent != null)
                {
                    foreach (var child in parent.Children)
                    {
                        if (child is TextBlock textBlock)
                        {
                            textBlock.Text = $"{(int)_opacity}%";
                            break;
                        }
                    }
                }
            }
            
            SaveConfig();
        }

        private void ResizeGrip_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            // Calculate new scale based on drag delta
            double scaleDelta = (e.HorizontalChange + e.VerticalChange) / 200.0;
            _scale = Math.Clamp(_scale + scaleDelta * 100, 50, 300);
            ApplyScale();
            SaveConfig();
        }

        private void ResetScale_Click(object sender, RoutedEventArgs e)
        {
            _scale = 100;
            ApplyScale();
            SaveConfig();
        }

        private void ApplyScale()
        {
            if (PanelScale != null)
            {
                var scaleValue = _scale / 100.0;
                PanelScale.ScaleX = scaleValue;
                PanelScale.ScaleY = scaleValue;
            }
        }

        private void UpdateOpacityVisual()
        {
            var opacityValue = _opacity / 100.0;
            if (MainBorder != null)
            {
                var color = System.Windows.Media.Color.FromArgb(
                    (byte)(opacityValue * 255), 30, 30, 46);
                MainBorder.Background = new System.Windows.Media.SolidColorBrush(color);
            }
        }

        private void PanelContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            // Update menu items when menu opens
            if (sender is ContextMenu menu)
            {
                // Find and update the slider value
                foreach (var item in menu.Items)
                {
                    if (item is MenuItem menuItem)
                    {
                        // Check for layout menu
                        if (menuItem.Items.Count >= 2)
                        {
                            var first = menuItem.Items[0] as MenuItem;
                            var second = menuItem.Items[1] as MenuItem;
                            if (first != null && second != null)
                            {
                                // Check if this is the orientation menu
                                if (first.Header?.ToString()?.Contains("Горизонтально") == true)
                                {
                                    first.IsChecked = !_isVertical;
                                    second.IsChecked = _isVertical;
                                }
                            }
                        }
                        
                        // Check for opacity slider
                        if (menuItem.Items.Count == 1 && menuItem.Items[0] is StackPanel panel)
                        {
                            var header = menuItem.Header?.ToString() ?? "";
                            if (header.Contains("Прозрачность"))
                            {
                                foreach (var child in panel.Children)
                                {
                                    if (child is Slider slider)
                                    {
                                        slider.Value = _opacity;
                                    }
                                    if (child is TextBlock textBlock)
                                    {
                                        textBlock.Text = $"{(int)_opacity}%";
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void SaveConfig()
        {
            var paths = new System.Collections.Generic.List<string>();
            foreach (var s in _shortcuts)
            {
                paths.Add(s.Path);
            }
            
            _configService.Save(new AppConfig
            {
                WindowLeft = Left,
                WindowTop = Top,
                Opacity = _opacity,
                Scale = _scale,
                IsVertical = _isVertical,
                ShortcutPaths = paths
            });
        }
    }
}
