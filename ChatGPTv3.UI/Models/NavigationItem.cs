using IconPacks.Avalonia.MaterialDesign;

namespace ChatGPTv3.UI.Models;

public class NavigationItem
{
    public string Name { get; set; } = string.Empty;
    public PackIconMaterialDesignKind IconKind { get; set; }
    public string PageKey { get; set; } = string.Empty;
}
