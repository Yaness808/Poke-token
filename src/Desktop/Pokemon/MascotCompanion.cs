using System;
using System.IO;
using System.Threading;
using System.Windows.Media;
using TokenPet.Core;
using TokenPet.Desktop.Views;
using TokenPet.Infrastructure.Configuration;
using TokenPet.Infrastructure.Pokemon;

namespace TokenPet.Desktop.Pokemon
{
    internal sealed class MascotCompanion
    {
        private readonly Preferences preferences;
        private readonly MascotControl control;
        private ImageSource currentImage;
        private int pendingId;
        private DateTime retryAfter;
        private CancellationTokenSource artworkRequest;

        internal PokemonCatalog Catalog { get; private set; }
        internal PokemonArtwork Artwork { get; private set; }
        internal PokemonSpecies Species { get; private set; }
        internal int SelectedId { get { return Catalog.Get(preferences.PokemonId).Id; } }

        internal MascotCompanion(Preferences preferences, MascotControl control, string cacheDirectory)
        {
            this.preferences = preferences;
            this.control = control;
            Catalog = new PokemonCatalog();
            Artwork = new PokemonArtwork(cacheDirectory);

            if (!string.IsNullOrEmpty(preferences.MascotPath))
            {
                try
                {
                    currentImage = MascotView.LoadCustom(preferences.MascotPath);
                    control.Show(currentImage, "自定义头像");
                    return;
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                catch (ArgumentException) { }
                catch (FormatException) { }
                catch (NotSupportedException) { }
            }

            Species = Catalog.Get(preferences.PokemonId);
            control.Show(null, Species.Number, Species);
        }

        internal void Select(PokemonSpecies species, ImageSource image)
        {
            CancelArtwork();
            preferences.PokemonId = species.Id;
            preferences.MascotPath = null;
            Species = species;
            currentImage = image;
            retryAfter = DateTime.MinValue;
            control.Show(image, image == null ? species.Number : species.Name, species);
            RefreshArtwork();
        }

        internal void SelectCustom(string path)
        {
            var image = MascotView.LoadCustom(path);
            CancelArtwork();
            preferences.MascotPath = path;
            Species = null;
            currentImage = image;
            control.Show(image, "自定义头像");
        }

        internal async void RefreshArtwork()
        {
            if (Species == null || currentImage != null || pendingId == Species.Id || DateTime.UtcNow < retryAfter) return;
            var species = Species;
            var request = new CancellationTokenSource();
            artworkRequest = request;
            pendingId = species.Id;
            try
            {
                var data = await Artwork.GetAsync(species.Id, request.Token);
                if (request.IsCancellationRequested) return;
                currentImage = MascotView.DecodeArtwork(data);
                control.Show(currentImage, currentImage == null ? species.Number : species.Name, species);
                retryAfter = DateTime.UtcNow.AddSeconds(45);
            }
            catch (OperationCanceledException) { }
            finally
            {
                if (artworkRequest == request)
                {
                    artworkRequest = null;
                    pendingId = 0;
                }
                request.Dispose();
            }
        }

        internal void CancelArtwork()
        {
            if (artworkRequest == null) return;
            artworkRequest.Cancel();
            artworkRequest = null;
            pendingId = 0;
        }
    }
}
