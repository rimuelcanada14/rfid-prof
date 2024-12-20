public class BorrowedBooks
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int BookId { get; set; }
    public DateTime DateBorrowed { get; set; }
    public DateTime DLBorrow { get; set; }

    // Navigation properties
    public virtual User User { get; set; }
    public virtual Book Book { get; set; }  // This is the navigation property for Book
}