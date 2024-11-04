using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using System.Linq;

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
        if (ModelState.IsValid)
        {
            var passwordHasher = new PasswordHasher<User>();
            user.Password = passwordHasher.HashPassword(user, user.Password); // Hash the password
            _context.Users.Add(user); // Add user to the database
            _context.SaveChanges(); // Save changes to the database
            return RedirectToAction("Index", "Home"); // Redirect to the login page
        }
        return View(user); // Return the view with validation errors
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
                    TempData["UserName"] = user.Name; // Store the user's name in TempData
                    return RedirectToAction("Welcome"); // Redirect to welcome page
                }
            }
        }
        else if (loginMethod == "rfid")
        {
            // RFID login logic
            var user = _context.Users.SingleOrDefault(u => u.rfid == rfid);
            if (user != null)
            {
                // Successful RFID login
                TempData["UserName"] = user.Name; // Store the user's name in TempData
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
        var userName = TempData["UserName"]?.ToString(); // Retrieve UserName from TempData
        ViewBag.UserName = userName; // Pass the user's name to the ViewBag
        return View();
    }
}
