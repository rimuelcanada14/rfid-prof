using System.ComponentModel.DataAnnotations;

public class Book
{
    public int BookId { get; set; }  // Primary key

    [Required]
    public string Title { get; set; }

    [Required]
    public string Author { get; set; }

    public string Publisher { get; set; }

    [Required]
    public string Availability { get; set; } // e.g., "Available" or "Borrowed"

    public int TimesBorrowed { get; set; }
    public int TimesReturned { get; set; }

    [Required]
    public string BookRFID { get; set; }

    public string BookCoverUrl { get; set; }
}

