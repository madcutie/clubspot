# docker build -f docker/jobs.Dockerfile -t clubspot-jobs .   (context: the repository root)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/backend/global.json src/backend/Directory.Build.props ./
COPY src/backend/src/Core/ClubSpot.SharedKernel/ClubSpot.SharedKernel.csproj src/Core/ClubSpot.SharedKernel/
COPY src/backend/src/Core/ClubSpot.Domain/ClubSpot.Domain.csproj src/Core/ClubSpot.Domain/
COPY src/backend/src/Core/ClubSpot.Application/ClubSpot.Application.csproj src/Core/ClubSpot.Application/
COPY src/backend/src/Infrastructure/ClubSpot.Infrastructure/ClubSpot.Infrastructure.csproj src/Infrastructure/ClubSpot.Infrastructure/
COPY src/backend/src/Infrastructure/ClubSpot.Infrastructure.MercadoPago/ClubSpot.Infrastructure.MercadoPago.csproj src/Infrastructure/ClubSpot.Infrastructure.MercadoPago/
COPY src/backend/src/Jobs/ClubSpot.JobService/ClubSpot.JobService.csproj src/Jobs/ClubSpot.JobService/
RUN dotnet restore src/Jobs/ClubSpot.JobService/ClubSpot.JobService.csproj

COPY src/backend/ .
RUN dotnet publish src/Jobs/ClubSpot.JobService/ClubSpot.JobService.csproj -c Release -o /app --no-restore

# aspnet only adds the web stack: J2 listens on no port. ICU and tzdata come from this image too.
FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "ClubSpot.JobService.dll"]
