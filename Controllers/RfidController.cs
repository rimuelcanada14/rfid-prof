using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

public class RFIDController : Controller
{
    private readonly IRFIDService _rfidService;

    public RFIDController(IRFIDService rfidService)
    {
        _rfidService = rfidService;
    }

    [HttpGet]
    [Route("api/rfid/read")]
    public async Task<IActionResult> ReadRFID()
    {
        try
        {
            var rfidValue = await _rfidService.ReadRFIDAsync();
            return Json(new { success = true, rfidValue });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
}