using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using zPlayer.App.Converters;
using zPlayer.Core;

namespace zPlayer.App.Artwork
{
    public static class CoverArt
    {
        internal static ArtworkLoader? Loader { get; set; }

        public static readonly DependencyProperty ReferenceProperty = DependencyProperty.RegisterAttached(
            "Reference", typeof(ArtworkRef?), typeof(CoverArt), new PropertyMetadata(null, OnReferenceChanged));

        public static ArtworkRef? GetReference(DependencyObject element) => (ArtworkRef?)element.GetValue(ReferenceProperty);
        public static void SetReference(DependencyObject element, ArtworkRef? value) => element.SetValue(ReferenceProperty, value);

        public static readonly DependencyProperty SmallProperty = DependencyProperty.RegisterAttached(
            "Small", typeof(bool), typeof(CoverArt), new PropertyMetadata(false));

        public static bool GetSmall(DependencyObject element) => (bool)element.GetValue(SmallProperty);
        public static void SetSmall(DependencyObject element, bool value) => element.SetValue(SmallProperty, value);

        private static readonly DependencyProperty RequestProperty = DependencyProperty.RegisterAttached(
            "Request", typeof(CancellationTokenSource), typeof(CoverArt));

        public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
            "Enabled", typeof(bool), typeof(CoverArt), new PropertyMetadata(false, OnEnabledChanged));

        public static bool GetEnabled(DependencyObject element) => (bool)element.GetValue(EnabledProperty);
        public static void SetEnabled(DependencyObject element, bool value) => element.SetValue(EnabledProperty, value);

        private static void OnEnabledChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) {
            if (sender is not FrameworkElement element) return;
            if ((bool)e.NewValue) {
                element.Loaded += OnLoaded;
                element.Unloaded += OnUnloaded;
                Refresh(element);
            }
            else {
                element.Loaded -= OnLoaded;
                element.Unloaded -= OnUnloaded;
                Stop(element);
            }
        }

        private static void OnReferenceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) {
            if (sender is FrameworkElement element && GetEnabled(element)) Refresh(element);
        }

        private static void OnLoaded(object sender, RoutedEventArgs e) => Refresh((FrameworkElement)sender);

        private static void OnUnloaded(object sender, RoutedEventArgs e) {
            FrameworkElement element = (FrameworkElement)sender;
            Stop(element);
            Show(element, ArtworkConverter.DefaultArtwork);
        }

        private static void Stop(FrameworkElement element) {
            if (element.GetValue(RequestProperty) is CancellationTokenSource request) request.Cancel();
            element.ClearValue(RequestProperty);
        }

        private static void Show(FrameworkElement element, ImageSource source) {
            if (element is Image image) image.Source = source;
            else if (element is Border border) {
                ImageBrush brush = new(source) { Stretch = Stretch.UniformToFill };
                brush.Freeze();
                border.Background = brush;
            }
        }

        private static async void Refresh(FrameworkElement element) {
            Stop(element);
            Show(element, ArtworkConverter.DefaultArtwork);
            if (!element.IsLoaded || GetReference(element) is not { } cover || Loader is not { } loader) return;

            using CancellationTokenSource request = new();
            element.SetValue(RequestProperty, request);
            try {
                ImageSource? result = await loader.GetAsync(cover, GetSmall(element) ? 96 : 384, request.Token);
                if (!request.IsCancellationRequested && result != null) Show(element, result);
            }
            catch (OperationCanceledException) when (request.IsCancellationRequested) {
            }
            catch (Exception ex) {
                Debug.WriteLine($"Could not load cover {cover.OwnerId}: {ex.Message}");
            }
            finally {
                if (ReferenceEquals(element.GetValue(RequestProperty), request)) element.ClearValue(RequestProperty);
            }
        }
    }
}
