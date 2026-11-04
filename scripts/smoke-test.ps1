param(
    [string]$BaseUrl = "http://localhost:5003"
)

$ErrorActionPreference = "Stop"
$passed = [System.Collections.Generic.List[string]]::new()
$createdUserId = $null
$createdReservationId = $null
$createdRoomId = $null
$createdFloorId = $null
$createdFloorRef = $null
$createdDocument = $null
$admin = $null

function Assert-Check([bool]$condition, [string]$name, [string]$detail = "") {
    if (-not $condition) { throw "FALLO: $name. $detail" }
    $script:passed.Add($name)
    Write-Host "PASS  $name" -ForegroundColor Green
}

function New-TestClient {
    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.AllowAutoRedirect = $false
    $handler.CookieContainer = [System.Net.CookieContainer]::new()
    return [pscustomobject]@{
        Handler = $handler
        Http = [System.Net.Http.HttpClient]::new($handler)
        Cookies = $handler.CookieContainer
    }
}

function Send-Request($client, [string]$method, [string]$path, $fields = $null) {
    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::new($method), "$($script:BaseUrl)$path")
    if ($null -ne $fields) {
        $pairs = [System.Collections.Generic.List[System.Collections.Generic.KeyValuePair[string,string]]]::new()
        foreach ($key in $fields.Keys) {
            foreach ($value in @($fields[$key])) {
                $pairs.Add([System.Collections.Generic.KeyValuePair[string,string]]::new([string]$key, [string]$value))
            }
        }
        $request.Content = [System.Net.Http.FormUrlEncodedContent]::new($pairs)
    }
    $response = $client.Http.SendAsync($request).GetAwaiter().GetResult()
    $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    $location = if ($response.Headers.Location) { $response.Headers.Location.ToString() } else { "" }
    return [pscustomobject]@{ Status = [int]$response.StatusCode; Body = $body; Location = $location }
}

function Get-Page($client, [string]$path) { Send-Request $client "GET" $path }

function Get-Token([string]$html) {
    $match = [regex]::Match($html, '<input[^>]*name="__RequestVerificationToken"[^>]*value="([^"]+)"')
    if (-not $match.Success) { throw "No se encontró token antifalsificación en el formulario." }
    return [System.Net.WebUtility]::HtmlDecode($match.Groups[1].Value)
}

function Post-Form($client, [string]$path, [string]$html, [System.Collections.IDictionary]$fields) {
    $post = [ordered]@{}
    foreach ($key in $fields.Keys) { $post[$key] = $fields[$key] }
    $post["__RequestVerificationToken"] = Get-Token $html
    Send-Request $client "POST" $path $post
}

function Login([string]$email, [string]$password) {
    $client = New-TestClient
    $page = Get-Page $client "/Account/Login"
    if ($page.Status -ne 200) { throw "Login GET devolvió HTTP $($page.Status)." }
    $result = Post-Form $client "/Account/Login?returnUrl=%2F" $page.Body ([ordered]@{
        "Input.Email" = $email
        "Input.Password" = $password
        "Input.RememberMe" = "false"
    })
    return [pscustomobject]@{ Client = $client; Response = $result }
}

function Read-DotEnv([string]$key, [string]$default) {
    $envPath = Join-Path $PSScriptRoot "..\.env"
    if (Test-Path $envPath) {
        $line = Get-Content $envPath | Where-Object { $_ -match "^\s*$([regex]::Escape($key))=" } | Select-Object -First 1
        if ($line) { return (($line -split "=", 2)[1]).Trim().Trim('"').Trim("'") }
    }
    return $default
}

function Invoke-CleanupSql([string]$query) {
    $dbUser = Read-DotEnv "DB_USER" "sa"
    $dbPassword = Read-DotEnv "DB_PASSWORD" ""
    if ([string]::IsNullOrWhiteSpace($dbPassword)) { throw "DB_PASSWORD no está disponible en .env para limpiar los datos temporales." }
    $query = "SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; SET ARITHABORT ON; SET CONCAT_NULL_YIELDS_NULL ON; SET NUMERIC_ROUNDABORT OFF; $query"
    $output = & docker exec hoteleria-mssql /opt/mssql-tools18/bin/sqlcmd -S localhost -U $dbUser -P $dbPassword -C -d HOTEL -b -Q $query 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Falló la limpieza SQL temporal: $($output -join ' ')" }
}

try {
    $script:BaseUrl = $BaseUrl.TrimEnd('/')
    $anon = New-TestClient
    $loginPage = Get-Page $anon "/Account/Login"
    Assert-Check ($loginPage.Status -eq 200 -and $loginPage.Body.Contains("Iniciar sesión")) "La página de login carga para usuarios anónimos"

    $badLogin = Post-Form $anon "/Account/Login?returnUrl=%2F" $loginPage.Body ([ordered]@{
        "Input.Email" = (Read-DotEnv "ADMIN_EMAIL" "admin@hoteleria.com")
        "Input.Password" = "incorrecta-smoke"
        "Input.RememberMe" = "false"
    })
    Assert-Check ($badLogin.Status -eq 200 -and $badLogin.Body.Contains("incorrectos")) "Credenciales inválidas se rechazan"

    $adminLogin = Login (Read-DotEnv "ADMIN_EMAIL" "admin@hoteleria.com") (Read-DotEnv "ADMIN_PASSWORD" "Admin123!")
    $admin = $adminLogin.Client
    Assert-Check ($adminLogin.Response.Status -eq 302 -and $adminLogin.Response.Location -eq "/") "Admin inicia sesión y conserva ReturnUrl permitido"
    Assert-Check ($admin.Cookies.GetCookies([uri]$BaseUrl)[".AspNetCore.Identity.Application"] -ne $null) "Identity emite cookie de autenticación"
    foreach ($path in @("/", "/Habitaciones", "/Reservas", "/Usuarios", "/Chat")) {
        $page = Get-Page $admin $path
        Assert-Check ($page.Status -eq 200) "Admin puede abrir $path" "HTTP $($page.Status)"
        if ($path -eq "/") { Assert-Check ($page.Body.Contains('[Autenticación] Usuario autenticado') -and $page.Body.Contains('"cargo":"admin"')) "La consola recibe el cargo autenticado en Development" }
    }
    $adminLayout = Get-Page $admin "/Reservas"
    Assert-Check ($adminLayout.Body.Contains('aria-label="Chat operativo, todavía no habilitado"') -and -not $adminLayout.Body.Contains('>notifications</span>')) "Admin ve el acceso informativo al chat y ya no ve la campana"
    Assert-Check ($adminLayout.Body.Contains('id="reservationModal"') -and ([regex]::Matches($adminLayout.Body, 'onclick="abrirModalReserva\(\)"').Count -eq 1)) "Reservas muestra un único disparador superior y el modal integrado"

    $workerLogin = Login (Read-DotEnv "DEMO_WORKER_EMAIL" "trabajador@hoteleria.com") (Read-DotEnv "DEMO_WORKER_PASSWORD" "Worker123!")
    $worker = $workerLogin.Client
    Assert-Check ($workerLogin.Response.Status -eq 302 -and $workerLogin.Response.Location.StartsWith("/Reservas")) "Trabajador inicia sesión y recibe destino compatible con su cargo" "HTTP $($workerLogin.Response.Status), Location '$($workerLogin.Response.Location)'"
    foreach ($path in @("/Habitaciones", "/Reservas", "/Usuarios")) {
        $page = Get-Page $worker $path
        Assert-Check ($page.Status -eq 200) "Trabajador puede abrir $path" "HTTP $($page.Status)"
    }
    $workerLayout = Get-Page $worker "/Habitaciones"
    Assert-Check ($workerLayout.Body.Contains('aria-label="Chat operativo, todavía no habilitado"') -and -not $workerLayout.Body.Contains('>notifications</span>')) "Trabajador ve el acceso informativo al chat y ya no ve la campana"
    $workerCreateGuest = Get-Page $worker "/Usuarios/Crear"
    Assert-Check ($workerCreateGuest.Status -eq 302 -and $workerCreateGuest.Location.Contains("AccessDenied")) "Trabajador no puede crear huéspedes sin crud_usuarios"
    $workerDashboard = Get-Page $worker "/"
    Assert-Check ($workerDashboard.Status -eq 302 -and $workerDashboard.Location.Contains("AccessDenied")) "Trabajador no puede abrir el dashboard de admin"
    Assert-Check ((Get-Page $worker "/Account/AccessDenied").Status -eq 200) "Página de acceso denegado está disponible"

    $userLogin = Login (Read-DotEnv "DEMO_USER_EMAIL" "usuario@hoteleria.com") (Read-DotEnv "DEMO_USER_PASSWORD" "User123!")
    $user = $userLogin.Client
    Assert-Check ($userLogin.Response.Status -eq 302 -and $userLogin.Response.Location.StartsWith("/Habitaciones")) "Usuario inicia sesión y aterriza en una sección permitida" "HTTP $($userLogin.Response.Status), Location '$($userLogin.Response.Location)'"
    foreach ($path in @("/Habitaciones", "/Usuarios", "/Chat")) {
        $page = Get-Page $user $path
        Assert-Check ($page.Status -eq 200) "Usuario puede consultar $path" "HTTP $($page.Status)"
    }
    $userLayout = Get-Page $user "/Habitaciones"
    Assert-Check ($userLayout.Body.Contains('aria-label="Chat operativo, todavía no habilitado"') -and -not $userLayout.Body.Contains('>notifications</span>')) "Usuario ve el acceso informativo al chat y ya no ve la campana"
    $userReservations = Get-Page $user "/Reservas"
    Assert-Check ($userReservations.Status -eq 302 -and $userReservations.Location.Contains("AccessDenied")) "Usuario no puede abrir reservas sin crud_requerimientos"
    $userCreateRoom = Get-Page $user "/Habitaciones/Crear"
    Assert-Check ($userCreateRoom.Status -eq 302 -and $userCreateRoom.Location.Contains("AccessDenied")) "Usuario no puede crear habitaciones sin crud_habitaciones"

    $unique = ([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds() % 100000000).ToString()
    $email = "smk$unique@x.co"
    $document = "SMK$unique"
    $createdDocument = $document
    $guestPage = Get-Page $admin "/Usuarios/Crear"
    Assert-Check ($guestPage.Status -eq 200) "Admin abre formulario de huéspedes"
    $guestPost = Post-Form $admin "/Usuarios/Crear" $guestPage.Body ([ordered]@{
        "Persona.TipoDocumento" = "DNI"
        "Persona.NumeroDocumento" = $document
        "Persona.Nombres" = "Smoke"
        "Persona.Apellidos" = "Integration"
        "Persona.Nacionalidad" = "Prueba"
        "Persona.Telefono" = "000000000"
        "Persona.EMail" = $email
    })
    Assert-Check ($guestPost.Status -eq 302) "Alta de huésped guarda y redirige"
    $guestSearch = Get-Page $admin "/Usuarios?buscar=$document"
    Assert-Check ($guestSearch.Status -eq 200 -and $guestSearch.Body.Contains($email)) "Huésped creado aparece en la búsqueda"
    $guestMatch = [regex]::Match($guestSearch.Body, 'href="/Usuarios/Detalles/(\d+)"')
    if (-not $guestMatch.Success) { throw "No se encontró la ficha del huésped recién creado." }
    $createdUserId = [long]$guestMatch.Groups[1].Value
    $guestDetails = Get-Page $admin "/Usuarios/Detalles/$createdUserId"
    Assert-Check ($guestDetails.Status -eq 200 -and $guestDetails.Body.Contains($document)) "Ficha del huésped carga"

    $guestEdit = Get-Page $admin "/Usuarios/Editar/$createdUserId"
    $editGuestPost = Post-Form $admin "/Usuarios/Editar/$createdUserId" $guestEdit.Body ([ordered]@{
        "Persona.TipoDocumento" = "DNI"
        "Persona.NumeroDocumento" = $document
        "Persona.Nombres" = "Smoke"
        "Persona.Apellidos" = "Updated"
        "Persona.Nacionalidad" = "Prueba"
        "Persona.Telefono" = "999999999"
        "Persona.EMail" = $email
    })
    Assert-Check ($editGuestPost.Status -eq 302) "Edición de huésped guarda"
    $guestDetails = Get-Page $admin "/Usuarios/Detalles/$createdUserId"
    Assert-Check ($guestDetails.Status -eq 200 -and $guestDetails.Body.Contains("999999999")) "Ficha refleja los cambios del huésped"

    $reservationPage = Get-Page $admin "/Reservas"
    Assert-Check ($reservationPage.Status -eq 200 -and $reservationPage.Body.Contains("id=`"reservationModal`"")) "Crear reserva abre el formulario modal desde el listado"
    $bookingUserMatch = [regex]::Match($reservationPage.Body, '<option value="(\d+)">([^<]*· usuario)</option>', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if (-not $bookingUserMatch.Success) { throw "No se encontró un usuario con cargo usuario en el selector de reservas." }
    $bookingUserId = [long]$bookingUserMatch.Groups[1].Value
    $bookingUserName = [System.Net.WebUtility]::HtmlDecode(($bookingUserMatch.Groups[2].Value -split '·')[0]).Trim()
    $startDate = (Get-Date).Date.AddDays(40).ToString("yyyy-MM-dd")
    $availablePage = Get-Page $admin "/Reservas?handler=Disponibles&inicio=$startDate&noches=1"
    Assert-Check ($availablePage.Status -eq 200) "Endpoint de disponibilidad responde"
    $rooms = @($availablePage.Body | ConvertFrom-Json)
    if ($rooms.Count -eq 0) { throw "No hay habitaciones disponibles para probar la creación de una reserva." }
    $selectedRoomId = [long]$rooms[0].habitacionId
    $reservationPost = Post-Form $admin "/Reservas?handler=Crear" $reservationPage.Body ([ordered]@{
        "Reserva.UsuarioId" = $bookingUserId.ToString()
        "Reserva.InicioReserva" = $startDate
        "Reserva.TotalNoches" = "1"
        "Reserva.EstadoReserva" = "PENDIENTE"
        "Reserva.HabitacionIds" = $selectedRoomId.ToString()
    })
    $reservationError = [regex]::Match($reservationPost.Body, '<div[^>]*validation-summary-errors[^>]*>(.*?)</div>', [System.Text.RegularExpressions.RegexOptions]::Singleline)
    Assert-Check ($reservationPost.Status -eq 302) "Reserva con huésped y habitación disponible se crea" "$(if ($reservationError.Success) { [System.Net.WebUtility]::HtmlDecode(($reservationError.Groups[1].Value -replace '<[^>]+>', ' ')).Trim() })"
    $reservationSearch = Get-Page $admin "/Reservas?UsuarioId=$bookingUserId"
    $reservationMatch = [regex]::Match($reservationSearch.Body, 'href="/Reservas/Detalles\?id=(\d+)"')
    if (-not $reservationMatch.Success) { throw "No se encontró la reserva recién creada en el listado." }
    $createdReservationId = [long]$reservationMatch.Groups[1].Value
    $reservationDetails = Get-Page $admin "/Reservas/Detalles?id=$createdReservationId"
    $reservationGuestOffset = $reservationDetails.Body.IndexOf("Huésped", [System.StringComparison]::OrdinalIgnoreCase)
    $reservationGuestSnippet = if ($reservationGuestOffset -ge 0) { $reservationDetails.Body.Substring($reservationGuestOffset, [Math]::Min(500, $reservationDetails.Body.Length - $reservationGuestOffset)) } else { "sin texto de huésped" }
    Assert-Check ($reservationDetails.Status -eq 200 -and $reservationDetails.Body.Contains($bookingUserName)) "Detalle de reserva muestra huésped y habitación" "HTTP $($reservationDetails.Status); usuario=$bookingUserName; $reservationGuestSnippet"

    $reservationEdit = Get-Page $admin "/Reservas/Editar?id=$createdReservationId"
    $editReservationPost = Post-Form $admin "/Reservas/Editar?id=$createdReservationId" $reservationEdit.Body ([ordered]@{ "Reserva.EstadoReserva" = "CONFIRMADA" })
    $editReservationError = [regex]::Match($editReservationPost.Body, '<div[^>]*validation-summary-errors[^>]*>(.*?)</div>', [System.Text.RegularExpressions.RegexOptions]::Singleline)
    Assert-Check ($editReservationPost.Status -eq 302) "Cambio de estado de reserva se guarda" "HTTP $($editReservationPost.Status), Location '$($editReservationPost.Location)', $(if ($editReservationError.Success) { [System.Net.WebUtility]::HtmlDecode(($editReservationError.Groups[1].Value -replace '<[^>]+>', ' ')).Trim() })"
    $reservationDetails = Get-Page $admin "/Reservas/Detalles?id=$createdReservationId"
    Assert-Check ($reservationDetails.Status -eq 200 -and $reservationDetails.Body.Contains("Confirmada")) "Detalle refleja el estado confirmado"

    $reservationIndex = Get-Page $admin "/Reservas"
    $deleteConfirmed = Post-Form $admin "/Reservas?handler=Eliminar&id=$createdReservationId" $reservationIndex.Body ([ordered]@{})
    Assert-Check ($deleteConfirmed.Status -eq 302) "Intento de eliminación de reserva confirmada se gestiona"
    $stillThere = Get-Page $admin "/Reservas?UsuarioId=$bookingUserId"
    Assert-Check ($stillThere.Body.Contains("#$createdReservationId")) "No se permite eliminar una reserva confirmada"

    $reservationEdit = Get-Page $admin "/Reservas/Editar?id=$createdReservationId"
    $resetReservation = Post-Form $admin "/Reservas/Editar?id=$createdReservationId" $reservationEdit.Body ([ordered]@{ "Reserva.EstadoReserva" = "PENDIENTE" })
    Assert-Check ($resetReservation.Status -eq 302) "Reserva de prueba vuelve a pendiente para limpieza"
    $reservationIndex = Get-Page $admin "/Reservas"
    $deletePending = Post-Form $admin "/Reservas?handler=Eliminar&id=$createdReservationId" $reservationIndex.Body ([ordered]@{})
    Assert-Check ($deletePending.Status -eq 302) "Reserva pendiente se elimina"
    $reservationSearch = Get-Page $admin "/Reservas?buscar=$document"
    Assert-Check (-not $reservationSearch.Body.Contains($email)) "Reserva eliminada deja de aparecer"
    $createdReservationId = $null

    $guestEdit = Get-Page $admin "/Usuarios/Editar/$createdUserId"
    $deleteFormMatch = [regex]::Match($guestEdit.Body, '<form[^>]*action="([^"]*handler=Eliminar[^"]*)"')
    $deleteGuestPath = if ($deleteFormMatch.Success) { $deleteFormMatch.Groups[1].Value } else { "/Usuarios/Editar/$createdUserId?handler=Eliminar" }
    $deleteGuest = Post-Form $admin $deleteGuestPath $guestEdit.Body ([ordered]@{})
    Assert-Check ($deleteGuest.Status -eq 302) "Huésped sin reservas ni cuenta se elimina" "POST '$deleteGuestPath' HTTP $($deleteGuest.Status), Location '$($deleteGuest.Location)'"
    $guestSearch = Get-Page $admin "/Usuarios?buscar=$document"
    Assert-Check (-not $guestSearch.Body.Contains($email)) "Huésped eliminado deja de aparecer"
    $createdUserId = $null

    $roomsBeforePage = Get-Page $admin "/Habitaciones"
    $roomIdsBefore = @([regex]::Matches($roomsBeforePage.Body, '/Habitaciones/Detalles/(\d+)') | ForEach-Object { [long]$_.Groups[1].Value })
    $maxRoomBefore = if ($roomIdsBefore.Count) { ($roomIdsBefore | Measure-Object -Maximum).Maximum } else { 0L }
    $roomPage = Get-Page $admin "/Habitaciones/Crear"
    $roomPost = Post-Form $admin "/Habitaciones/Crear" $roomPage.Body ([ordered]@{
        "Habitacion.NroPiso" = "1"
        "Habitacion.NroCamas" = "1"
        "Habitacion.TipoVipNombre" = "ESTANDAR"
        "Habitacion.Estado" = "LIBRE"
    })
    $roomError = [regex]::Match($roomPost.Body, '<div[^>]*validation-summary-errors[^>]*>(.*?)</div>', [System.Text.RegularExpressions.RegexOptions]::Singleline)
    Assert-Check ($roomPost.Status -eq 302) "Alta de habitación guarda" "HTTP $($roomPost.Status), Location '$($roomPost.Location)', $(if ($roomError.Success) { [System.Net.WebUtility]::HtmlDecode(($roomError.Groups[1].Value -replace '<[^>]+>', ' ')).Trim() })"
    $roomIndex = Get-Page $admin "/Habitaciones"
    $roomIdMatch = [regex]::Match($roomIndex.Body, "Habitación #(\d+) creada correctamente")
    if ($roomIdMatch.Success) {
        $createdRoomId = [long]$roomIdMatch.Groups[1].Value
    }
    else {
        $roomIdsAfter = @([regex]::Matches($roomIndex.Body, '/Habitaciones/Detalles/(\d+)') | ForEach-Object { [long]$_.Groups[1].Value })
        $newRoomIds = @($roomIdsAfter | Where-Object { $_ -gt $maxRoomBefore })
        if (-not $newRoomIds.Count) { throw "No se pudo identificar la habitación temporal creada." }
        $createdRoomId = [long](($newRoomIds | Measure-Object -Maximum).Maximum)
    }
    $roomDetails = Get-Page $admin "/Habitaciones/Detalles/$createdRoomId"
    Assert-Check ($roomDetails.Status -eq 200 -and $roomDetails.Body.Contains("Habitación $createdRoomId")) "Detalle de habitación carga"
    $roomEdit = Get-Page $admin "/Habitaciones/Editar/$createdRoomId"
    $roomUpdate = Post-Form $admin "/Habitaciones/Editar/$createdRoomId" $roomEdit.Body ([ordered]@{
        "Habitacion.HabitacionId" = $createdRoomId.ToString()
        "Habitacion.NroPiso" = "1"
        "Habitacion.NroCamas" = "1"
        "Habitacion.TipoVipNombre" = "ESTANDAR"
        "Habitacion.Estado" = "MANTENIMIENTO"
    })
    Assert-Check ($roomUpdate.Status -eq 302) "Edición de habitación guarda"
    $roomDetails = Get-Page $admin "/Habitaciones/Detalles/$createdRoomId"
    Assert-Check ($roomDetails.Body.Contains("MANTENIMIENTO")) "Detalle refleja el estado de mantenimiento"
    $roomEdit = Get-Page $admin "/Habitaciones/Editar/$createdRoomId"
    $roomRestore = Post-Form $admin "/Habitaciones/Editar/$createdRoomId" $roomEdit.Body ([ordered]@{
        "Habitacion.HabitacionId" = $createdRoomId.ToString()
        "Habitacion.NroPiso" = "1"
        "Habitacion.NroCamas" = "1"
        "Habitacion.TipoVipNombre" = "ESTANDAR"
        "Habitacion.Estado" = "LIBRE"
    })
    Assert-Check ($roomRestore.Status -eq 302) "Estado de habitación de prueba se restaura"

    $floorIndex = Get-Page $admin "/Habitaciones"
    if ($floorIndex.Body.Contains("Piso 99")) { throw "El piso 99 ya existe; se omite la prueba para no sobrescribir datos reales." }
    $floorRef = "Smoke-$unique"
    $floorPost = Post-Form $admin "/Habitaciones?handler=GuardarPiso" $floorIndex.Body ([ordered]@{ PisoNro = "99"; PisoRef = $floorRef })
    Assert-Check ($floorPost.Status -eq 302) "Alta de piso guarda"
    $createdFloorId = 99L
    $createdFloorRef = $floorRef
    $floorIndex = Get-Page $admin "/Habitaciones"
    Assert-Check ($floorIndex.Status -eq 200 -and $floorIndex.Body.Contains($floorRef)) "Piso creado aparece en la estructura"

    $csv = Get-Page $admin "/Habitaciones?handler=Export"
    Assert-Check ($csv.Status -eq 200 -and $csv.Body.Contains("Codigo,Piso,Referencia")) "Exportación de habitaciones genera CSV"

    Write-Host "`n$($passed.Count) comprobaciones pasaron." -ForegroundColor Cyan
}
finally {
    if ($createdReservationId -or $createdUserId -or $createdRoomId -or $createdFloorId -or $createdDocument) {
        $cleanup = @()
        if ($createdDocument) {
            $safeDocument = $createdDocument.Replace("'", "''")
            $cleanup += "DECLARE @SmokeUserId bigint = (SELECT TOP(1) usuario_id FROM Usuarios WHERE numero_documento = N'$safeDocument' AND nombres = N'Smoke'); IF @SmokeUserId IS NOT NULL BEGIN DELETE rh FROM [Reserva Habitaciones] rh INNER JOIN Reservas r ON r.reserva_id = rh.reserva_id WHERE r.usuario_id = @SmokeUserId; DELETE FROM Reservas WHERE usuario_id = @SmokeUserId; DELETE FROM Usuarios WHERE usuario_id = @SmokeUserId AND numero_documento = N'$safeDocument'; END;"
        }
        elseif ($createdReservationId) {
            $cleanup += "DELETE FROM [Reserva Habitaciones] WHERE reserva_id = $createdReservationId; DELETE FROM Reservas WHERE reserva_id = $createdReservationId;"
        }
        if ($createdRoomId) { $cleanup += "DELETE FROM Habitaciones WHERE habitacion_id = $createdRoomId;" }
        if ($createdFloorId) { $cleanup += "DELETE FROM Pisos WHERE nro_piso = $createdFloorId AND ref_piso = '$createdFloorRef';" }
        if ($cleanup.Count -gt 0) {
            try { Invoke-CleanupSql ($cleanup -join " "); Write-Host "Limpieza de datos temporales completada." -ForegroundColor DarkGray }
            catch { Write-Warning "No se completó la limpieza automática. Revisa únicamente los datos smoke-test creados: $($_.Exception.Message)" }
        }
    }
    if ($admin) { $admin.Http.Dispose(); $admin.Handler.Dispose() }
}
