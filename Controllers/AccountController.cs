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
    public IActionResult Logout()
    {
        HttpContext.Session.Remove("UserName");
        return RedirectToAction("Index", "Home"); // Redirect to home page after logout
    }
}
