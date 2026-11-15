using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using TokenPet.Core;
using TokenPet.Desktop.Views;
using TokenPet.Infrastructure.Pokemon;

namespace TokenPet.Desktop.Pokemon
{
    internal sealed class PickerWindow : Window
    {
        private readonly KantoCatalog catalog;
        private readonly PokemonArtwork artwork;
        private readonly Grid view;
        private readonly ListBox entries;
        private readonly TextBox search;
        private CancellationTokenSource previewRequest;

        internal PickerWindow(Window owner, KantoCatalog catalog, PokemonArtwork artwork, int currentId)
        {
            this.catalog = catalog;
            this.artwork = artwork;
            Owner = owner;
            Title = "切换宝可梦";
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Width = 540;
            Height = 520;
            MinWidth = 450;
            MinHeight = 400;
            using (var stream = typeof(PickerWindow).Assembly.GetManifestResourceStream("TokenPet.Picker.xaml"))
                view = (Grid)XamlReader.Load(stream);
            Content = view;
            entries = Find<ListBox>("Entries");
            search = Find<TextBox>("Search");
            search.TextChanged += (s, e) => Filter(null);
            entries.SelectionChanged += (s, e) => PreviewSelection();
            Find<Button>("SelectPokemon").Click += (s, e) => DialogResult = true;
            Loaded += (s, e) => search.Focus();
            Closed += (s, e) => CancelPreview();
            Filter(catalog.Get(currentId));
        }

        internal PokemonSpecies Selected { get { return entries.SelectedItem as PokemonSpecies; } }
        internal ImageSource SelectedArtwork { get { return Find<Image>("Artwork").Source; } }

        private T Find<T>(string name) where T : class { return (T)view.FindName(name); }

        private void Filter(PokemonSpecies preferred)
        {
            var matches = catalog.Search(search.Text).ToArray();
            var prior = preferred ?? Selected;
            entries.ItemsSource = matches;
            entries.SelectedItem = matches.Contains(prior) ? prior : matches.FirstOrDefault();
            if (entries.SelectedItem != null) entries.ScrollIntoView(entries.SelectedItem);
            Find<TextBlock>("Count").Text = string.Format("{0} / {1} 只", matches.Length, KantoCatalog.Count);
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
            Find<TextBlock>("SpeciesName").Text = species == null ? "" : species.Name;
            Find<TextBlock>("SpeciesInfo").Text = species == null ? "" : species.Number + " · " + species.English + "\n" + species.TypeLabel;
            status.Text = species == null ? "没有匹配的宝可梦" : "正在加载立绘…";
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
