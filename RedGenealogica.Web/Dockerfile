# ── Etapa 1: build ──────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copiar csproj y restaurar dependencias primero (mejor cache)
COPY RedGenealogica.Web/RedGenealogica.Web.csproj RedGenealogica.Web/
RUN dotnet restore RedGenealogica.Web/RedGenealogica.Web.csproj

# Copiar todo el código y publicar
COPY RedGenealogica.Web/ RedGenealogica.Web/
WORKDIR /src/RedGenealogica.Web
RUN dotnet publish RedGenealogica.Web.csproj -c Release -o /app/publish --no-restore

# ── Etapa 2: runtime ─────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Crear carpeta para PDFs subidos (Railway tiene filesystem efímero,
# pero sirve para pruebas. Para producción real usar S3/Cloudflare R2)
RUN mkdir -p wwwroot/pdfs

COPY --from=build /app/publish .

# Railway asigna el puerto via variable PORT
ENV ASPNETCORE_URLS=http://+:${PORT:-8080}
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "RedGenealogica.Web.dll"]