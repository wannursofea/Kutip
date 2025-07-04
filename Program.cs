using Kutip.Data;
using Kutip.Services;
using Kutip.Services.Implementations;
using Kutip.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
//using System.Net.Http.
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(connectionString, sqlServerOptions =>
    {
        sqlServerOptions.CommandTimeout(120);
    });
});

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedEmail = false;

    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 8;
    options.Password.RequiredUniqueChars = 1;

    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    options.User.RequireUniqueEmail = true;

})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddScoped<IFileService, FileService>();

var googleMapsApiKey = builder.Configuration["GoogleMaps:ApiKey"];
if (string.IsNullOrEmpty(googleMapsApiKey))
{
    throw new InvalidOperationException("GoogleMaps:ApiKey is not configured in appsettings.json or environment variables.");
}

builder.Services.AddHttpClient<IGeocodingService, GoogleMapsGeocodingService>((serviceProvider, client) =>
{
    client.BaseAddress = new Uri("https://maps.googleapis.com/");
    client.DefaultRequestHeaders.Accept.Clear();
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
}).SetHandlerLifetime(TimeSpan.FromMinutes(5));
//.AddTypedClient<IGeocodingService, GoogleMapsGeocodingService>((httpClient, serviceProvider) =>
//{
//    var logger = serviceProvider.GetRequiredService<ILogger<GoogleMapsGeocodingService>>();
//    return new GoogleMapsGeocodingService(httpClient, googleMapsApiKey, logger);
//});

builder.Services.AddHttpClient<IRoutingService, GoogleMapsRoutingService>((serviceProvider, client) =>
{
    client.BaseAddress = new Uri("https://maps.googleapis.com/");
    client.DefaultRequestHeaders.Accept.Clear();
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
}).SetHandlerLifetime(TimeSpan.FromMinutes(5));
  //.AddTypedClient<IRoutingService, GoogleMapsRoutingService>((httpClient, serviceProvider) =>
  //{
  //    var logger = serviceProvider.GetRequiredService<ILogger<GoogleMapsRoutingService>>();
  //    return new GoogleMapsRoutingService(httpClient, googleMapsApiKey, logger);
  //});



builder.Services.AddScoped<LocationService>(); 
builder.Services.AddScoped<BinRoutingService>(); 

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

builder.Services.AddTransient<IEmailSender, EmailSender>();

var app = builder.Build();

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
app.UseStaticFiles();
app.UseRouting();

app.UseAuthorization();

// app.MapStaticAssets(); // Keeping it for now assuming it's custom.

using (var scope = app.Services.CreateScope())
{
    var serviceProvider = scope.ServiceProvider;
    try
    {
        await DbSeeder.SeedRolesAndAdminAsync(serviceProvider);
        Console.WriteLine("Database seeding completed successfully.");
    }
    catch (Exception ex)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Login}/{id?}");

app.MapRazorPages()
    .WithStaticAssets();

// This part is redundant as it's already done above in the DbSeeder start block
// using (var scope = app.Services.CreateScope())
// {
//     await DbSeeder.SeedRolesAndAdminAsync(scope.ServiceProvider);
// }

app.Run();