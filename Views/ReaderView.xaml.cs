using ComicReader.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace ComicReader.Views;

/// <summary>
/// Interaction logic for ReaderView.xaml with keyboard navigation, fluid page transitions,
/// and smooth cinematic loading screen fade-outs.
/// </summary>
public partial class ReaderView : UserControl
{
    private int _lastPageIndex = -1;
    private Point _lastDragPoint;
    private bool _isPanning;

    public ReaderView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Focus();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ReaderViewModel oldVm)
        {
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (e.NewValue is ReaderViewModel newVm)
        {
            newVm.PropertyChanged += OnViewModelPropertyChanged;
            _lastPageIndex = newVm.CurrentPageIndex;

            // Set initial state of cinematic loading overlay
            if (newVm.IsLoadingDocument)
            {
                CinematicLoadingOverlay.Visibility = Visibility.Visible;
                CinematicLoadingOverlay.Opacity = 1.0;
            }
            else
            {
                CinematicLoadingOverlay.Visibility = Visibility.Collapsed;
                CinematicLoadingOverlay.Opacity = 0.0;
            }

            ResetScrollViewerPosition();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ReaderViewModel vm) return;

        if (e.PropertyName == nameof(ReaderViewModel.CurrentPageIndex))
        {
            int oldIndex = _lastPageIndex;
            int newIndex = vm.CurrentPageIndex;
            _lastPageIndex = newIndex;

            if (oldIndex != -1 && oldIndex != newIndex)
            {
                TriggerPageTurnAnimation(newIndex > oldIndex);
            }

            ResetScrollViewerPosition();
        }
        else if (e.PropertyName == nameof(ReaderViewModel.CurrentPageImage) ||
                 e.PropertyName == nameof(ReaderViewModel.CurrentComic))
        {
            ResetScrollViewerPosition();
        }
        else if (e.PropertyName == nameof(ReaderViewModel.IsLoadingDocument))
        {
            TriggerLoadingOverlayTransition(vm.IsLoadingDocument);
        }
    }

    /// <summary>
    /// Instantly resets the ScrollViewer horizontal and vertical offsets to 0 (top-left).
    /// Dispatched with Loaded priority to guarantee alignment even after image layout recalculation.
    /// </summary>
    private void ResetScrollViewerPosition()
    {
        PageScrollViewer.ScrollToHome();
        Dispatcher.InvokeAsync(() =>
        {
            PageScrollViewer.ScrollToHome();
            UpdateViewerCursor();
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    #region Mouse Drag Panning & Multi-Axis Navigation

    private void PageScrollViewer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Allow mouse drag panning whenever content exceeds viewport bounds
        if (PageScrollViewer.ExtentWidth > PageScrollViewer.ViewportWidth ||
            PageScrollViewer.ExtentHeight > PageScrollViewer.ViewportHeight)
        {
            _lastDragPoint = e.GetPosition(PageScrollViewer);
            _isPanning = true;
            PageScrollViewer.CaptureMouse();
            PageScrollViewer.Cursor = Cursors.SizeAll;
        }
    }

    private void PageScrollViewer_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_isPanning && PageScrollViewer.IsMouseCaptured)
        {
            var currentPoint = e.GetPosition(PageScrollViewer);
            var deltaX = currentPoint.X - _lastDragPoint.X;
            var deltaY = currentPoint.Y - _lastDragPoint.Y;

            if (Math.Abs(deltaX) > 0 || Math.Abs(deltaY) > 0)
            {
                PageScrollViewer.ScrollToHorizontalOffset(PageScrollViewer.HorizontalOffset - deltaX);
                PageScrollViewer.ScrollToVerticalOffset(PageScrollViewer.VerticalOffset - deltaY);
                _lastDragPoint = currentPoint;
            }
        }
    }

    private void PageScrollViewer_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPanning)
        {
            _isPanning = false;
            PageScrollViewer.ReleaseMouseCapture();
            UpdateViewerCursor();
        }
    }

    private void PageScrollViewer_LostMouseCapture(object sender, MouseEventArgs e)
    {
        _isPanning = false;
        UpdateViewerCursor();
    }

    private void PageScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (!_isPanning)
        {
            UpdateViewerCursor();
        }
    }

    private void PageScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Ctrl + Mouse Wheel for instant fluid zooming
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 && DataContext is ReaderViewModel vm)
        {
            if (e.Delta > 0)
            {
                vm.ZoomInCommand.Execute(null);
            }
            else if (e.Delta < 0)
            {
                vm.ZoomOutCommand.Execute(null);
            }
            e.Handled = true;
        }
        // Shift + Mouse Wheel for horizontal scrolling
        else if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
        {
            if (e.Delta > 0)
            {
                PageScrollViewer.LineLeft();
            }
            else if (e.Delta < 0)
            {
                PageScrollViewer.LineRight();
            }
            e.Handled = true;
        }
    }

    private void UpdateViewerCursor()
    {
        if (PageScrollViewer.ExtentWidth > PageScrollViewer.ViewportWidth ||
            PageScrollViewer.ExtentHeight > PageScrollViewer.ViewportHeight)
        {
            PageScrollViewer.Cursor = Cursors.Hand;
        }
        else
        {
            PageScrollViewer.Cursor = Cursors.Arrow;
        }
    }

    #region Scrubber Drag-Completed Routing

    private void PageSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        CommitSliderScrub();
    }

    private void PageSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // Catches direct track clicks outside the thumb handle
        CommitSliderScrub();
    }

    private void PageSlider_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        // Commits scrub when navigating the slider using arrow/page keys
        if (e.Key is Key.Left or Key.Right or Key.Home or Key.End or Key.PageUp or Key.PageDown)
        {
            CommitSliderScrub();
        }
    }

    private void CommitSliderScrub()
    {
        if (DataContext is ReaderViewModel vm)
        {
            _ = vm.CommitScrubAsync();
        }
    }

    #endregion

    #endregion

    /// <summary>
    /// Smoothly fades out the cinematic loading screen using a native DoubleAnimation Storyboard.
    /// </summary>
    private void TriggerLoadingOverlayTransition(bool isLoading)
    {
        if (isLoading)
        {
            CinematicLoadingOverlay.BeginAnimation(UIElement.OpacityProperty, null);
            CinematicLoadingOverlay.Visibility = Visibility.Visible;
            CinematicLoadingOverlay.Opacity = 1.0;
        }
        else
        {
            // Smooth fade-out animation over 450ms
            var fadeOutAnimation = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(450),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            fadeOutAnimation.Completed += (s, e) =>
            {
                if (DataContext is ReaderViewModel currentVm && !currentVm.IsLoadingDocument)
                {
                    CinematicLoadingOverlay.Visibility = Visibility.Collapsed;
                }
            };

            CinematicLoadingOverlay.BeginAnimation(UIElement.OpacityProperty, fadeOutAnimation);
        }
    }

    /// <summary>
    /// Triggers a fluid horizontal slide and opacity cross-fade animation on page change.
    /// </summary>
    private void TriggerPageTurnAnimation(bool goingForward)
    {
        if (DataContext is not ReaderViewModel vm) return;

        double offset = goingForward ? 45.0 : -45.0;
        int durationMs = Math.Max(180, vm.AnimationDurationMs);

        // Fluid horizontal slide
        var slideAnimation = new DoubleAnimation
        {
            From = offset,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
        };
        PageSlideTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, slideAnimation);

        // Fluid cross-fade opacity
        var fadeAnimation = new DoubleAnimation
        {
            From = 0.35,
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        PageContentHost.BeginAnimation(UIElement.OpacityProperty, fadeAnimation);
    }

    private const double ScrollStep = 48.0;

    private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not ReaderViewModel vm) return;

        switch (e.Key)
        {
            // Vertical Keyboard Scrolling (Fit Width / Zoomed Pages)
            case Key.Up:
                if (PageScrollViewer.VerticalOffset > 0)
                {
                    var targetOffset = Math.Max(0, PageScrollViewer.VerticalOffset - ScrollStep);
                    PageScrollViewer.ScrollToVerticalOffset(targetOffset);
                }
                e.Handled = true;
                break;

            case Key.Down:
                if (PageScrollViewer.VerticalOffset < PageScrollViewer.ScrollableHeight)
                {
                    var targetOffset = Math.Min(PageScrollViewer.ScrollableHeight, PageScrollViewer.VerticalOffset + ScrollStep);
                    PageScrollViewer.ScrollToVerticalOffset(targetOffset);
                }
                e.Handled = true;
                break;

            case Key.Right:
            case Key.PageDown:
            case Key.Space when (Keyboard.Modifiers & ModifierKeys.Shift) == 0:
                if (vm.CanGoNext)
                {
                    _ = vm.NextPageAsync();
                    e.Handled = true;
                }
                break;

            case Key.Left:
            case Key.PageUp:
            case Key.Back:
            case Key.Space when (Keyboard.Modifiers & ModifierKeys.Shift) != 0:
                if (vm.CanGoPrevious)
                {
                    _ = vm.PreviousPageAsync();
                    e.Handled = true;
                }
                break;

            case Key.Home:
                if (vm.TotalPages > 0)
                {
                    _ = vm.GoToPageAsync(0);
                    e.Handled = true;
                }
                break;

            case Key.End:
                if (vm.TotalPages > 0)
                {
                    _ = vm.GoToPageAsync(vm.TotalPages - 1);
                    e.Handled = true;
                }
                break;

            case Key.Escape:
                vm.CloseReaderCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.H:
                vm.ToggleControlsOverlayCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }
}
