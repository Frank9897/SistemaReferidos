// ============================================================
// Program.cs
// Ubicación: Program.cs (raíz del proyecto)
//
// CORRECCIONES APLICADAS:
//   [MEJORA-1] Se agrega seed del rol "Admin" al iniciar la aplicación.
//              Sin esto, AdministradorController ([Authorize(Roles="Admin")])
//              nunca funciona porque el rol no existe en la base de datos.
//
//   [MEJORA-2] HttpClient del ServicioPagos se registra como IHttpClientFactory
//              en lugar de instanciarse con new HttpClient() dentro del servicio.
//              Esto evita agotamiento de sockets (socket exhaustion) en producción.
//
// NOTA: El primer usuario Admin debe asignarse manualmente con:
//       UPDATE "AspNetUserRoles" SET ...
//       o mediante el panel de admin una vez que esté implementado.
// ============================================================

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RedGenealogica.Web.Data;
using RedGenealogica.Web.Models;
using RedGenealogica.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();

// Servicios de negocio
builder.Services.AddScoped<ServicioUsuarios>();
builder.Services.AddScoped<ServicioPagos>();
builder.Services.AddScoped<ServicioReferidos>();
builder.Services.AddScoped<ServicioRangos>();

// [MEJORA-2] HttpClientFactory para ServicioPagos (evita socket exhaustion)
builder.Services.AddHttpClient();

// Base de datos PostgreSQL
builder.Services.AddDbContext<ContextoAplicacion>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("ConexionPrincipal")));

// Identity con roles
builder.Services.AddIdentity<Usuario, IdentityRole<int>>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;

    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ContextoAplicacion>()
.AddDefaultTokenProviders();

// Configuración de la cookie de autenticación
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Autenticacion/Login";
    options.AccessDeniedPath = "/Autenticacion/Login";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
});

var app = builder.Build();

// ----------------------------------------------------------------
// [MEJORA-1] Seed del rol Admin y datos iniciales
// Se ejecuta una sola vez al arrancar. Si el rol ya existe, no hace nada.
// ----------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();

    // Crea el rol Admin si no existe
    if (!await roleManager.RoleExistsAsync("Admin"))
    {
        await roleManager.CreateAsync(new IdentityRole<int>("Admin"));
    }

    // Podrías agregar aquí la creación del primer admin si no existe:
    // var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
    // var adminEmail = builder.Configuration["Admin:Email"];
    // var adminExistente = await userManager.FindByEmailAsync(adminEmail);
    // if (adminExistente == null) { ... crear admin ... }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Inicio/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Inicio}/{action=Index}/{id?}");

app.Run();
