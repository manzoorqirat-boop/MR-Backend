# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj first and restore separately (layer caching speeds up rebuilds)
COPY *.csproj .
RUN dotnet restore

# Copy the rest of the source and publish
COPY . .
RUN dotnet publish -c Release -o /app --no-restore

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .

# Railway injects PORT at runtime; Program.cs reads it and binds Kestrel accordingly.
# This ENV is just a fallback default for local `docker run`.
ENV PORT=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SiteReportApp.dll"]
