using System.IO.Ports;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elijah.Data.Repository;
using Elijah.Domain.Entities;
using Elijah.Logic.Abstract;
using GenericRepository.Repository;
using Microsoft.EntityFrameworkCore;

namespace Elijah.Logic.Concrete;

public class OpenThermService(
    SerialPort serialPort,
    IZigbeeRepository repo) : IOpenThermService
{
    // private readonly IRepository<OpenTherm> _openThermRepo = openThermRepo;
    // private readonly IRepository<Device> _deviceRepo = deviceRepo;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Establishes connection to ESP and waits for ready signal.
    /// NOTE: This re-initializes the injected SerialPort instance. Consider injecting
    /// serial port settings instead of the port itself for better DI practices.
    /// </summary>
    public async Task ESPConnect()
    {
        // CAUTION: This replaces the injected SerialPort instance
        serialPort = new SerialPort("/dev/ttyUSB1", 115200)
        {
            ReadTimeout = 2000,
            WriteTimeout = 2000
        };

        serialPort.Open();
        Console.WriteLine("Serial port opened. Waiting for ESP to reset...");
        await Task.Delay(4000);

        string response = "";
        int attempts = 0;
        while (!response.Contains("ESP_READY") && attempts < 20)
        {
            try
            {
                Console.WriteLine(attempts);
                response += serialPort.ReadExisting();
                await Task.Delay(200);
                attempts++;
            }
            catch (TimeoutException)
            {
                // Expected during wait
            }
        }

        if (response.Contains("ESP_READY"))
        {
            Console.WriteLine("ESP_READY received!");
            serialPort.WriteLine("test");
        }
        else
        {
            Console.WriteLine("Failed to receive ESP_READY");
        }
    }

    /// <summary>
    /// Queries all OpenTherm configs and sends them as JSON to ESP.
    /// ESP expects: { "ID": "config", "values": { "1": {"interval":60,"threshold":1.5} } }
    /// </summary>
    public async Task SendConfigToEspAsync()
    {
        if (!serialPort.IsOpen)
            throw new InvalidOperationException("Serial port is not open. Call ESPConnect() first.");

        var configs = await repo.Query<OpenTherm>().ToListAsync();
        var valuesDict = configs.ToDictionary(
            c => c.Id.ToString(),
            c => new { interval = c.IntervalSec, threshold = c.Threshold }
        );

        var message = new
        {
            ID = "config",
            values = valuesDict
        };

        string json = JsonSerializer.Serialize(message, _jsonOptions) + "\n";
        var buffer = Encoding.UTF8.GetBytes(json);
        await serialPort.BaseStream.WriteAsync(buffer, 0, buffer.Length);
        await serialPort.BaseStream.FlushAsync();
        Console.WriteLine($"Sent config for {configs.Count} parameters");
    }

    /// <summary>
    /// Listens continuously for JSON messages from ESP.
    /// Yields messages with ID and VALUE properties.
    /// </summary>
    public async IAsyncEnumerable<IOpenThermService.IncomingMessage> ListenForIncomingMessagesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!serialPort.IsOpen)
            throw new InvalidOperationException("Serial port is not open. Call ESPConnect() first.");

        using var reader = new StreamReader(serialPort.BaseStream, Encoding.UTF8, true, 1024, leaveOpen: true);

        while (!cancellationToken.IsCancellationRequested)
        {
            string? line = null;
            IOpenThermService.IncomingMessage? message = null;

            try
            {
                line = await reader.ReadLineAsync().WaitAsync(cancellationToken);
                if (line == null) break; 

                message = JsonSerializer.Deserialize<IOpenThermService.IncomingMessage>(line, _jsonOptions);
            }
            catch (OperationCanceledException)
            {
                break; 
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"JSON error: {ex.Message}");
                Console.WriteLine($"Raw line: {line}");
                continue; 
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Serial error: {ex.Message}");
                await Task.Delay(1000, cancellationToken);
                continue; 
            }

            
            if (message?.ID != null)
            {
                yield return message;
            }
            else if (line != null)
            {
                Console.WriteLine($"Malformed message: {line}");
            }
        }
    }

    /// <summary>
    /// Sends a parameter update as ID/VALUE JSON pair.
    /// </summary>
    public async Task SendParameterAsync(string id, object value)
    {
        if (!serialPort.IsOpen)
            throw new InvalidOperationException("Serial port is not open. Call ESPConnect() first.");

        var message = new IdValueMessage(id, value);
        string json = JsonSerializer.Serialize(message, _jsonOptions) + "\n";

        var buffer = Encoding.UTF8.GetBytes(json);
        await serialPort.BaseStream.WriteAsync(buffer, 0, buffer.Length);
        await serialPort.BaseStream.FlushAsync();
        Console.WriteLine($"Sent: ID={id}, VALUE={value}");
    }
    
    /// <summary>
    /// Upserts (updates or creates) an OpenTherm configuration entry in the database.
    /// </summary>
    public async Task UpdateOrCreateConfigAsync(int id, int intervalSec, float threshold)
    {
        var existingConfig = await repo.Query<OpenTherm>()
            .FirstOrDefaultAsync(c => c.Id == id);

        if (existingConfig != null)
        {
            existingConfig.IntervalSec = intervalSec;
            existingConfig.Threshold = threshold;
        }
        else
        {
            // Create new
            var newConfig = new OpenTherm
            {
                Id = id,
                IntervalSec = intervalSec,
                Threshold = threshold
            };
            await repo.CreateAsync(newConfig);
        }

       
        await repo.SaveChangesAsync();
    }


    // public record IncomingMessage(string ID, JsonElement VALUE);


    private record IdValueMessage(string ID, object VALUE);
}