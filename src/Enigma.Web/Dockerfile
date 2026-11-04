# syntax=docker/dockerfile:1
# src/Enigma.Web/Dockerfile
# Build context: repo root (docker build -f src/Enigma.Web/Dockerfile .)

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first for layer caching
COPY Directory.Build.props Directory.Build.props
COPY Directory.Packages.props Directory.Packages.props
COPY src/Enigma.Shared/Enigma.Shared.csproj src/Enigma.Shared/
COPY src/Enigma.Web/Enigma.Web.csproj src/Enigma.Web/
RUN dotnet restore src/Enigma.Web/Enigma.Web.csproj

# Copy source and publish (Blazor WASM produces statics in /app/wwwroot/)
COPY src/Enigma.Shared/ src/Enigma.Shared/
COPY src/Enigma.Web/ src/Enigma.Web/
RUN dotnet publish src/Enigma.Web/Enigma.Web.csproj -c Release -o /app --no-restore

FROM caddy:alpine
COPY --from=build /app/wwwroot /srv
EXPOSE 80
