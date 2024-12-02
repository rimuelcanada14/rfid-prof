using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Mail;
using System.IO;
using UserAuthApp.Migrations;


public class AdminOnlyAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var role = context.HttpContext.Session.GetString("Role");

        if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)) // Case-insensitive check
        {
            context.Result = new RedirectToActionResult("Welcome", "Account", null);
        }

        base.OnActionExecuting(context);
    }
}



public class AccountController : Controller
{
    private readonly UserDbContext _context;

    public AccountController(UserDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public IActionResult Input() => View();

    [HttpGet]
    public IActionResult Signup() => View();

    [HttpPost]
    public IActionResult Signup(User user)
    {
        // Check if StudentNumber, Email, or RFID already exists in the database
        bool userExists = _context.Users.Any(u => u.StudentNumber == user.StudentNumber 
                                                || u.Email == user.Email 
                                                || u.rfid == user.rfid);
        if (userExists)
        {
            TempData["UserAlreadyExists"] = true; // Set TempData to trigger the popup
            return View(user); // Return the view without redirecting
        }

        if (ModelState.IsValid)
        {
            var passwordHasher = new PasswordHasher<User>();
            user.Password = passwordHasher.HashPassword(user, user.Password);

            // Set the DateIssued to the current UTC date and time
            user.DateIssued = DateTime.UtcNow;

            user.BorrowedBooks = null;
            user.ReturnedBooks = null;

            _context.Users.Add(user);
            _context.SaveChanges();

            TempData["RegistrationSuccess"] = true; // Set TempData flag for success
            return RedirectToAction("Signup"); // Redirect back to the Signup view
        }
        
        return View(user);
    }

    [HttpGet]
    public IActionResult Login() => View(); // Return the login view

    [HttpPost]
    public IActionResult Login(string loginMethod, string email, string password, string rfid)
    {
        User user = null;

        if (loginMethod == "manual")
        {
            // Manual login logic
            user = _context.Users.SingleOrDefault(u => u.Email == email);
            if (user != null)
            {
                var passwordHasher = new PasswordHasher<User>();
                var result = passwordHasher.VerifyHashedPassword(user, user.Password, password);
                if (result == PasswordVerificationResult.Success)
                {
                    // Store user's name in session
                    HttpContext.Session.SetString("UserName", user.Name);
                    HttpContext.Session.SetString("Role", user.role);
                    return RedirectBasedOnRole(user.role); // Redirect based on role
                }
            }
            else
            {
                TempData["UserNotFound"] = true; // Set flag to show popup
                return RedirectToAction("Login", "Account");
            }
        }
        else if (loginMethod == "rfid")
        {
            // RFID login logic
            user = _context.Users.SingleOrDefault(u => u.rfid == rfid);
            if (user != null)
            {
                // Store user's name in session
                HttpContext.Session.SetString("UserName", user.Name);
                HttpContext.Session.SetString("Role", user.role);
                return RedirectBasedOnRole(user.role); // Redirect based on role
            }
            else
            {
                TempData["UserNotFound"] = true; // Set flag to show popup
                return RedirectToAction("Input", "Account");
            }
        }

        // If login failed
        ModelState.AddModelError("", "Invalid login attempt.");
        return View("Input"); // Return to the login view with error
    }

    private IActionResult RedirectBasedOnRole(string role)
    {
        if (role == "Student")
        {
            return RedirectToAction("Welcome");
        }
        else if (role == "Admin")
        {
            return RedirectToAction("AdminPanel");
        }

        // Default redirection if role is not matched
        return RedirectToAction("Input", "Account");
    }

    public IActionResult Welcome()
    {
        var userName = HttpContext.Session.GetString("UserName");

        if (string.IsNullOrEmpty(userName))
        {
            // Redirect to the home page if the user is not logged in
            return RedirectToAction("Index", "Home");
        }

        // Get the user details
        var user = _context.Users.FirstOrDefault(u => u.Name == userName);
        if (user == null)
        {
            TempData["ErrorMessage"] = "User not found. Please try logging in again.";
            return RedirectToAction("Input", "Account");
        }

        // Set session variables
        HttpContext.Session.SetString("Name", user.Name);
        HttpContext.Session.SetString("StudentId", user.StudentNumber);
        HttpContext.Session.SetString("Course", user.Course);
        HttpContext.Session.SetString("Email", user.Email);
        HttpContext.Session.SetInt32("UserId", user.UserId);
        HttpContext.Session.SetString("ContactNumber", user.ContactNumber);
        HttpContext.Session.SetString("ProfilePicUrl", user.ProfilePicUrl ?? "~/images/pfp.png");

        if (user.DateIssued.HasValue)
        {
            HttpContext.Session.SetString("DateIssued", user.DateIssued.Value.ToString("yyyy-MM-dd"));
        }
        else
        {
            HttpContext.Session.SetString("DateIssued", string.Empty);
        }

        // Check for overdue books using navigation properties
        var overdueBooks = _context.BorrowedBooks
            .Include(b => b.Book)
            .Where(b => b.UserId == user.UserId && b.DLBorrow < DateTime.UtcNow)
            .Select(b => new 
            {
                Title = b.Book != null ? b.Book.Title : "Unknown",
                BookCoverUrl = b.Book != null ? b.Book.BookCoverUrl : "/images/default-cover.png", // Default image if null
                DaysOverdue = (DateTime.UtcNow - b.DLBorrow).Days
            })
            .ToList();

        bool hasOverdueBooks = overdueBooks.Any();
        HttpContext.Session.SetInt32("HasOverdueBooks", hasOverdueBooks ? 1 : 0);

        
        // Pass overdue books to the view
        ViewBag.OverdueBooks = overdueBooks;

        ViewBag.UserName = userName;
        return View();
    }


    [AdminOnly]
    [HttpGet]
    public IActionResult AdminPanel()
    {
        return View(); 
    }

    [AdminOnly]
    [HttpGet]
    public IActionResult AdminBookManagement()
    {
        var userName = HttpContext.Session.GetString("UserName");

        if (string.IsNullOrEmpty(userName))
        {
            // Redirect to Input page if user is not logged in
            return RedirectToAction("Input", "Account");
        }

        // Retrieve books from the database
        var books = _context.Books.ToList(); // Gets all books from the "Books" table

        ViewBag.UserName = userName; // Set the user's name in ViewBag for display in the view
        return View(books); // Pass books as the model to the view
    }
    [AdminOnly]
    [HttpPost]
    public IActionResult DeleteBook(int id)
    {
        var book = _context.Books.Find(id);
        
        if (book == null)
        {
            return NotFound();
        }

        _context.Books.Remove(book);
        _context.SaveChanges();

        return RedirectToAction("AdminBookManagement");
    }

    [AdminOnly]
    [HttpGet]
    public IActionResult AdminNewBook()
    {
        return View();
    }

    [AdminOnly]
    [HttpPost]
    public async Task<IActionResult> AddNewBook(IFormFile bookCover, string title, string author, string rfid, string publisher)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(author) || string.IsNullOrWhiteSpace(rfid))
        {
            TempData["ErrorMessage"] = "All fields are required.";
            TempData["ShowPopup"] = "Error";  // Show Error popup if any field is missing
            return RedirectToAction("AdminNewBook"); // Stay on the same page
        }

        // Check if a book with the same title, author, publisher, or RFID already exists
        var existingBook = await _context.Books
            .FirstOrDefaultAsync(b => b.Title == title && b.Author == author && b.Publisher == publisher || b.BookRFID == rfid);

        if (existingBook != null)
        {
            TempData["ErrorMessage"] = "Book already exists.";
            TempData["ShowPopup"] = "Error";  // Show error popup if book exists
            return RedirectToAction("AdminNewBook"); // Stay on the same page
        }

        // Define the path to save the uploaded file
        string bookCoverUrl = "/images/default-cover.jpg"; // Default cover image

        if (bookCover != null && bookCover.Length > 0)
        {
            // Save uploaded file
            var filePath = Path.Combine("wwwroot/images/covers", Path.GetFileName(bookCover.FileName));
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await bookCover.CopyToAsync(stream);
            }
            bookCoverUrl = $"/images/covers/{bookCover.FileName}";
        }

        // Create a new book record
        var newBook = new Book
        {
            Title = title,
            Author = author,
            BookRFID = rfid,
            Publisher = publisher,
            BookCoverUrl = bookCoverUrl,
            Availability = "Available", // Assuming a default value for Availability
            TimesBorrowed = 0,
            TimesReturned = 0
        };

        // Add and save the new book to the database
        _context.Books.Add(newBook);
        await _context.SaveChangesAsync();

        // Set success message and popup display
        TempData["SuccessMessage"] = "New book added successfully!";
        TempData["ShowPopup"] = "Success";  // Show Success popup
        return RedirectToAction("AdminNewBook"); // Stay on the same page to show the popup
    }



    [AdminOnly]
    public IActionResult AdminEditBook(int id)
    {
        var book = _context.Books.Find(id);
        if (book == null)
        {
            TempData["ErrorMessage"] = "Book not found.";
            return RedirectToAction("AdminBookManagement");
        }

        // Map the Book entity to BookEditViewModel
        var model = new BookEditViewModel
        {
            BookId = book.BookId,
            Title = book.Title,
            Author = book.Author,
            Publisher = book.Publisher,
            BookCoverUrl = book.BookCoverUrl
        };

        return View(model);
    }

    [AdminOnly]
    [HttpPost]
    public IActionResult UpdateBook(BookEditViewModel model)
    {
        if (ModelState.IsValid)
        {
            // Check if a book with the same Title, Author, and Publisher already exists
            var existingBook = _context.Books
                .FirstOrDefault(b => b.Title == model.Title && 
                                    b.Author == model.Author && 
                                    b.Publisher == model.Publisher &&
                                    b.BookId != model.BookId); // Exclude the current book being edited

            if (existingBook != null)
            {
                // Book with the same details exists
                TempData["ErrorMessage"] = "Book already exists.";
                TempData["ShowPopup"] = "Error";
                return RedirectToAction("AdminEditBook", new { id = model.BookId });
            }

            var book = _context.Books.Find(model.BookId);
            if (book != null)
            {
                // Update fields if the book is found
                book.Title = model.Title;
                book.Author = model.Author;
                book.Publisher = model.Publisher;

                _context.SaveChanges();
                TempData["SuccessMessage"] = "Book updated successfully.";
                TempData["ShowPopup"] = "Success";
                return RedirectToAction("AdminEditBook", new { id = model.BookId });
            }
            else
            {
                TempData["ErrorMessage"] = "Book not found.";
                TempData["ShowPopup"] = "Error";
            }
        }
        else
        {
            TempData["ErrorMessage"] = "An error occurred. Please check the entered values.";
            TempData["ShowPopup"] = "Error";
        }

        return RedirectToAction("AdminEditBook", new { id = model.BookId });
    }



    [HttpGet]
    [AdminOnly] // Ensure only admins can access this view
    public IActionResult AdminUserManagement()
    {
        var userName = HttpContext.Session.GetString("UserName");

        if (string.IsNullOrEmpty(userName))
        {
            // Redirect to Input page if user is not logged in
            return RedirectToAction("Input", "Account");
        }
        var user = _context.Users.ToList(); // Fetch all users from the database
        return View(user); // Pass the list of users to the view
    }

    [AdminOnly]
    public IActionResult AdminUserDeletion(int id)
    {
        var user = _context.Users
            .Include(u => u.ReturnedBooks)
                .ThenInclude(bb => bb.Book)  // Include the related Book for ReturnedBooks
            .Include(u => u.BorrowedBooks)
                .ThenInclude(bb => bb.Book)  // Include the related Book for BorrowedBooks
            .FirstOrDefault(u => u.UserId == id);

        if (user == null)
        {
            return NotFound();
        }

        // Pass the user and their borrowed and returned books to the view
        return View(user);
    }

    [AdminOnly]
[AdminOnly]
[HttpPost]
public IActionResult SaveUserRole(int userId, string role, string StudentNumber, string Name, string Course, string Email, string ContactNumber)
{
    if (userId == 0 || string.IsNullOrEmpty(role))
    {
        return BadRequest("Invalid userId or role.");
    }

    // Find the user in the database using the integer userId
    var user = _context.Users.FirstOrDefault(u => u.UserId == userId);

    if (user == null)
    {
        return NotFound("User not found.");
    }

    // Track if there were any changes
    bool hasChanges = false;
    string changesBody = $"Dear {user.Name},\n\nYour account details have been updated by an administrator.\n\n" +
                         "=======UPDATED INFORMATION=======\n\n";

    // Check and update fields, if necessary
    if (Name != user.Name)
    {
        user.Name = Name;
        changesBody += $"Name: {Name}\n";
        hasChanges = true;
    }
    if (StudentNumber != user.StudentNumber)
    {
        user.StudentNumber = StudentNumber;
        changesBody += $"Student Number: {StudentNumber}\n";
        hasChanges = true;
    }

    if (Course != user.Course)
    {
        user.Course = Course;
        changesBody += $"Course: {Course}\n";
        hasChanges = true;
    }

    if (Email != user.Email)
    {
        user.Email = Email;
        changesBody += $"Email: {Email}\n";
        hasChanges = true;
    }

    if (ContactNumber != user.ContactNumber)
    {
        user.ContactNumber = ContactNumber;
        changesBody += $"Contact Number: {ContactNumber}\n";
        hasChanges = true;
    }

    if (role != user.role)
    {
        user.role = role;
        changesBody += $"Role: {role}\n";
        hasChanges = true;
    }

    // Save the changes to the database
    _context.SaveChanges();

    // Only send email if there were changes
    if (hasChanges)
    {
        // Add final message and sign-off
        changesBody += "\nIf you did not make these changes, please contact the system administrator immediately.\n\n" +
                       "Best regards,\nPLM Library System";

        // Send the email notification
        try
        {
            using (var client = new SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                Credentials = new NetworkCredential("plmlibrary241@gmail.com", "qduv gkqu sitl maee"), // Replace with your credentials
                EnableSsl = true
            })
            {
                var mailMessage = new MailMessage
                {
                    From = new MailAddress("plmlibrary241@gmail.com"),
                    Subject = "Account Details Updated",
                    Body = changesBody,
                    IsBodyHtml = false
                };
                mailMessage.To.Add(user.Email); // Send email to the updated email address

                client.Send(mailMessage);
            }
        }
        catch (Exception ex)
        {
            // Log the exception if email fails to send
            Console.WriteLine($"Email sending failed: {ex.Message}");
        }
    }

    // Store success message in TempData
    TempData["SuccessMessage"] = "The user has been notified with the changes.";

    // Redirect to AdminUserManagement
    return RedirectToAction("AdminUserDeletion", "Account", new { id = user.UserId });
}






    public IActionResult DeleteUser(int id)
    {
        var user = _context.Users.FirstOrDefault(u => u.UserId == id);
        if (user == null)
        {
            return NotFound();
        }

        _context.Users.Remove(user);
        _context.SaveChanges();
        
        return RedirectToAction("AdminUserManagement"); // Redirect back to the user management page
    }


    [HttpGet]
    [AdminOnly] // Ensure only admins can access this view
    public IActionResult AdminBookStat()
    {
        var userName = HttpContext.Session.GetString("UserName");

        if (string.IsNullOrEmpty(userName))
        {
            // Redirect to Input page if user is not logged in
            return RedirectToAction("Input", "Account");
        }

        // Retrieve books from the database
        var books = _context.Books.ToList(); // Gets all books from the "Books" table

        ViewBag.UserName = userName; // Set the user's name in ViewBag for display in the view
        return View(books); // Pass books as the model to the view
    }
    
    
    public IActionResult Return()
    {
        var userName = HttpContext.Session.GetString("UserName");

        if (string.IsNullOrEmpty(userName))
        {
            // Redirect to Input page if the user is not logged in
            return RedirectToAction("Input", "Account");
        }

        // Retrieve the user ID based on the username
        var user = _context.Users.FirstOrDefault(u => u.Name == userName);
        if (user == null)
        {
            // Handle case where the user is not found
            return RedirectToAction("Input", "Account");
        }

        var userId = user.UserId;

        // Retrieve books borrowed by the logged-in user by joining with BorrowedBooks and Books
        var borrowedBooks = _context.BorrowedBooks
            .Where(b => b.UserId == userId)
            .Join(
                _context.Books,
                borrowedBook => borrowedBook.BookId,
                book => book.BookId,
                (borrowedBook, book) => new BorrowedBookViewModel
                {
                    BookCoverUrl = book.BookCoverUrl,
                    Title = book.Title,
                    Author = book.Author,
                    DateBorrowed = borrowedBook.DateBorrowed,
                    DLBorrow = borrowedBook.DLBorrow,
                    BookId = book.BookId
                }
            )
            .ToList();
        var overdueBooks = _context.BorrowedBooks
            .Include(b => b.Book)
            .Where(b => b.UserId == user.UserId && b.DLBorrow < DateTime.UtcNow)
            .Select(b => new 
            {
                Title = b.Book != null ? b.Book.Title : "Unknown",
                BookCoverUrl = b.Book != null ? b.Book.BookCoverUrl : "/images/default-cover.png", // Default image if null
                DaysOverdue = (DateTime.UtcNow - b.DLBorrow).Days
            })
            .ToList();

        bool hasOverdueBooks = overdueBooks.Any();
        HttpContext.Session.SetInt32("HasOverdueBooks", hasOverdueBooks ? 1 : 0);

        
        // Pass overdue books to the view
        ViewBag.OverdueBooks = overdueBooks;

        ViewBag.UserName = userName;

        return View(borrowedBooks); // Pass the borrowed books with additional data to the view
    }

    [HttpPost]
    public IActionResult ReturnBook(int bookId, string rfid)
    {
        var userName = HttpContext.Session.GetString("UserName");

        if (string.IsNullOrEmpty(userName))
        {
            TempData["ReturnError"] = "User not logged in.";
            return RedirectToAction("Return");
        }

        var user = _context.Users.FirstOrDefault(u => u.Name == userName);
        if (user == null)
        {
            TempData["ReturnError"] = "User not found.";
            return RedirectToAction("Return");
        }

        var book = _context.Books.FirstOrDefault(b => b.BookId == bookId);
        if (book == null)
        {
            TempData["ReturnError"] = "Book not found.";
            return RedirectToAction("Return");
        }

        // Ensure the RFID match is case-insensitive and trim any extra spaces
        if (string.Compare(book.BookRFID.Trim(), rfid.Trim(), StringComparison.OrdinalIgnoreCase) != 0)
        {
            TempData["ReturnError"] = "Book RFID does not match.";
            return RedirectToAction("Return");
        }

        // Check if the book exists in the BorrowedBooks table for this user
        var borrowedBook = _context.BorrowedBooks
            .FirstOrDefault(b => b.UserId == user.UserId && b.BookId == book.BookId);

        if (borrowedBook != null)
        {
            // Proceed with returning the book
            book.TimesReturned += 1;
            book.Availability = "Available";

            // Create an entry in the ReturnedBooks table
            var returnedBook = new ReturnedBook
            {
                BookId = book.BookId,
                UserId = user.UserId,
                DateBorrowed = borrowedBook.DateBorrowed.ToUniversalTime(), // Convert to UTC
                DateReturned = DateTime.UtcNow // Convert to UTC
            };

            _context.ReturnedBooks.Add(returnedBook);

            // Remove the borrowed book record
            _context.BorrowedBooks.Remove(borrowedBook);
            _context.Books.Update(book);
            _context.SaveChanges();

            TempData["ReturnSuccess"] = true;
            return RedirectToAction("Return");
        }
        else
        {
            TempData["ReturnError"] = "This book hasn't been borrowed by you.";
            return RedirectToAction("Return");
        }
    }





    public IActionResult Search()
    {
        var userName = HttpContext.Session.GetString("UserName");

        if (string.IsNullOrEmpty(userName))
        {
            // Redirect to Input page if the user is not logged in
            return RedirectToAction("Input", "Account");
        }

        // Retrieve the user ID based on the username
        var user = _context.Users.FirstOrDefault(u => u.Name == userName);
        if (user == null)
        {
            // Handle case where the user is not found
            return RedirectToAction("Input", "Account");
        }

        var userId = user.UserId;

        if (string.IsNullOrEmpty(userName))
        {
            // Redirect to Input page if user is not logged in
            return RedirectToAction("Input", "Account");
        }

        // Retrieve books from the database
        var books = _context.Books.ToList(); // Gets all books from the "Books" table

        var overdueBooks = _context.BorrowedBooks
            .Include(b => b.Book)
            .Where(b => b.UserId == user.UserId && b.DLBorrow < DateTime.UtcNow)
            .Select(b => new 
            {
                Title = b.Book != null ? b.Book.Title : "Unknown",
                BookCoverUrl = b.Book != null ? b.Book.BookCoverUrl : "/images/default-cover.png", // Default image if null
                DaysOverdue = (DateTime.UtcNow - b.DLBorrow).Days
            })
            .ToList();

        bool hasOverdueBooks = overdueBooks.Any();
        HttpContext.Session.SetInt32("HasOverdueBooks", hasOverdueBooks ? 1 : 0);

        
        // Pass overdue books to the view
        ViewBag.OverdueBooks = overdueBooks;

        ViewBag.UserName = userName;
        return View(books); // Pass books as the model to the view
    }

    public IActionResult ScannedBorrow(string rfid)
{
    if (string.IsNullOrEmpty(rfid))
    {
        return RedirectToAction("Return", "Account");
    }

    var book = _context.Books.FirstOrDefault(b => b.BookRFID == rfid);
    if (book != null)
    {
        return View("ScannedBorrow", book); // Pass a single Book object
    }
    else
    {
        ViewBag.ErrorMessage = "No book found. Please try again."; // Set the error message
        return View("Welcome"); // Redirect to the Welcome page
    }
}


    [HttpPost]
    public IActionResult RegisterBorrow(int bookId)
    {
        var userName = HttpContext.Session.GetString("UserName");

        if (string.IsNullOrEmpty(userName))
        {
            // Redirect to login if user is not logged in
            return RedirectToAction("Input", "Account");
        }

        var user = _context.Users.FirstOrDefault(u => u.Name == userName);
        if (user == null)
        {
            return RedirectToAction("Input", "Account");
        }

        var book = _context.Books.FirstOrDefault(b => b.BookId == bookId);
        if (book == null || book.Availability == "Borrowed")
        {
            return RedirectToAction("Return", "Account");
        }

        // Add an entry in BorrowedBooks
        var borrowedBook = new BorrowedBooks
        {
            UserId = user.UserId,
            BookId = book.BookId,
            DateBorrowed = DateTime.UtcNow,
            DLBorrow = DateTime.UtcNow.AddDays(20) // Deadline 20 days from now
        };

        // Update TimesBorrowed and Availability of the book
        book.TimesBorrowed += 1;
        book.Availability = "Borrowed";

        // Explicitly mark the book as modified to ensure Entity Framework tracks it
        _context.Books.Update(book);

        // Add the borrowed book entry to BorrowedBooks table
        _context.BorrowedBooks.Add(borrowedBook);

        // Save changes to the database
        _context.SaveChanges();

        return RedirectToAction("Return", "Account");
    }


    public IActionResult Profile()
    {
        var userName = HttpContext.Session.GetString("UserName");

        if (string.IsNullOrEmpty(userName))
        {
            // Redirect to the home page if the user is not logged in
            return RedirectToAction("Index", "Home");
        }

        // Use FirstOrDefault to avoid the exception when there are multiple users with the same name
        var user = _context.Users.FirstOrDefault(u => u.Name == userName);
        if (user == null)
        {
            TempData["ErrorMessage"] = "User not found. Please try logging in again.";
            return RedirectToAction("Input", "Account");
        }

        var overdueBooks = _context.BorrowedBooks
            .Include(b => b.Book)
            .Where(b => b.UserId == user.UserId && b.DLBorrow < DateTime.UtcNow)
            .Select(b => new 
            {
                Title = b.Book != null ? b.Book.Title : "Unknown",
                BookCoverUrl = b.Book != null ? b.Book.BookCoverUrl : "/images/default-cover.png", // Default image if null
                DaysOverdue = (DateTime.UtcNow - b.DLBorrow).Days
            })
            .ToList();

        bool hasOverdueBooks = overdueBooks.Any();
        HttpContext.Session.SetInt32("HasOverdueBooks", hasOverdueBooks ? 1 : 0);

        
        // Pass overdue books to the view
        ViewBag.OverdueBooks = overdueBooks;

        ViewBag.UserName = userName;

        // Set user data in ViewBag or ViewModel for display on the profile page
        ViewBag.User = user;
        return View();
    }

    [HttpGet]
    public IActionResult EditProfile()
    {
        var userName = HttpContext.Session.GetString("UserName");
        var name = HttpContext.Session.GetString("Name");
        var studentId = HttpContext.Session.GetString("StudentId");
        var course = HttpContext.Session.GetString("Course");
        var email = HttpContext.Session.GetString("Email");
        var contactNumber = HttpContext.Session.GetString("ContactNumber");

        if (string.IsNullOrEmpty(userName))
        {
            return RedirectToAction("Index", "Home"); // Redirect if user is not logged in
        }

        var user = _context.Users.FirstOrDefault(u => u.Name == userName);
        if (user != null)
        {
            ViewBag.UserName = userName;
            ViewBag.Name = name;
            ViewBag.StudentId = studentId;
            ViewBag.Course = course;
            ViewBag.Email = email;
            ViewBag.ContactNumber = contactNumber;
            ViewBag.ProfilePicUrl = user.ProfilePicUrl ?? "/images/pfp.png"; // Pass the user's current profile pic URL, or default to "/images/pfp.png"
        }

        return View();
    }



    public async Task<IActionResult> EditProfile(IFormFile profilePicture, string Name, string StudentNumber, string Course, string Email, string ContactNumber, string rfid)
    {
        var userName = HttpContext.Session.GetString("UserName");

        if (string.IsNullOrEmpty(userName))
        {
            return RedirectToAction("Input", "Account");
        }

        // Fetch the existing user from the database
        var user = _context.Users.FirstOrDefault(u => u.Name == userName);
        if (user == null)
        {
            return NotFound();
        }

        // Check for duplicates (excluding the current user)
        var isDuplicate = _context.Users
            .Any(u => u.UserId != user.UserId && (
                u.StudentNumber == StudentNumber ||
                u.Email == Email ||
                u.ContactNumber == ContactNumber ||
                u.rfid == rfid
            ));

        if (isDuplicate)
        {
            return BadRequest(new { message = "Invalid credentials. User already exists." });
        }

        try
        {
            // Track if any information (excluding profile picture) has changed
            bool hasChanges = false;
            string changesBody = $"Request for Editing Information\n\nUser Details:\nName: {userName}\nRFID: {user.rfid}\n\n ====INFORMATION TO BE CHANGED=======\n";

            // Handle profile picture upload separately and immediately
            if (profilePicture != null && profilePicture.Length > 0)
            {
                // Create the profile directory if it doesn't exist
                var profileDirectory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "profile");
                if (!Directory.Exists(profileDirectory))
                {
                    Directory.CreateDirectory(profileDirectory);
                }

                // Generate a unique file name
                var fileName = $"{Guid.NewGuid().ToString()}{Path.GetExtension(profilePicture.FileName)}";
                var filePath = Path.Combine(profileDirectory, fileName);

                // Save the file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await profilePicture.CopyToAsync(stream);
                }

                // Update the profile picture URL
                user.ProfilePicUrl = $"/images/profile/{fileName}";
                HttpContext.Session.SetString("ProfilePicUrl", user.ProfilePicUrl);
                _context.SaveChanges();

            }

            // Check and track changes for each field
            if (Name != user.Name)
            {
                changesBody += $"\nCurrent Name: {user.Name}\n";
                changesBody += $"Requested Name: {Name}\n";
                hasChanges = true;
            }

            if (StudentNumber != user.StudentNumber)
            {
                changesBody += $"\nCurrent Student Number: {user.StudentNumber}\n";
                changesBody += $"Requested Student Number: {StudentNumber}\n";
                hasChanges = true;
            }

            if (Course != user.Course)
            {
                changesBody += $"\nCurrent Course: {user.Course}\n";
                changesBody += $"Requested Course: {Course}\n";
                hasChanges = true;
            }

            if (Email != user.Email)
            {
                changesBody += $"\nCurrent Email: {user.Email}\n";
                changesBody += $"Requested Email: {Email}\n";
                hasChanges = true;
            }

            if (ContactNumber != user.ContactNumber)
            {
                changesBody += $"\nCurrent Contact Number: {user.ContactNumber}\n";
                changesBody += $"Requested Contact Number: {ContactNumber}\n";
                hasChanges = true;
            }

            // Only send email if there are changes to information (not profile picture)
            if (hasChanges)
            {
                // Configure SMTP Client for Gmail
                using (var client = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential("plmlibrary241@gmail.com", "qduv gkqu sitl maee"),
                    EnableSsl = true
                })
                {
                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress("plmlibrary241@gmail.com"),
                        Subject = "Request for Editing Information",
                        Body = changesBody,
                        IsBodyHtml = false
                    };
                    mailMessage.To.Add("rscanada2021@plm.edu.ph");

                    await client.SendMailAsync(mailMessage);
                }
            }

            // Return success response
            return Ok(new { 
                message = hasChanges ? "Request sent" : "Profile picture updated", 
                profilePicUrl = user.ProfilePicUrl 
            });
        }
        catch (Exception ex)
        {
            // Log the exception
            return StatusCode(500, new { message = "An error occurred while processing the request: " + ex.Message });
        }
    }


    [HttpGet]
    public IActionResult LibraryCard()
    {
        var userName = HttpContext.Session.GetString("UserName");

        if (string.IsNullOrEmpty(userName))
        {
            // Redirect to the home page if the user is not logged in
            return RedirectToAction("Index", "Home");
        }

        // Use FirstOrDefault to avoid the exception when there are multiple users with the same name
        var user = _context.Users.FirstOrDefault(u => u.Name == userName);
        if (user == null)
        {
            TempData["ErrorMessage"] = "User not found. Please try logging in again.";
            return RedirectToAction("Input", "Account");
        }

        // Set user data in ViewBag or ViewModel for display on the profile page
        ViewBag.User = user;
        return View();
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Remove("UserName");
        return RedirectToAction("Index", "Home"); // Redirect to home page after logout
    }
    [HttpPost]
    public async Task<IActionResult> NewBookInformation(Book model, IFormFile bookCover)
    {
        if (ModelState.IsValid)
        {
            // Process the uploaded image
            if (bookCover != null)
            {
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images", bookCover.FileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await bookCover.CopyToAsync(stream);
                }
                model.BookCoverUrl = $"/images/{bookCover.FileName}";
            }

            // Add the new book to the database (example using a DbContext)
            _context.Books.Add(model);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index", "AdminBookManagement"); // Redirect after successful add
        }

        return View(model); // Return to the form with validation errors
    }

    [AdminOnly]
    [HttpPost]
    public async Task<IActionResult> DownloadBookList()
    {
    // Get the current user's name
    var userName = HttpContext.Session.GetString("UserName");

    if (string.IsNullOrEmpty(userName))
    {
        return RedirectToAction("Input", "Account");
    }

    // Retrieve books from the database, ordered by times borrowed
    var books = _context.Books
        .OrderByDescending(b => b.TimesBorrowed)
        .ToList();

    // You can customize this further if needed
    ViewBag.UserName = userName;
    return View("BookListDownload", books);
}
}
