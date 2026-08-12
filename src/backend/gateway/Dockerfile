FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
ENV ASPNETCORE_URLS=http://*:8080
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy Directory.Build.props files
COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
COPY ["src/backend/Directory.Build.props", "backend/"]

# Copy csproj files for restore
COPY ["src/backend/gateway/Gateway.csproj", "backend/gateway/"]
COPY ["src/backend/shared/Api/Api.csproj", "backend/shared/Api/"]
COPY ["src/backend/shared/Domain/Domain.csproj", "backend/shared/Domain/"]
COPY ["src/backend/shared/OTLP/OTLP.csproj", "backend/shared/OTLP/"]
COPY ["src/backend/shared/Infrastructure/Infrastructure.csproj", "backend/shared/Infrastructure/"]
COPY ["src/backend/shared/Application/Application.csproj", "backend/shared/Application/"]
COPY ["src/contracts/Contracts/Contracts.csproj", "contracts/Contracts/"]

RUN dotnet restore "backend/gateway/Gateway.csproj"

# Copy everything else
COPY src/ .

WORKDIR "/src/backend/gateway"
RUN dotnet build "Gateway.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Gateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Gateway.dll"]