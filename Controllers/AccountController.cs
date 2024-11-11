using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using System.Linq;
using Microsoft.AspNetCore.Http;

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
        if (loginMethod == "manual")
        {
            // Manual login logic
            var user = _context.Users.SingleOrDefault(u => u.Email == email);
            if (user != null)
            {
                var passwordHasher = new PasswordHasher<User>();
                var result = passwordHasher.VerifyHashedPassword(user, user.Password, password);
                if (result == PasswordVerificationResult.Success)
                {
                    // Successful manual login
                    HttpContext.Session.SetString("UserName", user.Name); // Store the user's name in session
                    return RedirectToAction("Welcome"); // Redirect to welcome page
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
            var user = _context.Users.SingleOrDefault(u => u.rfid == rfid);
            if (user != null)
            {
                // Successful RFID login
                HttpContext.Session.SetString("UserName", user.Name); // Store the user's name in session
                return RedirectToAction("Welcome"); // Redirect to welcome page
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
    if (book == null)
    {
        TempData["ErrorMessage"] = "Book not found.";
        return RedirectToAction("ScannedBorrow", new { rfid = book.BookRFID });
    }

    // Add an entry in BorrowedBooks
    var borrowedBook = new BorrowedBooks
    {
        UserId = user.UserId,
        BookId = book.BookId,
        DateBorrowed = DateTimeOffset.UtcNow, // Current time in UTC
        DLBorrow = DateTimeOffset.UtcNow.AddDays(20) // Deadline 20 days from now
    };

    // Update TimesBorrowed and Availability of the book
    book.TimesBorrowed += 1;  // Increment TimesBorrowed
    book.Availability = "Borrowed"; // Set availability to "Borrowed"

    // Explicitly mark the book as modified to ensure Entity Framework tracks it
    _context.Books.Update(book);

    // Add the borrowed book entry to BorrowedBooks table
    _context.BorrowedBooks.Add(borrowedBook);

    // Save changes to the database
    _context.SaveChanges();

    TempData["SuccessMessage"] = "Book successfully registered as borrowed.";  // Set success message
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

        if (string.IsNullOrEmpty(userName))
        {
            return RedirectToAction("Index", "Home");
        }

        // Using FirstOrDefault to prevent the exception
        var user = _context.Users.FirstOrDefault(u => u.Name == userName);
        if (user == null)
        {
            return RedirectToAction("Index", "Home");
        }

        return View(user); // Pass the User model to the view for editing
    }



[HttpPost]
public IActionResult EditProfile(User updatedUser)
{
    var userName = HttpContext.Session.GetString("UserName");

    if (string.IsNullOrEmpty(userName))
    {
        return RedirectToAction("Input", "Account");
    }

    var user = _context.Users.FirstOrDefault(u => u.Name == userName);
    if (user == null)
    {
        return NotFound();
    }

    // Check if StudentNumber is empty or null and handle accordingly
    if (string.IsNullOrEmpty(updatedUser.StudentNumber))
    {
        // You could set a default value or handle it based on your business logic
        updatedUser.StudentNumber = user.StudentNumber; // retain the current StudentNumber
    }

    // Perform update
    user.StudentNumber = updatedUser.StudentNumber;
    user.Course = updatedUser.Course;
    user.Email = updatedUser.Email;
    user.rfid = updatedUser.rfid;
    user.role = updatedUser.role;

    try
    {
        _context.SaveChanges();
        HttpContext.Session.SetString("StudentId", user.StudentNumber);
        HttpContext.Session.SetString("Course", user.Course);
        HttpContext.Session.SetString("PlmEmail", user.Email);
        return Ok();
    }
    catch (Exception ex)
    {
        return StatusCode(500, new { message = "An error occurred while updating the profile.", details = ex.Message });
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
}

    
