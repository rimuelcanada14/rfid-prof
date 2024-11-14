using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;

public class AdminOnlyAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var role = context.HttpContext.Session.GetString("Role");

        if (!string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase)) // Case-insensitive check
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
        if (role == "student")
        {
            return RedirectToAction("Welcome");
        }
        else if (role == "admin")
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

        // Use FirstOrDefault to avoid the exception when there are multiple users with the same name
        var user = _context.Users.FirstOrDefault(u => u.Name == userName);
        if (user == null)
        {
            TempData["ErrorMessage"] = "User not found. Please try logging in again.";
            return RedirectToAction("Input", "Account");
        }

        // Set additional user information in session for quick access
        HttpContext.Session.SetString("StudentId", user.StudentNumber);
        HttpContext.Session.SetString("Course", user.Course);
        HttpContext.Session.SetString("Email", user.Email);
        HttpContext.Session.SetInt32("UserId", user.UserId);
        HttpContext.Session.SetString("ContactNumber", user.ContactNumber);

        // Handle nullable DateIssued by checking if it has a value
        if (user.DateIssued.HasValue)
        {
            HttpContext.Session.SetString("DateIssued", user.DateIssued.Value.ToString("yyyy-MM-dd"));
        }
        else
        {
            // Optionally set a default value or handle the case where DateIssued is null
            HttpContext.Session.SetString("DateIssued", string.Empty); // Or another appropriate default
        }

        // Pass user's name to the view
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
    [HttpPost]
    public async Task<IActionResult> AddNewBook(IFormFile bookCover, string title, string author, string rfid, string publisher)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(author) || string.IsNullOrWhiteSpace(rfid))
        {
            TempData["ErrorMessage"] = "All fields are required.";
            return RedirectToAction("AdminBookManagement");
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

        // Set success message and redirect back to the book management page
        TempData["SuccessMessage"] = "New book added successfully!";
        return RedirectToAction("AdminBookManagement");
    }
    public IActionResult AdminEditBook(int id)
    {
        // Fetch the book by ID from the database
        var book = _context.Books.Find(id);

        if (book == null)
        {
            TempData["ErrorMessage"] = "Book not found.";
            return RedirectToAction("AdminBookManagement");
        }

        return View(book); // Pass the book model to the edit view
    }

    [HttpPost]
    public IActionResult UpdateBook([Bind("BookId,Title,Author,Publisher")] Book model)
    {
        if (ModelState.IsValid)
        {
            var book = _context.Books.Find(model.BookId);
            if (book != null)
            {
                book.Title = model.Title;
                book.Author = model.Author;
                book.Publisher = model.Publisher;

                _context.SaveChanges();
                TempData["SuccessMessage"] = "Book updated successfully.";
                return RedirectToAction("AdminBookManagement");
            }
            else
            {
                TempData["ErrorMessage"] = "Book not found.";
            }
        }
        else
        {
            TempData["ErrorMessage"] = "An error occurred. Please check the entered values.";
        }

        return View("AdminEditBook", model);
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
            // Redirect to Input page if user is not logged in
            return RedirectToAction("Input", "Account");
        }

        // Retrieve books from the database
        var books = _context.Books.ToList(); // Gets all books from the "Books" table

        ViewBag.UserName = userName;
        return View(books); // Pass books as the model to the view
    }

    public IActionResult ScannedBorrow(string rfid)
    {
        if (string.IsNullOrEmpty(rfid))
        {
            ViewBag.ErrorMessage = "RFID not provided";
            return View("Welcome");
        }

        var book = _context.Books.FirstOrDefault(b => b.BookRFID == rfid);
        if (book != null)
        {
            return View("ScannedBorrow", book); // Pass a single Book object
        }
        else
        {
            ViewBag.ErrorMessage = "No book found. Please try again";
            return View("Welcome");
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
            TempData["ErrorMessage"] = "User not found. Please try logging in again.";
            return RedirectToAction("Input", "Account");
        }

        var book = _context.Books.FirstOrDefault(b => b.BookId == bookId);
        if (book == null || book.Availability == "Borrowed")
        {
            TempData["ErrorMessage"] = "This book is already borrowed.";
            return RedirectToAction("ScannedBorrow", new { rfid = book?.BookRFID });
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
        book.Availability = "Borrowed"; // Mark the book as borrowed

        // Explicitly mark the book as modified to ensure Entity Framework tracks it
        _context.Books.Update(book);

        // Add the borrowed book entry to BorrowedBooks table
        _context.BorrowedBooks.Add(borrowedBook);

        // Save changes to the database
        _context.SaveChanges();

        TempData["SuccessMessage"] = "Book successfully registered as borrowed."; // Set success message
        return RedirectToAction("ScannedBorrow", new { rfid = book.BookRFID });
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

        // Set user data in ViewBag or ViewModel for display on the profile page
        ViewBag.User = user;
        return View();
    }

    [HttpGet]
    public IActionResult EditProfile()
    {
        var userName = HttpContext.Session.GetString("UserName");
        var studentId = HttpContext.Session.GetString("StudentId");
        var course = HttpContext.Session.GetString("Course");
        var Email = HttpContext.Session.GetString("Email");
        var contactNumber = HttpContext.Session.GetString("ContactNumber");

        if (string.IsNullOrEmpty(userName))
        {
            return RedirectToAction("Index", "Home"); // Redirect if user is not logged in
        }

        ViewBag.UserName = userName;
        ViewBag.StudentId = studentId;
        ViewBag.Course = course;
        ViewBag.Email = Email;
        ViewBag.ContactNumber = contactNumber;

        return View();
    }

    [HttpPost]
    public IActionResult EditProfile([FromBody] User updatedUser)
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
                u.StudentNumber == updatedUser.StudentNumber ||
                u.Email == updatedUser.Email ||
                u.ContactNumber == updatedUser.ContactNumber
            ));

        if (isDuplicate)
        {
            return BadRequest(new { message = "Invalid credentials. User already exists." });
        }

        // Update user properties
        user.StudentNumber = updatedUser.StudentNumber;
        user.Course = updatedUser.Course;
        user.Email = updatedUser.Email;
        user.ContactNumber = updatedUser.ContactNumber;

        try
        {
            _context.SaveChanges();

            // Update session data
            HttpContext.Session.SetString("StudentId", user.StudentNumber);
            HttpContext.Session.SetString("Course", user.Course);
            HttpContext.Session.SetString("Email", user.Email);
            HttpContext.Session.SetString("ContactNumber", user.ContactNumber);

            return Ok();
        }
        catch (Exception ex)
        {
            // Log the exception if you have logging configured
            return StatusCode(500, "An error occurred while updating the profile.");
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
}
