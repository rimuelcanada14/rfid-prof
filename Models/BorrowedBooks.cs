public class BorrowedBooks
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int BookId { get; set; }
    public DateTimeOffset DateBorrowed { get; set; }
    public DateTimeOffset DLBorrow { get; set; }

}
