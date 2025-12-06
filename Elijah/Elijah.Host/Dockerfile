# ---------- BUILD ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Elijah.sln", "./"]
COPY ["Elijah.Host/Elijah.Host.csproj", "Elijah.Host/"]
COPY ["Elijah.Logic/Elijah.Logic.csproj", "Elijah.Logic/"]
COPY ["Elijah.Data/Elijah.Data.csproj", "Elijah.Data/"]
COPY ["Elijah.Domain/Elijah.Domain.csproj", "Elijah.Domain/"]
COPY ["packages/", "/packages/"]
RUN dotnet nuget add source /packages --name LocalPkgs
RUN dotnet restore "Elijah.Host/Elijah.Host.csproj"
COPY . .
WORKDIR "/src/Elijah.Host"
RUN dotnet build "Elijah.Host.csproj" -c $BUILD_CONFIGURATION -o /app/build

# ---------- PUBLISH ----------
FROM build AS publish
RUN dotnet publish "Elijah.Host.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --self-contained true \
    -r linux-x64

# ---------- RUNTIME ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
RUN mkdir -p /app/config && chmod 777 /app/config

# Install PostgreSQL client for pg_isready
RUN apt-get update && apt-get install -y postgresql-client && rm -rf /var/lib/apt/lists/*

EXPOSE 8883
USER root

# Copy the published app
COPY --from=publish /app/publish ./

# Copy the wait script
COPY Elijah.Host/wait-for-postgres.sh ./
RUN chmod +x wait-for-postgres.sh

ENTRYPOINT ["./wait-for-postgres.sh"]



