using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using TokenPet.Core;
using TokenPet.Desktop.Views;
using TokenPet.Infrastructure.Pokemon;

namespace TokenPet.Desktop.Pokemon
{
    internal sealed class PickerWindow : Window
    {
        private readonly PokemonCatalog catalog;
        private readonly PokemonArtwork artwork;
        private readonly Grid view;
        private readonly ListBox entries;
        private readonly TextBox search;
        private readonly ComboBox generation;
        private readonly CheckBox favoritesOnly;
        private readonly HashSet<int> favorites;
        private CancellationTokenSource previewRequest;

        internal PickerWindow(Window owner, PokemonCatalog catalog, PokemonArtwork artwork, int currentId,
            IEnumerable<int> favoriteIds)
        {
            this.catalog = catalog;
            this.artwork = artwork;
            favorites = new HashSet<int>(favoriteIds);
            Owner = owner;
            Title = "切换宝可梦";
            Width = 546;
            Height = 515;
            MinWidth = 460;
            MinHeight = 420;
            ResizeMode = ResizeMode.CanResizeWithGrip;
            using (var stream = typeof(PickerWindow).Assembly.GetManifestResourceStream("TokenPet.Picker.xaml"))
                view = (Grid)XamlReader.Load(stream);
            ComicDialog.Apply(this, view);
            ComicDialog.EnableTitleDrag(this, Find<TextBlock>("Heading"));
            entries = Find<ListBox>("Entries");
            search = Find<TextBox>("Search");
            generation = Find<ComboBox>("Generation");
            favoritesOnly = Find<CheckBox>("FavoritesOnly");
            search.TextChanged += (s, e) => Filter(null);
            generation.SelectionChanged += (s, e) => Filter(null);
            favoritesOnly.Checked += (s, e) => Filter(null);
            favoritesOnly.Unchecked += (s, e) => Filter(null);
            entries.SelectionChanged += (s, e) => PreviewSelection();
            Find<Button>("Favorite").Click += (s, e) => ToggleFavorite();
            Find<Button>("SelectPokemon").Click += (s, e) => DialogResult = true;
            Loaded += (s, e) =>
            {
                search.Focus();
                if (entries.SelectedItem != null) entries.ScrollIntoView(entries.SelectedItem);
            };
            Closed += (s, e) => CancelPreview();
            Filter(catalog.Get(currentId));
        }

        internal PokemonSpecies Selected { get { return entries.SelectedItem as PokemonSpecies; } }
        internal ImageSource SelectedArtwork { get { return Find<Image>("Artwork").Source; } }
        internal int[] FavoriteIds { get { return favorites.OrderBy(id => id).ToArray(); } }

        private T Find<T>(string name) where T : class { return (T)view.FindName(name); }

        private void Filter(PokemonSpecies preferred)
        {
            var matches = catalog.Search(search.Text, generation.SelectedIndex)
                .Where(species => favoritesOnly.IsChecked != true || favorites.Contains(species.Id)).ToArray();
            var prior = preferred ?? Selected;
            entries.ItemsSource = matches;
            entries.SelectedItem = matches.Contains(prior) ? prior : matches.FirstOrDefault();
            if (entries.SelectedItem != null) entries.ScrollIntoView(entries.SelectedItem);
            else PreviewSelection();
            Find<TextBlock>("Count").Text = string.Format("{0} 只宝可梦", matches.Length);
        }

        private void ToggleFavorite()
        {
            var species = Selected;
            if (species == null) return;
            if (!favorites.Add(species.Id)) favorites.Remove(species.Id);
            if (favoritesOnly.IsChecked == true) Filter(species);
            else UpdateFavoriteButton(species);
        }

        private void UpdateFavoriteButton(PokemonSpecies species)
        {
            var button = Find<Button>("Favorite");
            var star = Find<Path>("FavoriteStar");
            bool saved = species != null && favorites.Contains(species.Id);
            button.IsEnabled = species != null;
            button.Visibility = species == null ? Visibility.Collapsed : Visibility.Visible;
            string label = saved ? "取消收藏" : "收藏";
            Find<TextBlock>("FavoriteLabel").Text = label;
            AutomationProperties.SetName(button, label);
            if (saved) star.SetResourceReference(Shape.FillProperty, "ComicFavoriteGold");
            else star.Fill = Brushes.Transparent;
        }

        private async void PreviewSelection()
        {
            CancelPreview();
            var species = Selected;
            var image = Find<Image>("Artwork");
            var status = Find<TextBlock>("Status");
            var select = Find<Button>("SelectPokemon");
            image.Source = null;
            select.IsEnabled = false;
            UpdateFavoriteButton(species);
            Find<TextBlock>("SpeciesName").Text = species == null ? "" : species.Name;
            Find<TextBlock>("SpeciesInfo").Text = species == null ? "" : species.Number + " · " + species.English + "\n" + species.TypeLabel;
            status.Text = species == null
                ? (favoritesOnly.IsChecked == true && favorites.Count == 0 ? "还没有收藏的宝可梦" : "没有匹配的宝可梦")
                : "正在加载立绘…";
            if (species == null) return;
            select.IsEnabled = true;

            var request = new CancellationTokenSource();
            previewRequest = request;
            try
            {
                // Debounce key navigation and typing before requesting any uncached image.
                await System.Threading.Tasks.Task.Delay(150, request.Token);
                var data = await artwork.GetAsync(species.Id, request.Token);
                if (request.IsCancellationRequested) return;
                image.Source = MascotView.DecodeArtwork(data);
                status.Text = image.Source == null ? "暂时无法加载立绘。可以先选择，联网后会自动重试。" : "";
            }
            catch (OperationCanceledException) { }
            finally
            {
                if (previewRequest == request) previewRequest = null;
                request.Dispose();
            }
        }

        private void CancelPreview()
        {
            if (previewRequest != null) previewRequest.Cancel();
        }
    }
}
