using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
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
    var userInfo = uri.UserInfo.Split(':', 2);
    // Credentials in a URL are percent-encoded; unescape so passwords with
    // special characters (@ : / # etc.) don't silently break the connection.
    var dbUser = Uri.UnescapeDataString(userInfo[0]);
    var dbPass = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
    connectionString = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};" +
                        $"Username={dbUser};Password={dbPass};SSL Mode=Require;Trust Server Certificate=true";
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// ---- Services ----
builder.Services.AddScoped<AnalyticsService>();
builder.Services.AddScoped<DataEntryService>();
builder.Services.AddScoped<ExcelImportService>();
builder.Services.AddScoped<ScorecardService>();
builder.Services.AddScoped<ScorecardExcelService>();
builder.Services.AddScoped<AuthService>();

// ---- Authentication: JWT bearer tokens issued by AuthService ----
// Set JWT_SECRET on the backend service in production. Site users carry a
// "siteId" claim; corporate users carry Role=Corporate (see UserScopeExtensions).
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "SiteReportApp",
            ValidateAudience = true,
            ValidAudience = "SiteReportApp",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = AuthService.GetSigningKey(),
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });
builder.Services.AddAuthorization();

// ---- CORS: allow your React frontend ----
// The deployed frontend origin comes from the FRONTEND_URL env var (set this on the
// backend's Railway service to your frontend's public URL, e.g.
// https://your-frontend.up.railway.app). localhost:5173 is always allowed for local dev.
var frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL");
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var origins = new List<string> { "http://localhost:5173" };
        if (!string.IsNullOrWhiteSpace(frontendUrl))
            origins.Add(frontendUrl.TrimEnd('/'));

        policy.WithOrigins(origins.ToArray())
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Serialize enums (CompletionStatus, TrainingStatus, ProjectStatus, ReportPeriodStatus, InitiativeType)
// as their string names instead of raw integers — the frontend matches against names like
// "Completed" / "InProgress" (see index.css .status-* classes and constants.js).
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Railway sets PORT env var; bind Kestrel to it
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();

// ---- Ensure the database schema exists ----
// On a fresh Railway Postgres there are no tables yet, so every query would 500.
// EnsureCreated() creates the schema from the model on first run and is a no-op
// afterwards. NOTE: EnsureCreated and EF migrations don't mix — if you later add
// migrations, switch this to db.Database.Migrate() instead.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // ---- Startup resilience ----
    // On platforms like Railway the app container can start before Postgres is
    // ready (or while it is restarting/waking). Instead of crash-looping on the
    // first failed handshake, retry with a delay and log where we're connecting.
    var startupLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    const int maxAttempts = 12;          // ~1 minute total
    for (var attempt = 1; ; attempt++)
    {
        try
        {
            db.Database.EnsureCreated();
            startupLogger.LogInformation("Database connection established on attempt {Attempt}.", attempt);
            break;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            var csb = new Npgsql.NpgsqlConnectionStringBuilder(db.Database.GetConnectionString());
            startupLogger.LogWarning(
                "Database not reachable yet at {Host}:{Port}/{Db} (attempt {Attempt}/{Max}): {Message}. Retrying in 5s…",
                csb.Host, csb.Port, csb.Database, attempt, maxAttempts, ex.Message);
            Thread.Sleep(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            var csb = new Npgsql.NpgsqlConnectionStringBuilder(db.Database.GetConnectionString());
            startupLogger.LogCritical(ex,
                "Could not connect to Postgres at {Host}:{Port}/{Db} after {Max} attempts. " +
                "Check that the database service is running and DATABASE_URL points at it.",
                csb.Host, csb.Port, csb.Database, maxAttempts);
            throw;
        }
    }

    // ---- Additive schema for tables introduced after the DB was first created ----
    // EnsureCreated() only builds the schema when the database is completely empty;
    // once any table exists it is a no-op, so a newly-added DbSet (ScorecardEntries)
    // never gets a table and every query fails with 42P01 "relation does not exist".
    // Since this project intentionally does NOT use EF migrations, we create the new
    // table here with an idempotent CREATE TABLE IF NOT EXISTS that matches the
    // EF model (default PascalCase names, identity PK, int FKs). Safe to run every boot.
    //
    // NOTE: ExecuteSqlRaw runs the string through string.Format, so any literal
    // '{' or '}' in the SQL must be doubled ('{{' / '}}') or it throws
    // FormatException. The JSON DEFAULT '{}' values below are escaped as '{{}}'.
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ""ScorecardEntries"" (
            ""Id""             integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
            ""SiteId""         integer NOT NULL,
            ""ReportPeriodId"" integer NOT NULL,
            ""MetricKey""      character varying(64) NOT NULL,
            ""RowIndex""       integer NOT NULL,
            ""CellsJson""      text NOT NULL,
            CONSTRAINT ""FK_ScorecardEntries_Sites_SiteId""
                FOREIGN KEY (""SiteId"") REFERENCES ""Sites"" (""Id"") ON DELETE RESTRICT,
            CONSTRAINT ""FK_ScorecardEntries_ReportPeriods_ReportPeriodId""
                FOREIGN KEY (""ReportPeriodId"") REFERENCES ""ReportPeriods"" (""Id"") ON DELETE RESTRICT
        );

        CREATE INDEX IF NOT EXISTS ""IX_ScorecardEntries_SiteId_ReportPeriodId_MetricKey""
            ON ""ScorecardEntries"" (""SiteId"", ""ReportPeriodId"", ""MetricKey"");

        CREATE UNIQUE INDEX IF NOT EXISTS ""IX_ScorecardEntries_SiteId_ReportPeriodId_MetricKey_RowIndex""
            ON ""ScorecardEntries"" (""SiteId"", ""ReportPeriodId"", ""MetricKey"", ""RowIndex"");

        CREATE INDEX IF NOT EXISTS ""IX_ScorecardEntries_MetricKey_ReportPeriodId""
            ON ""ScorecardEntries"" (""MetricKey"", ""ReportPeriodId"");

        -- ---- Auth: Users table (added with the site-wise login feature) ----
        CREATE TABLE IF NOT EXISTS ""Users"" (
            ""Id""            integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
            ""Username""      character varying(64)  NOT NULL,
            ""DisplayName""   character varying(128) NOT NULL,
            ""PasswordHash""  text NOT NULL,
            ""Role""          text NOT NULL,
            ""SiteId""        integer NULL,
            ""IsActive""      boolean NOT NULL DEFAULT TRUE,
            ""CreatedAtUtc""  timestamp with time zone NOT NULL DEFAULT now(),
            CONSTRAINT ""FK_Users_Sites_SiteId""
                FOREIGN KEY (""SiteId"") REFERENCES ""Sites"" (""Id"") ON DELETE RESTRICT
        );
        CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Users_Username"" ON ""Users"" (""Username"");

        -- ---- Initiative attachments (evidence files, bytes in Postgres) ----
        CREATE TABLE IF NOT EXISTS ""InitiativeAttachments"" (
            ""Id""            integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
            ""InitiativeId""  integer NOT NULL,
            ""FileName""      character varying(260) NOT NULL,
            ""ContentType""   character varying(128) NOT NULL,
            ""SizeBytes""     bigint NOT NULL,
            ""Data""          bytea NOT NULL,
            ""UploadedBy""    text NOT NULL DEFAULT '',
            ""UploadedAtUtc"" timestamp with time zone NOT NULL DEFAULT now(),
            CONSTRAINT ""FK_InitiativeAttachments_Initiatives""
                FOREIGN KEY (""InitiativeId"") REFERENCES ""Initiatives"" (""Id"") ON DELETE CASCADE
        );
        CREATE INDEX IF NOT EXISTS ""IX_InitiativeAttachments_InitiativeId""
            ON ""InitiativeAttachments"" (""InitiativeId"");

        -- ---- Initiative change requests (post-submission corrections with approval) ----
        CREATE TABLE IF NOT EXISTS ""InitiativeChangeRequests"" (
            ""Id""               integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
            ""InitiativeId""     integer NOT NULL,
            ""RequestType""      text NOT NULL DEFAULT 'Update',
            ""OriginalJson""     text NOT NULL DEFAULT '{{}}',
            ""ProposedJson""     text NOT NULL DEFAULT '{{}}',
            ""Justification""    text NOT NULL DEFAULT '',
            ""Status""           text NOT NULL DEFAULT 'Pending',
            ""RequestedBy""      text NOT NULL DEFAULT '',
            ""RequestedAtUtc""   timestamp with time zone NOT NULL DEFAULT now(),
            ""DecidedBy""        text NULL,
            ""DecidedAtUtc""     timestamp with time zone NULL,
            ""DecisionComments"" text NULL,
            CONSTRAINT ""FK_InitiativeChangeRequests_Initiatives""
                FOREIGN KEY (""InitiativeId"") REFERENCES ""Initiatives"" (""Id"") ON DELETE CASCADE
        );
        CREATE INDEX IF NOT EXISTS ""IX_InitiativeChangeRequests_InitiativeId_Status""
            ON ""InitiativeChangeRequests"" (""InitiativeId"", ""Status"");

        -- ---- Review workflow columns on SiteSubmissions (additive, idempotent) ----
        ALTER TABLE ""SiteSubmissions"" ADD COLUMN IF NOT EXISTS ""Status""         text NOT NULL DEFAULT 'NotStarted';
        ALTER TABLE ""SiteSubmissions"" ADD COLUMN IF NOT EXISTS ""ReviewedAtUtc""  timestamp with time zone NULL;
        ALTER TABLE ""SiteSubmissions"" ADD COLUMN IF NOT EXISTS ""ReviewedBy""     text NULL;
        ALTER TABLE ""SiteSubmissions"" ADD COLUMN IF NOT EXISTS ""ReviewComments"" text NULL;

        -- Backfill: rows submitted under the old honor-system flow become 'Submitted'
        UPDATE ""SiteSubmissions"" SET ""Status"" = 'Submitted'
            WHERE ""IsSubmitted"" = TRUE AND ""Status"" = 'NotStarted';
    ");

    // Seed the first corporate admin (username: admin) if no users exist yet.
    var auth = scope.ServiceProvider.GetRequiredService<AuthService>();
    auth.SeedAdminAsync().GetAwaiter().GetResult();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
