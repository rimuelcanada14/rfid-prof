using System.ComponentModel.DataAnnotations;

public class User
{
    public int UserId { get; set; }

    [Required]
    public string Name { get; set; }

    [Required]
    public string? StudentNumber { get; set; }


    [Required]
    public string Course { get; set; }

    [Required]
    [EmailAddress]
    public string Email { get; set; }

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; }

    [Required]
    public string rfid { get; set; } // RFID property

    [Required]
    public string role { get; set; } = "student"; // Role property with default value

    public DateTime? DateIssued { get; set; }

    [Required]
    public string ContactNumber { get; set; } 
}