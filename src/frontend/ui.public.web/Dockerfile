FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]

COPY ["src/frontend/ui.public.web/UI.Public.Web.csproj", "frontend/ui.public.web/"]
COPY ["src/frontend/ui.shared/UI.Shared.csproj", "frontend/ui.shared/"]
COPY ["src/contracts/Contracts/Contracts.csproj", "contracts/Contracts/"]
COPY ["src/contracts/Applications.Contracts/Applications.Contracts.csproj", "contracts/Applications.Contracts/"]
COPY ["src/backend/shared/Domain/Domain.csproj", "backend/shared/Domain/"]

RUN dotnet restore "frontend/ui.public.web/UI.Public.Web.csproj"

COPY src/ .

WORKDIR "/src/frontend/ui.public.web"
RUN dotnet publish "UI.Public.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://*:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "UI.Public.Web.dll"]
