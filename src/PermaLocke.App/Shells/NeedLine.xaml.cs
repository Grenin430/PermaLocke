using System.Windows;
using System.Windows.Controls;

namespace PermaLocke.App.Shells;

public partial class NeedLine : UserControl
{
    public static readonly DependencyProperty IdleProperty = DependencyProperty.Register(
        nameof(Idle), typeof(string), typeof(NeedLine), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty WrapProperty = DependencyProperty.Register(
        nameof(Wrap), typeof(bool), typeof(NeedLine),
        new PropertyMetadata(false, (d, e) =>
        {
            var line = (NeedLine)d;
            line.Hint.Wrap = (bool)e.NewValue;
            line.Hint.Trim = !(bool)e.NewValue;
        }));

    public NeedLine()
    {
        InitializeComponent();
    }

    /// <summary>What the line says when the section asks nothing of the game.</summary>
    public string Idle
    {
        get => (string)GetValue(IdleProperty);
        set => SetValue(IdleProperty, value);
    }

    /// <summary>The sentence goes on a second line instead of being cut when there is not room for it.</summary>
    public bool Wrap
    {
        get => (bool)GetValue(WrapProperty);
        set => SetValue(WrapProperty, value);
    }
}
