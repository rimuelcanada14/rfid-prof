using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

public class MockRFIDService : IRFIDService
{
    private readonly ILogger<MockRFIDService> _logger;
    private readonly Random _random = new Random();

    public MockRFIDService(ILogger<MockRFIDService> logger)
    {
        _logger = logger;
    }

    public async Task<string> ReadRFIDAsync()
    {
        // Simulate some processing time
        await Task.Delay(1000);

        // Generate a random RFID number (8 bytes)
        byte[] buffer = new byte[8];
        _random.NextBytes(buffer);
        string rfidValue = BitConverter.ToString(buffer).Replace("-", "");

        _logger.LogInformation($"Mock RFID read: {rfidValue}");
        return rfidValue;
    }
}