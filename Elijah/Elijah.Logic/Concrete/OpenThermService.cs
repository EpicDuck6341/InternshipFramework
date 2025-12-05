using System.IO.Ports;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Elijah.Data.Repository;
using Elijah.Domain.Entities;
using Elijah.Logic.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Elijah.Logic.Concrete;

// ---------------------------------------------------- //
// OpenTherm gateway communication service              //
// Manages serial connection with ESP and data exchange //
// ---------------------------------------------------- //
public class OpenThermService(
    SerialPort serialPort,
    IZigbeeRepository repo) : IOpenThermService
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // --------------------------------------------------------- //
    // Establishes connection to ESP and waits for ready signal  //
    // --------------------------------------------------------- //
    public async Task EspConnect()
    {
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
                
            }
        }

        Console.WriteLine(response.Contains("ESP_READY")
            ? "ESP_READY received!"
            : "Failed to receive ESP_READY");

        if (response.Contains("ESP_READY"))
            serialPort.WriteLine("test");
    }

    // -------------------------------------------------------- //
    // Queries OpenTherm configs and sends them as JSON to ESP  //
    // -------------------------------------------------------- //
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

    // ----------------------------------------------- //
    // Listens continuously for JSON messages from ESP //
    // ----------------------------------------------- //
    public async IAsyncEnumerable<IOpenThermService.IncomingMessage> ListenForIncomingMessagesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!serialPort.IsOpen)
            throw new InvalidOperationException("Serial port is not open. Call ESPConnect() first.");

        using var reader = new StreamReader(serialPort.BaseStream, Encoding.UTF8, true, 1024, leaveOpen: true);

        while (!cancellationToken.IsCancellationRequested)
        {
            string? line = null;
            IOpenThermService.IncomingMessage? message;

            try
            {
                // ReSharper disable once MethodSupportsCancellation
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
                Console.WriteLine($"JSON error: {ex.Message}\nRaw line: {line}");
                continue;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Serial error: {ex.Message}");
                await Task.Delay(1000, cancellationToken);
                continue;
            }

            if (message?.Id != null)
                yield return message;
            else
                Console.WriteLine($"Malformed message: {line}");
        }
    }

    // ----------------------------------------------- //
    // Sends a parameter update as ID/VALUE JSON pair  //
    // ----------------------------------------------- //
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

    // -------------------------------------------------------- //
    // Upserts an OpenTherm configuration entry in the database //
    // -------------------------------------------------------- //
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
            await repo.CreateAsync(new OpenTherm
            {
                Id = id,
                IntervalSec = intervalSec,
                Threshold = threshold
            });
        }

        await repo.SaveChangesAsync();
    }

    private record IdValueMessage(string Id, object Value);
}
