using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace PomodoroBar;

// A quiet instrument panel: the time and its focus/break track carry the emphasis.
internal static class UiTheme
{
    internal static readonly SolidColorBrush Background = Color("#202A32");
    internal static readonly SolidColorBrush Surface = Color("#293640");
    internal static readonly SolidColorBrush Line = Color("#485B68");
    internal static readonly SolidColorBrush Ink = Color("#F6F8F9");
    internal static readonly SolidColorBrush Muted = Color("#B5C6CF");
    internal static readonly SolidColorBrush Focus = Color("#96D3E4");
    internal static readonly SolidColorBrush Rest = Color("#A7DABB");
    internal static readonly FontFamily Display = new("Bahnschrift, Segoe UI");
    internal static readonly FontFamily Digits = new("Cascadia Mono, Consolas");
    internal static SolidColorBrush Color(string hex)
    {
        var brush = BarSurface.Brush(hex); brush.Freeze(); return brush;
    }
    internal static TextBlock Text(string text, double size = 13, bool strong = false, Brush? ink = null) => new() {
        Text = text, FontSize = size, FontWeight = strong ? FontWeights.SemiBold : FontWeights.Normal,
        Foreground = ink ?? Ink, TextWrapping = TextWrapping.Wrap
    };
    private static Binding Parent(string name) => new(name) { RelativeSource = RelativeSource.TemplatedParent };
    internal static Button Button(object content, Action action, bool primary = false)
    {
        var b = new Button {
            Content = content, Margin = new Thickness(3), Padding = new Thickness(12,9,12,9),
            Background = primary ? Focus : Surface, Foreground = primary ? Background : Ink,
            BorderBrush = primary ? Focus : Line, Cursor = Cursors.Hand, MinHeight = 36,
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center
        };
        var border = new FrameworkElementFactory(typeof(Border),"frame");
        border.SetValue(Border.CornerRadiusProperty,new CornerRadius(6));
        border.SetValue(Border.BorderThicknessProperty,new Thickness(1));
        border.SetBinding(Border.BackgroundProperty,Parent("Background"));
        border.SetBinding(Border.BorderBrushProperty,Parent("BorderBrush"));
        border.SetBinding(Border.PaddingProperty,Parent("Padding"));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetBinding(ContentPresenter.ContentProperty,Parent("Content"));
        presenter.SetBinding(ContentPresenter.HorizontalAlignmentProperty,Parent("HorizontalContentAlignment"));
        presenter.SetBinding(ContentPresenter.VerticalAlignmentProperty,Parent("VerticalContentAlignment"));
        presenter.SetValue(ContentPresenter.RecognizesAccessKeyProperty,true);
        border.AppendChild(presenter);
        var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BorderBrushProperty,Focus,"frame")); template.Triggers.Add(hover);
        var pressed = new Trigger { Property = System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty, Value = true };
        pressed.Setters.Add(new Setter(UIElement.OpacityProperty,.7)); template.Triggers.Add(pressed);
        var keyboard = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
        keyboard.Setters.Add(new Setter(Border.BorderBrushProperty,Ink,"frame"));
        keyboard.Setters.Add(new Setter(Border.BorderThicknessProperty,new Thickness(2),"frame")); template.Triggers.Add(keyboard);
        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty,.55)); template.Triggers.Add(disabled);
        b.Template = template; b.Click += (_,_) => action(); return b;
    }
    internal static CheckBox Switch(string label)
    {
        var box = new CheckBox { Content = label, Foreground = Ink, Cursor = Cursors.Hand, MinHeight = 34, VerticalContentAlignment = VerticalAlignment.Center };
        var frame = new FrameworkElementFactory(typeof(Border),"focus");
        frame.SetValue(Border.PaddingProperty,new Thickness(3)); frame.SetValue(Border.CornerRadiusProperty,new CornerRadius(5));
        frame.SetValue(Border.BorderThicknessProperty,new Thickness(1)); frame.SetValue(Border.BorderBrushProperty,Brushes.Transparent);
        var row = new FrameworkElementFactory(typeof(StackPanel)); row.SetValue(StackPanel.OrientationProperty,Orientation.Horizontal);
        var track = new FrameworkElementFactory(typeof(Border),"track");
        track.SetValue(FrameworkElement.WidthProperty,34d); track.SetValue(FrameworkElement.HeightProperty,20d);
        track.SetValue(FrameworkElement.MarginProperty,new Thickness(0,0,12,0));
        track.SetValue(Border.CornerRadiusProperty,new CornerRadius(10)); track.SetValue(Border.BackgroundProperty,Line);
        var thumb = new FrameworkElementFactory(typeof(Border),"thumb");
        thumb.SetValue(FrameworkElement.WidthProperty,14d); thumb.SetValue(FrameworkElement.HeightProperty,14d);
        thumb.SetValue(FrameworkElement.MarginProperty,new Thickness(3)); thumb.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Left);
        thumb.SetValue(Border.CornerRadiusProperty,new CornerRadius(7)); thumb.SetValue(Border.BackgroundProperty,Ink);
        track.AppendChild(thumb); row.AppendChild(track);
        var text = new FrameworkElementFactory(typeof(ContentPresenter)); text.SetBinding(ContentPresenter.ContentProperty,Parent("Content"));
        text.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center); row.AppendChild(text); frame.AppendChild(row);
        var template = new ControlTemplate(typeof(CheckBox)) { VisualTree = frame };
        var on = new Trigger { Property = System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, Value = true };
        on.Setters.Add(new Setter(Border.BackgroundProperty,Focus,"track"));
        on.Setters.Add(new Setter(Border.BackgroundProperty,Background,"thumb"));
        on.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Right,"thumb")); template.Triggers.Add(on);
        var focus = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
        focus.Setters.Add(new Setter(Border.BorderBrushProperty,Focus,"focus")); template.Triggers.Add(focus);
        box.Template = template; return box;
    }
    internal static ContextMenu Menu()
    {
        var menu = new ContextMenu { Background = Surface, Foreground = Ink, BorderBrush = Line, BorderThickness = new Thickness(1), Padding = new Thickness(5) };
        var frame = new FrameworkElementFactory(typeof(Border),"item");
        frame.SetValue(Border.BackgroundProperty,Brushes.Transparent); frame.SetValue(Border.PaddingProperty,new Thickness(14,9,14,9));
        frame.SetValue(Border.CornerRadiusProperty,new CornerRadius(4));
        var content = new FrameworkElementFactory(typeof(ContentPresenter)); content.SetBinding(ContentPresenter.ContentProperty,Parent("Header")); frame.AppendChild(content);
        var template = new ControlTemplate(typeof(MenuItem)) { VisualTree = frame };
        var active = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
        active.Setters.Add(new Setter(Border.BackgroundProperty,Background,"item")); template.Triggers.Add(active);
        var style = new Style(typeof(MenuItem)); style.Setters.Add(new Setter(Control.ForegroundProperty,Ink)); style.Setters.Add(new Setter(Control.TemplateProperty,template));
        menu.Resources.Add(typeof(MenuItem),style);
        return menu;
    }

}

internal sealed class TrayColors : System.Windows.Forms.ProfessionalColorTable
{
    private static System.Drawing.Color Paint(string hex) => System.Drawing.ColorTranslator.FromHtml(hex);
    public override System.Drawing.Color ToolStripDropDownBackground => Paint("#293640");
    public override System.Drawing.Color ImageMarginGradientBegin => ToolStripDropDownBackground;
    public override System.Drawing.Color ImageMarginGradientMiddle => ToolStripDropDownBackground;
    public override System.Drawing.Color ImageMarginGradientEnd => ToolStripDropDownBackground;
    public override System.Drawing.Color MenuItemSelected => Paint("#485B68");
    public override System.Drawing.Color MenuItemBorder => Paint("#96D3E4");
    public override System.Drawing.Color MenuBorder => Paint("#485B68");
    public override System.Drawing.Color SeparatorDark => MenuBorder;
    public override System.Drawing.Color SeparatorLight => MenuBorder;
}
