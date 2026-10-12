<#
.SYNOPSIS
    Script rápido para commit y push en Windows (PowerShell)
.DESCRIPTION
    Hace git add, commit con mensaje convencional, y push al remoto.
    Uso: .\commit-push.ps1 "tipo: mensaje" [rama]
.EJEMPLOS
    .\commit-push.ps1 "feat: agregar filtro por estado en habitaciones"
    .\commit-push.ps1 "fix: corregir precio total en detalles" bedrooms-browser
#>

param(
    [Parameter(Mandatory=$true, Position=0)]
    [string]$Mensaje,

    [Parameter(Position=1)]
    [string]$Rama = (git branch --show-current),

    [switch]$ForcePush
)

# Colores para output
$Green  = [ConsoleColor]::Green
$Yellow = [ConsoleColor]::Yellow
$Red    = [ConsoleColor]::Red
$Cyan   = [ConsoleColor]::Cyan
$Reset  = [ConsoleColor]::Gray

function Write-Color($msg, $color) {
    Write-Host $msg -ForegroundColor $color
}

# Validar mensaje conventional commits
$patron = '^(feat|fix|chore|docs|refactor|test|style|perf|ci|build|revert)(\(.+\))?: .+'
if ($Mensaje -notmatch $patron) {
    Write-Color "⚠️  Advertencia: El mensaje no sigue Conventional Commits." $Yellow
    Write-Color "   Formato: tipo(alcance): descripción" $Yellow
    Write-Color "   Tipos: feat, fix, chore, docs, refactor, test, style, perf, ci, build, revert" $Yellow
    $confirm = Read-Host "¿Continuar de todos modos? (s/N)"
    if ($confirm -notmatch '^[sS]$') { exit 1 }
}

Write-Color "=== Commit & Push Script ===" $Cyan
Write-Color "Rama actual: $Rama" $Cyan
Write-Color "Mensaje: $Mensaje" $Cyan
Write-Color "" $Reset

# 1. git status
Write-Color "📋 Estado del repo:" $Yellow
git status --short

# 2. git add -A
Write-Color "`n➕ Agregando cambios..." $Yellow
git add -A

# 3. git commit
Write-Color "`n💾 Creando commit..." $Yellow
try {
    git commit -m $Mensaje
    Write-Color "✅ Commit creado" $Green
} catch {
    Write-Color "❌ Error en commit (¿nada para commitear?)" $Red
    exit 1
}

# 4. git push
Write-Color "`n🚀 Push a remoto (rep-origin/$Rama)..." $Yellow
$pushArgs = @("push", "rep-origin", $Rama)
if ($ForcePush) { $pushArgs += "--force-with-lease" }
$pushResult = git $pushArgs

if ($LASTEXITCODE -eq 0) {
    Write-Color "✅ Push exitoso" $Green
} else {
    Write-Color "❌ Push falló:" $Red
    Write-Color $pushResult $Red
    Write-Color "`n💡 Prueba: .\commit-push.ps1 '$Mensaje' $Rama -ForcePush" $Yellow
    exit 1
}

Write-Color "`n=== Listo ===" $Green