using System.ComponentModel.DataAnnotations;

public class BookEditViewModel
{
    public int BookId { get; set; }

    [Required]
    public string Title { get; set; }

    [Required]
    public string Author { get; set; }

    [Required]
    public string Publisher { get; set; }
}
