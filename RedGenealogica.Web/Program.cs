// ============================================================
// Program.cs
// ============================================================

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RedGenealogica.Web.Data;
using RedGenealogica.Web.Models;
using RedGenealogica.Web.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddScoped<ServicioUsuarios>();
builder.Services.AddScoped<ServicioPagos>();
builder.Services.AddScoped<ServicioReferidos>();
builder.Services.AddScoped<ServicioRangos>();
builder.Services.AddScoped<ServicioRetiros>();
builder.Services.AddScoped<ServicioNotificaciones>();
builder.Services.AddScoped<ServicioPremios>();
builder.Services.AddScoped<ServicioCorreos>();

builder.Services.AddHttpClient();

builder.Services.AddDbContext<ContextoAplicacion>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("ConexionPrincipal")));

builder.Services.AddIdentity<Usuario, IdentityRole<int>>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedAccount = false;
    /*Suspensión automática por intentos fallidos*/
    options.Lockout.DefaultLockoutTimeSpan  = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers      = true;
})
.AddEntityFrameworkStores<ContextoAplicacion>()
.AddDefaultTokenProviders()
.AddErrorDescriber<ErroresIdentityEspanol>();

builder.Services.AddAuthentication()
    .AddGoogle(options =>
    {
        options.ClientId     = builder.Configuration["Google:ClientId"]!;
        options.ClientSecret = builder.Configuration["Google:ClientSecret"]!;
        options.CallbackPath = "/signin-google";
        options.Events.OnRedirectToAuthorizationEndpoint = context =>
        {
            var redirectUri = context.RedirectUri.Replace("http://", "https://");
            context.Response.Redirect(redirectUri);
            return Task.CompletedTask;
        };
    });

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Autenticacion/Login";
    options.AccessDeniedPath = "/Autenticacion/Login";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
});

builder.Services.AddDataProtection()
    .PersistKeysToDbContext<ContextoAplicacion>();

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("login", opt =>
    {
        opt.PermitLimit        = 10;
        opt.Window             = TimeSpan.FromMinutes(5);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit         = 0;
    });

    options.RejectionStatusCode = 429;
});


var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
    if (!await roleManager.RoleExistsAsync("Admin"))
        await roleManager.CreateAsync(new IdentityRole<int>("Admin"));
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Inicio/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
/*Headers de seguridad HTTP — el navegador no sabe que tu app no debe cargar en iframes de otros sitios, ni ejecutar scripts de orígenes externos. Un middleware agrega esto en un bloque en Program.cs. Protege contra clickjacking y XSS básico.*/
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Frame-Options"]        = "DENY";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-XSS-Protection"]       = "1; mode=block";
    context.Response.Headers["Referrer-Policy"]        = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"]     = "camera=(), microphone=(), geolocation=()";
    await next();
});

app.UseRouting();
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
});
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Inicio}/{action=Index}/{id?}");

app.Run();

// ── Errores de Identity en español ───────────────────────────
public class ErroresIdentityEspanol : IdentityErrorDescriber
{
    public override IdentityError DefaultError()
        => new() { Code = nameof(DefaultError), Description = "Ocurrió un error desconocido." };
    public override IdentityError ConcurrencyFailure()
        => new() { Code = nameof(ConcurrencyFailure), Description = "Error de concurrencia. El registro fue modificado." };
    public override IdentityError PasswordMismatch()
        => new() { Code = nameof(PasswordMismatch), Description = "La contraseña es incorrecta." };
    public override IdentityError InvalidToken()
        => new() { Code = nameof(InvalidToken), Description = "Token inválido." };
    public override IdentityError LoginAlreadyAssociated()
        => new() { Code = nameof(LoginAlreadyAssociated), Description = "Ya existe un usuario con este acceso externo." };
    public override IdentityError InvalidUserName(string? userName)
        => new() { Code = nameof(InvalidUserName), Description = $"El nombre de usuario '{userName}' no es válido. Solo se permiten letras y números." };
    public override IdentityError InvalidEmail(string? email)
        => new() { Code = nameof(InvalidEmail), Description = $"El correo electrónico '{email}' no es válido." };
    public override IdentityError DuplicateUserName(string userName)
        => new() { Code = nameof(DuplicateUserName), Description = $"Ya existe una cuenta con el usuario '{userName}'." };
    public override IdentityError DuplicateEmail(string email)
        => new() { Code = nameof(DuplicateEmail), Description = $"Ya existe una cuenta registrada con el correo '{email}'." };
    public override IdentityError InvalidRoleName(string? role)
        => new() { Code = nameof(InvalidRoleName), Description = $"El nombre de rol '{role}' no es válido." };
    public override IdentityError DuplicateRoleName(string role)
        => new() { Code = nameof(DuplicateRoleName), Description = $"El rol '{role}' ya existe." };
    public override IdentityError UserAlreadyHasPassword()
        => new() { Code = nameof(UserAlreadyHasPassword), Description = "El usuario ya tiene contraseña." };
    public override IdentityError UserLockoutNotEnabled()
        => new() { Code = nameof(UserLockoutNotEnabled), Description = "El bloqueo no está habilitado para este usuario." };
    public override IdentityError UserAlreadyInRole(string role)
        => new() { Code = nameof(UserAlreadyInRole), Description = $"El usuario ya tiene el rol '{role}'." };
    public override IdentityError UserNotInRole(string role)
        => new() { Code = nameof(UserNotInRole), Description = $"El usuario no tiene el rol '{role}'." };
    public override IdentityError PasswordTooShort(int length)
        => new() { Code = nameof(PasswordTooShort), Description = $"La contraseña debe tener al menos {length} caracteres." };
    public override IdentityError PasswordRequiresNonAlphanumeric()
        => new() { Code = nameof(PasswordRequiresNonAlphanumeric), Description = "La contraseña debe contener al menos un carácter especial." };
    public override IdentityError PasswordRequiresDigit()
        => new() { Code = nameof(PasswordRequiresDigit), Description = "La contraseña debe contener al menos un número (0-9)." };
    public override IdentityError PasswordRequiresLower()
        => new() { Code = nameof(PasswordRequiresLower), Description = "La contraseña debe contener al menos una letra minúscula." };
    public override IdentityError PasswordRequiresUpper()
        => new() { Code = nameof(PasswordRequiresUpper), Description = "La contraseña debe contener al menos una letra mayúscula." };
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars)
        => new() { Code = nameof(PasswordRequiresUniqueChars), Description = $"La contraseña debe contener al menos {uniqueChars} caracteres únicos." };
}
