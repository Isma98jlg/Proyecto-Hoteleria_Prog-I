# Imagen de la aplicación para Render (o cualquier host de contenedores).
# La configuración y los secretos llegan por variables de entorno, nunca en la imagen.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Hoteleria-App/Hoteleria-App.csproj Hoteleria-App/
RUN dotnet restore Hoteleria-App/Hoteleria-App.csproj
COPY Hoteleria-App/ Hoteleria-App/
RUN dotnet publish Hoteleria-App/Hoteleria-App.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
# ML.NET (LightGBM y factorización de matrices) necesita OpenMP en Linux.
RUN apt-get update && apt-get install -y --no-install-recommends libgomp1 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_ENVIRONMENT=Production
# Render envía el tráfico al puerto 10000 por defecto.
ENV ASPNETCORE_HTTP_PORTS=10000
EXPOSE 10000
USER $APP_UID
ENTRYPOINT ["dotnet", "Hoteleria-App.dll"]
