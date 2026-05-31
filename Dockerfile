# Multi-stage Dockerfile for .NET 8
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file (correct pad!)
COPY Backend_ActiviteitenPlanner.csproj Backend_ActiviteitenPlanner/

# Restore
RUN dotnet restore Backend_ActiviteitenPlanner.csproj

# Copy rest of the repo
COPY . .

# Move into project folder
WORKDIR /src/Backend_ActiviteitenPlanner

# Publish
RUN dotnet publish Backend_ActiviteitenPlanner.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:80

COPY --from=build /app/publish .

EXPOSE 80

ENTRYPOINT ["dotnet", "Backend_ActiviteitenPlanner.dll"]