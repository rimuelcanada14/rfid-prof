using System;
using System.IO.Ports;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public class RFIDService : IRFIDService, IDisposable
{
    private readonly SerialPort _serialPort;
    private readonly ILogger<RFIDService> _logger;
    private bool _disposed = false;

    public RFIDService(IConfiguration configuration, ILogger<RFIDService> logger)
    {
        _logger = logger;

        // Get COM port settings from configuration or use defaults
        string portName = configuration.GetValue<string>("RFID:Settings:PortName") ?? "COM1";
        int baudRate = configuration.GetValue<int>("RFID:Settings:BaudRate", 9600);

        _serialPort = new SerialPort
        {
            PortName = portName,
            BaudRate = baudRate,
            DataBits = 8,
            Parity = Parity.None,
            StopBits = StopBits.One,
            ReadTimeout = 10000,  // 1 second timeout
            WriteTimeout = 1000
        };

        _logger.LogInformation($"Initializing RFID reader on port {portName} with baud rate {baudRate}");
    }

    public async Task<string> ReadRFIDAsync()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(RFIDService));
        }

        try
        {
            if (!_serialPort.IsOpen)
            {
                _serialPort.Open();
                _logger.LogInformation("Serial port opened successfully");
            }

            // Create a buffer to store the RFID data
            byte[] buffer = new byte[12];  // Adjust size based on your RFID reader's output
            int bytesRead = 0;

            // Read the RFID asynchronously
            await Task.Run(() =>
            {
                bytesRead = _serialPort.Read(buffer, 0, buffer.Length);
            });

            if (bytesRead > 0)
            {
                string rfidValue = BitConverter.ToString(buffer, 0, bytesRead).Replace("-", "");
                _logger.LogInformation($"RFID read successful: {rfidValue}");
                return rfidValue;
            }
            else
            {
                throw new InvalidOperationException("No RFID data received");
            }
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning(ex, "Timeout while reading RFID");
            throw new InvalidOperationException("RFID read timeout. Please try again.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading RFID");
            throw new InvalidOperationException("Failed to read RFID card.", ex);
        }
        finally
        {
            if (_serialPort.IsOpen)
            {
                _serialPort.Close();
                _logger.LogInformation("Serial port closed");
            }
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                if (_serialPort != null)
                {
                    if (_serialPort.IsOpen)
                    {
                        _serialPort.Close();
                    }
                    _serialPort.Dispose();
                }
            }
            _disposed = true;
        }
    }

    ~RFIDService()
    {
        Dispose(false);
    }
}