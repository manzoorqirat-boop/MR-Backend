using Microsoft.EntityFrameworkCore;
using SiteReportApp.Data;
using SiteReportApp.Services;

var builder = WebApplication.CreateBuilder(args);

// ---- Database: Postgres on Railway ----
// Railway injects DATABASE_URL like: postgres://user:pass@host:port/dbname
// Npgsql wants a different format, so convert if needed.
string? connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
if (!string.IsNullOrEmpty(databaseUrl))
{
    var uri = new Uri(databaseUrl);
    var userInfo = uri.UserInfo.Split(':');
    connectionString = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};" +
                        $"Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=true";
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// ---- Services ----
builder.Services.AddScoped<AnalyticsService>();
builder.Services.AddScoped<DataEntryService>();
builder.Services.AddScoped<ExcelImportService>();

// ---- CORS: allow your React frontend (adjust origin for prod) ----
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",                 // local Vite dev
                "https://your-frontend.up.railway.app"   // replace with deployed frontend URL
              )
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Railway sets PORT env var; bind Kestrel to it
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");
app.UseAuthorization();
app.MapControllers();

app.Run();
