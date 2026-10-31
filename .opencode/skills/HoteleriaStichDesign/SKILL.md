---
name: HoteleriaStichDesign
description: >-
  Implementa y maqueta el front-end de Hoteleria-App (ASP.NET Core Razor Pages, .NET 10)
  replicando con fidelidad el sistema de diseño "Stitch" de GrandStay HMS. Usa esta skill
  SIEMPRE que el usuario pida construir, estilizar o rediseñar pantallas del sistema
  hotelero, migrar el proyecto de Bootstrap a Tailwind, reproducir cualquier pantalla del
  diseño ubicado en Resources/Front-End/design/stitch_hotel_management_system_interface/,
  o crear el layout compartido (header/drawer), dashboard, tablas de reservas,
  habitaciones, huéspedes o la pantalla de login. Aplícala también cuando solo pidan
  "poner bonita", "que se vea como el diseño", "usar los colores del hotel", o mencionen
  módulos HMS, GrandStay, Material Symbols, Plus Jakarta Sans o Tailwind, aunque no digan
  la palabra "Stitch" ni "skill".
---

# HoteleriaStichDesign

Implementa el front-end de **Hoteleria-App** reproduciendo píxel a píxel el diseño
generado con **Stitch** para "GrandStay HMS". El objetivo no es "algo parecido": es
**fidelidad al diseño**, usando exactamente el mismo sistema de tokens (Tailwind +
Material Design 3), tipografía e iconos que los artefactos de referencia.

## Contexto del proyecto (monolito .NET)

El front-end no es una SPA separada: vive **dentro** del monolito ASP.NET Core
(Razor Pages, .NET 10) en `Hoteleria-App/`:

- Vistas → `Hoteleria-App/Pages/**` (`.cshtml` + code-behind `.cshtml.cs`)
- CSS/JS/fonts/imágenes → `Hoteleria-App/wwwroot/**` (`css/site.css`, `js/site.js`)
- Lógica de página → en el propio code-behind `*.cshtml.cs` (namespace raíz `Hoteleria_App`)

**Backend en desarrollo.** Ya existe la capa de datos (EF Core + migraciones + `DbSeeder`
+ SQL Server vía Docker), pero la lógica de negocio se construye progresivamente. Al
implementar una vista:
- Enlaza `@Model` contra el code-behind y consume los servicios reales (hoy
  `IHabitacionService`/`HabitacionService`) o el `HoteleriaDbContext` cuando aplique.
- Si un servicio aún no existe, mantenlo desacoplado y listo para instanciar el real.
  La BD se aprovisiona sola (migraciones + semilla); no inventes otra fuente de datos.

## Dónde vive el diseño (fuentes de verdad)

```
Resources/Front-End/design/stitch_hotel_management_system_interface/
├── pantalla_de_inicio_y_acceso_grandstay_hms/          # Login / acceso
├── dashboard_general_m_tricas_hist_ricas_hms/          # Dashboard + analítica
├── gesti_n_de_reservas_y_estancias_hms_estilo_dashboard_unificado/  # Reservas (listado+ficha+KPI)
├── gesti_n_de_reservas_panel_fijo_sin_selecci_n/       # Reservas (estado vacío/sin selección)
├── gesti_n_de_reservas_flujo_creaci_n_nueva_reserva_modal_e_r_pasarela/  # Nueva reserva (modal + pasarela)
├── gesti_n_de_habitaciones_y_pisos_hms_unificado/      # Habitaciones y pisos
├── directorio_y_crud_de_hu_spedes_hms_oficial/         # Directorio / CRUD huéspedes
├── logo-app/                                           # Logo GrandStay (screen.png)
├── export_last_vers_301026_hoteleriaer.png/            # Captura global de la versión completa
└── hospitality_operations_platform/DESIGN.md           # Documento de marca (prosa; ver nota)
```

Cada pantalla tiene **dos** artefactos que SIEMPRE hay que consultar juntos:

- **`code.html`** — HTML+Tailwind generado por Stitch. Fuente de verdad del **markup**,
  clases y del `tailwind.config` (tokens exactos). Es una sola página autocontenida.
- **`screen.png`** — la captura visual. Fuente de verdad del **aspecto final**. Comparar
  el resultado contra esta imagen antes de dar por terminada una pantalla.

Los tokens consolidados y listos para copiar están en
[`references/design-system.md`](references/design-system.md). **Léelo antes de escribir
CSS.** Contiene colores, tipografía, radios, espaciados, elevación y patrones de
componentes con los valores reales.

## Reglas críticas de fidelidad (léelas antes de tocar nada)

1. **Fuente de verdad de hex = `tailwind.config` (o `references/design-system.md`), NO la
   prosa de `DESIGN.md`.** La sección "Brand & Style" de `DESIGN.md` describe una paleta
   aspiracional (`#0F172A`, `#D97706`, `#059669`, `#FDFCFA`…) que **no coincide** con lo
   renderizado. Los valores reales son los del config: `primary #000000`,
   `primary-container #131b2e`, `secondary #904d00`, `on-tertiary-container #069669`,
   `background #faf9f7`, etc. Si hay ambigüedad, manda el `tailwind.config` del
   `code.html` más reciente.
2. **Usa las clases de token, no hex sueltos.** `bg-primary`, `bg-surface-container-lowest`,
   `text-on-surface`, `border-surface-container-high`, `text-on-tertiary-container`,
   etc. El único motivo para un hex arbitrario es que el propio `code.html` lo use.
3. **UI en español** (`lang="es"`), con los rótulos tal como aparecen en el diseño
   ("Gestión de Reservas y Estancias", "Habitaciones y Pisos", "Iniciar sesión"…).
4. **Tipografía:** Plus Jakarta Sans para TODO el texto; Material Symbols Outlined para
   iconos. Nada de fuentes del template por defecto.
5. **Radios:** en este sistema `rounded-xl` = **0.75rem** y `rounded` = 0.25rem (el config
   los sobrescribe). No uses el valor Tailwind estándar de memoria.

## Stack objetivo (migración Bootstrap → Tailwind)

El proyecto partió del template Razor Pages con **Bootstrap**. La skill exige migrar a
Tailwind (CDN, igual que los `code.html`) y no mezclar ambos.

Pasos de migración (hacer una vez en el shell):

1. **Quitar Bootstrap** de `Pages/Shared/_Layout.cshtml`: los `<link>` de
   `lib/bootstrap/dist/css/bootstrap.min.css`, los `<script>` de bootstrap, y TODAS las
   clases `navbar*`, `container`, `dropdown*`, `form-*`, etc. del template.
2. **En `<head>` de `_Layout.cshtml`** cargar, en este orden de referencia:
   ```html
   <link href="https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700&display=swap" rel="stylesheet">
   <link href="https://fonts.googleapis.com/css2?family=Material+Symbols+Outlined:wght,FILL@100..700,0..1&display=swap" rel="stylesheet">
   <script src="https://cdn.tailwindcss.com?plugins=forms,container-queries"></script>
   <script id="tailwind-config">tailwind.config={ darkMode:"class", theme:{ extend:{ colors:{...}, borderRadius:{...}, spacing:{...}, fontFamily:{...}, fontSize:{...} } } };</script>
   ```
   El objeto `tailwind.config` completo (para copiar/pegar) está en
   `references/design-system.md` y en cualquier `code.html`. Es **idéntico** en todas las
   pantallas: defínelo **una sola vez** en el layout compartido, no por página.
3. **Base CSS** (el reset `@layer base{...}`, `::-webkit-scrollbar{display:none}`, etc.)
   va en `wwwroot/css/site.css`. Mantener `site.css` o usar `_Layout.cshtml.css`
   (scoped). **Importante Razor:** los bloques `@media`, `@layer`, `@keyframes` etc. dentro
   de un `.cshtml` requieren escapar el `@` como `@@`. Preferir siempre el archivo `.css`
   real para evitar este problema.
4. **JS de página** va en `wwwroot/js/site.js` (o en una sección `@section Scripts`).

> Tailwind Play CDN es lo que usa el propio diseño y basta para desarrollo fiel. Para
> producción, avisar que conviene migrar a un build con Tailwind CLI; no hacerlo
> silenciosamente.

## Workflow para implementar una pantalla

Sigue estos pasos en orden para cada pantalla pedida:

1. **Identifica la pantalla** en el catálogo de abajo y abre su `code.html` + `screen.png`.
2. **Detecta qué es compartido vs propio:** el header, el drawer lateral, el modal
   Bento ⌘K y el buscador son **shell compartido** (van en `_Layout.cshtml` o en un
   Partial). El contenido entre `<main>…</main>` es lo propio de la página.
3. **Asegura el shell** (si aún no está): fonts + Tailwind + tokens + header/drawer.
   Si ya está migrado, no lo dupliques.
4. **Traslada el markup del `code.html` a la Razor Page** (`Pages/…/X.cshtml`),
   conservando las clases Tailwind EXACTAS (son las que producen el diseño). La regla de
   oro: **el HTML de la vista debe ser estructuralmente idéntico al `code.html`**; solo
   cambia lo dinámico.
5. **Sustituye lo dinámico** por Razor:
   - Datos hardcodeados (filas, nombres, cifras) → `@Model.X` / `@foreach` / `@if`.
   - `onclick="alert('…')"` y `onclick="open…()"` (navegación simulada de Stitch) →
     handlers reales: `<a asp-page="…">`, `@Html.Razor`, `asp-for`, `<form method="post">`,
     o funciones en `site.js`. **No dejar `alert()` de maqueta.**
   - Enlaces de logo/imgs externos (`lh3.googleusercontent.com/…`) → asset local en
     `wwwroot` (el logo está en `design/…/logo-app/screen.png`).
6. **Verifica** con `dotnet build` y `dotnet run` (la BD se aprovisiona sola con
   `docker compose up -d` + migraciones EF Core). Compara contra `screen.png`.

## Catálogo de pantallas (diseño → Razor Page)

| Carpeta de diseño | Pantalla a implementar | Página Razor |
|---|---|---|
| `pantalla_de_inicio_y_acceso_grandstay_hms` | Login / acceso (estilo propio, más simple) | nueva `Pages/Login.cshtml` (o `Cuenta/Login`) |
| `dashboard_general_m_tricas_hist_ricas_hms` | Dashboard General & Analítica Histórica | `Pages/Index.cshtml` |
| `gesti_n_de_reservas_y_estancias_hms_estilo_dashboard_unificado` | Reservas: listado + ficha + KPI | `Pages/Reservas/Index.cshtml` |
| `gesti_n_de_reservas_panel_fijo_sin_selecci_n` | Reservas: panel sin selección (estado vacío) | variante de `Pages/Reservas/Index.cshtml` |
| `gesti_n_de_reservas_flujo_creaci_n_nueva_reserva_modal_e_r_pasarela` | Nueva reserva: modal + pasarela | `Pages/Reservas/Crear.cshtml` |
| `gesti_n_de_habitaciones_y_pisos_hms_unificado` | Habitaciones y Pisos | `Pages/Habitaciones/Index.cshtml` (+ Detalles/Editar/PorPiso/PorTipoVip) |
| `directorio_y_crud_de_hu_spedes_hms_oficial` | Directorio y CRUD de huéspedes | `Pages/Usuarios/Index.cshtml` (+ Crear/Editar) |
| `logo-app/screen.png` | Logo de marca GrandStay | asset en `wwwroot` (header/log) |
| `export_last_vers_301026_hoteleriaer.png` | Referencia global de la versión completa | (solo consulta) |

Los 4 módulos canónicos del producto (presentes en header/drawer/Bento) son:
**1. Dashboard General · 2. Habitaciones y Pisos · 3. Reservas y Estancias ·
4. Huéspedes / Directorio.**

## Componentes del shell compartido (`_Layout.cshtml`)

Reproducirlos exactamente como en cualquier `code.html`:

- **Header oficial** (`<header class="h-16 … backdrop-blur-xl border-b …">`): botón
  "Módulos HMS v3.2" (abre drawer), logo GrandStay, buscador con badge `⌘K`,
  notificaciones y perfil de usuario.
- **Flyout drawer** (`w-80`, overlay `bg-black/40 backdrop-blur-sm`): navegación a los 4
  módulos, "Atajos de turno" y tarjeta de "Auditoría Nocturna".
- **Bento command launcher (⌘K)**: modal `max-w-3xl` con matriz de 4 módulos + acciones
  rápidas ("+ Nueva Reserva", "Exportar XLS/PDF").
- El estado "Activo" se marca con `bg-primary-container text-on-primary` en el ítem
  activo y `bg-primary text-on-primary` en el botón/tab activo.

Extrae estas piezas desde el `code.html` y conviértelas en Razor renderizando la
navegación real (`asp-page`) y el estado activo con `@Model`/`ViewData` en lugar de
`onclick="return false"`.

## Checklist de calidad (antes de dar una pantalla por terminada)

- [ ] Bootstrap eliminado; Tailwind CDN + `tailwind.config` cargados en el layout.
- [ ] Fuentes Plus Jakarta Sans + Material Symbols cargadas y en uso.
- [ ] Colores vía clases de token (no hex sueltos).
- [ ] Radios correctos (`rounded`/`rounded-lg`/`rounded-xl`/`rounded-full` según diseño).
- [ ] UI en español con rótulos idénticos al diseño.
- [ ] Datos hardcodeados reemplazados por `@Model`/loops; sin `alert()` de maqueta.
- [ ] Iconos Material Symbols (`material-symbols-outlined`) en todos los iconos.
- [ ] `dotnet build` compila y la página renderiza; comparada contra `screen.png`.
- [ ] No quedan restos del template (navbar Bootstrap, `container`, `footer` de plantilla).

## Gotchas específicos de Razor al traer Tailwind

- **Escape de `@`:** dentro de `.cshtml`, escapa `@media`/`@layer`/`@keyframes` como `@@`
  o —mejor— muévelos a `site.css`. El objeto `tailwind.config` es JS puro (sin `@`), así
  que puede ir inline sin problema.
- **Hashtags / `html` literal:** al pegar HTML con `&amp;` y `&nbsp;`, usa
  `@Html.Raw(...)` o escribe las entidades tal cual (Razor las respeta).
- **Clases con `[` `]`:** los valores arbitrarios (`text-[24px]`, `max-w-[1780px]`,
  `shadow-[0_1px_8px_rgba(0,0,0,0.03)]`) son válidos en Razor; no requieren escape.
- **`<img>` con SVG/logo:** usa `wwwroot`, no URLs de Google externas de maqueta.
- **RootNamespace** del proyecto es `Hoteleria_App` (el modelo de página vive en
  `Hoteleria_App.Pages.*`). Cuidado con los `using` si creas páginas nuevas.
- **BBDD:** SQL Server por Docker (`docker compose up -d`); la app aplica migraciones
  EF Core + semilla al arrancar. No hay modo "mock": la base de datos es siempre real.

## Si una pantalla no existe aún en el diseño

No inventes un estilo nuevo. Extiende el sistema: usa los mismos tokens de
`references/design-system.md`, la misma tipografía e iconos, y los mismos patrones de
componente (tabla, badge, KPI card, botones) para que luzca idéntico al resto del producto.
