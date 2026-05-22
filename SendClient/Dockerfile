FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Nur das Projektfile kopieren
COPY ["SendClient.csproj", "./"]

# Restore
RUN dotnet restore "./SendClient.csproj"

# Restlichen Code kopieren
COPY . .

# Build
RUN dotnet build "./SendClient.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./SendClient.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app

# Publish-Output kopieren
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "SendClient.dll"]
