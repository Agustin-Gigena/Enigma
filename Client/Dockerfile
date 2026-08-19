# syntax=docker/dockerfile:1
# Client/Dockerfile
# Build context: repo root (docker build -f Client/Dockerfile .)

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first for layer caching
COPY Shared/Enigma.Shared.csproj Shared/
COPY Client/Enigma.Client.csproj Client/
RUN dotnet restore Client/Enigma.Client.csproj

# Copy source and publish (Blazor WASM produces statics in /app/wwwroot/)
COPY Shared/ Shared/
COPY Client/ Client/
RUN dotnet publish Client/Enigma.Client.csproj -c Release -o /app --no-restore

FROM caddy:alpine
COPY --from=build /app/wwwroot /srv
EXPOSE 80
