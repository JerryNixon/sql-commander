# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props .
COPY SqlCmdr.Library/SqlCmdr.Library.csproj SqlCmdr.Library/
COPY SqlCmdr.Web/SqlCmdr.Web.csproj SqlCmdr.Web/
RUN dotnet restore SqlCmdr.Web/SqlCmdr.Web.csproj
RUN dotnet tool install --tool-path /dab-tools Microsoft.DataApiBuilder --version 2.0.8

COPY . .
RUN dotnet publish SqlCmdr.Web/SqlCmdr.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_EnableDiagnostics=0
ENV SQLCMDR_FILE_LOG=0
ENV PATH="/opt/dab-tools:${PATH}"
LABEL org.opencontainers.image.title="sql-commander" \
      org.opencontainers.image.description="Lightweight web-based SQL Server exploration tool" \
      org.opencontainers.image.version="1.1.0" \
      org.opencontainers.image.source="https://github.com/JerryNixon/sql-commander" \
      org.opencontainers.image.licenses="MIT"
RUN adduser --disabled-password --gecos "app" appuser
COPY --from=build --chown=appuser:appuser /app/publish .
COPY --from=build /dab-tools /opt/dab-tools
USER appuser
EXPOSE 8080
ENTRYPOINT ["dotnet", "SqlCmdr.Web.dll"]
