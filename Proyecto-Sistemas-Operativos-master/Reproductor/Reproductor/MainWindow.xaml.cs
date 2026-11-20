using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace WpfAppReproductorMp3
{
    public static class PlaylistStore
    {
        private static readonly HashSet<string> SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".mp4", ".avi", ".wmv", ".wav", ".m4a", ".flac", ".wma"
        };

        public static bool IsSupportedExtension(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return false;
            }
            string ext = Path.GetExtension(filePath);
            return !string.IsNullOrEmpty(ext) && SupportedExtensions.Contains(ext);
        }

        public static string GetFallbackPlaylistPath()
        {
            string envDir = Environment.GetEnvironmentVariable("REPRODUCTOR_DATA_DIR");
            string dir = !string.IsNullOrWhiteSpace(envDir)
                ? envDir.Trim()
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReproductorAudioVideo");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "playlist.txt");
        }

        public static List<string> NormalizePaths(IEnumerable<string> paths)
        {
            var unique = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (paths == null) return unique;

            foreach (string p in paths)
            {
                if (string.IsNullOrWhiteSpace(p)) continue;
                string trimmed = p.Trim();
                if (IsSupportedExtension(trimmed) && seen.Add(trimmed))
                {
                    unique.Add(trimmed);
                }
            }
            return unique;
        }

        public static void SaveFallback(IEnumerable<string> paths)
        {
            List<string> cleaned = NormalizePaths(paths);
            File.WriteAllLines(GetFallbackPlaylistPath(), cleaned, Encoding.UTF8);
        }

        public static List<string> LoadFallback()
        {
            string path = GetFallbackPlaylistPath();
            if (!File.Exists(path))
            {
                return new List<string>();
            }
            return NormalizePaths(File.ReadAllLines(path, Encoding.UTF8));
        }

        public static int GetNextIndex(int currentIndex, int count)
        {
            if (count <= 0) return -1;
            if (currentIndex < 0 || currentIndex >= count - 1) return 0;
            return currentIndex + 1;
        }

        public static int GetPreviousIndex(int currentIndex, int count)
        {
            if (count <= 0) return -1;
            if (currentIndex <= 0 || currentIndex >= count) return count - 1;
            return currentIndex - 1;
        }
    }

    public partial class MainWindow : Window
    {
        public PerformanceCounter RAM;
        public PerformanceCounter CPU;
        public ListBoxItem currentTrack;
        public ListBoxItem PreviousTrack;
        public Brush currentTrackIndicator;
        public Brush TrackColor;
        public DispatcherTimer Timer;
        public DispatcherTimer TimerSystem;
        public DispatcherTimer TimerPlay;
        public bool isDragging;
        public int LogicalCores;

        public MainWindow()
        {
            this.InitializeComponent();
            currentTrack = null;
            PreviousTrack = null;
            currentTrackIndicator = Brushes.DodgerBlue;
            TrackColor = listaDeReproduccion.Foreground;
            LogicalCores = Math.Max(1, Environment.ProcessorCount);

            InitializeCountersSafe();

            Timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            Timer.Tick += Timer_Tick;

            TimerSystem = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            TimerSystem.Tick += TimerSystem_Tick;
            TimerSystem.Start();

            isDragging = false;
            TimerPlay = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            TimerPlay.Tick += TimerPlay_Tick;
            TimerPlay.Start();

            LoadTracks();
        }

        private void InitializeCountersSafe()
        {
            try
            {
                string procName = Process.GetCurrentProcess().ProcessName;
                RAM = new PerformanceCounter("Process", "Working Set - Private", procName);
                CPU = new PerformanceCounter("Process", "% Processor Time", procName, true);
            }
            catch
            {
                RAM = null;
                CPU = null;
            }
        }

        private List<string> CollectTrackPaths()
        {
            var paths = new List<string>();
            for (int x = 0; x < listaDeReproduccion.Items.Count; x++)
            {
                ListBoxItem lstItem = listaDeReproduccion.Items[x] as ListBoxItem;
                if (lstItem != null && lstItem.Tag != null)
                {
                    paths.Add(lstItem.Tag.ToString());
                }
            }
            return PlaylistStore.NormalizePaths(paths);
        }

        private void DeleteTracks()
        {
            using (Reproductor.TracksEntities context = new Reproductor.TracksEntities())
            {
                context.Tracks.RemoveRange(context.Tracks);
                context.SaveChanges();
            }
        }

        private void SaveTracks()
        {
            List<string> paths = CollectTrackPaths();
            try
            {
                PlaylistStore.SaveFallback(paths);
            }
            catch
            {
                // Non-fatal fallback persistence error.
            }

            try
            {
                using (Reproductor.TracksEntities context = new Reproductor.TracksEntities())
                {
                    context.Tracks.RemoveRange(context.Tracks);
                    foreach (string path in paths)
                    {
                        context.Tracks.Add(new Reproductor.Tracks { Track = path });
                    }
                    context.SaveChanges();
                }
            }
            catch
            {
                // Entity Framework SQL Server DB is optional; local fallback already saved.
            }
        }

        private void LoadTracks()
        {
            List<string> loaded = new List<string>();
            try
            {
                using (Reproductor.TracksEntities context = new Reproductor.TracksEntities())
                {
                    loaded = PlaylistStore.NormalizePaths(context.Tracks.Select(t => t.Track).ToList());
                }
            }
            catch
            {
                loaded = PlaylistStore.LoadFallback();
            }

            if (loaded.Count == 0)
            {
                loaded = PlaylistStore.LoadFallback();
            }

            AddFilesToPlaylist(loaded, autoPlayIfIdle: false);
            if (listaDeReproduccion.HasItems)
            {
                listaDeReproduccion.SelectedIndex = 0;
            }
        }

        private void AddFilesToPlaylist(IEnumerable<string> fileNames, bool autoPlayIfIdle)
        {
            var existing = new HashSet<string>(CollectTrackPaths(), StringComparer.OrdinalIgnoreCase);
            foreach (string fileName in PlaylistStore.NormalizePaths(fileNames))
            {
                if (!existing.Add(fileName))
                {
                    continue;
                }

                ListBoxItem lstItem = new ListBoxItem
                {
                    Content = Path.GetFileNameWithoutExtension(fileName),
                    Tag = fileName
                };
                listaDeReproduccion.Items.Add(lstItem);
            }

            if (autoPlayIfIdle && currentTrack == null && listaDeReproduccion.HasItems)
            {
                listaDeReproduccion.SelectedIndex = 0;
                PlayTrack();
            }
        }

        private void TimerPlay_Tick(object sender, EventArgs e)
        {
            if (!isDragging)
            {
                TimeSpan ts = MEmusicPlayer.Position;
                lblTimer.Content = ts.ToString(@"hh\:mm\:ss");
            }
        }

        private void TimerSystem_Tick(object sender, EventArgs e)
        {
            try
            {
                if (CPU != null)
                {
                    lblCpu.Content = Convert.ToInt32(CPU.NextValue() / LogicalCores) + " %";
                }
                else
                {
                    lblCpu.Content = "N/A";
                }

                if (RAM != null)
                {
                    lblRam.Content = (Convert.ToInt64(RAM.NextValue()) / 1024L) + " KB";
                }
                else
                {
                    using (Process proc = Process.GetCurrentProcess())
                    {
                        lblRam.Content = (proc.PrivateMemorySize64 / 1024L) + " KB";
                    }
                }
            }
            catch
            {
                // Keep UI responsive even when perf counters are restricted.
            }

            lbl_volume.Content = Convert.ToInt32(SliderVolume.Value * 100);
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (!isDragging)
            {
                SliderTimeLine.Value = MEmusicPlayer.Position.TotalSeconds;
            }
        }

        private void ListBox_DragEnter(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        }

        private void listBox1_Drop(object sender, DragEventArgs e)
        {
            string[] files = e.Data.GetData(DataFormats.FileDrop, false) as string[];
            if (files != null && files.Length > 0)
            {
                AddFilesToPlaylist(files, autoPlayIfIdle: true);
            }
        }

        private void MEmusicPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            if (MEmusicPlayer.NaturalDuration.HasTimeSpan)
            {
                TimeSpan ts = MEmusicPlayer.NaturalDuration.TimeSpan;
                SliderTimeLine.Maximum = Math.Max(1.0, ts.TotalSeconds);
                SliderTimeLine.SmallChange = 1;
            }
            Timer.Start();
        }

        private void MEmusicPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            SliderTimeLine.Value = 0;
            MoveToNextTrack();
        }

        private void MoveToNextTrack()
        {
            int count = listaDeReproduccion.Items.Count;
            if (count == 0) return;

            int idx = currentTrack != null ? listaDeReproduccion.Items.IndexOf(currentTrack) : listaDeReproduccion.SelectedIndex;
            int nextIdx = PlaylistStore.GetNextIndex(idx, count);
            if (nextIdx >= 0)
            {
                listaDeReproduccion.SelectedIndex = nextIdx;
                PlayTrack();
            }
        }

        private void MoveToPrecedentTrack()
        {
            int count = listaDeReproduccion.Items.Count;
            if (count == 0) return;

            int idx = currentTrack != null ? listaDeReproduccion.Items.IndexOf(currentTrack) : listaDeReproduccion.SelectedIndex;
            int prevIdx = PlaylistStore.GetPreviousIndex(idx, count);
            if (prevIdx >= 0)
            {
                listaDeReproduccion.SelectedIndex = prevIdx;
                PlayTrack();
            }
        }

        private void btnPause_Click(object sender, RoutedEventArgs e)
        {
            if (listaDeReproduccion.HasItems)
            {
                MEmusicPlayer.Pause();
            }
        }

        private void btnPlay_Click(object sender, RoutedEventArgs e)
        {
            if (!listaDeReproduccion.HasItems) return;
            if (listaDeReproduccion.SelectedIndex < 0)
            {
                listaDeReproduccion.SelectedIndex = 0;
            }
            PlayTrack();
        }

        private void PlayTrack()
        {
            ListBoxItem selected = listaDeReproduccion.SelectedItem as ListBoxItem;
            if (selected == null || selected.Tag == null)
            {
                return;
            }

            string path = selected.Tag.ToString();
            if (selected != currentTrack)
            {
                if (!File.Exists(path))
                {
                    MessageBox.Show("El archivo multimedia no se encuentra en la ruta:\n" + path, "Archivo no encontrado", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (currentTrack != null)
                {
                    PreviousTrack = currentTrack;
                    PreviousTrack.Foreground = TrackColor;
                }

                currentTrack = selected;
                currentTrack.Foreground = currentTrackIndicator;
                MEmusicPlayer.Source = new Uri(path);
                SliderTimeLine.Value = 0;
            }

            MEmusicPlayer.Play();
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            if (listaDeReproduccion.HasItems)
            {
                MEmusicPlayer.Stop();
                SliderTimeLine.Value = 0;
            }
        }

        private void Rectangle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void btnCerrar_Click(object sender, RoutedEventArgs e)
        {
            SaveTracks();
            this.Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            Timer?.Stop();
            TimerSystem?.Stop();
            TimerPlay?.Stop();
            RAM?.Dispose();
            CPU?.Dispose();
            base.OnClosed(e);
        }

        private void SliderTimeLine_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            isDragging = true;
        }

        private void SliderTimeLine_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            isDragging = false;
            MEmusicPlayer.Position = TimeSpan.FromSeconds(SliderTimeLine.Value);
        }

        private void btnNext_Click(object sender, RoutedEventArgs e)
        {
            if (listaDeReproduccion.HasItems)
            {
                MoveToNextTrack();
            }
        }

        private void btnPrecedent_Click(object sender, RoutedEventArgs e)
        {
            if (listaDeReproduccion.HasItems)
            {
                MoveToPrecedentTrack();
            }
        }

        private void SliderTimeLine_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            MEmusicPlayer.Position = TimeSpan.FromSeconds(SliderTimeLine.Value);
        }

        private void btnAbrir_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog fd = new OpenFileDialog
            {
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                Multiselect = true,
                Title = "Seleccione archivos multimedia",
                Filter = "Archivos Multimedia (*.mp3;*.mp4;*.wmv;*.avi;*.wav;*.m4a)|*.mp3;*.mp4;*.wmv;*.avi;*.wav;*.m4a|Todos los archivos (*.*)|*.*"
            };

            try
            {
                if (fd.ShowDialog() == true)
                {
                    AddFilesToPlaylist(fd.FileNames, autoPlayIfIdle: true);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error al abrir archivos", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ListaDeReproduccion_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (listaDeReproduccion.SelectedItem != null)
            {
                PlayTrack();
            }
        }
    }
}
