FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
RUN apt-get update && apt-get install -y --no-install-recommends libkrb5-3 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
ENV DOTNET_SYSTEM_GLOBALIZATION_PREDEFINED_CULTURES_ONLY=false \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy Directory.Build.props files
COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
COPY ["src/backend/Directory.Build.props", "backend/"]

# Copy all csproj files for restore
COPY ["src/backend/users/service/Users.csproj", "backend/users/service/"]
COPY ["src/backend/users/modules/Users.Application/Users.Application.csproj", "backend/users/modules/Users.Application/"]
COPY ["src/backend/users/modules/Users.Infrastructure/Users.Infrastructure.csproj", "backend/users/modules/Users.Infrastructure/"]
COPY ["src/backend/users/modules/Users.Domain/Users.Domain.csproj", "backend/users/modules/Users.Domain/"]
COPY ["src/backend/shared/Api/Api.csproj", "backend/shared/Api/"]
COPY ["src/backend/shared/OTLP/OTLP.csproj", "backend/shared/OTLP/"]
COPY ["src/backend/shared/Application/Application.csproj", "backend/shared/Application/"]
COPY ["src/backend/shared/Domain/Domain.csproj", "backend/shared/Domain/"]
COPY ["src/backend/shared/Infrastructure/Infrastructure.csproj", "backend/shared/Infrastructure/"]
COPY ["src/backend/shared/Rpc/Rpc.csproj", "backend/shared/Rpc/"]
COPY ["src/backend/shared/MessageBus/MessageBus.csproj", "backend/shared/MessageBus/"]
COPY ["src/contracts/Contracts/Contracts.csproj", "contracts/Contracts/"]
COPY ["src/contracts/Users.Contracts/Users.Contracts.csproj", "contracts/Users.Contracts/"]
COPY ["src/aspire/Teriberka.ServiceDefaults/Teriberka.ServiceDefaults.csproj", "aspire/Teriberka.ServiceDefaults/"]

RUN dotnet restore "backend/users/service/Users.csproj"

# Copy everything else
COPY src/ .

WORKDIR "/src/backend/users/service"
RUN dotnet build "Users.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Users.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Users.dll"]
