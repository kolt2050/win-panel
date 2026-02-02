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
                            
                            _adorner = new DragAdorner(MainBorder, button, 0.7, new Point(offsetX, offsetY));
                            _adorner.UpdatePosition(mousePos);
                            layer.Add(_adorner);
                        }

                        // Dim button
                        button.Opacity = 0.2;

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
                            button.Opacity = 1.0;
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

        private ShortcutItem? _placeholder;

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                if (_placeholder == null)
                {
                    _placeholder = new ShortcutItem { IsPlaceholder = true };
                    _shortcuts.Add(_placeholder);
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void Window_DragLeave(object sender, DragEventArgs e)
        {
            if (_placeholder != null)
            {
                _shortcuts.Remove(_placeholder);
                _placeholder = null;
            }
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (_adorner != null)
            {
                var point = e.GetPosition(MainBorder);
                _adorner.UpdatePosition(point);
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop) && _placeholder != null)
            {
                UpdatePlaceholderPosition(e.GetPosition(ShortcutsPanel));
            }
            else if (e.Data.GetDataPresent(typeof(ShortcutItem)))
            {
                // Internal reordering
                var droppedData = e.Data.GetData(typeof(ShortcutItem)) as ShortcutItem;
                if (droppedData != null)
                {
                   UpdatePlaceholderPosition(e.GetPosition(ShortcutsPanel)); 
                   // Reusing the same positioning logic works because UpdatePlaceholderPosition
                   // moves the item at 'currentIndex' to 'estimatedIndex'.
                   // For internal drag, 'droppedData' is already in the list, so 
                   // IndexOf(droppedData) will return the current index, 
                   // effectively moving it to the new mouse position.
                   
                   // However, UpdatePlaceholderPosition relies on _placeholder.
                   // So we need a unified method or adapting logic.
                   // Let's adapt UpdatePlaceholderPosition to take an item to move.
                   UpdateItemPosition(droppedData, e.GetPosition(ShortcutsPanel));
                }
            }
        }

        private void UpdatePlaceholderPosition(Point mousePos)
        {
            if (_placeholder == null) return;
            UpdateItemPosition(_placeholder, mousePos);
        }

        private void UpdateItemPosition(ShortcutItem item, Point mousePos)
        {
            int targetIndex = _shortcuts.Count - 1; 
            int visualIndex = 0;

            for (int i = 0; i < _shortcuts.Count; i++)
            {
                var current = _shortcuts[i];
                if (current == item) continue;

                var container = ShortcutsPanel.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
                if (container != null)
                {
                    // Transform bounds to ShortcutsPanel coordinates
                    var transform = container.TransformToAncestor(ShortcutsPanel);
                    var topLeft = transform.Transform(new Point(0, 0));
                    var center = _isVertical 
                        ? topLeft.Y + (container.ActualHeight / 2)
                        : topLeft.X + (container.ActualWidth / 2);

                    var mouseValue = _isVertical ? mousePos.Y : mousePos.X;

                    if (mouseValue < center)
                    {
                        targetIndex = visualIndex; // Insert before this item
                        break;
                    }
                }
                visualIndex++;
            }
            
            // Adjust target index if we are moving "forward" in the list 
            // relative to the original full list indices, accounting for the item's removal
            // However, ObservableCollection.Move handles indices naturally.
            // We just need to map visualIndex (index in list without item) to real index.
            
            // Example: [A, B, Item, C]
            // Visuals: [A, B, C]
            // Mouse before C (Index 2 in visual list).
            // Target = 2.
            // Move(2, 2) -> No change?
            // Item is at 2. We want it before C (which is at 3).
            // If we Move(2, 3), result [A, B, C, Item].
            // If we Move(2, 2), result [A, B, Item, C].
            
            // Wait, visual loop gave us index 2 (C).
            // C is at real index 3.
            // visualIndex 0->A(0). 1->B(1). 2->C(3).
            // So if targetIndex is 2.
            // We want to insert at 3?
            
            // Let's refine the loop strategy:
            // We want to find the real index 'k' such that we insert *before* 'k'.
            // If we break at C (real index 3), we want to be at 3.
            // Move(2, 3)?
            
            // Let's rely on mapping.
            // List `[A, B, Item, C]`.
            // i=0(A). Mouse<A? No.
            // i=1(B). Mouse<B? No.
            // i=2(Item). Skip.
            // i=3(C). Mouse<C? Yes. Break.
            // Target Loop Index `i` = 3.
            // Result: Insert before 3.
            // Move(2, 3).
            
            // What if Mouse > C?
            // Loop finishes. `targetIndex` init to?
            // Use `targetIndex = -1` start.
            // If -1, append.
            // Append means index = Count - 1?
            
            // Let's restart logic using REAL indices.
            int insertionIndex = _shortcuts.Count - 1;
            bool found = false;

            for (int i = 0; i < _shortcuts.Count; i++)
            {
                var current = _shortcuts[i];
                if (current == item) continue;

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
                        insertionIndex = i;
                        found = true;
                        break;
                    }
                }
            }
            
            // Correction for Move behavior
            // We want to arrive at 'insertionIndex'. 
            // If item is current at 'oldIndex'.
            // If oldIndex < insertionIndex, the item shifts others down.
            // e.g. [A, item, B]. Move item to after B (index 2).
            // Loop: A(0) no. B(2) < center? No.
            // End Loop. insertionIndex = default (Count-1 = 2).
            // Move(1, 2) -> [A, B, item]. Correct.
            
            // [A, item, B]. Mouse < (A).
            // Loop: A(0) < center? Yes. Break. insertionIndex = 0.
            // Move(1, 0) -> [item, A, B]. Correct.

            // [A, item, B]. Mouse < (B).
            // Loop: A(0) no. B(2) < center? Yes. Break. insertionIndex = 2.
            // Move(1, 2) -> [A, B, item]? NO!
            // If we insert before B (index 2), we want [A, item, B].
            // But Move(1, 2) places it at 2.
            // Wait, inserting "Before B" means Index should be 2?
            // Original: 0:A, 1:Item, 2:B.
            // If we remove Item: 0:A, 1:B.
            // Insert at 1 -> [A, Item, B].
            // So we want result index 1.
            
            // But Loop gave us i=2.
            // So if insertionIndex > oldIndex, decrement?
            
            int currentIndex = _shortcuts.IndexOf(item);
            if (found && insertionIndex > currentIndex)
            {
                insertionIndex--;
            }
            
            if (currentIndex != insertionIndex)
            {
                _shortcuts.Move(currentIndex, insertionIndex);
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            int insertIndex = -1;
            if (_placeholder != null)
            {
                insertIndex = _shortcuts.IndexOf(_placeholder);
                _shortcuts.Remove(_placeholder);
                _placeholder = null;
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (var file in files)
                {
                    if (File.Exists(file) || Directory.Exists(file))
                    {
                        if (insertIndex != -1)
                        {
                            AddShortcut(file, insertIndex);
                            insertIndex++; 
                        }
                        else
                        {
                            AddShortcut(file);
                        }
                    }
                }
                SaveConfig();
            }
            else if (e.Data.GetDataPresent(typeof(ShortcutItem)))
            {
                // Internal reorder finalization
                SaveConfig();
                e.Handled = true;
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
            var icon = IconExtractor.GetIcon(path);
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
