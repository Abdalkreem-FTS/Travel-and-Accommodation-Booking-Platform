FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /source

COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/HotelBooking.Gateway/HotelBooking.Gateway.csproj src/HotelBooking.Gateway/

RUN dotnet restore src/HotelBooking.Gateway/HotelBooking.Gateway.csproj

COPY src/HotelBooking.Gateway/ src/HotelBooking.Gateway/

RUN dotnet publish src/HotelBooking.Gateway/HotelBooking.Gateway.csproj \
    --configuration Release \
    --no-restore \
    --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY --from=build /app ./

USER $APP_UID

EXPOSE 8080

ENTRYPOINT ["dotnet", "HotelBooking.Gateway.dll"]
