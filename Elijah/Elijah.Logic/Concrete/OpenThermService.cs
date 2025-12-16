using System.IO.Ports;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elijah.Data.Repository;
using Elijah.Domain.Entities;
using Elijah.Logic.Abstract;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Elijah.Logic.Concrete;

// ---------------------------------------------------- //
// OpenTherm gateway communication service              //
// Manages serial connection with ESP and data exchange //
// ---------------------------------------------------- //
public class OpenThermService(
    SerialPort serialPort,
    IAzureIoTHubService azure,
    IZigbeeRepository repo) : IOpenThermService, IHostedService
{
    
    private readonly SemaphoreSlim _serialLock = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    
    private Task? _processingTask;
    private StreamReader? _streamReader;
    private bool _isDisposed;
    private CancellationTokenSource? _linkedCts;
    
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        Console.WriteLine("OpenThermService starting...");
    
      
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
        _processingTask = Task.Run(() => ProcessLoopAsync(linkedCts.Token), linkedCts.Token);
    
        
        _linkedCts = linkedCts; 
    
        return Task.CompletedTask;
    }



    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_isDisposed)
            return;

        Console.WriteLine("OpenThermService stopping...");
        _cts.Cancel();
        _linkedCts?.Dispose();
        
        if (_processingTask != null)
        {
            try
            {
                await Task.WhenAny(_processingTask, Task.Delay(5000, cancellationToken));
            }
            catch (OperationCanceledException) { /* Expected */ }
        }

        await CloseSerialPortAsync();
    }
    
    private async Task ProcessLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await EspConnectAsync(cancellationToken);
                await SendConfigToEspAsync(cancellationToken);

                await foreach (var msg in ListenForIncomingMessagesAsync(cancellationToken))
                {
                    await azure.SendTelemetryAsync("OpenTherm", msg.Id, msg.Value);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("OpenTherm processing loop cancelled.");
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine("OpenTherm error in processing loop");
                
                try { await Task.Delay(5000, cancellationToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }


    // --------------------------------------------------------- //
    // Establishes connection to ESP and waits for ready signal  //
    // --------------------------------------------------------- //
    private async Task EspConnectAsync(CancellationToken cancellationToken)
    {
        if (serialPort.IsOpen)
            return;

        try
        {
            serialPort.Open();
            Console.WriteLine("SerialPort opened successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Failed to open SerialPort:");
            Console.WriteLine(ex);
        }
        _streamReader = new StreamReader(serialPort.BaseStream, Encoding.UTF8, true, 1024, leaveOpen: true);
        
        Console.WriteLine("Serial port opened. Waiting for ESP to reset...");
        await Task.Delay(8000, cancellationToken);
    }

    private async Task CloseSerialPortAsync()
    {
        if (serialPort.IsOpen)
        {
            _streamReader?.Dispose();
            _streamReader = null;
            serialPort.Close();
        }
    }

    // -------------------------------------------------------- //
    // Queries OpenTherm configs and sends them as JSON to ESP  //
    // -------------------------------------------------------- //
    public async Task SendConfigToEspAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        await _serialLock.WaitAsync(cancellationToken);
        try
        {
            if (!serialPort.IsOpen)
                throw new InvalidOperationException("Serial port is not open. Call EspConnectAsync() first.");

            var configs = await repo.Query<OpenTherm>().ToListAsync(cancellationToken);

            if (configs.Count == 0)
            {
                Console.WriteLine("No configuration found in the database. Sending empty config.");
                return;
            }

            var valuesDict = configs.ToDictionary(
                c => c.Id.ToString(),
                c => new { interval = c.IntervalSec, threshold = c.Threshold }
            );

            var message = new { ID = "config", values = valuesDict };
            string json = JsonSerializer.Serialize(message, _jsonOptions) + "\n";
            var buffer = Encoding.UTF8.GetBytes(json);
        
            await serialPort.BaseStream.WriteAsync(buffer, 0, buffer.Length, cancellationToken);
            await serialPort.BaseStream.FlushAsync(cancellationToken);
        
            Console.WriteLine($"Sent config for {configs.Count} parameters"); 
        }
        finally
        {
            _serialLock.Release();
        }
    }

    // ----------------------------------------------- //
    // Listens continuously for JSON messages from ESP //
    // ----------------------------------------------- //
    public async IAsyncEnumerable<IOpenThermService.IncomingMessage> ListenForIncomingMessagesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        
        if (!serialPort.IsOpen || _streamReader == null)
            throw new InvalidOperationException("Serial port is not open. Call EspConnectAsync() first.");

        while (!cancellationToken.IsCancellationRequested)
        {
            string? line = null;
            IOpenThermService.IncomingMessage? message;

            try
            {
                line = await _streamReader.ReadLineAsync().WaitAsync(cancellationToken);
                if (line == null) 
                {
                    Console.WriteLine("End of stream reached");
                    yield break;
                }

                message = JsonSerializer.Deserialize<IOpenThermService.IncomingMessage>(line, _jsonOptions);
            }
            catch (OperationCanceledException) { yield break; }
            catch (JsonException ex)
            {
                Console.WriteLine("JSON deserialization failed. Raw line: {Line}", line);
                continue;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Serial communication error");
                
                try { await Task.Delay(1000, cancellationToken); }
                catch (OperationCanceledException) { yield break; }
                
                continue;
            }

            if (message?.Id != null)
                yield return message;
            else
                Console.WriteLine("Malformed message (missing ID): {Line}", line);
        }
    }

    // ----------------------------------------------- //
    // Sends a parameter update as ID/VALUE JSON pair  //
    // ----------------------------------------------- //
    public async Task SendParameterAsync(string id, object value, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"DEBUG: SendParameterAsync called with ID={id}, VALUE={value}");

        ObjectDisposedException.ThrowIf(_isDisposed, this);
        Console.WriteLine("DEBUG: Checked object disposal.");

        await _serialLock.WaitAsync(cancellationToken);
        Console.WriteLine("DEBUG: Acquired serial lock.");

        try
        {
            if (!serialPort.IsOpen)
            {
                Console.WriteLine("DEBUG: Serial port is not open!");
                throw new InvalidOperationException("Serial port is not open. Call EspConnectAsync() first.");
            }
            Console.WriteLine("DEBUG: Serial port is open.");

            var message = new IdValueMessage(id, value);
            string json = JsonSerializer.Serialize(message, _jsonOptions) + "\n";
            Console.WriteLine($"DEBUG: Serialized message: {json.Trim()}");

            var buffer = Encoding.UTF8.GetBytes(json);
            await serialPort.BaseStream.WriteAsync(buffer, 0, buffer.Length, cancellationToken);
            Console.WriteLine("DEBUG: Written message to serial port.");

            await serialPort.BaseStream.FlushAsync(cancellationToken);
            Console.WriteLine("DEBUG: Flushed serial port buffer.");

            Console.WriteLine($"DEBUG: Sent parameter successfully: ID={id}, VALUE={value}");
        }
        finally
        {
            _serialLock.Release();
            Console.WriteLine("DEBUG: Released serial lock.");
        }
    }


    // -------------------------------------------------------- //
    // Upserts an OpenTherm configuration entry in the database //
    // -------------------------------------------------------- //
    public async Task UpdateOrCreateConfigAsync(int id, int intervalSec, float threshold, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        
        var existingConfig = await repo.Query<OpenTherm>()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

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


    public class IdValueMessage
    {
        [JsonPropertyName("ID")]
        public string ID { get; set; }

        [JsonPropertyName("value")]
        public object Value { get; set; }

        public IdValueMessage(string id, object value)
        {
            ID = id;
            Value = value;
        }
    }
    
    public void Dispose()
    {
        if (_isDisposed) return;
    
        _isDisposed = true;  // Set flag first
        _cts.Cancel();
    
        try { _processingTask?.Wait(TimeSpan.FromSeconds(5)); } catch { /* ignore */ }
    
        _streamReader?.Dispose();
        if (serialPort.IsOpen) serialPort.Close();
        serialPort.Dispose();
    
        _cts.Dispose();
        _serialLock.Dispose();
    }
}
