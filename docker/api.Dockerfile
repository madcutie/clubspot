# docker build -f docker/api.Dockerfile -t clubspot-api .   (context: the repository root)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Project files first: restore is the slow layer and it only has to run again when a reference changes.
COPY src/backend/global.json src/backend/Directory.Build.props ./
COPY src/backend/src/Core/ClubSpot.SharedKernel/ClubSpot.SharedKernel.csproj src/Core/ClubSpot.SharedKernel/
COPY src/backend/src/Core/ClubSpot.Domain/ClubSpot.Domain.csproj src/Core/ClubSpot.Domain/
COPY src/backend/src/Core/ClubSpot.Application/ClubSpot.Application.csproj src/Core/ClubSpot.Application/
COPY src/backend/src/Infrastructure/ClubSpot.Infrastructure/ClubSpot.Infrastructure.csproj src/Infrastructure/ClubSpot.Infrastructure/
COPY src/backend/src/Infrastructure/ClubSpot.Infrastructure.MercadoPago/ClubSpot.Infrastructure.MercadoPago.csproj src/Infrastructure/ClubSpot.Infrastructure.MercadoPago/
COPY src/backend/src/Api/ClubSpot.Api/ClubSpot.Api.csproj src/Api/ClubSpot.Api/
RUN dotnet restore src/Api/ClubSpot.Api/ClubSpot.Api.csproj

COPY src/backend/ .
# The OpenAPI contract is a build output written back into the repo (ADR-0016), and there is no repo
# here: the export would run the app to write a file nothing in the image reads.
RUN dotnet publish src/Api/ClubSpot.Api/ClubSpot.Api.csproj -c Release -o /app --no-restore \
    -p:ExportOpenApiDocument=false

# InvariantGlobalization=false needs ICU and the club's time zone needs tzdata. This image carries
# both; the -alpine variant carries neither and the app dies at startup without naming either one.
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# No TZ on purpose: every business day is resolved in the club's own zone (ClubCalendar), so the
# container stays on UTC and its clock can never silently shift a day.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "ClubSpot.Api.dll"]
