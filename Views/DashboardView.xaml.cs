using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace CraftStation.Views;

public partial class DashboardView : UserControl
{
    private readonly List<(FrameworkElement Element, double Delay)> _revealTargets = new();
    private readonly HashSet<FrameworkElement> _revealed = new();
    private bool _initialized;

    public DashboardView()
    {
        InitializeComponent();
    }

    private void Dashboard_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_initialized)
        {
            _initialized = true;
            _revealTargets.Add((HeroEyebrow, 0.00));
            _revealTargets.Add((HeroTitle, 0.10));
            _revealTargets.Add((HeroSubtitle, 0.18));
            _revealTargets.Add((HeroMeta, 0.26));
            _revealTargets.Add((HeroButton, 0.34));
            _revealTargets.Add((HeroStatusPanel, 0.42));
            _revealTargets.Add((AccountCard, 0.52));
            _revealTargets.Add((StatsCard, 0.58));
            _revealTargets.Add((QuickTitle, 0.64));
            _revealTargets.Add((QuickCard1, 0.68));
            _revealTargets.Add((QuickCard2, 0.72));
            _revealTargets.Add((QuickCard3, 0.76));
            _revealTargets.Add((QuickCard4, 0.80));
        }

        if (Resources["ScanBeamAnim"] is Storyboard scan)
            scan.Begin(this);
        if (Resources["BreathAnim"] is Storyboard breath)
            breath.Begin(this);

        RevealVisible();
    }

    private void RootScroll_OnScrollChanged(object sender, ScrollChangedEventArgs e) => RevealVisible();

    private void RevealVisible()
    {
        if (!IsLoaded || RootScroll.ViewportHeight <= 0)
            return;

        var viewport = RootScroll.ViewportHeight;
        foreach (var (element, delay) in _revealTargets)
        {
            if (_revealed.Contains(element) || element.ActualHeight <= 0)
                continue;

            var position = element.TransformToAncestor(RootScroll).Transform(new Point(0, 0));
            if (position.Y >= viewport * 0.92 || position.Y + element.ActualHeight <= 0)
                continue;

            _revealed.Add(element);
            AnimateReveal(element, delay);
        }
    }

    private static void AnimateReveal(FrameworkElement element, double delay)
    {
        var begin = TimeSpan.FromSeconds(delay);
        var fade = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.55))
        {
            BeginTime = begin,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(fade, element);
        Storyboard.SetTargetProperty(fade, new PropertyPath("Opacity"));

        var slide = new DoubleAnimation(24, 0, TimeSpan.FromSeconds(0.6))
        {
            BeginTime = begin,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(slide, element);
        Storyboard.SetTargetProperty(slide, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));

        var storyboard = new Storyboard();
        storyboard.Children.Add(fade);
        storyboard.Children.Add(slide);
        storyboard.Begin(element);
    }
}
