# Sistema de diseño "Stitch" — GrandStay HMS v3.2

Referencia consolidada y autoritativa del sistema de diseño usado por las pantallas
Stitch de Hoteleria-App. **Este archivo es la fuente de verdad de los valores exactos.**

> ⚠️ **Regla de fidelidad nº 1 — de dónde salen los valores:**
> Los valores reales renderizados están en el `tailwind.config` embebido dentro de cada
> `code.html` (línea del `<script id="tailwind-config">`). El documento
> `Resources/Front-End/design/.../hospitality_operations_platform/DESIGN.md` contiene una
> sección en prosa ("Brand & Style") que describe hex **aspiracionales** (`#0F172A`,
> `#D97706`, `#059669`, `#FDFCFA`…) que **NO coinciden** con lo renderizado. Esa prosa es
> la *intención de marca*, pero el `tailwind.config` es el *valor real*. Siempre usa los
> tokens de abajo (y sus nombres de clase) y, si hay duda, el `tailwind.config` del
> `code.html` más reciente manda.

---

## 1. Setup Tailwind (config exacto)

Cada pantalla carga:

```html
<link href="https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700&display=swap" rel="stylesheet">
<link href="https://fonts.googleapis.com/css2?family=Material+Symbols+Outlined:wght,FILL@100..700,0..1&display=swap" rel="stylesheet">
<script src="https://cdn.tailwindcss.com?plugins=forms,container-queries"></script>
<script id="tailwind-config">tailwind.config={ ... tokens de abajo ... }</script>
```

Config raíz: `darkMode: "class"`.

---

## 2. Colores (tokens Material Design 3)

Paleta canónica (aplicar siempre como clases: `bg-<token>`, `text-<token>`, `border-<token>`,
`ring-<token>`, etc. — **nunca** hex sueltos, salvo excepciones puntuales del propio diseño).

### Primarios (navegación, cabeceras, botones principales)
| Token | Valor | Uso típico |
|---|---|---|
| `primary` | `#000000` | Botones/acciones principales (`bg-primary text-on-primary`) |
| `on-primary` | `#ffffff` | Texto sobre `primary` |
| `primary-container` | `#131b2e` | Nav activa, chips, bloques destacados |
| `on-primary-container` | `#7c839b` | Texto sobre `primary-container` |
| `primary-fixed` | `#dae2fd` | Fondos fijos claros |
| `primary-fixed-dim` | `#bec6e0` | Variante atenuada |
| `on-primary-fixed` | `#131b2e` | Texto sobre `primary-fixed` |
| `on-primary-fixed-variant` | `#3f465c` | Texto secundario sobre fijos |
| `inverse-primary` | `#bec6e0` | Primario en superficies inversas |

### Secundarios (VIP / prioridad / ámbar)
| Token | Valor |
|---|---|
| `secondary` | `#904d00` |
| `on-secondary` | `#ffffff` |
| `secondary-container` | `#fe932c` |
| `on-secondary-container` | `#663500` |
| `secondary-fixed` | `#ffdcc3` |
| `secondary-fixed-dim` | `#ffb77d` |
| `on-secondary-fixed` | `#2f1500` |
| `on-secondary-fixed-variant` | `#6e3900` |

### Terciarios (confirmación / esmeralda)
| Token | Valor |
|---|---|
| `tertiary` | `#000000` |
| `on-tertiary` | `#ffffff` |
| `tertiary-container` | `#002114` |
| `on-tertiary-container` | `#069669` ← el "verde confirmado" visible |
| `tertiary-fixed` | `#85f8c4` |
| `tertiary-fixed-dim` | `#68dba9` |
| `on-tertiary-fixed` | `#002114` |
| `on-tertiary-fixed-variant` | `#005137` |

### Error / ocupado / urgencia
| Token | Valor |
|---|---|
| `error` | `#ba1a1a` |
| `on-error` | `#ffffff` |
| `error-container` | `#ffdad6` |
| `on-error-container` | `#93000a` |

### Superficies neutras (lona + contenedores)
| Token | Valor | Rol |
|---|---|---|
| `background` | `#faf9f7` | Fondo base |
| `on-background` | `#1a1c1b` | Texto base |
| `surface` | `#faf9f7` | Superficie principal |
| `surface-bright` | `#faf9f7` | |
| `surface-dim` | `#dadad8` | |
| `surface-variant` | `#e3e2e0` | |
| `surface-tint` | `#565e74` | Tinte |
| `surface-container-lowest` | `#ffffff` | Tarjetas blancas |
| `surface-container-low` | `#f4f3f1` | |
| `surface-container` | `#efeeec` | Contenedor estándar |
| `surface-container-high` | `#e9e8e6` | |
| `surface-container-highest` | `#e3e2e0` | |
| `on-surface` | `#1a1c1b` | Texto sobre superficie |
| `on-surface-variant` | `#45464d` | Texto secundario |
| `outline` | `#76777d` | Bordes/texto tenue |
| `outline-variant` | `#c6c6cd` | Hairlines |
| `inverse-surface` | `#2f3130` | |
| `inverse-on-surface` | `#f1f1ef` | |

---

## 3. Tipografía

Familia única: **Plus Jakarta Sans**. Iconos: **Material Symbols Outlined**.

Los tokens de tipo se usan con doble clase `font-<token> text-<token>`:

| Token | Tamaño | Line-height | Peso | Letter-spacing |
|---|---|---|---|---|
| `display-lg` | 3rem | 3.5rem | 700 | -0.03em |
| `headline-xl` | 2.25rem | 2.75rem | 600 | -0.025em |
| `headline-xl-mobile` | 1.75rem | 2.25rem | 600 | -0.02em |
| `headline-lg` | 1.75rem | 2.25rem | 600 | -0.02em |
| `headline-lg-mobile` | 1.375rem | 1.75rem | 600 | -0.015em |
| `headline-md` | 1.25rem | 1.625rem | 600 | -0.01em |
| `headline-sm` | 1.125rem | 1.5rem | 600 | -0.01em |
| `body-lg` | 1rem | 1.5rem | 400 | 0 |
| `body-md` | 0.875rem | 1.375rem | 400 | 0 |
| `body-sm` | 0.8125rem | 1.25rem | 400 | +0.005em |
| `label-md` | 0.75rem | 1rem | 600 | +0.04em |
| `label-sm` | 0.6875rem | 0.875rem | 700 | +0.06em |
| `numeric-table` | 0.875rem | 1.25rem | 500 | -0.01em |

Reglas de aplicación:
- **Figuras tabulares** (precios ADR/RevPAR, importes, horas, balances de folio):
  añadir `tabular-nums` para alineación de columnas.
- **Labels y overlines** (`label-sm`, `label-md`) en **mayúsculas** con `uppercase tracking-wider`.
- Pesos `600`/`700` reservados para títulos, acciones primarias y totales monetarios; el
  cuerpo queda en `400`.

---

## 4. Radio de esquinas

| Clase | Valor |
|---|---|
| `rounded` (DEFAULT) | 0.25rem |
| `rounded-lg` | 0.5rem |
| `rounded-xl` | 0.75rem |
| `rounded-full` | 9999px |

> En este sistema `rounded-xl` = **0.75rem** (no los 1.5rem de Tailwind estándar), porque
> el config lo sobrescribe.

Uso por tipo de elemento (según la propia marca):
- **Tarjetas y módulos** → `rounded-xl`
- **Inputs, selects, celdas, wraps de acción secundaria** → `rounded` / `rounded-lg`
- **Badges y pills de estado** → `rounded-full`

---

## 5. Espaciado (tokens personalizados)

Conviven con la escala numérica por defecto de Tailwind (`p-3.5`, `gap-4`, `gap-2.5`…).

| Token | Valor |
|---|---|
| `space-xs` | 0.25rem |
| `space-sm` | 0.5rem |
| `space-md` | 0.75rem |
| `space-lg` | 1.25rem |
| `space-xl` | 2rem |
| `gutter` | 1.25rem |
| `gutter-desktop` | 1.5rem |
| `margin` | 1rem |
| `margin-desktop` | 2rem |

---

## 6. Elevación (intención; usar bordes + sombras sutiles)

- **Nivel 0 — Lona:** `background` plano.
- **Nivel 1 — Tarjetas:** `surface-container-lowest` (blanco) con borde 1px
  (`border-surface-container`/`outline-variant`) y sombra ambiental sutil
  (`shadow-sm` / `shadow-[0_1px_3px_rgba(15,23,42,0.04)]`).
- **Nivel 2 — Overlays flotantes (modales rápidos, drawers):** superficie blanca con
  `backdrop-blur` y sombra `shadow-2xl`.
- **Nivel 3 — Modales de comando:** fondo `bg-primary/50 backdrop-blur-md` + tarjeta
  `rounded-2xl shadow-2xl`.

Evitar sombras pesadas: el diseño prefiere hairlines y `backdrop-blur` a drops grandes.

---

## 7. Componentes clave (patrones recurrentes en los `code.html`)

### Botones
- **Primario:** `bg-primary text-on-primary rounded` (o `rounded-lg`), `hover:opacity-90`.
- **Secundario:** `bg-surface-container-lowest border border-surface-container-high text-on-surface`, `hover:bg-surface-container`.
- **Ghost/icono:** `bg-surface-container hover:bg-surface-container-high text-on-surface`.

### Badges / indicadores de estado (pill)
Patrón: `inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full <bg> <text> font-label-sm text-label-sm font-bold` + icono `material-symbols-outlined text-[14px]`.
- **Confirmada / limpia / inspectada:** `bg-surface-container text-on-tertiary-container` + icono `verified`.
- **VIP / pendiente / prioridad:** `bg-secondary-fixed text-on-secondary-container` + icono `hourglass_top`/`star`.
- **Cancelada / liberada:** `bg-error-container text-on-error-container` + icono `cancel`.
- **En estancia:** `bg-primary-container text-on-primary` + icono `hotel`.

### Tabla relacional (tape chart / matriz)
- `thead` sticky (`sticky top-0`), fondo `bg-surface-container-low`, texto `text-outline`
  en `label-sm uppercase tracking-wider`.
- Filas con `divide-y divide-surface-container` + borde lateral de estado
  (`border-l-4 border-secondary` para pendiente, etc.).
- Estados en tab con contador: `Todas / Separadas / Confirmadas / Finalizadas / Liberadas`.
- Celda de huésped: avatar de iniciales (`w-9 h-9 rounded-full bg-…`).

### Header oficial + drawer (shell compartido, en `_Layout.cshtml`)
- Header `h-16` con logo GrandStay + botón "Módulos HMS v3.2" (abre drawer) + buscador
  `⌘K` + notificaciones + perfil (ej. "Elena Rostova · Admin General").
- Flyout drawer izquierdo `w-80` con los **4 módulos canónicos**:
  1. Dashboard General · 2. Habitaciones y Pisos · 3. Reservas y Estancias · 4. Huéspedes / Directorio.
- Comando **Bento** (⌘K): modal `max-w-3xl` con matriz de 4 módulos + acciones rápidas.

### KPI ribbon colapsable
Tarjetas `bg-surface rounded-xl` en grid `grid-cols-1 sm:grid-cols-2 lg:grid-cols-4`,
número grande (`text-[2rem] font-bold tracking-tight`) + tendencia
(`trending_up` en `text-on-tertiary-container`).

### Ficha CRU (panel derecho de detalle)
Tarjeta `xl:w-[480px] bg-surface-container-lowest rounded-2xl border` con header + cuerpo
de detalle + acciones.

---

## 8. Iconos y fuentes (hojas exactas)

```html
<link href="https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700&display=swap" rel="stylesheet">
<link href="https://fonts.googleapis.com/css2?family=Material+Symbols+Outlined:wght,FILL@100..700,0..1&display=swap" rel="stylesheet">
```

Uso del icono (siempre un `<span>` Material Symbols):
```html
<span class="material-symbols-outlined text-[20px] text-on-surface">calendar_month</span>
```

Iconos recurrentes: `grid_view`, `meeting_room`, `calendar_month`, `group`, `search`,
`notifications`, `person`, `menu`, `close`, `terminal`, `bolt`, `file_download`,
`person_add`, `equalizer`, `apartment`, `logout`, `payments`, `book_online`,
`hourglass_top`, `hourglass_bottom`, `verified`, `how_to_reg`, `done_all`, `cancel`,
`history`, `receipt_long`, `visibility`, `chevron_left`, `chevron_right`,
`trending_up`, `warning`, `hotel`, `hotel_class`.
