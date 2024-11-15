public class UserDeletionViewModel
{
    public User User { get; set; }
    public ICollection<BorrowedBooks> BorrowedBooks { get; set; }
    public ICollection<ReturnedBook> ReturnedBooks { get; set; }
}
