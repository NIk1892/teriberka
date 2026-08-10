FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
RUN apk add --no-cache icu-libs
WORKDIR /app
ENV ASPNETCORE_URLS=http://*:8080 \
    DOTNET_SYSTEM_GLOBALIZATION_PREDEFINED_CULTURES_ONLY=false \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
RUN apk add --no-cache libc6-compat
WORKDIR /src

# Copy Directory.Build.props files
COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
COPY ["src/backend/Directory.Build.props", "backend/"]

# Copy all csproj files for restore
COPY ["src/backend/files/service/Files.csproj", "backend/files/service/"]
COPY ["src/backend/files/modules/Files.Application/Files.Application.csproj", "backend/files/modules/Files.Application/"]
COPY ["src/backend/files/modules/Files.Infrastructure/Files.Infrastructure.csproj", "backend/files/modules/Files.Infrastructure/"]
COPY ["src/backend/files/modules/Files.Domain/Files.Domain.csproj", "backend/files/modules/Files.Domain/"]
COPY ["src/backend/shared/Api/Api.csproj", "backend/shared/Api/"]
COPY ["src/backend/shared/OTLP/OTLP.csproj", "backend/shared/OTLP/"]
COPY ["src/backend/shared/Application/Application.csproj", "backend/shared/Application/"]
COPY ["src/backend/shared/Domain/Domain.csproj", "backend/shared/Domain/"]
COPY ["src/backend/shared/Infrastructure/Infrastructure.csproj", "backend/shared/Infrastructure/"]
COPY ["src/backend/shared/Rpc/Rpc.csproj", "backend/shared/Rpc/"]
COPY ["src/backend/shared/MessageBus/MessageBus.csproj", "backend/shared/MessageBus/"]
COPY ["src/contracts/Contracts/Contracts.csproj", "contracts/Contracts/"]
COPY ["src/contracts/Files.Contracts/Files.Contracts.csproj", "contracts/Files.Contracts/"]
COPY ["src/aspire/Teriberka.ServiceDefaults/Teriberka.ServiceDefaults.csproj", "aspire/Teriberka.ServiceDefaults/"]

RUN dotnet restore "backend/files/service/Files.csproj"

# Copy everything else
COPY src/ .

WORKDIR "/src/backend/files/service"
RUN dotnet build "Files.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Files.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Files.dll"]