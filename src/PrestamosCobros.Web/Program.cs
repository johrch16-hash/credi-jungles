using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PrestamosCobros.BLL.Interfaces;
using PrestamosCobros.BLL.Services;
using PrestamosCobros.Web.Services;
using PrestamosCobros.DAL.Context;
using PrestamosCobros.DAL.Entities;
using PrestamosCobros.DAL.Repositories;
using PrestamosCobros.Infrastructure.Audit;
using PrestamosCobros.Infrastructure.Security;
using PrestamosCobros.Infrastructure.Services;
using PrestamosCobros.Infrastructure.Interfaces;
using Hangfire;
using Hangfire.MemoryStorage;
using FluentValidation;
using FluentValidation.AspNetCore;
using PrestamosCobros.BLL.Validators;
using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// FIX for Render: "The configured user limit (128) on the number of inotify instances has been reached"
// This prevents ASP.NET Core from exhausting the server's inotify watches on Linux containers.
Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "1");

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestHeadersTotalSize = 64 * 1024;
    options.Limits.MaxRequestLineSize = 16 * 1024;
});

builder.Services.AddHttpClient();

// --- Rate Limiting ---
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("LoginPolicy", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 30;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});


// --- Base de datos (Soporte dinámico para SQLite / PostgreSQL) ---
var defaultConn = Environment.GetEnvironmentVariable("DATABASE_URL");
if (string.IsNullOrEmpty(defaultConn))
{
    defaultConn = builder.Configuration.GetConnectionString("DefaultConnection");
}

if (!string.IsNullOrEmpty(defaultConn))
{
    defaultConn = ConnectionStringHelper.EnsureIPv4ConnectionString(defaultConn);
}

bool usePostgres = defaultConn != null && (defaultConn.Contains("Host=") || defaultConn.StartsWith("postgres://") || defaultConn.StartsWith("postgresql://"));

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (usePostgres)
    {
        options.UseNpgsql(defaultConn);
    }
    else
    {
        options.UseSqlite(defaultConn);
    }
});

// --- Identity ---
builder.Services.AddIdentity<Usuario, Rol>(options =>
{
    // Política de contraseñas
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;

    // Bloqueo de cuenta
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.AllowedForNewUsers = true;

    // Usuario
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders()
.AddPasswordValidator<PasswordHistoryValidator<Usuario>>();

// --- Cookie de autenticación ---
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(2);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// --- Servicios de la aplicación ---
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddSingleton<ICifradoService, CifradoService>();
builder.Services.AddScoped<IAuditoriaService, AuditoriaService>();
builder.Services.AddScoped<IClienteService, ClienteService>();
builder.Services.AddScoped<IPrestamoService, PrestamoService>();
builder.Services.AddScoped<IPagoService, PagoService>();
builder.Services.AddScoped<IPagarePdfService, PagarePdfService>();
builder.Services.AddScoped<INotificacionService, NotificacionService>();
builder.Services.AddScoped<IGastoService, GastoService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IComprobantePdfService, ComprobantePdfService>();
builder.Services.AddScoped<IReporteGeneralService, ReporteGeneralService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<WhatsAppService>();
builder.Services.AddScoped<IConfiguracionWhatsAppService, ConfiguracionWhatsAppService>();

// --- Hangfire ---
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseMemoryStorage());
builder.Services.AddHangfireServer();

// --- MVC & FluentValidation ---
builder.Services.AddControllersWithViews();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddFluentValidationClientsideAdapters();
builder.Services.AddValidatorsFromAssemblyContaining<ClienteCreateDtoValidator>();

var app = builder.Build();

// --- Middleware Diagnóstico de Excepciones Globales ---
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        // Si es un error de criptografía por TempData corrupto (Render ephemeral keys), limpiamos la cookie y redirigimos
        if (ex is System.Security.Cryptography.CryptographicException || 
            (ex.InnerException != null && ex.InnerException is System.Security.Cryptography.CryptographicException))
        {
            context.Response.Cookies.Delete(".AspNetCore.Mvc.CookieTempDataProvider");
            context.Response.Redirect(context.Request.Path);
            return;
        }

        Console.WriteLine($"[CRITICAL 500 ERROR] {ex}");
        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = 500;
            context.Response.ContentType = "text/html; charset=utf-8";
            var innerMsg = ex.InnerException != null ? ex.InnerException.Message : "Ninguna";
            var innerTrace = ex.InnerException != null ? ex.InnerException.StackTrace : "";
            await context.Response.WriteAsync($@"<!DOCTYPE html>
<html>
<head><title>Error 500 - Diagnóstico</title></head>
<body style='font-family: sans-serif; padding: 30px; background: #fff5f5; color: #800;'>
    <h1 style='color: #c00;'>Error 500 de Servidor</h1>
    <h3>Mensaje: {System.Net.WebUtility.HtmlEncode(ex.Message)}</h3>
    <p><b>Tipo Excepción:</b> {ex.GetType().FullName}</p>
    <h4>StackTrace Principal:</h4>
    <pre style='background: #fff; padding: 15px; border: 1px solid #f99; border-radius: 8px; overflow: auto; max-height: 300px;'>{System.Net.WebUtility.HtmlEncode(ex.StackTrace ?? "")}</pre>
    <h4>Excepción Interna (InnerException):</h4>
    <p><b>Mensaje:</b> {System.Net.WebUtility.HtmlEncode(innerMsg)}</p>
    <pre style='background: #fff; padding: 15px; border: 1px solid #f99; border-radius: 8px; overflow: auto; max-height: 300px;'>{System.Net.WebUtility.HtmlEncode(innerTrace ?? "")}</pre>
</body>
</html>");
        }
    }
});

app.UseDeveloperExceptionPage();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRateLimiter();

// --- Localización (Fuerza cultura en-US para que los decimales con . funcionen sin error) ---
var defaultCulture = new CultureInfo("en-US");
var localizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(defaultCulture),
    SupportedCultures = new List<CultureInfo> { defaultCulture },
    SupportedUICultures = new List<CultureInfo> { defaultCulture }
};
app.UseRequestLocalization(localizationOptions);

// --- Middleware para prevenir errores de TempData con llaves efímeras (Render) ---
app.Use(async (context, next) =>
{
    var tempDataFactory = context.RequestServices.GetService<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionaryFactory>();
    if (tempDataFactory != null)
    {
        var tempData = tempDataFactory.GetTempData(context);
        try
        {
            tempData.Load();
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            context.Response.Cookies.Delete(".AspNetCore.Mvc.CookieTempDataProvider");
        }
    }
    await next();
});

app.UseRouting();

// Cabeceras de seguridad
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdn.tailwindcss.com https://cdn.jsdelivr.net; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://cdn.jsdelivr.net; font-src 'self' https://fonts.gstatic.com; img-src 'self' data:; connect-src 'self' https://cdn.tailwindcss.com; frame-ancestors 'none';");
    context.Response.Headers.Append("Permissions-Policy", "geolocation=(), microphone=(), camera=()");
    await next();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// --- Hangfire Dashboard & Jobs ---
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    // Opcional: configurar autorización para Hangfire Dashboard aquí
});

RecurringJob.AddOrUpdate<INotificacionService>(
    "marcar-atrasos",
    x => x.MarcarCuotasAtrasadasAsync(),
    "*/2 * * * *");

RecurringJob.AddOrUpdate<INotificacionService>(
    "generar-recordatorios",
    x => x.GenerarRecordatoriosAsync(),
    "* * * * *");

RecurringJob.AddOrUpdate<INotificacionService>(
    "enviar-recordatorios-automaticos",
    x => x.EnviarRecordatoriosPendientesAsync(),
    "* * * * *");

// --- Seed de datos iniciales ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<AppDbContext>();
    
    if (usePostgres)
    {
        try
        {
            if (!await context.Database.CanConnectAsync())
            {
                Console.WriteLine("[DB Debug] Cannot connect to PostgreSQL database. Running migrations/creation...");
                string script = context.Database.GenerateCreateScript();
                await context.Database.ExecuteSqlRawAsync(script);
            }
            else
            {
                try { await context.Database.EnsureCreatedAsync(); } catch (Exception ex) { Console.WriteLine("[DB Debug] EnsureCreated: " + ex.Message); }
                // Asegurar tablas base en PostgreSQL
                try
                {
                    await context.Database.ExecuteSqlRawAsync(@"
                        CREATE TABLE IF NOT EXISTS Preferencias (
                            Id SERIAL PRIMARY KEY,
                            Clave TEXT NOT NULL,
                            Valor TEXT NOT NULL
                        );");
                } catch (Exception ex) { Console.WriteLine($"[DB Debug] Preferencias table creation info: {ex.Message}"); }

                try
                {
                    await context.Database.ExecuteSqlRawAsync(@"CREATE UNIQUE INDEX IF NOT EXISTS IX_Preferencias_Clave ON Preferencias (Clave);");
                } catch (Exception ex) { Console.WriteLine($"[DB Debug] Index IX_Preferencias_Clave info: {ex.Message}"); }

                try
                {
                    await context.Database.ExecuteSqlRawAsync(@"
                        CREATE TABLE IF NOT EXISTS ConfiguracionesWhatsApp (
                            Id SERIAL PRIMARY KEY,
                            PhoneNumberId TEXT NOT NULL,
                            AccessToken TEXT NOT NULL,
                            FechaActualizacion TEXT NOT NULL
                        );");
                } catch (Exception ex) { Console.WriteLine($"[DB Debug] ConfiguracionesWhatsApp table creation info: {ex.Message}"); }

                try
                {
                    await context.Database.ExecuteSqlRawAsync(@"
                        CREATE TABLE IF NOT EXISTS ""SoporteTickets"" (
                            ""SoporteTicketId"" SERIAL PRIMARY KEY,
                            ""UsuarioId"" INTEGER NOT NULL,
                            ""NombreUsuario"" VARCHAR(150) NOT NULL DEFAULT '',
                            ""EmailUsuario"" VARCHAR(150) NULL,
                            ""Titulo"" VARCHAR(200) NOT NULL DEFAULT '',
                            ""Mensaje"" TEXT NOT NULL DEFAULT '',
                            ""ImagenUrl"" VARCHAR(500) NULL,
                            ""FechaCreacion"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
                            ""Estado"" VARCHAR(30) NOT NULL DEFAULT 'Pendiente',
                            ""RespuestaMaster"" TEXT NULL,
                            ""FechaRespuesta"" TIMESTAMP WITH TIME ZONE NULL
                        );");
                } catch (Exception ex) { Console.WriteLine($"[DB Debug] SoporteTickets table creation info: {ex.Message}"); }

                try
                {
                    await context.Database.ExecuteSqlRawAsync(@"
                        ALTER TABLE ""SoporteTickets"" 
                        ALTER COLUMN ""FechaCreacion"" TYPE TIMESTAMP WITH TIME ZONE USING ""FechaCreacion""::timestamp with time zone;");
                } catch (Exception ex) { Console.WriteLine($"[DB Debug] SoporteTickets FechaCreacion alter info: {ex.Message}"); }

                try
                {
                    await context.Database.ExecuteSqlRawAsync(@"
                        ALTER TABLE ""SoporteTickets"" 
                        ALTER COLUMN ""FechaRespuesta"" TYPE TIMESTAMP WITH TIME ZONE USING ""FechaRespuesta""::timestamp with time zone;");
                } catch (Exception ex) { Console.WriteLine($"[DB Debug] SoporteTickets FechaRespuesta alter info: {ex.Message}"); }

                // Add missing columns if they were not created initially
                try
                {
                    await context.Database.ExecuteSqlRawAsync(@"
                        ALTER TABLE ""SoporteTickets"" 
                        ADD COLUMN IF NOT EXISTS ""EmailUsuario"" VARCHAR(150) NULL;");
                } catch (Exception ex) { Console.WriteLine($"[DB Debug] SoporteTickets EmailUsuario alter info: {ex.Message}"); }

                try
                {
                    await context.Database.ExecuteSqlRawAsync(@"
                        ALTER TABLE ""SoporteTickets"" 
                        ADD COLUMN IF NOT EXISTS ""Estado"" VARCHAR(30) NOT NULL DEFAULT 'Pendiente';");
                } catch (Exception ex) { Console.WriteLine($"[DB Debug] SoporteTickets Estado alter info: {ex.Message}"); }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DB Debug] Warning: Error during database cleanup check: {ex.Message}");
        }
    }
    else
    {
        // Usamos EnsureCreatedAsync en SQLite local primero para que cree las tablas de EF
        await context.Database.EnsureCreatedAsync();

        try
        {
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS Preferencias (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Clave TEXT NOT NULL,
                    Valor TEXT NOT NULL
                );");
        } catch (Exception ex) { Console.WriteLine($"[DB Debug] Preferencias table info: {ex.Message}"); }

        try
        {
            await context.Database.ExecuteSqlRawAsync(@"CREATE UNIQUE INDEX IF NOT EXISTS IX_Preferencias_Clave ON Preferencias (Clave);");
        } catch (Exception ex) { Console.WriteLine($"[DB Debug] IX_Preferencias_Clave index info: {ex.Message}"); }

        try
        {
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ConfiguracionesWhatsApp (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    PhoneNumberId TEXT NOT NULL,
                    AccessToken TEXT NOT NULL,
                    FechaActualizacion TEXT NOT NULL
                );");
        } catch (Exception ex) { Console.WriteLine($"[DB Debug] ConfiguracionesWhatsApp table info: {ex.Message}"); }

        try
        {
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS MensajesRecibidos (
                    MensajeRecibidoId INTEGER PRIMARY KEY AUTOINCREMENT,
                    ClienteId INTEGER NULL,
                    TelefonoRemitente TEXT NOT NULL,
                    TipoMensaje TEXT NOT NULL,
                    Contenido TEXT NOT NULL,
                    MetaMediaId TEXT NOT NULL,
                    FechaRecibido TEXT NOT NULL,
                    Leido INTEGER NOT NULL,
                    FOREIGN KEY (ClienteId) REFERENCES Clientes (ClienteId)
                );");
        } catch (Exception ex) { Console.WriteLine($"[DB Debug] MensajesRecibidos table info: {ex.Message}"); }

        try
        {
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS SoporteTickets (
                    SoporteTicketId INTEGER PRIMARY KEY AUTOINCREMENT,
                    UsuarioId INTEGER NOT NULL,
                    NombreUsuario TEXT NOT NULL DEFAULT '',
                    EmailUsuario TEXT NULL,
                    Titulo TEXT NOT NULL DEFAULT '',
                    Mensaje TEXT NOT NULL DEFAULT '',
                    ImagenUrl TEXT NULL,
                    FechaCreacion TEXT NOT NULL,
                    Estado TEXT NOT NULL DEFAULT 'Pendiente',
                    RespuestaMaster TEXT NULL,
                    FechaRespuesta TEXT NULL
                );");
        } catch (Exception ex) { Console.WriteLine($"[DB Debug] SoporteTickets table info: {ex.Message}"); }

        // SQLite missing columns
        try
        {
            await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"SoporteTickets\" ADD COLUMN \"EmailUsuario\" TEXT NULL;");
        } catch (Exception ex) { Console.WriteLine($"[DB Debug] SoporteTickets EmailUsuario SQLite: {ex.Message}"); }

        try
        {
            await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"SoporteTickets\" ADD COLUMN \"Estado\" TEXT NOT NULL DEFAULT 'Pendiente';");
        } catch (Exception ex) { Console.WriteLine($"[DB Debug] SoporteTickets Estado SQLite: {ex.Message}"); }
    }

    // ── Migración Universal: Agregar columnas de permisos y teléfono al usuario ──
    try
    {
        await context.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"Prestamos\" ADD COLUMN \"TipoCredito\" TEXT NOT NULL DEFAULT 'Cuota Completa';");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[DB Debug] Columna TipoCredito ya existe o falló: {ex.Message}");
    }

    try
    {
        string tipoFecha = usePostgres ? "timestamp without time zone NULL" : "TEXT NULL";
        await context.Database.ExecuteSqlRawAsync(
            $"ALTER TABLE \"RegistrosVentas\" ADD COLUMN \"FechaSorteo\" {tipoFecha};");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[DB Debug] Columna FechaSorteo ya existe o falló: {ex.Message}");
    }

    try
    {
        string tipoBool = usePostgres ? "BOOLEAN NOT NULL DEFAULT FALSE" : "INTEGER NOT NULL DEFAULT 0";
        await context.Database.ExecuteSqlRawAsync(
            $"ALTER TABLE \"RegistrosVentas\" ADD COLUMN \"Cancelado\" {tipoBool};");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[DB Debug] Columna Cancelado ya existe o falló: {ex.Message}");
    }

    var columnasMigracion = new Dictionary<string, string>
    {
        ["Telefono"] = "TEXT NULL",
        ["PermitirGestionClientes"] = "BOOLEAN NOT NULL DEFAULT TRUE",
        ["PermitirGestionPrestamos"] = "BOOLEAN NOT NULL DEFAULT TRUE",
        ["PermitirRegistroGastos"] = "BOOLEAN NOT NULL DEFAULT TRUE",
        ["PermitirRegistroPagos"] = "BOOLEAN NOT NULL DEFAULT TRUE",
        ["PermitirEnviarNotificaciones"] = "BOOLEAN NOT NULL DEFAULT TRUE",
        ["PermitirVerReportes"] = "BOOLEAN NOT NULL DEFAULT TRUE",
        ["PermitirVerAuditoria"] = "BOOLEAN NOT NULL DEFAULT TRUE",
        ["PermitirGestionPersonal"] = "BOOLEAN NOT NULL DEFAULT TRUE",
        ["PermitirConfigWhatsApp"] = "BOOLEAN NOT NULL DEFAULT TRUE",
        ["PermitirConfigCorreo"] = "BOOLEAN NOT NULL DEFAULT TRUE",
        ["PermitirPapelera"] = "BOOLEAN NOT NULL DEFAULT TRUE"
    };

    foreach (var col in columnasMigracion)
    {
        try
        {
            string tipoColumna = col.Value;
            if (!usePostgres) 
            {
                // Convertir BOOLEAN a INTEGER para SQLite
                tipoColumna = tipoColumna.Replace("BOOLEAN", "INTEGER").Replace("TRUE", "1");
            }

            // Usar comillas dobles para respetar mayúsculas en PostgreSQL ("Usuarios" y nombres de columnas)
            await context.Database.ExecuteSqlRawAsync(
                $"ALTER TABLE \"Usuarios\" ADD COLUMN \"{col.Key}\" {tipoColumna};");
        }
        catch (Exception ex)
        {
            // Ignorar error si la columna ya existe
            Console.WriteLine($"[DB Debug] Columna {col.Key} ya existe o falló: {ex.Message}");
        }
    }

    var roleManager = services.GetRequiredService<RoleManager<Rol>>();
    var userManager = services.GetRequiredService<UserManager<Usuario>>();

    // Crear roles
    if (!await roleManager.RoleExistsAsync("Master"))
        await roleManager.CreateAsync(new Rol { Name = "Master" });
    if (!await roleManager.RoleExistsAsync("Admin"))
        await roleManager.CreateAsync(new Rol { Name = "Admin" });
    if (!await roleManager.RoleExistsAsync("Asistente"))
        await roleManager.CreateAsync(new Rol { Name = "Asistente" });
    if (!await roleManager.RoleExistsAsync("Cobrador"))
        await roleManager.CreateAsync(new Rol { Name = "Cobrador" });

    // Crear usuario admin inicial
    if (await userManager.FindByEmailAsync("admin@sistema.com") == null)
    {
        var admin = new Usuario
        {
            UserName = "admin@sistema.com",
            Email = "admin@sistema.com",
            NombreCompleto = "Administrador",
            EmailConfirmed = true,
            Activo = true
        };
        var result = await userManager.CreateAsync(admin, "Admin123!");
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, "Admin");
            await userManager.AddToRoleAsync(admin, "Master");
        }
    }
    else
    {
        var admin = await userManager.FindByEmailAsync("admin@sistema.com");
        if (admin != null)
        {
            if (!await userManager.IsInRoleAsync(admin, "Admin"))
            {
                await userManager.AddToRoleAsync(admin, "Admin");
            }
            if (!await userManager.IsInRoleAsync(admin, "Master"))
            {
                await userManager.AddToRoleAsync(admin, "Master");
            }
        }
    }
}

app.Run();

// --- Conversor de URIs PostgreSQL ---
public static class ConnectionStringHelper
{
    public static string EnsureIPv4ConnectionString(string connStrOrUri)
    {
        if (string.IsNullOrEmpty(connStrOrUri)) return connStrOrUri;

        Console.WriteLine("[DNS Debug] Processing connection string or URI...");
        string connectionString = connStrOrUri;

        if (connStrOrUri.StartsWith("postgres://") || connStrOrUri.StartsWith("postgresql://"))
        {
            Console.WriteLine("[DNS Debug] Input is PostgreSQL URI. Converting to connection string...");
            connectionString = ConvertPostgreSqlUriToConnectionString(connStrOrUri);
        }

        try
        {
            var builder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
            builder.SslMode = Npgsql.SslMode.Require;

            // Mask password in console output
            var debugBuilder = new Npgsql.NpgsqlConnectionStringBuilder(builder.ConnectionString);
            if (!string.IsNullOrEmpty(debugBuilder.Password))
            {
                debugBuilder.Password = "***";
            }
            Console.WriteLine($"[DNS Debug] Final connection string (masked): {debugBuilder.ConnectionString}");

            return builder.ConnectionString;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DNS Debug] Error parsing/resolving connection string: {ex.Message}");
            return connectionString;
        }
    }

    public static string ConvertPostgreSqlUriToConnectionString(string uriString)
    {
        if (string.IsNullOrEmpty(uriString) || (!uriString.StartsWith("postgres://") && !uriString.StartsWith("postgresql://")))
        {
            return uriString;
        }

        try
        {
            string raw = uriString;
            string scheme = raw.StartsWith("postgresql://") ? "postgresql://" : "postgres://";
            string remaining = raw.Substring(scheme.Length);

            int atIndex = remaining.LastIndexOf('@');
            if (atIndex == -1)
            {
                return uriString;
            }

            string credentials = remaining.Substring(0, atIndex);
            string connectionInfo = remaining.Substring(atIndex + 1);

            int colonIndex = credentials.IndexOf(':');
            string username = colonIndex == -1 ? credentials : credentials.Substring(0, colonIndex);
            string password = colonIndex == -1 ? "" : credentials.Substring(colonIndex + 1);

            username = Uri.UnescapeDataString(username);
            password = Uri.UnescapeDataString(password);

            int slashIndex = connectionInfo.IndexOf('/');
            string hostAndPort = slashIndex == -1 ? connectionInfo : connectionInfo.Substring(0, slashIndex);
            string database = slashIndex == -1 ? "" : connectionInfo.Substring(slashIndex + 1);

            int queryIndex = database.IndexOf('?');
            if (queryIndex != -1)
            {
                database = database.Substring(0, queryIndex);
            }
            database = Uri.UnescapeDataString(database);

            string host = hostAndPort;
            int port = 5432;
            int portColonIndex = hostAndPort.LastIndexOf(':');
            if (portColonIndex != -1)
            {
                host = hostAndPort.Substring(0, portColonIndex);
                string portStr = hostAndPort.Substring(portColonIndex + 1);
                int.TryParse(portStr, out port);
            }

            var builder = new Npgsql.NpgsqlConnectionStringBuilder
            {
                Host = host,
                Port = port,
                Database = database,
                Username = username,
                Password = password
            };

            return builder.ConnectionString;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DNS Debug] Exception converting URI to Connection String: {ex.Message}");
            return uriString;
        }
    }
}
