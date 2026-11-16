# Build context is the repository root (see docker-compose.yml).

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, from project files only, so code changes don't invalidate the package cache.
COPY global.json Directory.Build.props ./
COPY src/StayDesk.Domain/StayDesk.Domain.csproj src/StayDesk.Domain/
COPY src/StayDesk.Application/StayDesk.Application.csproj src/StayDesk.Application/
COPY src/StayDesk.Infrastructure/StayDesk.Infrastructure.csproj src/StayDesk.Infrastructure/
COPY src/StayDesk.Api/StayDesk.Api.csproj src/StayDesk.Api/
RUN dotnet restore src/StayDesk.Api/StayDesk.Api.csproj

COPY src/ src/
RUN dotnet publish src/StayDesk.Api/StayDesk.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# The aspnet image ships a non-root user; the app listens on 8080.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "StayDesk.Api.dll"]
