using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Core.Interfaces;
using BoatSpotFinder.Core.Services;
using BoatSpotFinder.Core.Settings;
using BoatSpotFinder.Infrastructure.Data;
using BoatSpotFinder.Infrastructure.Email;
using BoatSpotFinder.Infrastructure.Logging;
using BoatSpotFinder.Infrastructure.Repositories;
using BoatSpotFinder.Infrastructure.Search;
using BoatSpotFinder.Web.Infrastructure.Storage;
using BoatSpotFinder.Web.Infrastructure;
using Elastic.Clients.Elasticsearch;
using Hangfire;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NLog.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Host.UseNLog();

builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("AppSettings"));
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));

var smtpHost = builder.Configuration["Smtp:Host"];
if (string.IsNullOrWhiteSpace(smtpHost))
{
    builder.Services.AddScoped<IEmailSender, ConsoleEmailSender>();
}
else
{
    builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
}

builder.Services.AddControllersWithViews(o =>
    o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 6;
    options.Password.RequiredUniqueChars = 1;
    options.SignIn.RequireConfirmedEmail = true;
})
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders()
    .AddSignInManager<CustomSignInManager>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/account/login";
    options.AccessDeniedPath = "/account/access-denied";
});

builder.Services.AddDataProtection()
    .PersistKeysToDbContext<AppDbContext>();

builder.Services.AddScoped<IAdminSettingsRepository, AdminSettingsRepository>();
builder.Services.AddScoped<IInvitationRepository, InvitationRepository>();
builder.Services.AddScoped<IMarinaAdminRepository, MarinaAdminRepository>();
builder.Services.AddScoped<IAuditLogger, NLogAuditLogger>();
builder.Services.AddScoped<IMarinaRepository, MarinaRepository>();
builder.Services.AddScoped<ISpotRepository, SpotRepository>();
builder.Services.AddScoped<ISpotSeasonalRuleRepository, SpotSeasonalRuleRepository>();
builder.Services.AddScoped<ISpotSeasonalRuleService, SpotSeasonalRuleService>();
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();

var esUri = builder.Configuration["Elasticsearch:Uri"];
if (string.IsNullOrWhiteSpace(esUri))
{
    builder.Services.AddScoped<IMarinaSearchService, NullMarinaSearchService>();
}
else
{
    var esSettings = new ElasticsearchClientSettings(new Uri(esUri))
        .DefaultIndex("marinas");
    builder.Services.AddSingleton(new ElasticsearchClient(esSettings));
    builder.Services.AddScoped<IMarinaSearchService, ElasticsearchMarinaSearchService>();
}

builder.Services.AddHangfire(c =>
    c.UseSqlServerStorage(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddHangfireServer();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireAdminAuthFilter()]
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHealthChecks("/health");

app.Lifetime.ApplicationStarted.Register(() => Task.Run(async () =>
{
    try
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var search = scope.ServiceProvider.GetRequiredService<IMarinaSearchService>();
        var marinas = await db.Set<Marina>().Where(m => m.IsActive).ToListAsync();
        foreach (var marina in marinas)
            await search.IndexAsync(marina);
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "ES startup seed failed");
    }
}));

app.Run();
