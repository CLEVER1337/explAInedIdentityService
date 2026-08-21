# Identity service (:5125). Mints the JWTs every other service validates, so the Jwt:* stanza
# passed in here must match the one the other containers get.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# csproj first so a source-only change reuses the restore layer.
COPY explAInedIdentityService.csproj ./
RUN dotnet restore explAInedIdentityService.csproj

COPY . .
RUN dotnet publish explAInedIdentityService.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish ./

# Same port inside the container as on the host, so the Jwt:Issuer/Audience URLs and the
# service map in CLAUDE.md keep meaning the same thing under a 1:1 compose mapping.
ENV ASPNETCORE_HTTP_PORTS=5125
EXPOSE 5125

# Migrations auto-apply at startup (db.Database.Migrate()), so Postgres must be reachable
# before this container starts — no separate migration step.
USER $APP_UID
ENTRYPOINT ["dotnet", "explAInedIdentityService.dll"]
