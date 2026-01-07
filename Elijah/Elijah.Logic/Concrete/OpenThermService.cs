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
using Microsoft.Extensions.Logging;
using FacilicomLogManager.Extensions;

namespace Elijah.Logic.Concrete;

// ---------------------------------------------------- //
// OpenTherm gateway communication service              //
// Manages serial connection with ESP and data exchange //
// ---------------------------------------------------- //
public class OpenThermService(
    SerialPort serialPort,
    IAzureIoTHubService azure,
    IZigbeeRepository repo,
    ILogger<OpenThermService> logger) : IOpenThermService, IHostedService
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
        logger
            .WithFacilicomContext(friendlyMessage: "OpenTherm service starten")
            .SendLogInformation("OpenThermService StartAsync");
            
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        Console.WriteLine("OpenThermService starting...");
    
      
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
        _processingTask = Task.Run(() => ProcessLoopAsync(linkedCts.Token), linkedCts.Token);
    
        
        _linkedCts = linkedCts; 
    
        logger
            .WithFacilicomContext(friendlyMessage: "OpenTherm service gestart")
            .SendLogInformation("OpenThermService starten voltooid");
        return Task.CompletedTask;
    }



    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger
            .WithFacilicomContext(friendlyMessage: "OpenTherm service stoppen")
            .SendLogInformation("OpenThermService StopAsync");

        if (_isDisposed)
        {
            logger
                .WithFacilicomContext(friendlyMessage: "Service al gedisoned")
                .SendLogInformation("StopAsync afgebroken - service al gedisoned");
            return;
        }

        Console.WriteLine("OpenThermService stopping...");
        _cts.Cancel();
        _linkedCts?.Dispose();
        
        if (_processingTask != null)
        {
            try
            {
                await Task.WhenAny(_processingTask, Task.Delay(5000, cancellationToken));
                logger
                    .WithFacilicomContext(friendlyMessage: "Processing task gestopt")
                    .SendLogInformation("Processing task gestopt");
            }
            catch (OperationCanceledException) 
            { 
                logger
                    .WithFacilicomContext(friendlyMessage: "Operatie geannuleerd")
                    .SendLogInformation("OperationCanceledException verwacht in StopAsync");
            }
        }

        await CloseSerialPortAsync();
        
        logger
            .WithFacilicomContext(friendlyMessage: "OpenTherm service gestopt")
            .SendLogInformation("OpenThermService StopAsync voltooid");
    }
    
    private async Task ProcessLoopAsync(CancellationToken cancellationToken)
    {
        logger
            .WithFacilicomContext(friendlyMessage: "OpenTherm processing loop starten")
            .SendLogInformation("ProcessLoopAsync started");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                logger
                    .WithFacilicomContext(friendlyMessage: "ESP verbinding opzetten")
                    .SendLogInformation("EspConnectAsync aanroepen");
                await EspConnectAsync(cancellationToken);
                
                logger
                    .WithFacilicomContext(friendlyMessage: "Configuratie versturen naar ESP")
                    .SendLogInformation("SendConfigToEspAsync aanroepen");
                await SendConfigToEspAsync(cancellationToken);

                await foreach (var msg in ListenForIncomingMessagesAsync(cancellationToken))
                {
                    logger
                        .WithFacilicomContext(friendlyMessage: "Telemetrie ontvangen van OpenTherm")
                        .SendLogInformation("Verzenden telemetrie - ID: {Id}, Value: {Value}", msg.Id, msg.Value);
                    await azure.SendTelemetryAsync("OpenTherm", msg.Id, msg.Value);
                }
            }
            catch (OperationCanceledException)
            {
                logger
                    .WithFacilicomContext(friendlyMessage: "Processing loop geannuleerd")
                    .SendLogInformation("OpenTherm processing loop cancelled.");
                break;
            }
            catch (Exception ex)
            {
                logger
                    .WithFacilicomContext(friendlyMessage: "Fout in processing loop")
                    .SendLogError(ex, "Fout in ProcessLoopAsync - Message: {Message}", ex.Message);
                
                try { await Task.Delay(5000, cancellationToken); }
                catch (OperationCanceledException) { break; }
            }
        }
        
        logger
            .WithFacilicomContext(friendlyMessage: "Processing loop beeindigd")
            .SendLogInformation("ProcessLoopAsync ended");
    }


    // --------------------------------------------------------- //
    // Establishes connection to ESP and waits for ready signal  //
    // --------------------------------------------------------- //
    private async Task EspConnectAsync(CancellationToken cancellationToken)
    {
        logger
            .WithFacilicomContext(friendlyMessage: "Verbinden met ESP")
            .SendLogInformation("EspConnectAsync started");

        if (serialPort.IsOpen)
        {
            logger
                .WithFacilicomContext(friendlyMessage: "Serial port al open")
                .SendLogInformation("Serial port is al open");
            return;
        }

        try
        {
            serialPort.Open();
            logger
                .WithFacilicomContext(friendlyMessage: "Serial port geopend")
                .SendLogInformation("SerialPort opened successfully");
        }
        catch (Exception ex)
        {
            logger
                .WithFacilicomContext(friendlyMessage: "Fout bij openen serial port")
                .SendLogError(ex, "Failed to open SerialPort - Message: {Message}", ex.Message);
            Console.WriteLine("Failed to open SerialPort:");
            Console.WriteLine(ex);
        }
        _streamReader = new StreamReader(serialPort.BaseStream, Encoding.UTF8, true, 1024, leaveOpen: true);
        
        logger
            .WithFacilicomContext(friendlyMessage: "Wachten op ESP reset")
            .SendLogInformation("Serial port opened. Waiting for ESP to reset...");
        await Task.Delay(8000, cancellationToken);
    }

    private async Task CloseSerialPortAsync()
    {
        logger
            .WithFacilicomContext(friendlyMessage: "Serial port sluiten")
            .SendLogInformation("CloseSerialPortAsync started");

        if (serialPort.IsOpen)
        {
            _streamReader?.Dispose();
            _streamReader = null;
            serialPort.Close();
            logger
                .WithFacilicomContext(friendlyMessage: "Serial port gesloten")
                .SendLogInformation("Serial port closed");
        }
    }

    // -------------------------------------------------------- //
    // Queries OpenTherm configs and sends them as JSON to ESP  //
    // -------------------------------------------------------- //
    public async Task SendConfigToEspAsync(CancellationToken cancellationToken = default)
    {
        logger
            .WithFacilicomContext(friendlyMessage: "Configuratie versturen naar ESP")
            .SendLogInformation("SendConfigToEspAsync started");

        ObjectDisposedException.ThrowIf(_isDisposed, this);

        await _serialLock.WaitAsync(cancellationToken);
        try
        {
            if (!serialPort.IsOpen)
            {
                logger
                    .WithFacilicomContext(friendlyMessage: "Serial port niet open")
                    .SendLogError("Serial port is not open. Call EspConnectAsync() first.");
                throw new InvalidOperationException("Serial port is not open. Call EspConnectAsync() first.");
            }

            var configs = await repo.Query<OpenTherm>().ToListAsync(cancellationToken);

            if (configs.Count == 0)
            {
                logger
                    .WithFacilicomContext(friendlyMessage: "Geen configuratie gevonden")
                    .SendLogWarning("Geen OpenTherm configuratie gevonden in database");
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
        
            logger
                .WithFacilicomContext(friendlyMessage: "Configuratie verstuurd naar ESP")
                .SendLogInformation("Config verstuurd voor {Count} parameters", configs.Count);
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
        logger
            .WithFacilicomContext(friendlyMessage: "Luisteren naar ESP berichten")
            .SendLogInformation("ListenForIncomingMessagesAsync started");

        ObjectDisposedException.ThrowIf(_isDisposed, this);
        
        if (!serialPort.IsOpen || _streamReader == null)
        {
            logger
                .WithFacilicomContext(friendlyMessage: "Serial port niet open")
                .SendLogError("Serial port is not open. Call EspConnectAsync() first.");
            throw new InvalidOperationException("Serial port is not open. Call EspConnectAsync() first.");
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            string? line = null;
            IOpenThermService.IncomingMessage? message;

            try
            {
                line = await _streamReader.ReadLineAsync().WaitAsync(cancellationToken);
                if (line == null) 
                {
                    logger
                        .WithFacilicomContext(friendlyMessage: "End of stream bereikt")
                        .SendLogInformation("End of stream reached");
                    yield break;
                }

                message = JsonSerializer.Deserialize<IOpenThermService.IncomingMessage>(line, _jsonOptions);
            }
            catch (OperationCanceledException) 
            { 
                logger
                    .WithFacilicomContext(friendlyMessage: "Operatie geannuleerd")
                    .SendLogInformation("OperationCanceledException in ListenForIncomingMessagesAsync");
                yield break; 
            }
            catch (JsonException ex)
            {
                logger
                    .WithFacilicomContext(friendlyMessage: "JSON parsing fout")
                    .SendLogError(ex, "JSON deserialization failed. Raw line: {Line}", line);
                continue;
            }
            catch (Exception ex)
            {
                logger
                    .WithFacilicomContext(friendlyMessage: "Serial communicatie fout")
                    .SendLogError(ex, "Serial communication error");
                
                try { await Task.Delay(1000, cancellationToken); }
                catch (OperationCanceledException) { yield break; }
                
                continue;
            }

            if (message?.Id != null)
            {
                logger
                    .WithFacilicomContext(friendlyMessage: "Bericht ontvangen: {Id}", message.Id)
                    .SendLogInformation("IncomingMessage ontvangen - ID: {Id}", message.Id);
                yield return message;
            }
            else
            {
                logger
                    .WithFacilicomContext(friendlyMessage: "Malformed bericht")
                    .SendLogWarning("Malformed message (missing ID): {Line}", line);
            }
        }
    }

    // ----------------------------------------------- //
    // Sends a parameter update as ID/VALUE JSON pair  //
    // ----------------------------------------------- //
    public async Task SendParameterAsync(string id, object value, CancellationToken cancellationToken = default)
    {
        logger
            .WithFacilicomContext(friendlyMessage: "Parameter versturen: {id}", id)
            .SendLogInformation("SendParameterAsync called - ID: {Id}, Value: {Value}", id, value);

        Console.WriteLine($"DEBUG: SendParameterAsync called with ID={id}, VALUE={value}");

        ObjectDisposedException.ThrowIf(_isDisposed, this);
        Console.WriteLine("DEBUG: Checked object disposal.");

        await _serialLock.WaitAsync(cancellationToken);
        Console.WriteLine("DEBUG: Acquired serial lock.");

        try
        {
            if (!serialPort.IsOpen)
            {
                logger
                    .WithFacilicomContext(friendlyMessage: "Serial port niet open")
                    .SendLogError("Serial port is not open!");
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

            logger
                .WithFacilicomContext(friendlyMessage: "Parameter succesvol verstuurd")
                .SendLogInformation("Verstuurd parameter - ID: {Id}, Value: {Value}", id, value);
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
        logger
            .WithFacilicomContext(friendlyMessage: "Config updaten/aanmaken: {id}", id.ToString())
            .SendLogInformation("UpdateOrCreateConfigAsync - ID: {Id}, Interval: {Interval}, Threshold: {Threshold}", id, intervalSec, threshold);

        ObjectDisposedException.ThrowIf(_isDisposed, this);
        
        var existingConfig = await repo.Query<OpenTherm>()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (existingConfig != null)
        {
            logger
                .WithFacilicomContext(friendlyMessage: "Bestaande config updaten")
                .SendLogInformation("Bestaande config gevonden - ID: {Id}", id);
            existingConfig.IntervalSec = intervalSec;
            existingConfig.Threshold = threshold;
        }
        else
        {
            logger
                .WithFacilicomContext(friendlyMessage: "Nieuwe config aanmaken")
                .SendLogInformation("Nieuwe config aanmaken - ID: {Id}", id);
            await repo.CreateAsync(new OpenTherm
            {
                Id = id,
                IntervalSec = intervalSec,
                Threshold = threshold
            });
        }

        await repo.SaveChangesAsync();
        
        logger
            .WithFacilicomContext(friendlyMessage: "Config opgeslagen")
            .SendLogInformation("UpdateOrCreateConfigAsync voltooid - ID: {Id}", id);
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
        if (_isDisposed) 
        {
            logger
                .WithFacilicomContext(friendlyMessage: "Service al gedisoned")
                .SendLogInformation("Dispose afgebroken - service al gedisoned");
            return;
        }

        logger
            .WithFacilicomContext(friendlyMessage: "OpenTherm service disposing")
            .SendLogInformation("OpenThermService Dispose");
    
        _isDisposed = true;  // Set flag first
        _cts.Cancel();
    
        try { _processingTask?.Wait(TimeSpan.FromSeconds(5)); } catch { /* ignore */ }
    
        _streamReader?.Dispose();
        if (serialPort.IsOpen) serialPort.Close();
        serialPort.Dispose();
    
        _cts.Dispose();
        _serialLock.Dispose();
        
        logger
            .WithFacilicomContext(friendlyMessage: "OpenTherm service gedisoned")
            .SendLogInformation("OpenThermService Dispose voltooid");
    }
}