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
RUN dotnet publish "Elijah.Host.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# ---------- RUNTIME ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
RUN mkdir -p /app/config && chmod 777 /app/config
EXPOSE 8883
USER root
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Elijah.Host.dll"]