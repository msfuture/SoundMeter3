using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI;
using Windows.Foundation;
using Windows.Foundation.Collections;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;
using System.Threading;
using Windows.Devices.Bluetooth.Advertisement;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SoundMeter3
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private GattCharacteristic fff1;
        private GattCharacteristic fff2;
        private BluetoothLEDevice device;
        private int notificationCount = 0;
        private DispatcherQueue dispatcherQueue;
        private CancellationTokenSource pollCts;
        private List<double> dbHistory = new List<double>();
        private const int MaxPoints = 100;
        public MainWindow()
        {
            this.InitializeComponent();
            dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        
        }

        private void Output(string message)
        {
            dispatcherQueue.TryEnqueue(() =>
            {
                OutputBox.Text += message + "\n";
            });
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            pollCts = new CancellationTokenSource();
            Output("▶️ Messung gestartet (alle 500 ms).");
            byte[] trigger = new byte[] { 0x30, 0x3B };

            try
            {
                while (!pollCts.IsCancellationRequested)
                {
                    var writer = new DataWriter();
                    writer.WriteBytes(trigger);
                    var result = await fff2.WriteValueAsync(writer.DetachBuffer());
                    if (result != GattCommunicationStatus.Success)
                    {
                        Output("❌ Fehler beim Senden.");
                    }
                    await Task.Delay(500, pollCts.Token);
                }
            }
            catch (TaskCanceledException)
            {
                Output("⏹️ Messung gestoppt.");
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            pollCts?.Cancel();
        }

        private async Task InitializeBleAsync()
        {
            Output("🔍 Suche nach 'SoundMeter'...");

            var tcs = new TaskCompletionSource<ulong>();
            var watcher = new BluetoothLEAdvertisementWatcher
            {
                ScanningMode = BluetoothLEScanningMode.Active
            };

            watcher.Received += (w, evt) =>
            {
                if (!string.IsNullOrEmpty(evt.Advertisement.LocalName) && evt.Advertisement.LocalName.Contains("SoundMeter"))
                {
                    tcs.TrySetResult(evt.BluetoothAddress);
                    watcher.Stop();
                }
            };

            watcher.Start();
            var bluetoothAddress = await tcs.Task;

            device = await BluetoothLEDevice.FromBluetoothAddressAsync(bluetoothAddress);
            if (device == null)
            {
                Output("❌ Gerät nicht gefunden.");
                return;
            }

            var servicesResult = await device.GetGattServicesAsync();
            var service = servicesResult.Services.FirstOrDefault(s => s.Uuid == Guid.Parse("0000fff0-0000-1000-8000-00805f9b34fb"));
            if (service == null)
            {
                Output("❌ FFF0-Service nicht gefunden.");
                return;
            }

            var characteristicsResult = await service.GetCharacteristicsAsync();
            fff1 = characteristicsResult.Characteristics.FirstOrDefault(c => c.Uuid == Guid.Parse("0000fff1-0000-1000-8000-00805f9b34fb"));
            fff2 = characteristicsResult.Characteristics.FirstOrDefault(c => c.Uuid == Guid.Parse("0000fff2-0000-1000-8000-00805f9b34fb"));

            if (fff1 == null || fff2 == null)
            {
                Output("❌ FFF1 oder FFF2 nicht gefunden.");
                return;
            }

            fff1.ValueChanged += (sender, args) =>
            {
                var reader = DataReader.FromBuffer(args.CharacteristicValue);
                byte[] data = new byte[reader.UnconsumedBufferLength];
                reader.ReadBytes(data);
                if (data.Length >= 3)
                {
                    int raw = (data[1] << 8) | data[2];
                    double db = raw / 10.0;
                    dispatcherQueue.TryEnqueue(() =>
                    {
                        DbText.Text = $"{db:F1} dB(A)";
                        UpdateChart(db);
                    });
                }
            };

            var notifyStatus = await fff1.WriteClientCharacteristicConfigurationDescriptorAsync(
                GattClientCharacteristicConfigurationDescriptorValue.Notify);
            if (notifyStatus != GattCommunicationStatus.Success)
            {
                Output("❌ Notify konnte nicht aktiviert werden.");
                return;
            }

            Output("✅ Gerät verbunden und bereit. Drücke 'Start'.");
        }

        private void UpdateChart(double value)
        {
            dbHistory.Add(value);
            if (dbHistory.Count > MaxPoints)
                dbHistory.RemoveAt(0);

            var geometry = new PathGeometry();
            var figure = new PathFigure();
            if (dbHistory.Count > 0)
            {
                double width = ChartCanvas.ActualWidth > 0 ? ChartCanvas.ActualWidth : 300;
                double height = ChartCanvas.ActualHeight > 0 ? ChartCanvas.ActualHeight : 100;

                figure.StartPoint = new Windows.Foundation.Point(0, ScaleY(dbHistory[0], height));

                for (int i = 1; i < dbHistory.Count; i++)
                {
                    double x = i * (width / MaxPoints);
                    double y = ScaleY(dbHistory[i], height);
                    figure.Segments.Add(new LineSegment { Point = new Windows.Foundation.Point(x, y) });
                }
            }

            geometry.Figures.Add(figure);

            var path = new Microsoft.UI.Xaml.Shapes.Path
            {
                Stroke = new SolidColorBrush(Colors.LightCoral),
                StrokeThickness = 2,
                Data = geometry
            };

            ChartCanvas.Children.Clear();
            ChartCanvas.Children.Add(path);
        }

        private double ScaleY(double db, double height)
        {
            double min = 30;  // dB(A) minimum
            double max = 100; // dB(A) maximum
            db = Math.Clamp(db, min, max);
            return height - ((db - min) / (max - min) * height);
        }

        private async void initButton_Click(object sender, RoutedEventArgs e)
        {
            await InitializeBleAsync();
        }
    }
}
