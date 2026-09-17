# Etapa de compilación (Build Stage)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app
ENV CACHE_BUSTER=20260916.2

# Copiar archivos de proyectos (.csproj) y restaurar dependencias
COPY src/PrestamosCobros.Web/PrestamosCobros.Web.csproj src/PrestamosCobros.Web/
COPY src/PrestamosCobros.BLL/PrestamosCobros.BLL.csproj src/PrestamosCobros.BLL/
COPY src/PrestamosCobros.DAL/PrestamosCobros.DAL.csproj src/PrestamosCobros.DAL/
COPY src/PrestamosCobros.Infrastructure/PrestamosCobros.Infrastructure.csproj src/PrestamosCobros.Infrastructure/

RUN dotnet restore src/PrestamosCobros.Web/PrestamosCobros.Web.csproj

# Copiar todo el código y compilar la aplicación
COPY src/ src/
RUN echo "Cache buster $(date +%s)" && dotnet publish src/PrestamosCobros.Web/PrestamosCobros.Web.csproj -c Release -o /publish

# Etapa de ejecución (Runtime Stage)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV CACHE_BUSTER=20260916.2
COPY --from=build /publish .

# Configurar ASP.NET Core para escuchar en el puerto 8080
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "PrestamosCobros.Web.dll"]
