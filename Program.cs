using Microsoft.AspNetCore.Identity.UI.Services;
using Kutip.Services;
using Kutip.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(connectionString, sqlServerOptions =>
    {
        sqlServerOptions.CommandTimeout(120); 
    });
});

//builder.Services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true).AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

//builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
//    .AddEntityFrameworkStores<ApplicationDbContext>()
//    .AddDefaultUI()
//    .AddDefaultTokenProviders();

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Sign-in options (keep if you have specific needs like confirmed account)
    options.SignIn.RequireConfirmedEmail = false; // Set to true if you want to require email confirmation

    // Password settings - ADD THESE LINES FOR PASSWORD REQUIREMENTS
    options.Password.RequireDigit = true;           
    options.Password.RequireLowercase = true;       
    options.Password.RequireNonAlphanumeric = true; 
    options.Password.RequireUppercase = true;       
    options.Password.RequiredLength = 8;            
    options.Password.RequiredUniqueChars = 1;       

    // Lockout settings (optional, but good for security)
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5); // User locked out for 5 minutes
    options.Lockout.MaxFailedAccessAttempts = 5;                      // After 5 failed attempts, user is locked out
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.RequireUniqueEmail = true; // Ensures email is unique

})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders(); 

builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

builder.Services.AddTransient<IEmailSender, EmailSender>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

// DbSeeder start
using (var scope = app.Services.CreateScope())
{
    var serviceProvider = scope.ServiceProvider;
    try
    {
        // This is the call to your DbSeeder
        await DbSeeder.SeedRolesAndAdminAsync(serviceProvider);
        Console.WriteLine("Database seeding completed successfully.");
    }
    catch (Exception ex)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}
// DbSeeder end

//app.MapControllerRoute(
//    name: "loginEntry",
//    pattern: "login", 
//    defaults: new { controller = "Home", action = "Login" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Login}/{id?}"); 

app.MapRazorPages()
   .WithStaticAssets();
//await DbSeeder.SeedRolesAndAdminAsync(app.Services);

using (var scope = app.Services.CreateScope())
{
    await DbSeeder.SeedRolesAndAdminAsync(scope.ServiceProvider);
}
    app.Run();
