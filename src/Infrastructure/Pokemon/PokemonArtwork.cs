using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace TokenPet.Infrastructure.Pokemon
{
    internal sealed class PokemonArtwork
    {
        private static readonly HttpClient Client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        private const int MaxBytes = 2 * 1024 * 1024;
        private const string Primary = "https://raw.githubusercontent.com/PokeAPI/sprites/c0542f7c51215d9672439012838c99f520596cbd/sprites/pokemon/other/official-artwork/";
        private const string Mirror = "https://assets.pokemon.com/assets/cms2/img/pokedex/full/";
        private readonly string directory;

        internal PokemonArtwork(string directory) { this.directory = directory; }

        internal byte[] GetCached(int id)
        {
            return ReadCache(CachePath(id));
        }

        internal Task<byte[]> GetAsync(int id, CancellationToken cancellation)
        {
            string number = id.ToString("000", CultureInfo.InvariantCulture);
            string path = CachePath(id);
            return GetPngAsync(path, Primary + id.ToString(CultureInfo.InvariantCulture) + ".png",
                Mirror + number + ".png", cancellation);
        }

        private static async Task<byte[]> GetPngAsync(string path, string primary, string mirror, CancellationToken cancellation)
        {
            byte[] existing = await Task.Run(() => ReadCache(path), cancellation).ConfigureAwait(false);
            if (existing != null) return existing;

            byte[] data = await Download(primary, cancellation).ConfigureAwait(false);
            if (data == null && mirror != null) data = await Download(mirror, cancellation).ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested();
            if (data != null) await Task.Run(() => SaveCache(path, data), cancellation).ConfigureAwait(false);
            return data;
        }

        private string CachePath(int id)
        {
            if (id < 1 || id > KantoCatalog.Count) throw new ArgumentOutOfRangeException("id");
            return Path.Combine(directory, id.ToString("000", CultureInfo.InvariantCulture) + ".png");
        }

        private static byte[] ReadCache(string path)
        {
            try
            {
                var file = new FileInfo(path);
                if (!file.Exists || file.Length > MaxBytes) return null;
                byte[] data = File.ReadAllBytes(path);
                return IsPng(data) ? data : null;
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

        private static async Task<byte[]> Download(string url, CancellationToken cancellation)
        {
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(12));
                try
                {
                    using (var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false))
                    {
                        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > MaxBytes) return null;
                        using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        using (var buffer = new MemoryStream())
                        {
                            var chunk = new byte[16384];
                            int count;
                            while ((count = await stream.ReadAsync(chunk, 0, chunk.Length, timeout.Token).ConfigureAwait(false)) != 0)
                            {
                                if (buffer.Length + count > MaxBytes) return null;
                                buffer.Write(chunk, 0, count);
                            }
                            byte[] data = buffer.ToArray();
                            return IsPng(data) ? data : null;
                        }
                    }
                }
                catch (HttpRequestException) { return null; }
                catch (IOException) { return null; }
                catch (OperationCanceledException)
                {
                    cancellation.ThrowIfCancellationRequested();
                    return null;
                }
            }
        }

        private static void SaveCache(string path, byte[] data)
        {
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(temp, data);
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            finally
            {
                try { File.Delete(temp); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        private static bool IsPng(byte[] data)
        {
            return data.Length >= 33 && data.Length <= MaxBytes &&
                data[0] == 137 && data[1] == 80 && data[2] == 78 && data[3] == 71 &&
                data[4] == 13 && data[5] == 10 && data[6] == 26 && data[7] == 10;
        }
    }
}
