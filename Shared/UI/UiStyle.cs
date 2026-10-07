using Godot;

namespace ChristiansSpilBox.Shared.UI;

public static class UiStyle
{
    public static readonly Color Night = new("#101b2c");
    public static readonly Color Panel = new("#20344b");
    public static readonly Color Sky = new("#6bd6e9");
    public static readonly Color Gold = new("#ffd16d");

    public static Label Label(string text, int size, Color? color = null)
    {
        var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color ?? Colors.White);
        return label;
    }

    public static Button Button(string text, int size = 28)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(230, 66), FocusMode = Control.FocusModeEnum.All };
        button.AddThemeFontSizeOverride("font_size", size);
        button.AddThemeColorOverride("font_color", Night);
        button.AddThemeStyleboxOverride("normal", Box(Gold));
        button.AddThemeStyleboxOverride("hover", Box(Sky));
        button.AddThemeStyleboxOverride("pressed", Box(new Color("#dda84d")));
        button.AddThemeStyleboxOverride("focus", Box(Colors.Transparent));
        return button;
    }

    public static StyleBoxFlat Box(Color color, int radius = 18)
    {
        return new StyleBoxFlat
        {
            BgColor = color,
            CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
            ContentMarginLeft = 18, ContentMarginRight = 18,
            ContentMarginTop = 12, ContentMarginBottom = 12
        };
    }

    public static ColorRect Backdrop(Color color)
    {
        var rect = new ColorRect { Color = color, MouseFilter = Control.MouseFilterEnum.Ignore };
        rect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        return rect;
    }
}
