FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .

RUN ls -R

RUN dotnet restore Backend_ActiviteitenPlanner/Backend_ActiviteitenPlanner.csproj
RUN dotnet publish Backend_ActiviteitenPlanner/Backend_ActiviteitenPlanner.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Backend_ActiviteitenPlanner.dll"]