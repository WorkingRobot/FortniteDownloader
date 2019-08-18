using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using FortniteDownloader;
using FortniteDownloader.Net;
using Newtonsoft.Json;

namespace DownloaderApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public sealed partial class MainWindow : Window, IDisposable
    {
        private readonly ObservableCollection<CheckboxItem> FileList = new ObservableCollection<CheckboxItem>();
        private readonly Client Client = new Client();
        private IDownloader FileDownloader;

        public MainWindow()
        {
            InitializeComponent();
            foreach (var v in Downloader.SavedManifests.Keys)
                ManifestVersionCombo.Items.Add(v);
            ManifestVersionCombo.SelectedIndex = ManifestVersionCombo.Items.Count - 1;
            FileListBox.ItemsSource = FileList;
        }

        private void Log(string text) =>
            Dispatcher.InvokeAsync(() => LogBox.AppendText(text + "\n"));

        private async void GetManifestClick(object sender, RoutedEventArgs e)
        {
            ManifestBtn.IsEnabled = false;
            DownloadBtn.IsEnabled = false;
            if (RadioSelect.IsChecked ?? false)
            {
                FileDownloader = new Downloader(Downloader.SavedManifests[ManifestVersionCombo.Text], Client);
            }
            else if (RadioLogin.IsChecked ?? false)
            {
                FileDownloader = new AuthedDownloader();
            }
            else if (RadioCustom.IsChecked ?? false)
            {
                if (string.IsNullOrWhiteSpace(CustomManifestBox.Text))
                {
                    MessageBox.Show(this, "Enter a custom manifest id first!", "DownloaderApp", MessageBoxButton.OK);
                    ManifestBtn.IsEnabled = true;
                    return;
                }
                FileDownloader = new Downloader(CustomManifestBox.Text, Client);
            }
            Dictionary<string, FileChunkPart[]>.KeyCollection keys;
            Log($"Downloading manifest {FileDownloader.ManifestUrl}");
            try
            {
                keys = (await FileDownloader.GetDownloadManifest().ConfigureAwait(false)).FileManifests.Keys;
            }
            catch (JsonReaderException)
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(this, "Your manifest is invalid!", "DownloaderApp", MessageBoxButton.OK);
                    ManifestBtn.IsEnabled = true;
                });
                return;
            }
            Log($"Got {keys.Count} files");
            Dispatcher.Invoke(() =>
            {
                FileList.Clear();
                foreach (var file in keys)
                {
                    FileList.Add(new CheckboxItem(file));
                }
                ManifestBtn.IsEnabled = true;
                DownloadBtn.IsEnabled = true;
            });
        }

        private async void StartDownloadBtn(object sender, RoutedEventArgs e)
        {
            var selectedFiles = new List<string>();
            foreach (var file in FileList)
            {
                if (file.Checked)
                {
                    selectedFiles.Add(file.Key);
                }
            }
            if (selectedFiles.Count == 0)
            {
                MessageBox.Show(this, $"Select some files first!", "DownloaderApp", MessageBoxButton.OK);
                return;
            }
            var manifest = await FileDownloader.GetDownloadManifest().ConfigureAwait(false);
            var downloadSize = selectedFiles.Sum(f => manifest.FileManifests[f].Sum(chunk => (long)chunk.Size));
            if (Dispatcher.Invoke(() => MessageBox.Show(this, $"The total download size is {GetReadableSize(downloadSize)}. Continue?", "DownloaderApp", MessageBoxButton.YesNo) != MessageBoxResult.Yes))
                return;
            Dispatcher.Invoke(() =>
            {
                ProgBar.Value = 0;
                ManifestBtn.IsEnabled = false;
                DownloadBtn.IsEnabled = false;
            });
            _ = DownloadAsync(selectedFiles.ToArray(), downloadSize, manifest).ContinueWith(t => Dispatcher.Invoke(() =>
            {
                Log($"Downloaded {selectedFiles.Count} files");
                ManifestBtn.IsEnabled = true;
                DownloadBtn.IsEnabled = true;
            }));
        }

        private Task DownloadAsync(string[] files, long downloadSize, Manifest manifest)
        {
            var tasks = new Task[files.Length];
            Log($"Downloading {files.Length} files (Size: {GetReadableSize(downloadSize)})");
            for (var i = 0; i < files.Length; i++)
            {
                var file = files[i];
                tasks[i] = DownloadAsync(file, manifest, n => Dispatcher.InvokeAsync(() => ProgBar.Value += (double)n / downloadSize));
            }
            return Task.WhenAll(tasks);
        }

        private async Task DownloadAsync(string file, Manifest manifest, Action<int> progress)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            using (var downloadStream = new DownloadStream(file, manifest, false, Client))
            using (var fileStream = File.OpenWrite(file))
            using (var semaphore = new SemaphoreSlim(20))
            {
                Log($"Downloading {file} in {downloadStream.ChunkCount} chunks (Size: {GetReadableSize(downloadStream.Length)})");
                var tasks = new Task[downloadStream.ChunkCount];

                var writeChunkInd = 0;
                for (var i = 0; i < downloadStream.ChunkCount; i++)
                {
                    await semaphore.WaitAsync().ConfigureAwait(false);
                    tasks[i] = downloadStream.GetChunk(i).ContinueWith(async (t, nObj) =>
                    {
                        var n = (int)nObj;
                        while (n != writeChunkInd)
                        {
                            await Task.Delay(25);
                        }
                        await fileStream.WriteAsync(t.Result, 0, t.Result.Length).ConfigureAwait(false);
                        writeChunkInd++;
                        semaphore.Release();
                        progress(t.Result.Length);
                    }, i).Unwrap();
                }
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            Log($"Downloaded {file}");
        }

        private static string GetReadableSize(long size)
        {
            var absolute_i = size < 0 ? -size : size;
            string suffix;
            double readable;
            if (absolute_i >= 0x40000000)
            {
                suffix = "GB";
                readable = size >> 20;
            }
            else if (absolute_i >= 0x100000)
            {
                suffix = "MB";
                readable = size >> 10;
            }
            else if (absolute_i >= 0x400)
            {
                suffix = "KB";
                readable = size;
            }
            else
            {
                return size.ToString("0 B");
            }
            readable /= 1024;
            return readable.ToString("0.## ") + suffix;
        }

        private bool disposed;
        public void Dispose()
        {
            if (disposed) return;
            FileDownloader.Dispose();
            Client.Dispose();
            disposed = true;
        }

        private class CheckboxItem
        {
            public string Key { get; set; }
            public bool Checked { get; set; }

            public CheckboxItem(string key) => Key = key;
        }
    }
}
