using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MacroDoButler
{
    public partial class MainWindow
    {
        private bool syncingBrowser;
        private bool presetGridView;
        private double presetCardWidth = -1;
        private IDictionary<string, ImageSource> gameArtwork = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);

        private sealed class GameBrowserRow
        {
            public string Name { get; set; }
            public int Count { get; set; }
            public ImageSource Icon { get; set; }
        }

        private void RefreshGameBrowser(IList<string> names)
        {
            bool previous = syncingBrowser;
            syncingBrowser = true;
            try
            {
                var all = presetService.LoadAll();
                GamesList.ItemsSource = names.Select(name => new GameBrowserRow
                {
                    Name = name,
                    Count = all.Count(p => string.Equals(p.GameName, name, StringComparison.OrdinalIgnoreCase)),
                    Icon = gameArtwork.TryGetValue(GameArtworkService.Key(name), out var icon) ? icon : null
                }).ToList();
                GamesList.SelectedValue = GameSelector.SelectedItem;
                UpdateBrowserGameIcon(GameSelector.SelectedItem as string);
            }
            finally { syncingBrowser = previous; }
        }

        private void SyncPresetBrowser(IList<Preset> presets, string game)
        {
            bool previous = syncingBrowser;
            syncingBrowser = true;
            try
            {
                GamesList.SelectedValue = game;
                UpdateBrowserGameIcon(game);
                PresetBrowser.ItemsSource = presets;
                PresetBrowser.SelectedValue = PresetSelector.SelectedItem;
                PresetBrowserTitle.Text = string.IsNullOrWhiteSpace(game) ? "Presets" : "Presets de " + game;
                PresetBrowserTitle.ToolTip = PresetBrowserTitle.Text;
                var current = presets.FirstOrDefault(p => p.Name == (string)PresetSelector.SelectedItem);
                SelectedPresetSummary.Visibility = current == null ? Visibility.Collapsed : Visibility.Visible;
                DeleteSelectedPresetButton.IsEnabled = current != null;
                SelectedPresetTitle.Text = current == null ? "Selecione ou crie um preset" : current.Name;
            }
            finally { syncingBrowser = previous; }
        }

        private void UpdateBrowserGameIcon(string game)
        {
            ImageSource icon;
            gameArtwork.TryGetValue(GameArtworkService.Key(game), out icon);
            GameHeaderIcon.Source = icon;
            GameHeaderFallback.Visibility = icon == null ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void LoadGameArtwork(object sender, RoutedEventArgs e)
        {
            try
            {
                gameArtwork = await Task.Run(() => new GameArtworkService().LoadLocalArtwork());
                if (IsLoaded) RefreshGameBrowser(presetService.LoadGames());
            }
            catch { /* Artwork is optional; the generic game symbol remains available. */ }
        }

        private void PresetsPage_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            // Let the game and preset lists consume their own scroll gestures.
            var source = e.OriginalSource as DependencyObject;
            while (source != null)
            {
                if (source is ListBox) return;
                if (source is Visual) source = VisualTreeHelper.GetParent(source);
                else if (source is FrameworkContentElement content) source = content.Parent;
                else break;
            }
            InnerScrollViewer_PreviewMouseWheel(sender, e);
        }

        private void GamesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (syncingBrowser || updatingSelectors || GamesList.SelectedValue == null) return;
            GameSelector.SelectedItem = GamesList.SelectedValue;
        }

        private void PresetBrowser_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (syncingBrowser || updatingSelectors || PresetBrowser.SelectedValue == null) return;
            PresetSelector.SelectedItem = PresetBrowser.SelectedValue;
        }

        private void PresetViewMode_Click(object sender, RoutedEventArgs e)
        {
            presetGridView = sender == GridViewButton;
            ListViewButton.IsChecked = !presetGridView;
            GridViewButton.IsChecked = presetGridView;
            PresetBrowser.ItemsPanel = (ItemsPanelTemplate)MainPage.FindResource(presetGridView ? "PresetGridPanel" : "PresetListPanel");
            presetCardWidth = -1;
            ApplyPresetCardWidth();
        }

        private void PresetBrowser_SizeChanged(object sender, SizeChangedEventArgs e) { ApplyPresetCardWidth(); }
        private void ApplyPresetCardWidth()
        {
            if (PresetBrowser == null) return;
            double available = Math.Max(0, PresetBrowser.ActualWidth - 24);
            int columns = Math.Max(1, (int)(available / 240));
            double width = presetGridView ? Math.Max(0, available / columns - 10) : double.NaN;
            if (width.Equals(presetCardWidth)) return;
            presetCardWidth = width;
            var style = new Style(typeof(ListBoxItem), (Style)MainPage.FindResource("BrowserItemStyle"));
            style.Setters.Add(new Setter(FrameworkElement.WidthProperty, width));
            style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0, 0, presetGridView ? 10 : 0, 8)));
            PresetBrowser.ItemContainerStyle = style;
        }

        private void EditBrowserPreset_Click(object sender, RoutedEventArgs e)
        {
            var preset = (sender as FrameworkElement)?.DataContext as Preset;
            if (preset == null) return;
            OpenPresetEditor(preset);
        }
        private void NewBrowserPreset_Click(object sender, RoutedEventArgs e)
        {
            string game = GameSelector.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(game))
            {
                ShowFeedback("Cadastre um jogo em Gerenciar jogos antes de criar o preset.", false);
                return;
            }
            OpenPresetEditor(null, game);
        }
    }
}
