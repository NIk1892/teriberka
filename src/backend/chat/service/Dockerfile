FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
RUN apt-get update && apt-get install -y --no-install-recommends libkrb5-3 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
ENV DOTNET_SYSTEM_GLOBALIZATION_PREDEFINED_CULTURES_ONLY=false \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy Directory.Build.props files
COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
COPY ["src/backend/Directory.Build.props", "backend/"]

# Copy all csproj files for restore
COPY ["src/backend/chat/service/Chat.csproj", "backend/chat/service/"]
COPY ["src/backend/chat/modules/Chat.Application/Chat.Application.csproj", "backend/chat/modules/Chat.Application/"]
COPY ["src/backend/chat/modules/Chat.Infrastructure/Chat.Infrastructure.csproj", "backend/chat/modules/Chat.Infrastructure/"]
COPY ["src/backend/chat/modules/Chat.Domain/Chat.Domain.csproj", "backend/chat/modules/Chat.Domain/"]
COPY ["src/backend/shared/Api/Api.csproj", "backend/shared/Api/"]
COPY ["src/backend/shared/OTLP/OTLP.csproj", "backend/shared/OTLP/"]
COPY ["src/backend/shared/Application/Application.csproj", "backend/shared/Application/"]
COPY ["src/backend/shared/Domain/Domain.csproj", "backend/shared/Domain/"]
COPY ["src/backend/shared/Infrastructure/Infrastructure.csproj", "backend/shared/Infrastructure/"]
COPY ["src/contracts/Contracts/Contracts.csproj", "contracts/Contracts/"]
COPY ["src/contracts/Chat.Contracts/Chat.Contracts.csproj", "contracts/Chat.Contracts/"]

RUN dotnet restore "backend/chat/service/Chat.csproj"

# Copy everything else
COPY src/ .

WORKDIR "/src/backend/chat/service"
RUN dotnet build "Chat.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Chat.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Chat.dll"]
