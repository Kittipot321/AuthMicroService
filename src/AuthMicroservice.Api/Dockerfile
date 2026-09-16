# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["global.json", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]

COPY ["src/AuthMicroservice.Core/AuthMicroservice.Core.csproj", "src/AuthMicroservice.Core/"]
COPY ["src/AuthMicroservice.Migrations.SqlServer/AuthMicroservice.Migrations.SqlServer.csproj", "src/AuthMicroservice.Migrations.SqlServer/"]
COPY ["src/AuthMicroservice.Migrations.Postgres/AuthMicroservice.Migrations.Postgres.csproj", "src/AuthMicroservice.Migrations.Postgres/"]
COPY ["src/AuthMicroservice.Migrations.Sqlite/AuthMicroservice.Migrations.Sqlite.csproj", "src/AuthMicroservice.Migrations.Sqlite/"]
COPY ["src/AuthMicroservice.Api/AuthMicroservice.Api.csproj", "src/AuthMicroservice.Api/"]

RUN dotnet restore "src/AuthMicroservice.Api/AuthMicroservice.Api.csproj"

COPY . .

RUN dotnet publish "src/AuthMicroservice.Api/AuthMicroservice.Api.csproj" \
    -c Release -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_EnableDiagnostics=0

USER app

ENTRYPOINT ["dotnet", "AuthMicroservice.Api.dll"]
