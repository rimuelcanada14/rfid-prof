using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Session;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<UserDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add session services
builder.Services.AddSession(); // Add this line to enable session
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Use session middleware
app.UseSession(); // Add this line to enable session in the request pipeline

// Other middleware and routing
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
