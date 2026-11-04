# src/Enigma.Api/Dockerfile
# Build context: repo root (docker build -f src/Enigma.Api/Dockerfile .)

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Build.props
COPY Directory.Packages.props Directory.Packages.props
COPY src/Enigma.Shared/Enigma.Shared.csproj src/Enigma.Shared/
COPY src/Enigma.Api/Enigma.Api.csproj src/Enigma.Api/
RUN dotnet restore src/Enigma.Api/Enigma.Api.csproj

COPY src/Enigma.Shared/ src/Enigma.Shared/
COPY src/Enigma.Api/ src/Enigma.Api/
RUN dotnet publish src/Enigma.Api/Enigma.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8081
ENTRYPOINT ["dotnet", "Enigma.Api.dll"]
