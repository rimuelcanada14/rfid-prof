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
            // Redirect to index if user is not logged in
            return RedirectToAction("Index", "Home");
        }

        // Fetch the user from the database and store additional data in the session
        var user = _context.Users.SingleOrDefault(u => u.Name == userName);
        if (user != null)
        {
            HttpContext.Session.SetString("StudentId", user.StudentNumber);
            HttpContext.Session.SetString("Course", user.Course);
            HttpContext.Session.SetString("PlmEmail", user.Email);
        }

        ViewBag.UserName = userName;
        return View();
    }

    public IActionResult Return()
    {
        var userName = HttpContext.Session.GetString("UserName");

        if (string.IsNullOrEmpty(userName))
        {
            // Redirect to index if user is not logged in
            return RedirectToAction("Index", "Home");
        }

        ViewBag.UserName = userName;
        return View();
    }
    public IActionResult Profile()
    {
        var userName = HttpContext.Session.GetString("UserName");

        if (string.IsNullOrEmpty(userName))
        {
            return RedirectToAction("Index", "Home");
        } 
        // Fetch the user from the database and store additional data in the session
        var user = _context.Users.SingleOrDefault(u => u.Name == userName);
        if (user != null)
        {
            HttpContext.Session.SetString("StudentId", user.StudentNumber);
            HttpContext.Session.SetString("Course", user.Course);
            HttpContext.Session.SetString("PlmEmail", user.Email);
        }

        ViewBag.UserName = userName;
        ViewBag.StudentId = HttpContext.Session.GetString("StudentId");
        ViewBag.Course = HttpContext.Session.GetString("Course");
        ViewBag.PlmEmail = HttpContext.Session.GetString("PlmEmail");
    
        return View();
    }

    [HttpGet]
    public IActionResult EditProfile()
    {
        var userName = HttpContext.Session.GetString("UserName");
        var studentId = HttpContext.Session.GetString("StudentId");
        var course = HttpContext.Session.GetString("Course");
        var plmEmail = HttpContext.Session.GetString("PlmEmail");
        // var contactNumber = HttpContext.Session.GetString("ContactNumber");

        if (string.IsNullOrEmpty(userName))
        {
            return RedirectToAction("Index", "Home"); // Redirect if user is not logged in
        }

        ViewBag.UserName = userName;
        ViewBag.StudentId = studentId;
        ViewBag.Course = course;
        ViewBag.PlmEmail = plmEmail;
        // ViewBag.ContactNumber = contactNumber;

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
        var user = _context.Users.SingleOrDefault(u => u.Name == userName);
        if (user == null)
        {
            return NotFound();
        }

        // Check for duplicates (excluding the current user)
        var isDuplicate = _context.Users
            .Any(u => u.Id != user.Id && (
                u.StudentNumber == updatedUser.StudentNumber ||
                u.Email == updatedUser.Email
                //add contact number
            ));

        if (isDuplicate)
        {
            return BadRequest(new { message = "Invalid credentials. User already exists." });
        }

        // Update user properties
        user.StudentNumber = updatedUser.StudentNumber;
        user.Course = updatedUser.Course;
        user.Email = updatedUser.Email;
        // Uncomment if you have ContactNumber in your User model
        // user.ContactNumber = updatedUser.ContactNumber;

        try
        {
            _context.SaveChanges();

            // Update session data
            HttpContext.Session.SetString("StudentId", user.StudentNumber);
            HttpContext.Session.SetString("Course", user.Course);
            HttpContext.Session.SetString("PlmEmail", user.Email);
            // HttpContext.Session.SetString("ContactNumber", user.ContactNumber);

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
            return RedirectToAction("Index", "Home");
        }

        // Fetch the user from the database
        var user = _context.Users.SingleOrDefault(u => u.Name == userName);
        if (user != null)
        {
            // Update session with all necessary user data
            HttpContext.Session.SetString("StudentId", user.StudentNumber);
            HttpContext.Session.SetString("Course", user.Course);
            HttpContext.Session.SetString("PlmEmail", user.Email);
            // HttpContext.Session.SetString("ContactNumber", user.ContactNumber ?? "");
            // HttpContext.Session.SetString("LibraryCardId", user.LibraryCardId ?? "");
            // HttpContext.Session.SetString("DateIssued", user.DateIssued?.ToString("MMMM dd, yyyy") ?? "");
        }

        // Pass all the data to the view through ViewBag
        ViewBag.UserName = userName;
        ViewBag.StudentId = HttpContext.Session.GetString("StudentId");
        ViewBag.Course = HttpContext.Session.GetString("Course");
        ViewBag.PlmEmail = HttpContext.Session.GetString("PlmEmail");
        // ViewBag.ContactNumber = HttpContext.Session.GetString("ContactNumber");
        // ViewBag.LibraryCardId = HttpContext.Session.GetString("LibraryCardId");
        // ViewBag.DateIssued = HttpContext.Session.GetString("DateIssued");
        return View();
    }
}
    
