FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY AirlineTicketing.Web/AirlineTicketing.Web.csproj AirlineTicketing.Web/
COPY AirlineTicketing.BUS/AirlineTicketing.BUS.csproj AirlineTicketing.BUS/
COPY AirlineTicketing.DAL/AirlineTicketing.DAL.csproj AirlineTicketing.DAL/
COPY AirlineTicketing.DTO/AirlineTicketing.DTO.csproj AirlineTicketing.DTO/
COPY AirlineTicketing.Domain/AirlineTicketing.Domain.csproj AirlineTicketing.Domain/
RUN dotnet restore AirlineTicketing.Web/AirlineTicketing.Web.csproj

COPY . .
RUN dotnet publish AirlineTicketing.Web/AirlineTicketing.Web.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "AirlineTicketing.Web.dll"]
