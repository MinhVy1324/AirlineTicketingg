FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
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

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Hộp thư demo (Email:Enabled=false) ghi vào App_Data/outbox. Ảnh runtime có sẵn
# user không phải root tên "app"; cấp quyền ghi cho đúng thư mục này rồi hạ quyền.
RUN mkdir -p /app/App_Data/outbox && chown -R app:app /app/App_Data

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "AirlineTicketing.Web.dll"]
