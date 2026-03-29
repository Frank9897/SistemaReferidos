// ============================================================
// Program.cs
// Ubicación: Program.cs
//
// CAMBIO: agrega ServicioRetiros al contenedor de dependencias.
// ============================================================

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RedGenealogica.Web.Data;
using RedGenealogica.Web.Models;
using RedGenealogica.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// Servicios de negocio
builder.Services.AddScoped<ServicioUsuarios>();
builder.Services.AddScoped<ServicioPagos>();
builder.Services.AddScoped<ServicioReferidos>();
builder.Services.AddScoped<ServicioRangos>();
builder.Services.AddScoped<ServicioRetiros>();   // NUEVO

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
})
.AddEntityFrameworkStores<ContextoAplicacion>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Autenticacion/Login";
    options.AccessDeniedPath = "/Autenticacion/Login";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
});

var app = builder.Build();

// Seed del rol Admin al arrancar
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
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Inicio}/{action=Index}/{id?}");

app.Run();
