using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

public class User
{
    public int UserId { get; set; }

    [Required]
    public string Name { get; set; }

    [Required]
    public string StudentNumber { get; set; }

    [Required]
    public string Course { get; set; }

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address")]
    [RegularExpression(@"^[^@\s]+@plm\.edu\.ph$", ErrorMessage = "Only PLM email addresses are allowed")]
    public string Email { get; set; }

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; }

    [Required]
    public string rfid { get; set; } // RFID property

    [Required]
    public string role { get; set; } = "Student"; // Role property with default value

    public DateTime? DateIssued { get; set; }

    [Required]
    public string ContactNumber { get; set; } = "NA";
    public string? ProfilePicUrl { get; set; }

    // Initialize collections to avoid null reference issues
    public virtual ICollection<BorrowedBooks> BorrowedBooks { get; set; } = new List<BorrowedBooks>();
    public virtual ICollection<ReturnedBook> ReturnedBooks { get; set; } = new List<ReturnedBook>();
}
