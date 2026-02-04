using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
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
        private double _opacity = 45;
        private double _scale = 100;
        private bool _isLoaded = false;
        private Point _dragStartPoint;
        private DragAdorner? _adorner;
        private bool _isDragging = false;

        public MainWindow()
        {
            InitializeComponent();
            ShortcutsPanel.ItemsSource = _shortcuts;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Enable acrylic blur effect - DISABLED to fix corner artifacts
            // WindowEffect.EnableBlur(this);
            
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
            // Restore opacity
            PanelOpacity = config.Opacity;
            
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

        private void Shortcut_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void Shortcut_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging) return;

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point position = e.GetPosition(null);
                if (Math.Abs(position.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(position.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    var button = sender as Button;
                    var shortcut = button?.DataContext as ShortcutItem;
                    
                    if (button == null || shortcut == null) return;

                    _isDragging = true;
                    try
                    {
                        // Create Adorner
                        var layer = AdornerLayer.GetAdornerLayer(MainBorder);
                        if (layer != null)
                        {
                            var mousePos = e.GetPosition(MainBorder);
                            // Calculate center offset based on actual button size
                            double offsetX = button.ActualWidth / 2;
                            double offsetY = button.ActualHeight / 2;
                            
                            _adorner = new DragAdorner(MainBorder, button, 0.7, new Point(offsetX, offsetY), new Size(button.ActualWidth, button.ActualHeight));
                            _adorner.UpdatePosition(mousePos);
                            layer.Add(_adorner);
                        }

                        // Dim button removed per user request
                        // button.Opacity = 0.2;

                        try
                        {
                            DragDrop.DoDragDrop(button, shortcut, DragDropEffects.Move);
                        }
                        finally
                        {
                            // Cleanup after drag (always runs)
                            if (_adorner != null)
                            {
                                layer?.Remove(_adorner);
                                _adorner = null;
                            }
                            // button.Opacity = 1.0;
                        }
                        e.Handled = true;
                    }
                    finally
                    {
                        _isDragging = false;
                    }
                }
            }
        }

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy | DragDropEffects.Link;

                // Create custom adorner for file drop if not already present
                if (_adorner == null)
                {
                    var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    if (files != null && files.Length > 0)
                    {
                        var path = files[0];
                        var icon = Services.IconExtractor.GetIcon(path);
                        
                        if (icon == null)
                        {
                            icon = Application.Current.FindResource("DefaultFileIcon") as ImageSource;
                        }

                        if (icon != null)
                        {
                            var image = new System.Windows.Controls.Image
                            {
                                Source = icon,
                                Width = 48,
                                Height = 48,
                                Opacity = 0.8
                            };
                            
                            // Measure and Arrange to ensure it renders in VisualBrush
                            image.Measure(new Size(48, 48));
                            image.Arrange(new Rect(0, 0, 48, 48));

                            var layer = AdornerLayer.GetAdornerLayer(MainBorder);
                            if (layer != null)
                            {
                                var mousePos = e.GetPosition(MainBorder);
                                _adorner = new DragAdorner(MainBorder, image, 1.0, new Point(24, 24), new Size(48, 48));
                                _adorner.UpdatePosition(mousePos);
                                layer.Add(_adorner);
                            }
                        }
                    }
                }
            }
            else if (e.Data.GetDataPresent(typeof(ShortcutItem)))
            {
                e.Effects = DragDropEffects.Move;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void Window_DragLeave(object sender, DragEventArgs e)
        {
             if (_adorner != null && e.Data.GetDataPresent(DataFormats.FileDrop))
             {
                 var layer = AdornerLayer.GetAdornerLayer(MainBorder);
                 layer?.Remove(_adorner);
                 _adorner = null;
             }
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (_adorner != null)
            {
                var point = e.GetPosition(MainBorder);
                _adorner.UpdatePosition(point);
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy | DragDropEffects.Link;
            }
            else if (e.Data.GetDataPresent(typeof(ShortcutItem)))
            {
                e.Effects = DragDropEffects.Move;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }

        private int GetInsertionIndex(Point mousePos)
        {
            int targetIndex = _shortcuts.Count;
            int visualIndex = 0;

            for (int i = 0; i < _shortcuts.Count; i++)
            {
                var container = ShortcutsPanel.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
                if (container != null)
                {
                    var transform = container.TransformToAncestor(ShortcutsPanel);
                    var topLeft = transform.Transform(new Point(0, 0));
                    var center = _isVertical 
                        ? topLeft.Y + (container.ActualHeight / 2)
                        : topLeft.X + (container.ActualWidth / 2);

                    var mouseValue = _isVertical ? mousePos.Y : mousePos.X;

                    if (mouseValue < center)
                    {
                        return i;
                    }
                }
            }
            return targetIndex;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            e.Handled = true;
            Point dropPos = e.GetPosition(ShortcutsPanel);
            int insertIndex = GetInsertionIndex(dropPos);

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (var file in files)
                {
                    if (File.Exists(file) || Directory.Exists(file))
                    {
                        AddShortcut(file, insertIndex);
                        // Increment index if adding multiple files to maintain order
                        insertIndex++; 
                    }
                }
                SaveConfig();
            }
            else if (e.Data.GetDataPresent(typeof(ShortcutItem)))
            {
                var droppedData = e.Data.GetData(typeof(ShortcutItem)) as ShortcutItem;
                if (droppedData != null)
                {
                    int oldIndex = _shortcuts.IndexOf(droppedData);
                    if (oldIndex != -1)
                    {
                        if (oldIndex < insertIndex)
                        {
                            insertIndex--;
                        }
                        
                        _shortcuts.Move(oldIndex, insertIndex);
                        SaveConfig();
                    }
                }
            }
            
            // Cleanup adorner
            if (_adorner != null)
            {
                var layer = AdornerLayer.GetAdornerLayer(MainBorder);
                layer?.Remove(_adorner);
                _adorner = null;
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
                SaveConfig();
            }
        }

        private void AddShortcut(string path, int index = -1)
        {
            // Copy .lnk and .url files to our managed directory
            var ext = Path.GetExtension(path).ToLower();
            if (ext == ".lnk" || ext == ".url")
            {
                try 
                {
                    var shortcutsDir = _configService.ShortcutsDirectory;
                    var fileName = Path.GetFileName(path);
                    var newPath = Path.Combine(shortcutsDir, fileName);
                    
                    // Handle duplicate names by appending a guid fragment if needed
                    if (File.Exists(newPath) && newPath != path)
                    {
                        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                        var uniqueName = $"{nameWithoutExt}_{Guid.NewGuid().ToString().Substring(0, 8)}{ext}";
                        newPath = Path.Combine(shortcutsDir, uniqueName);
                    }

                    if (path != newPath)
                    {
                        File.Copy(path, newPath, true);
                        path = newPath;
                    }
                }
                catch (Exception ex)
                {
                    // Failed to copy, continue with original path but warn?
                    // For now silent fallback to original path
                }
            }

            var icon = IconExtractor.GetIcon(path);
            if (icon == null)
            {
                // Try to get default icon from resources
                icon = Application.Current.FindResource("DefaultFileIcon") as ImageSource;
            }

            var name = Path.GetFileNameWithoutExtension(path);
            
            var item = new ShortcutItem
            {
                Name = name,
                Path = path,
                Icon = icon
            };

            if (index >= 0 && index <= _shortcuts.Count)
            {
                _shortcuts.Insert(index, item);
            }
            else
            {
                _shortcuts.Add(item);
            }
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
            contextMenu.DataContext = this;
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
            // Update Main and ItemsPanel orientation
            var orientation = _isVertical ? Orientation.Vertical : Orientation.Horizontal;
            MainLayout.Orientation = orientation;
            ShortcutsPanel.ItemsPanel = CreateItemsPanelTemplate(orientation);
            
            // Adjust DragHandle size/position hints if needed
            if (_isVertical)
            {
                DragHandle.Height = 15;
                DragHandle.Width = double.NaN; // Auto
                GripVisual.LayoutTransform = null;
            }
            else
            {
                DragHandle.Width = 15;
                DragHandle.Height = double.NaN; // Auto
                // Rotate grip for horizontal layout
                GripVisual.LayoutTransform = new RotateTransform(90);
            }
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

        public double PanelOpacity
        {
            get { return (double)GetValue(PanelOpacityProperty); }
            set { SetValue(PanelOpacityProperty, value); }
        }

        public static readonly DependencyProperty PanelOpacityProperty =
            DependencyProperty.Register("PanelOpacity", typeof(double), typeof(MainWindow), 
                new PropertyMetadata(45.0, OnPanelOpacityChanged));

        private static void OnPanelOpacityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var window = (MainWindow)d;
            window._opacity = (double)e.NewValue;
            window.UpdateOpacityVisual();
            
            if (window._isLoaded)
            {
                window.SaveConfig();
            }
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
                var color = System.Windows.Media.Color.FromArgb(
                    (byte)(opacityValue * 255), 30, 30, 46);
                MainBorder.Background = new System.Windows.Media.SolidColorBrush(color);
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

                        if (menuItem.Name == "MenuAutostart")
                        {
                            menuItem.IsChecked = SystemService.IsAutostartEnabled();
                        }
                    }
                }
            }
        }

        private void MenuAutostart_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item)
            {
                SystemService.SetAutostart(item.IsChecked);
            }
        }

        private void MenuUninstall_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Вы действительно хотите удалить приложение и все его настройки?\nПриложение будет закрыто и удалено.",
                "Полное удаление",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                SystemService.FullUninstall();
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
