FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /source

COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/HotelBooking.Domain/HotelBooking.Domain.csproj src/HotelBooking.Domain/
COPY src/HotelBooking.Application/HotelBooking.Application.csproj src/HotelBooking.Application/
COPY src/HotelBooking.Infrastructure/HotelBooking.Infrastructure.csproj src/HotelBooking.Infrastructure/
COPY src/HotelBooking.Workers/HotelBooking.Workers.csproj src/HotelBooking.Workers/

RUN dotnet restore src/HotelBooking.Workers/HotelBooking.Workers.csproj

COPY src/ src/

RUN dotnet publish src/HotelBooking.Workers/HotelBooking.Workers.csproj \
    --configuration Release \
    --no-restore \
    --output /app

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime

WORKDIR /app

COPY --from=build /app ./

USER $APP_UID

ENTRYPOINT ["dotnet", "HotelBooking.Workers.dll"]
