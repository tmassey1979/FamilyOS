# Family OS API — multi-stage build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY FamilyOS.sln ./
COPY src/FamilyOS.Contracts/FamilyOS.Contracts.csproj src/FamilyOS.Contracts/
COPY src/FamilyOS.Domain/FamilyOS.Domain.csproj src/FamilyOS.Domain/
COPY src/FamilyOS.Application/FamilyOS.Application.csproj src/FamilyOS.Application/
COPY src/FamilyOS.Infrastructure/FamilyOS.Infrastructure.csproj src/FamilyOS.Infrastructure/
COPY src/FamilyOS.Api/FamilyOS.Api.csproj src/FamilyOS.Api/
COPY tests/FamilyOS.Domain.Tests/FamilyOS.Domain.Tests.csproj tests/FamilyOS.Domain.Tests/

RUN dotnet restore src/FamilyOS.Api/FamilyOS.Api.csproj

COPY src/ src/
COPY tests/ tests/

ARG VERSION=0.1.0
RUN dotnet publish src/FamilyOS.Api/FamilyOS.Api.csproj -c Release -o /app/publish \
    -p:Version=$VERSION --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5080
EXPOSE 5080

HEALTHCHECK --interval=10s --timeout=5s --start-period=40s --retries=5 \
  CMD curl -fsS http://127.0.0.1:5080/health || exit 1

ENTRYPOINT ["dotnet", "FamilyOS.Api.dll"]
