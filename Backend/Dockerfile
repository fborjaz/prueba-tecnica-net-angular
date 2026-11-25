# ---------- Build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restaurar primero (aprovecha la caché de capas de Docker)
COPY CashManagement.sln ./
COPY src/CashManagement.Domain/CashManagement.Domain.csproj src/CashManagement.Domain/
COPY src/CashManagement.Application/CashManagement.Application.csproj src/CashManagement.Application/
COPY src/CashManagement.Infrastructure/CashManagement.Infrastructure.csproj src/CashManagement.Infrastructure/
COPY src/CashManagement.Api/CashManagement.Api.csproj src/CashManagement.Api/
RUN dotnet restore src/CashManagement.Api/CashManagement.Api.csproj

COPY . .
RUN dotnet publish src/CashManagement.Api/CashManagement.Api.csproj -c Release -o /app/publish --no-restore

# ---------- Runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    TZ=America/Guayaquil

COPY --from=build /app/publish .
RUN mkdir -p /app/logs && chown -R app:app /app
USER app

EXPOSE 8080
ENTRYPOINT ["dotnet", "CashManagement.Api.dll"]
