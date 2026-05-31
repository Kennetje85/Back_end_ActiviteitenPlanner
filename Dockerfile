# Multi-stage Dockerfile for .NET 8 (build from repo root)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy only the project file first to leverage layer caching
COPY ["Backend_ActiviteitenPlanner/Backend_ActiviteitenPlanner.csproj", "Backend_ActiviteitenPlanner/"]
RUN dotnet restore "/Backend_ActiviteitenPlanner.csproj"

# Copy rest of the repo and publish
COPY . .
WORKDIR "/src/Backend_ActiviteitenPlanner"
RUN dotnet publish "Backend_ActiviteitenPlanner.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:80

COPY --from=build /app/publish .
EXPOSE 80

ENTRYPOINT ["dotnet", "Backend_ActiviteitenPlanner.dll"]