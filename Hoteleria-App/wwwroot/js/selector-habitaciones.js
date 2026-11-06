// Selector de habitaciones al reservar. El texto se pinta con textContent: nunca se interpreta como HTML.
(() => {
    const moneda = new Intl.NumberFormat('es-CO', { maximumFractionDigits: 0 });

    function elemento(etiqueta, clases, texto) {
        const nodo = document.createElement(etiqueta);
        if (clases) nodo.className = clases;
        if (texto !== undefined) nodo.textContent = texto;
        return nodo;
    }

    document.querySelectorAll('[data-selector-habitaciones]').forEach(raiz => {
        const inicio = document.querySelector(raiz.dataset.inicio);
        const noches = document.querySelector(raiz.dataset.noches);
        const personas = raiz.querySelector('[data-filtro-personas]');
        const categoria = raiz.querySelector('[data-filtro-categoria]');
        const texto = raiz.querySelector('[data-filtro-texto]');
        const lista = raiz.querySelector('[data-lista]');
        const motor = raiz.querySelector('[data-motor]');
        const seleccion = new Map(); // id → precio por noche; se conserva al cambiar los filtros.
        const pendientes = (raiz.dataset.seleccionadas || '').split(',').filter(Boolean).map(Number);
        let consulta = 0;
        let espera;

        function actualizarTotal() {
            const porNoche = [...seleccion.values()].reduce((suma, precio) => suma + precio, 0);
            const total = porNoche * Math.max(1, Number(noches.value || 1));
            const cuenta = document.getElementById('selected-room-count');
            const estimado = document.getElementById('reservation-estimate');
            if (cuenta) cuenta.textContent = seleccion.size;
            if (estimado) estimado.textContent = '$' + moneda.format(total);
        }

        function aviso(mensaje, clases = 'text-outline') {
            lista.replaceChildren(elemento('p', `py-5 text-center text-sm ${clases}`, mensaje));
        }

        function opcion(h) {
            const marcada = seleccion.has(h.habitacionId);
            const fila = elemento('label', 'flex cursor-pointer items-center justify-between gap-3 rounded-xl border bg-surface-container-lowest p-3 transition-colors hover:border-primary/40 '
                + (marcada ? 'border-primary' : 'border-surface-container-high'));
            const izquierda = elemento('span', 'flex min-w-0 items-center gap-3');
            const casilla = elemento('input', 'h-4 w-4 shrink-0 rounded accent-primary');
            casilla.type = 'checkbox';
            casilla.name = raiz.dataset.campo;
            casilla.value = h.habitacionId;
            casilla.checked = marcada;
            casilla.addEventListener('change', () => {
                if (casilla.checked) seleccion.set(h.habitacionId, h.precioNoche);
                else seleccion.delete(h.habitacionId);
                fila.classList.toggle('border-primary', casilla.checked);
                fila.classList.toggle('border-surface-container-high', !casilla.checked);
                actualizarTotal();
            });
            const datos = elemento('span', 'flex min-w-0 flex-col');
            datos.append(
                elemento('strong', 'truncate text-sm font-semibold text-on-surface', `Habitación ${h.codigo}`),
                elemento('small', 'text-xs text-on-surface-variant', `${h.categoria} · ${h.camas} cama(s)`),
                elemento('small', 'flex items-center gap-1 text-xs text-on-surface-variant', h.ubicacion));
            izquierda.append(casilla, datos);
            const precio = elemento('span', 'shrink-0 text-right');
            precio.append(
                elemento('strong', 'block text-sm font-bold tabular-nums text-primary', '$' + moneda.format(h.precioNoche)),
                elemento('small', 'text-[11px] text-outline', 'por noche'));
            fila.append(izquierda, precio);
            return fila;
        }

        function mostrar(datos) {
            const elegida = categoria.value;
            categoria.replaceChildren(new Option('Todas', ''), ...datos.categorias.map(c => new Option(c, c)));
            categoria.value = datos.categorias.includes(elegida) ? elegida : '';

            // Lo marcado sigue a la vista, y primero, aunque no cumpla los filtros: así se pueden combinar habitaciones.
            const libres = new Map(datos.habitaciones.map(h => [h.habitacionId, h]));
            pendientes.splice(0).forEach(id => { if (libres.has(id)) seleccion.set(id, libres.get(id).precioNoche); });
            [...seleccion.keys()].forEach(id => { if (!libres.has(id)) seleccion.delete(id); });

            const marcadas = datos.habitaciones.filter(h => seleccion.has(h.habitacionId));
            const visibles = [...marcadas, ...datos.habitaciones.filter(h => h.coincide && !seleccion.has(h.habitacionId))];
            motor.textContent = `Búsqueda: ${datos.motor}`;
            actualizarTotal();

            if (visibles.length) {
                lista.replaceChildren(...visibles.map(opcion));
                return;
            }
            if (!datos.habitaciones.length) {
                aviso('No hay habitaciones libres para esas fechas.');
                return;
            }
            const cantidad = personas.value;
            const caja = elemento('div', 'flex flex-col items-center gap-2 py-5 text-center text-sm text-outline');
            caja.append(elemento('p', '', cantidad && !categoria.value && !texto.value.trim()
                ? `No hay habitaciones de exactamente ${cantidad} cama(s). Puede combinar varias.`
                : 'Ninguna habitación libre coincide con esos filtros.'));
            const verTodas = elemento('button', 'rounded-lg bg-surface-container px-3 py-1.5 text-xs font-semibold text-on-surface', 'Ver todas las disponibles');
            verTodas.type = 'button';
            verTodas.addEventListener('click', () => {
                personas.value = '';
                categoria.value = '';
                texto.value = '';
                cargar();
            });
            caja.append(verTodas);
            lista.replaceChildren(caja);
        }

        async function cargar() {
            if (!inicio.value || Number(noches.value) < 1) return;
            const numero = ++consulta;
            const parametros = new URLSearchParams({ handler: 'Disponibles', inicio: inicio.value, noches: noches.value });
            if (Number(personas.value) >= 1) parametros.set('personas', personas.value);
            if (categoria.value) parametros.set('categoria', categoria.value);
            if (texto.value.trim()) parametros.set('q', texto.value.trim());
            try {
                const respuesta = await fetch(`${location.pathname}?${parametros}`);
                if (!respuesta.ok) throw new Error(String(respuesta.status));
                const datos = await respuesta.json();
                // Si el huésped ya cambió otro filtro, esta respuesta llegó tarde y no se muestra.
                if (numero === consulta) mostrar(datos);
            } catch {
                if (numero === consulta) aviso('No se pudo consultar la disponibilidad. Inténtelo de nuevo.', 'text-error');
            }
        }

        function cargarEnUnMomento() {
            clearTimeout(espera);
            espera = setTimeout(cargar, 300);
        }

        inicio.addEventListener('change', cargar);
        noches.addEventListener('change', () => { actualizarTotal(); cargar(); });
        categoria.addEventListener('change', cargar);
        personas.addEventListener('input', cargarEnUnMomento);
        texto.addEventListener('input', cargarEnUnMomento);
        // Enter en un filtro no debe enviar la reserva.
        [personas, texto].forEach(campo => campo.addEventListener('keydown', evento => {
            if (evento.key === 'Enter') evento.preventDefault();
        }));

        cargar();
    });
})();
