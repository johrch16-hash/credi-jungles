# =============================================
# LendSoft Pro - Script de Publicación
# Ejecutar desde la raíz del proyecto.
# =============================================

param(
    [string]$OutputPath = ".\publicado",
    [string]$Environment = "Production"
)

Write-Host "=== LendSoft Pro - Build & Publish ===" -ForegroundColor Cyan
Write-Host "Entorno: $Environment" -ForegroundColor Yellow

# 1. Restaurar paquetes
Write-Host "`n[1/4] Restaurando paquetes NuGet..." -ForegroundColor Green
dotnet restore src\PrestamosCobros.Web\PrestamosCobros.Web.csproj
if ($LASTEXITCODE -ne 0) { Write-Host "ERROR en la restauración" -ForegroundColor Red; exit 1 }

# 2. Compilar
Write-Host "`n[2/4] Compilando..." -ForegroundColor Green
dotnet build src\PrestamosCobros.Web\PrestamosCobros.Web.csproj -c Release --no-restore
if ($LASTEXITCODE -ne 0) { Write-Host "ERROR en la compilación" -ForegroundColor Red; exit 1 }

# 3. Ejecutar tests
Write-Host "`n[3/4] Ejecutando tests..." -ForegroundColor Green
dotnet test tests\PrestamosCobros.Tests\PrestamosCobros.Tests.csproj -c Release --no-build
if ($LASTEXITCODE -ne 0) { Write-Host "ERROR en los tests" -ForegroundColor Red; exit 1 }

# 4. Publicar
Write-Host "`n[4/4] Publicando en $OutputPath ..." -ForegroundColor Green
dotnet publish src\PrestamosCobros.Web\PrestamosCobros.Web.csproj `
    -c Release `
    -o $OutputPath `
    --no-restore `
    /p:EnvironmentName=$Environment

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n=== PUBLICACIÓN EXITOSA ===" -ForegroundColor Green
    Write-Host "Archivos en: $OutputPath" -ForegroundColor White
    Write-Host "`nCopie la carpeta '$OutputPath' al servidor web." -ForegroundColor Yellow
    Write-Host "Configure IIS con:"
    Write-Host "  - Application Pool: Sin código administrado (.NET CLR v4.0)"
    Write-Host "  - Ruta física: apuntar a la carpeta '$OutputPath'"
    Write-Host "  - HTTPS: obligatorio con certificado SSL válido"
} else {
    Write-Host "`nERROR en la publicación" -ForegroundColor Red
}
