using ArkJsonValidator.Data;
using ArkJsonValidator.Middleware;
using ArkJsonValidator.Services;
using ArkJsonValidator.Validators;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews()
    .AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy = null);

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=ark_validator.db"));

builder.Services.AddSingleton<ValidatorRegistry>();
builder.Services.AddScoped<TemplateService>();
builder.Services.AddScoped<ValidationService>();

builder.Services.AddCors(opt => opt.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// Auto-migrate on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseHttpsRedirection();
app.UseCors();
app.UseStaticFiles();
app.UseRouting();

// Basic Auth guards /api routes
app.UseMiddleware<BasicAuthMiddleware>();

app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.MapControllers();

app.Run();
