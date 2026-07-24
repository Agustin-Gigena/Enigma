# Server/Dockerfile
# Build context: repo root (docker build -f Server/Dockerfile .)

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first for layer caching (restore is cached unless csproj changes)
COPY Shared/Enigma.Shared.csproj Shared/
COPY Server/Enigma.Server.csproj Server/
RUN dotnet restore Server/Enigma.Server.csproj

# Copy source and publish
COPY Shared/ Shared/
COPY Server/ Server/
RUN dotnet publish Server/Enigma.Server.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Enigma.Server.dll"]
