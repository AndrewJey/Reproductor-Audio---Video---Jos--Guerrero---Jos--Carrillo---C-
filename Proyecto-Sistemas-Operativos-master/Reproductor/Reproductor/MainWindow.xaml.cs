using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;//Proporciona complemento accesibles en tiempo de ejecución, que leen y escriben documentos en diferentes formatos de datos
using System.Windows.Input;//Proporciona tipos para admitir el sistema de entrada de WPF
using System.Windows.Media;//Proporciona la reproducción multimedia
using System.Windows.Media.Imaging;//Proporciona tipos que se utilizan para codificar y descodificar imágenes de mapa de bits
using System.Windows.Shapes;//Proporciona acceso a una biblioteca de formas que se puede usar en Lenguaje XAML
using System.Diagnostics;//Libreria de interactua con procesos del sistema, registros de eventos y contadores de rendimiento
using System.Windows.Threading;//Libreria de Hebras/Hilos
using Microsoft.Win32;//Libreria controla los eventos generados por el sistema operativo y manipulan el registro del sistema
using System.Linq;//Libreria Entity
namespace WpfAppReproductorMp3
{
	public partial class MainWindow : Window
	{
        /*Instancias publicas de contadores de rendimiento del sistema*/ 
        public PerformanceCounter RAM = new PerformanceCounter("Process", "Working Set - Private", Process.GetCurrentProcess().ProcessName);
        public PerformanceCounter CPU = new PerformanceCounter("Process", "% Processor Time", Process.GetCurrentProcess().ProcessName, true);
        /*Variables publicas*/
        //Variables para almacenar items
        public ListBoxItem currentTrack;
        public ListBoxItem PreviousTrack;
        //Variables de colores
        public Brush currentTrackIndicator;
        public Brush TrackColor;
        //Variables Temporizadores
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
            currentTrackIndicator = Brushes.Blue;
            TrackColor = listaDeReproduccion.Foreground;
            Timer = new DispatcherTimer();
            //intervalo del timer
            Timer.Interval = TimeSpan.FromSeconds(1);
            Timer.Tick += new EventHandler(Timer_Tick);
            //se obtine cantidad de nucleos del sistema
            LogicalCores = Get_LogicalCores();
            TimerSystem = new DispatcherTimer();
            TimerSystem.Tick += TimerSystem_Tick;
            //intervalo del TimerSystem
            TimerSystem.Interval = new TimeSpan(0,0,1);
            //inicio del TimerSystem
            TimerSystem.Start();
            isDragging = false;
            TimerPlay = new DispatcherTimer();
            TimerPlay.Tick += TimerPlay_Tick;
            //intervalo del TimerSystem
            TimerPlay.Interval = new TimeSpan(0, 0, 1);
            //inicio del TimerSystem
            TimerPlay.Start();
            LoadTracks();
        }
        /// <summary>
        /// Metodo para limpiar la lista de elementos guardada en la base de datos
        /// </summary>
        private void DeleteTracks()
        {
            using (Reproductor.TracksEntities context = new Reproductor.TracksEntities())
            {
                context.Tracks.RemoveRange(context.Tracks);
                context.SaveChanges();
            }
        }
        /// <summary>
        /// Metodo para guardar la lista de reproduccion en el base de datos
        /// </summary>
        private void SaveTracks()
        {
            using (Reproductor.TracksEntities context = new Reproductor.TracksEntities())
            {
                //metodo para limpiar la lista
                DeleteTracks();
                Reproductor.Tracks tracks = new Reproductor.Tracks();
                //for que recorre la lista de reproduccion
                for (int x = 0; x < listaDeReproduccion.Items.Count; x++)
                {
                    ListBoxItem lstItem = new ListBoxItem();
                    lstItem = (ListBoxItem)listaDeReproduccion.Items[x];
                    tracks.Track = lstItem.Tag.ToString();
                    context.Tracks.Add(tracks);
                    try
                    {
                        context.SaveChanges();
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show("Error:" + e);
                    }
                }               
            }
        }
        /// <summary>
        /// Metodo para obtener la lista de reproduccion en el base de datos
        /// </summary>
        private void LoadTracks()
        {
            using (Reproductor.TracksEntities context = new Reproductor.TracksEntities())
            {
                //select de la base de datos
                var DB = from Tracks in context.Tracks
                         select Tracks;
                //forech para recorrer el select
                foreach (var tracks in DB)
                {
                    ListBoxItem lstItem = new ListBoxItem();
                    lstItem.Content = System.IO.Path.GetFileNameWithoutExtension(tracks.Track);
                    lstItem.Tag = tracks.Track;
                    listaDeReproduccion.Items.Add(lstItem);
                    listaDeReproduccion.SelectedIndex = 0;
                    //metodo para reproducir 
                    PlayTrack();
                }
            }
        }
        /// <summary>
        /// Metodo para obtener los nucleos logicos del procesador
        /// </summary>
        /// <returns></returns>
        private int Get_LogicalCores()
        {
            int LogicalCount = 0;
            //forech que recorre la informacion del select al Win32 del sistema  
            foreach (var item in new System.Management.ManagementObjectSearcher("Select * from Win32_ComputerSystem").Get())
            {
                //se guarda la cantidad de procesos logicos que puede manejar el procesador
                LogicalCount += int.Parse(item["NumberOfLogicalProcessors"].ToString());
            }
            //retorna los nucleos logicos del procesador
            return LogicalCount;
        }
        /// <summary>
        /// Evento tick del Timer el cual muestra el tiempo actual de la posicion del barra
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TimerPlay_Tick(object sender, EventArgs e)
        {
            if (!isDragging) // Si no hay operación de arrastre en el sliderTimeLine su posición se actualiza cada segundo.
            {
                //obtiene la pocicion en tiempo de la reproduccion 
                TimeSpan ts = MEmusicPlayer.Position;
                //refresca el tiempo impreso en el label con un formato de tiempo
                lblTimer.Content = ts.ToString(@"hh\:mm\:ss");
            }
        }
        /// <summary>
        /// Evento tick del TimerSystem el cual refreca el cpu y ram
        /// </summary>
        /// <param name="sender"></param>2
        /// <param name="e"></param>
        private void TimerSystem_Tick(object sender, EventArgs e)
        {
            //se refresca la informacion de los Labels 
            lblCpu.Content = Convert.ToInt32(CPU.NextValue()/ LogicalCores) + " %";
            lblRam.Content = Convert.ToInt32(RAM.NextValue()) / (int)(1024);
            lbl_volume.Content = Convert.ToInt32(SliderVolume.Value * 100);
        }
        /// <summary>
        /// Evento tick del Timer el cual actualiza cada segundo
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Timer_Tick(object sender, EventArgs e)
        {
            if (!isDragging) // Si no hay operación de arrastre en el sliderTimeLine su posición se actualiza cada segundo
            {
                SliderTimeLine.Value = MEmusicPlayer.Position.TotalSeconds;   
            }
        }
        /// <summary>
        /// Evento DragEnter (Arrastre) del listbox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ListBox_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))//verifica si copia los datos al listbox
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;
        }
        /// <summary>
        /// evento drop del listbox el cual verifica los archivos para agregarlos
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listBox1_Drop(object sender, DragEventArgs e)
        {
            string[] s = (string[])e.Data.GetData(DataFormats.FileDrop, false);// String de los archivos del evento Drop del listbox
            foreach (string fileName in s)
            {
                if (System.IO.Path.GetExtension(fileName) == ".mp3" ||
                    System.IO.Path.GetExtension(fileName) == ".mp4" ||
                    System.IO.Path.GetExtension(fileName) == ".avi" ||
                    System.IO.Path.GetExtension(fileName) == ".wmv" ||
                    System.IO.Path.GetExtension(fileName) == ".MP3") // verifica las extenciones de los archivos
                {
                    ListBoxItem lstItem = new ListBoxItem();
                    //se obtiene el nombre del archivo
                    lstItem.Content = System.IO.Path.GetFileNameWithoutExtension(fileName);
                    //se guarda la direccion del archivo
                    lstItem.Tag = fileName;
                    //se agrega el ListItem a la lista de reproduccion
                    listaDeReproduccion.Items.Add(lstItem);
                }
            }
            if (currentTrack == null)//si no contiene un item
            {
                //seleciona el primer index
                listaDeReproduccion.SelectedIndex = 0;
                //metodo para reproducir 
                PlayTrack();
            }
        }
        /// <summary>
        /// Evento MediaOpened del MediaElement para ejecutar un archivo
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MEmusicPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {                     
            if (MEmusicPlayer.NaturalDuration.HasTimeSpan)//si hay una reproduccion a ejecutar
            {
                //se obtine el total de duracion del elemento a reproducir
                TimeSpan ts = MEmusicPlayer.NaturalDuration.TimeSpan;
                //se fija el rango maximo del SliderTimeLine
                SliderTimeLine.Maximum = ts.TotalSeconds;
                //se fija el incremento minimo del SliderTimeLine
                SliderTimeLine.SmallChange = 1;
            }
            //inicia el Timer
            Timer.Start();
        }
        /// <summary>
        /// Evento MediaEnded del MediaElement el cual resetea y ejecuta el siguiente archivo 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MEmusicPlayer_MediaEnded(object sender, RoutedEventArgs e) // Se dispara cuando se termina de reproducir una canción pero no cuando se hace click en Stop
        {
            //resetea el valor del SliderTimeLine  
            SliderTimeLine.Value = 0;
            //metodo para reproduccir el elemento siguiente de la lista
            MoveToNextTrack();        
        }
        /// <summary>
        /// Metodo para reproducir el siguiente elemento de la lista de reproduccion
        /// </summary>
        private void MoveToNextTrack()
        {
            //verifica si el index del elemento actual es menor a la cantidad de items de la lista
            if (listaDeReproduccion.Items.IndexOf(currentTrack) < listaDeReproduccion.Items.Count - 1)
            {
                //cambia la seleccion del index de la lista por el siguiente elemento  
                listaDeReproduccion.SelectedIndex = listaDeReproduccion.Items.IndexOf(currentTrack) + 1;
                //metodo para reproducir 
                PlayTrack();                
                return;
            }
            //verifica si el index del elemento actual es igual a la cantidad de items de la lista
            else if (listaDeReproduccion.Items.IndexOf(currentTrack) == listaDeReproduccion.Items.Count - 1)
            {
                //cambia la seleccion del index de la lista por el primer elemento de la lista  
                listaDeReproduccion.SelectedIndex = 0;
                //metodo para reproducir 
                PlayTrack();                
                return;
            }
        }
        /// <summary>
        /// Metodo para reproducir el elemento anterior de la lista de reproduccion
        /// </summary>
        private void MoveToPrecedentTrack()
        {
            //verifica si el index del elemento actual es mayor a 0 
            if (listaDeReproduccion.Items.IndexOf(currentTrack) > 0)
            {
                //cambia la seleccion del index de la lista por el index anterior al elemento actual  
                listaDeReproduccion.SelectedIndex = listaDeReproduccion.Items.IndexOf(currentTrack) -1;
                //metodo para reproducir 
                PlayTrack();
            }
            //verifica si el index del elemento actual es igual a 0 
            else if (listaDeReproduccion.Items.IndexOf(currentTrack) == 0)
            {
                //cambia la seleccion del index por el index del ultimo elemento de la lista
                listaDeReproduccion.SelectedIndex = listaDeReproduccion.Items.Count - 1;
                //metodo para reproducir 
                PlayTrack();
            }
        }
        /// <summary>
        /// evento click del button de pausa 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnPause_Click(object sender, RoutedEventArgs e)
        {
            if (listaDeReproduccion.HasItems)//si contiene items
            {
                //pausa la reproduccion
                MEmusicPlayer.Pause();
            }
        }
        /// <summary>
        /// evento click del button de reproducir
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnPlay_Click(object sender, RoutedEventArgs e)
        {
            if (listaDeReproduccion.HasItems)//si contiene items
            {
                //metodo para reproducir 
                PlayTrack();
            }
        }
        /// <summary>
        /// metodo para reproducir nuevos archivos agregado a la lista
        /// </summary>
        private void PlayTrack()
        {
            //si el item seleccionado actual es diferente al item en reproduccion   
            if (listaDeReproduccion.SelectedItem != currentTrack)
            {
                if (currentTrack != null)// verifica si contiene un elemento
                {
                    //cambia el elemento previo por el actual
                    PreviousTrack = currentTrack;
                    PreviousTrack.Foreground = TrackColor;
                }
                //agrega el item seleccionado actual de la lista
                currentTrack = (ListBoxItem)listaDeReproduccion.SelectedItem;                
                currentTrack.Foreground = currentTrackIndicator;
                //agrega la dirreccion del archivo del item seleccionado al MediaElement
                MEmusicPlayer.Source = new Uri(currentTrack.Tag.ToString());
                //resetea el SliderTimeLine 
                SliderTimeLine.Value = 0; 
                //reproduce el archivo del MediaElement
                MEmusicPlayer.Play();
            }
            else
            {
                //reproduce el archivo del MediaElement
                MEmusicPlayer.Play();
            }
        }
        /// <summary>
        /// evento click del button de detener
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            if (listaDeReproduccion.HasItems)//si contiene items
            {
                //detiene la repruduccion
                MEmusicPlayer.Stop();
                //resetea el SliderTimeLine 
                SliderTimeLine.Value = 0; 
            }
        }
        /// <summary>
        /// metodo MouseLeftButtonDown del regtangulo de la interfas de usuario
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Rectangle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }
        /// <summary>
        /// evento click del button de cerrar
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnCerrar_Click(object sender, RoutedEventArgs e)
        {
            //metodo para guardar la lista en le base de datos 
            SaveTracks();
            //cierra la aplicacion
            this.Close();
        }
        /// <summary>
        /// evento DragStarted del SliderTimeLine
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SliderTimeLine_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            //cambia el valor del boolean de arrastre
            isDragging = true;
        }
        /// <summary>
        /// evento dragcompleted para la barra de reproduccion
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SliderTimeLine_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            //cambia el valor del boolean de arrastre
            isDragging = false;
            // La posición del MediaElement se actualiza para que coincida con el progreso del sliderTimeLine
            MEmusicPlayer.Position = TimeSpan.FromSeconds(SliderTimeLine.Value);
        }
        /// <summary>
        /// evento click del button de siguiente 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnNext_Click(object sender, RoutedEventArgs e)
        {
            if (listaDeReproduccion.HasItems)//si contiene items
            {
                //metodo para reproducir el siguiente elemento de la lista 
                MoveToNextTrack();
            }
        }
        /// <summary>
        /// evento click del button de anterior
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnPrecedent_Click(object sender, RoutedEventArgs e)
        {
            if (listaDeReproduccion.HasItems)//si contiene items
            {
                //metodo para reproducir el anterior elemento de la lista
                MoveToPrecedentTrack();
            }
        }
        /// <summary>
        /// metodo MouseLeftButtonUp del SliderTimeLine
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SliderTimeLine_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            //actualiza la posicion de la reproduccion del MediaElement con la nueva seleccion en el SliderTimeLine  
            MEmusicPlayer.Position = TimeSpan.FromSeconds(SliderTimeLine.Value);
        }
        /// <summary>
        /// evento click del button de abrir 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnAbrir_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog fd = new OpenFileDialog();
            fd.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            fd.Multiselect = true;
            fd.Title = "Seleccione archivos de audio mp3 y video wmv";
            fd.Filter = "Archivos Multimedia mp3 y wmv (*.mp3),(*.wmv)|*.mp3;*.wmv|Audio mp3 (*.mp3)|*.mp3|video wmv (*.wmv)|*.wmv|video mp4 (*.mp4)|*.mp4";
            try
            {
                //se obtien la lista de archivos con un OpenFileDialog
                Nullable<bool> result = fd.ShowDialog();
                if (result == true)//si hay un resultado
                {
                    //se carga un array de archivos
                    string[] s = fd.FileNames;
                    foreach (string fileName in s)
                    {
                        if (System.IO.Path.GetExtension(fileName) == ".mp3" ||
                            System.IO.Path.GetExtension(fileName) == ".mp4" ||
                            System.IO.Path.GetExtension(fileName) == ".avi" ||
                            System.IO.Path.GetExtension(fileName) == ".wmv" ||
                            System.IO.Path.GetExtension(fileName) == ".MP3")//se verifica las extenciones de los archivos
                        {
                            ListBoxItem lstItem = new ListBoxItem();
                            //se obtiene el nombre del archivo
                            lstItem.Content = System.IO.Path.GetFileNameWithoutExtension(fileName);
                            //se guarda la dirrecion del archivo
                            lstItem.Tag = fileName;
                            //se agrega el ListItem a la lista de reproduccion
                            listaDeReproduccion.Items.Add(lstItem);
                        }
                    }
                    if (currentTrack == null)//si no contiene un item
                    {
                        //seleciona el primer index
                        listaDeReproduccion.SelectedIndex = 0;
                        //metodo para reproducir
                        PlayTrack();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        /// <summary>
        /// evento MouseDoubleClick del listbox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ListaDeReproduccion_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (currentTrack != null)//si no contiene un item
            {
                //agrega el item seleccionado actual de la lista
                currentTrack = (ListBoxItem)listaDeReproduccion.SelectedItem;
                currentTrack.Foreground = currentTrackIndicator;
                //agrega la dirreccion del archivo del item seleccionado al MediaElement
                MEmusicPlayer.Source = new Uri(currentTrack.Tag.ToString());
                //resetea el SliderTimeLine 
                SliderTimeLine.Value = 0;
                //reproduce el archivo del MediaElement
                MEmusicPlayer.Play();
            }
        }
    }
}