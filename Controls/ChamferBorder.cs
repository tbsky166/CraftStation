using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CraftStation.Controls;

/// <summary>
/// fz.wiki .ef-chamfer 的 WPF 等价实现：
/// 用 45° 切角多边形裁剪 Border，支持 6px / 9px 切角。
/// </summary>
public class ChamferBorder : Border
{
    public static readonly DependencyProperty ChamferProperty = DependencyProperty.Register(
        nameof(Chamfer),
        typeof(double),
        typeof(ChamferBorder),
        new FrameworkPropertyMetadata(6d, FrameworkPropertyMetadataOptions.AffectsRender, OnChamferChanged));

    public double Chamfer
    {
        get => (double)GetValue(ChamferProperty);
        set => SetValue(ChamferProperty, value);
    }

    private static void OnChamferChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((ChamferBorder)d).UpdateClip();

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        UpdateClip();
    }

    private void UpdateClip()
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0)
            return;

        var c = Math.Min(Chamfer, Math.Min(width / 2, height / 2));
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(0, c), isFilled: true, isClosed: true);
            ctx.LineTo(new Point(width - c, 0), isStroked: true, isSmoothJoin: false);
            ctx.LineTo(new Point(width, c), isStroked: true, isSmoothJoin: false);
            ctx.LineTo(new Point(width, height - c), isStroked: true, isSmoothJoin: false);
            ctx.LineTo(new Point(width - c, height), isStroked: true, isSmoothJoin: false);
            ctx.LineTo(new Point(c, height), isStroked: true, isSmoothJoin: false);
            ctx.LineTo(new Point(0, height - c), isStroked: true, isSmoothJoin: false);
        }
        geometry.Freeze();
        Clip = geometry;
    }
}
