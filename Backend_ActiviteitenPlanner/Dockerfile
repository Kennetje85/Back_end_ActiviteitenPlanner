FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj first (for caching)
COPY Backend_ActiviteitenPlanner/*.csproj Backend_ActiviteitenPlanner/

RUN dotnet restore Backend_ActiviteitenPlanner/Backend_ActiviteitenPlanner.csproj

# Copy everything
COPY . .

WORKDIR /src/Backend_ActiviteitenPlanner

RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 80

ENTRYPOINT ["dotnet", "Backend_ActiviteitenPlanner.dll"]