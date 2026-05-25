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

COPY --from=build /app/publish .

# Crear carpeta para PDFs DESPUÉS del COPY para que no se pise
RUN mkdir -p /app/storage/pdfs && chmod 777 /app/storage/pdfs

# Railway asigna el puerto via variable PORT
ENV ASPNETCORE_URLS=http://+:${PORT:-8080}
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "RedGenealogica.Web.dll"]
