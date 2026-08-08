using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CraftStation.ViewModels;

namespace CraftStation;

public partial class MainWindow : Window
{
    private string? _lastKey;
    private string? _pendingKey;
    private bool _busy;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void Window_OnLoaded(object sender, RoutedEventArgs e)
    {
        _lastKey = "dashboard";
        AnimatePageIn(PageHost);
    }

    private void TitleBar_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            Maximize_Click(sender, e);
            return;
        }
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void NavList_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || NavList.SelectedValue is not string key)
            return;
        if (key == _lastKey)
            return;
        if (_busy)
        {
            _pendingKey = key;
            return;
        }

        _busy = true;
        _ = TransitionAsync(vm, key);
    }

    private async System.Threading.Tasks.Task TransitionAsync(MainViewModel vm, string key)
    {
        try
        {
            // 旧页面先快速淡出，再切换，保持官网那种“丝滑”节奏
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(110))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            PageHost.BeginAnimation(OpacityProperty, fadeOut);
            await System.Threading.Tasks.Task.Delay(120);

            vm.NavigateCommand.Execute(key);
            _lastKey = key;
            AnimatePageIn(PageHost);

            while (_pendingKey != null && _pendingKey != _lastKey)
            {
                var next = _pendingKey;
                _pendingKey = null;

                var fadeOut2 = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(110))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };
                PageHost.BeginAnimation(OpacityProperty, fadeOut2);
                await System.Threading.Tasks.Task.Delay(120);

                vm.NavigateCommand.Execute(next);
                _lastKey = next;
                AnimatePageIn(PageHost);
            }
        }
        finally
        {
            _busy = false;
        }
    }

    private void AnimatePageIn(UIElement? element)
    {
        if (element == null)
            return;

        element.Opacity = 0;
        element.RenderTransformOrigin = new Point(0.5, 0);
        element.RenderTransform = new TranslateTransform(0, 16);

        var storyboard = new Storyboard();
        var opacity = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(320))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(opacity, element);
        Storyboard.SetTargetProperty(opacity, new PropertyPath(OpacityProperty));

        var slide = new DoubleAnimation(16, 0, TimeSpan.FromMilliseconds(380))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(slide, element);
        Storyboard.SetTargetProperty(slide, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));

        storyboard.Children.Add(opacity);
        storyboard.Children.Add(slide);
        storyboard.Begin();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
    }
}
