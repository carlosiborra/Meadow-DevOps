using System;
using System.Diagnostics;
using Meadow.Foundation.Relays;
using System.Threading;
using TemperatureWarriorCode;
using System.Runtime.InteropServices;


public class RoundController
{
    private TemperatureRange[] temperatureRanges;
    private int totalTime;
    private int refreshRate;
    private PIDController pidController;

    private Relay relayBombilla;
    private Relay relayPlaca;

    public bool Configure(TemperatureRange[] ranges, int total, int refresh, Relay relayBombilla, Relay relayPlaca, out string errorMessage)
    {
        errorMessage = null;

        if (ranges == null || ranges.Length == 0)
        {
            errorMessage = "Temperature ranges cannot be null or empty.";
            return false;
        }

        if (total <= 0)
        {
            errorMessage = "Total time must be greater than zero.";
            return false;
        }

        if (refresh <= 0)
        {
            errorMessage = "Refresh rate must be greater than zero.";
            return false;
        }

        temperatureRanges = ranges;
        totalTime = total;
        refreshRate = refresh;

        this.relayBombilla = relayBombilla;
        this.relayPlaca = relayPlaca;

        return true;
    }

    public void StartOperation()
    {
        Console.WriteLine("Starting the operations (PID and relayController)...");
        // Define the PID controller gains (kp, ki, kd). TODO: The gains should be tuned based on the system requirements.
        double kp = 0.8;
        double ki = 0.2;
        double kd = 0.001;
        // Create a PID controller with the specified gains (kp, ki, kd). TODO: The gains should be tuned based on the system requirements.
        pidController = new PIDController(kp, ki, kd);

        pidController.Reset(); // Reset the PID controller

        Stopwatch stopwatch = new Stopwatch(); // Create a stopwatch to measure the time elapsed

        // For each temperature range, run the PID controller until the specified duration is reached
        for (int i = 0; i < temperatureRanges.Length; i++)
        {
            double targetTemperature = (temperatureRanges[i].Min + temperatureRanges[i].Max) / 2.0; // Calculate the target temperature as the average of the minimum and maximum temperature of the range
            stopwatch.Start(); // Start the stopwatch

            // Se lanza un hilo que esté continuamente calculando la salida del control PID
            Thread thread = new Thread(() =>
            {
                while (true)
                {
                    // Calcular la salida del control PID
                    pidController.Compute(targetTemperature);
                    // Obtener la salida del control PID
                    int output = (int)Data.output;
                    //Console.WriteLine("PID Output: " + output);
                    Data.temp_structure.temperatures.Add(Convert.ToDouble(Data.temp_act));
                    Data.temp_structure.temp_max.Add(Convert.ToDouble(Data.temp_max[i]));
                    Data.temp_structure.temp_min.Add(Convert.ToDouble(Data.temp_min[i]));
                    Data.temp_structure.timestamp.Add(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                    // Esperar un tiempo antes de volver a calcular del tiempo que tarda el sensor en actualizar la temperatura.
                    Thread.Sleep(Data.refresh);
                }
            });
            // Iniciamos el hilo de cálculo del control PID
            thread.Start();

            // Se activa / desactiva el relay de la bombilla y la placa de Peltier según la salida del control PID
            while (stopwatch.Elapsed.Milliseconds < temperatureRanges[i].Duration)
            {
                Console.WriteLine($"Target temperature: {targetTemperature}");
                Console.WriteLine($"stopwatch.Elapsed.Milliseconds: {stopwatch.Elapsed.Milliseconds }");
                Console.WriteLine($"temperatureRanges[i].Duration: {temperatureRanges[i].Duration }");
                // TODO: Adapt the parameters of the ControlarRelay method to the specific system requirements.
                ControlarRelay(relayBombilla, relayPlaca, (int)Data.output, 60, 1000); // Applying the PID controller output to the system.
            }
            Console.WriteLine("Siguiente Range");
            Data.next_range=false;
            stopwatch.Stop();
            stopwatch.Reset();
        }
    }

    private void ControlarRelay(Relay relayBombilla, Relay relayPlaca, int intensidad, int intensityBreakpoint, int periodoTiempo)
    {
        Console.WriteLine("Intensidad: {0}, IntensityBreakpoint: {1}, Periodo: {2}", intensidad, intensityBreakpoint, periodoTiempo);
        
        if (intensidad <= intensityBreakpoint)
        {
            // Código de enfriamiento
            int tiempoEncendido = (intensidad - intensityBreakpoint) * 100 / (120 - intensityBreakpoint) * periodoTiempo / (-100);
            relayPlaca.IsOn = true;
            relayBombilla.IsOn = false;
            Console.WriteLine("❄️ Enfriando: Tiempo encendido del sistema de enfriamiento (peltier): {0}", tiempoEncendido);
            Thread.Sleep(tiempoEncendido);
            relayPlaca.IsOn = false;
            Thread.Sleep(periodoTiempo - tiempoEncendido);
        }
        else
        {
            // Código de calentamiento
            int tiempoEncendido = (intensidad - intensityBreakpoint) * 100 / (120 - intensityBreakpoint) * periodoTiempo / 100;
            relayPlaca.IsOn = false;
            relayBombilla.IsOn = true;
            Console.WriteLine("🔥 Tiempo encendido del sistema de calentamiento (bombilla): {0}", tiempoEncendido);
            Thread.Sleep(tiempoEncendido);
            relayBombilla.IsOn = false;
            Thread.Sleep(periodoTiempo - tiempoEncendido);
        }
        Console.WriteLine("Current temperature: {0}", Data.temp_act);
    }
}
