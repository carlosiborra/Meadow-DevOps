﻿using Meadow;
using Meadow.Foundation;
using Meadow.Foundation.Sensors.Temperature;
using Meadow.Devices;
using Meadow.Hardware;
using Meadow.Gateway.WiFi;
using Meadow.Units;
using System;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using TemperatureWarriorCode.Web;
using NETDuinoWar;
using Meadow.Foundation.Relays;


namespace TemperatureWarriorCode {
    public class MeadowApp : App<F7FeatherV2> {

        //Temperature Sensor
        AnalogTemperature sensor;

        //Time Controller Values
        public static int total_time = 0;
        public static int total_time_in_range = 0;
        public static int total_time_out_of_range = 0;

        // Relays
        public static Relay relayBombilla;
        public static Relay relayPlaca;

        public int count = 0;

        public override async Task Run() {
            if (count == 0) {
                Console.WriteLine("Initialization...");

                // TODO uncomment when needed 
                // Temperature Sensor Configuration
                sensor = new AnalogTemperature(analogPin: Device.Pins.A01, sensorType: AnalogTemperature.KnownSensorType.TMP36);
                sensor.TemperatureUpdated += AnalogTemperatureUpdated; // Subscribing to event (temp change)

                // TODO Modify this value according to the needs of the project
                sensor.StartUpdating(TimeSpan.FromSeconds(2)); // Start updating the temperature every 2 seconds. In our case, we need to decide the time to update the temperature. We could use a lower value to get more accurate results and obtain an average of the temperature deleting outliers.

                // TODO Local Network configuration (uncomment when needed)
                var wifi = Device.NetworkAdapters.Primary<IWiFiNetworkAdapter>();
                wifi.NetworkConnected += WiFiAdapter_ConnectionCompleted;

                //WiFi Channel
                WifiNetwork wifiNetwork = ScanForAccessPoints(Secrets.WIFI_NAME);

                wifi.NetworkConnected += WiFiAdapter_WiFiConnected;
                await wifi.Connect(Secrets.WIFI_NAME, Secrets.WIFI_PASSWORD);

                string IPAddress = wifi.IpAddress.ToString();

                //Connnect to the WiFi network.
                Console.WriteLine($"IP Address test: {IPAddress}");
                Data.IP = IPAddress;
                if (!string.IsNullOrWhiteSpace(IPAddress))
                {
                    Data.IP = IPAddress;
                    WebServer webServer = new WebServer(wifi.IpAddress, Data.Port);
                    if (webServer != null)
                    {
                        webServer.Start();
                    }
                }

                Console.WriteLine("Meadow Initialized!");

                count = count + 1;
            }
        }

        //TW Combat Round
        public static void StartRound() {

            Stopwatch timer = Stopwatch.StartNew(); 
            timer.Start();

            //Value to control the time for heating and cooling
            //First iteration is 100 for the time spend creating RoundController and thread
            int sleep_time = 20;

            // Initialization of time controller
            RoundController RoundController = new RoundController();

            // Relays initialization
            relayBombilla = InstantiateRelay(Device.Pins.D02, initialValue: false);
            relayPlaca = InstantiateRelay(Device.Pins.D03, initialValue: false);

            //Configuration of differents ranges
            TemperatureRange[] temperatureRanges = new TemperatureRange[Data.round_time.Length];

            //Range configurations
            bool success;
            string error_message = null;
            

            // Define temperature ranges for the round and duration for each range
            for (int i = 0; i < Data.temp_min.Length; i++) {
                Console.WriteLine(Data.temp_max[i]);
                temperatureRanges[i] = new TemperatureRange(double.Parse(Data.temp_min[i]), double.Parse(Data.temp_max[i]), int.Parse(Data.round_time[i]) * 1000);
                total_time += int.Parse(Data.round_time[i]);
            }
            
            //Initialization of RoundController with the ranges defined
            success = RoundController.Configure(temperatureRanges, total_time * 1000, Data.refresh, relayBombilla, relayPlaca, out error_message);
            Console.WriteLine(success);

            //Initialization of timer (thread that controls the time of the round)
            Thread t = new Thread(Timer);
            t.Start();

            //Stopwatch regTempTimer = new Stopwatch();

            // TODO: Thread que obtenga la temperatura actual según el tiempo de refresco (medidas continuas por el sensor), y tenga una logica que escoja un valor de temperatura basandose en los que ha medido anteriormente. Por ejemplo, descartando los outliers y calculando la media de los valores restantes. DETERMINAR CUALES SON OUTLIERS!
                    // Es decir, cada valor de tiempo de cadencia (especificado por el profesor), se escogerá un valor de temperatura (basándose en las mediciones que se han realizado en intervalos de tiempo más pequeños) que será el que se mostrará en las gráficas.
            RoundController.StartOperation(); // Start the round operation (PID controller for each temperature range)
            
            t.Abort();

            //regTempTimer.Start();

        }

            


        /*
        ESTE CODIGO NOS LO DAN, LO DEJO DE MOMENTO POR SI NOS ES UTIL EN EL FUTURO
        Console.WriteLine("STARTING");

        //THE TW START WORKING
        while (Data.is_working) {

            //This is the time refresh we did not do before
            Thread.Sleep(Data.refresh - sleep_time);

            //Temperature registration
            Console.WriteLine($"RegTempTimer={regTempTimer.Elapsed.ToString()}, enviando Temp={Data.temp_act}");
            RoundController.RegisterTemperature(double.Parse(Data.temp_act));
            regTempTimer.Restart();

        }
        Console.WriteLine("Round Finish");
        t.Abort();

        total_time_in_range += RoundController.TimeInRangeInMilliseconds;
        total_time_out_of_range += RoundController.TimeOutOfRangeInMilliseconds;
        Data.time_in_range_temp = (RoundController.TimeInRangeInMilliseconds / 1000);

        Console.WriteLine("Tiempo dentro del rango " + (((double)RoundController.TimeInRangeInMilliseconds / 1000)) + " s de " + total_time + " s");
        Console.WriteLine("Tiempo fuera del rango " + ((double)total_time_out_of_range / 1000) + " s de " + total_time + " s");
        */ 

        #region Relay
        private static Relay InstantiateRelay(IPin thePin, bool initialValue)
        {
            Relay theRelay = new Relay(Device.CreateDigitalOutputPort(thePin));
            theRelay.IsOn = initialValue;
            return theRelay;
        }
        #endregion


        //Round Timer
        private static void Timer() {
            Data.is_working = true;
            for (int i = 0; i < Data.round_time.Length; i++) {
                Data.time_left = int.Parse(Data.round_time[i]);

                while (Data.time_left > 0) {
                    Data.time_left--;
                    Thread.Sleep(1000);
                }
            }
            Data.is_working = false;
        }
        

        //Temperature and Display Updated
        void AnalogTemperatureUpdated(object sender, IChangeResult<Meadow.Units.Temperature> e) {

            Data.temp_act = Math.Round((Double)e.New.Celsius, 2).ToString();

            Console.WriteLine($"Temperature={Data.temp_act}");
        }

        void WiFiAdapter_WiFiConnected(object sender, EventArgs e) {
            if (sender != null) {
                Console.WriteLine($"Connecting to WiFi Network {Secrets.WIFI_NAME}");
            }
        }

        void WiFiAdapter_ConnectionCompleted(object sender, EventArgs e) {
            Console.WriteLine("Connection request completed.");
        }

        protected WifiNetwork ScanForAccessPoints(string SSID) {
            WifiNetwork wifiNetwork = null;
            ObservableCollection<WifiNetwork> networks = new ObservableCollection<WifiNetwork>(Device.NetworkAdapters.Primary<IWiFiNetworkAdapter>().Scan()?.Result?.ToList()); //REVISAR SI ESTO ESTA BIEN
            wifiNetwork = networks?.FirstOrDefault(x => string.Compare(x.Ssid, SSID, true) == 0);
            return wifiNetwork;
        }
    }
}
