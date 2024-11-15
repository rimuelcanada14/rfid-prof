using System.ComponentModel.DataAnnotations;

public class Book
{
    public int BookId { get; set; }  // Primary key

    [Required]
    public string Title { get; set; }

    [Required]
    public string Author { get; set; }
    
    [Required]
    public string Publisher { get; set; }

    [Required]
    public int TimesBorrowed { get; set; } = 0;  // Default value set to 0
    public int TimesReturned { get; set; } = 0;  // Default value set to 0

    [Required]
    public string? BookRFID { get; set; }
    public string? Availability { get; set; }
    public string? BookCoverUrl { get; set; }
    
}
    