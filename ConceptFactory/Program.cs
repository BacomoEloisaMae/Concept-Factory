using Microsoft.EntityFrameworkCore;
using ConceptFactory.Data;
using ConceptFactory.Utils;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews()
    .AddRazorRuntimeCompilation();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure()
    ));

// Sends the Gmail-based password reset code — see Utils/EmailService.cs
// and the "EmailSettings" section in appsettings.json for setup.
builder.Services.AddScoped<EmailService>();

var app = builder.Build();

// One-time, idempotent: hashes any still-plaintext AdminUsers/Users
// passwords (the seeded admin, older staff rows) in place. Safe on every
// boot — already-hashed rows are skipped. See Utils/PasswordMigration.cs.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await PasswordMigration.EnsureHashedAsync(db);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Auth/Login");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=hIndex}/{id?}");

app.Run();

