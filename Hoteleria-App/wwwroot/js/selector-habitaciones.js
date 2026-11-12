// Selector de habitaciones al reservar. El texto se pinta con textContent: nunca se interpreta como HTML.
(() => {
    // Mismo formato que Soles.Formatear en el servidor: «S/ 1,500.00».
    const cifras = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    const soles = monto => 'S/ ' + cifras.format(monto);

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
        const busqueda = raiz.querySelector('[data-busqueda]');
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
            if (estimado) estimado.textContent = soles(total);
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
            const titulo = elemento('span', 'flex flex-wrap items-center gap-1.5');
            titulo.append(elemento('strong', 'truncate text-sm font-semibold text-on-surface', `Habitación ${h.codigo}`));
            if (h.destacado) {
                // Lo marca ML.NET: afinidad con el historial del huésped, o lo más reservado si aún no tiene.
                const insignia = elemento('span', 'inline-flex items-center gap-0.5 rounded-full bg-tertiary-fixed px-2 py-0.5 text-[10px] font-bold uppercase tracking-wide text-on-tertiary-fixed');
                insignia.append(elemento('span', 'material-symbols-outlined text-[12px]', h.destacado === 'Para ti' ? 'auto_awesome' : 'trending_up'), document.createTextNode(h.destacado));
                titulo.append(insignia);
            }
            datos.append(
                titulo,
                elemento('small', 'text-xs text-on-surface-variant', `${h.categoria} · ${h.camas} cama(s)`),
                elemento('small', 'flex items-center gap-1 text-xs text-on-surface-variant', h.ubicacion));
            izquierda.append(casilla, datos);
            const precio = elemento('span', 'shrink-0 text-right');
            precio.append(
                elemento('strong', 'block text-sm font-bold tabular-nums text-primary', soles(h.precioNoche)),
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

            // Primero lo marcado, luego lo que coincide con los filtros y, debajo, el resto de lo libre:
            // ninguna habitación libre queda oculta (sirven para combinar o si cambia el número de personas).
            const marcadas = datos.habitaciones.filter(h => seleccion.has(h.habitacionId));
            const coincidentes = datos.habitaciones.filter(h => h.coincide && !seleccion.has(h.habitacionId));
            const otras = datos.habitaciones.filter(h => !h.coincide && !seleccion.has(h.habitacionId));
            // Al huésped no se le habla del motor: se le dice qué significa el orden. El personal ve qué buscador se usó.
            motor.textContent = datos.busquedaId ? (datos.orden ? 'Ordenadas según sus preferencias.' : '') : `Búsqueda: ${datos.motor}`;
            if (busqueda) busqueda.value = datos.busquedaId ?? '';
            actualizarTotal();

            if (!datos.habitaciones.length) {
                aviso('No hay habitaciones libres para esas fechas.');
                return;
            }

            const nodos = marcadas.map(opcion);
            if (coincidentes.length) {
                nodos.push(...coincidentes.map(opcion));
            } else {
                const cantidad = personas.value;
                nodos.push(elemento('p', 'rounded-lg bg-surface-container-lowest px-3 py-2.5 text-center text-xs text-on-surface-variant',
                    cantidad && !categoria.value && !texto.value.trim()
                        ? `No hay habitaciones de exactamente ${cantidad} cama(s): puede elegir otra o combinar varias.`
                        : 'Ninguna habitación libre coincide con esos filtros. Estas son las demás libres en esas fechas.'));
            }
            if (otras.length) {
                const titulo = elemento('div', 'mt-1 flex flex-wrap items-center justify-between gap-2 border-t border-surface-container-high px-1 pt-3');
                const textos = elemento('div', 'flex flex-col');
                textos.append(
                    elemento('strong', 'text-xs font-bold uppercase tracking-wide text-on-surface-variant', 'Otras habitaciones libres en esas fechas'),
                    elemento('small', 'text-[11px] text-outline', describirFiltros()));
                const quitar = elemento('button', 'rounded-lg bg-surface-container px-2.5 py-1 text-[11px] font-semibold text-on-surface hover:bg-surface-container-high', 'Quitar filtros');
                quitar.type = 'button';
                quitar.addEventListener('click', () => {
                    personas.value = '';
                    categoria.value = '';
                    texto.value = '';
                    cargar();
                });
                titulo.append(textos, quitar);
                nodos.push(titulo, ...otras.map(opcion));
            }
            lista.replaceChildren(...nodos);
        }

        // Explica por qué estas no coinciden, con los filtros que hay puestos.
        function describirFiltros() {
            const partes = [];
            if (Number(personas.value) >= 1) partes.push(`${personas.value} persona(s) = ${personas.value} cama(s)`);
            if (categoria.value) partes.push(`categoría ${categoria.value}`);
            if (texto.value.trim()) partes.push(`«${texto.value.trim()}»`);
            return partes.length ? `No coinciden con ${partes.join(', ')}. Sirven para combinar habitaciones.` : 'Sirven para combinar habitaciones.';
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
