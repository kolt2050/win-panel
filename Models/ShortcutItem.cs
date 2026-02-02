using System.Windows.Media;

namespace WinPanel.Models
{
    public class ShortcutItem
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public ImageSource? Icon { get; set; }
    }
}
