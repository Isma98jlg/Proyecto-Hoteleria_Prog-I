// Mapa de lugares del huésped. Los datos vienen de terceros: todo se pinta con textContent, nunca como HTML.
(() => {
    const raiz = document.getElementById('explora');
    if (!raiz) return;

    const parte = nombre => raiz.querySelector(`[data-explora-${nombre}]`);
    const lista = parte('lista');
    const aviso = parte('aviso');
    const resumen = parte('resumen');
    const radio = parte('radio');
    const ciudad = parte('ciudad');
    const botonTodo = parte('todo');
    const botonesFiltro = [...raiz.querySelectorAll('[data-explora-filtro]')];
    const filtros = new Map(botonesFiltro.map(b => [b.dataset.exploraFiltro, { etiqueta: b.dataset.etiqueta, icono: b.dataset.icono, color: b.dataset.color }]));
    const disponible = raiz.dataset.disponible === '1';
    const CLAVE_CIUDAD = 'clima-ciudad';

    let punto = null;
    let nombreLugar = '';
    let consulta = 0;
    let espera;
    let desplazamiento = 0;
    let hayMas = false;
    let lugares = [];
    const marcadores = new Map();

    const hayMapa = typeof L !== 'undefined';
    let mapa, capaLugares, capaZona;
    if (hayMapa) {
        mapa = L.map(parte('mapa'), { zoomControl: true }).setView([0, 0], 2);
        L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19,
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
        }).addTo(mapa);
        capaZona = L.layerGroup().addTo(mapa);
        capaLugares = L.layerGroup().addTo(mapa);
    } else {
        parte('mapa').classList.add('hidden');
    }

    function elemento(etiqueta, clases, texto) {
        const nodo = document.createElement(etiqueta);
        if (clases) nodo.className = clases;
        if (texto !== undefined) nodo.textContent = texto;
        return nodo;
    }

    function avisar(texto) {
        aviso.textContent = texto || '';
        aviso.classList.toggle('hidden', !texto);
    }

    function distanciaLegible(metros) {
        if (metros === null || metros === undefined) return '';
        return metros < 1000 ? `${metros} m` : `${(metros / 1000).toLocaleString('es', { maximumFractionDigits: 1 })} km`;
    }

    function enlace(texto, icono, href, externo = true) {
        const a = elemento('a', 'inline-flex items-center gap-1 rounded-lg bg-surface-container px-2.5 py-1 text-xs font-semibold text-on-surface hover:bg-surface-container-high');
        a.href = href;
        if (externo) {
            a.target = '_blank';
            a.rel = 'noopener noreferrer';
        }
        a.append(elemento('span', 'material-symbols-outlined text-[14px]', icono), document.createTextNode(texto));
        return a;
    }

    function acciones(l) {
        const caja = elemento('div', 'flex flex-wrap gap-1.5');
        caja.append(enlace('Cómo llegar', 'directions', `https://www.google.com/maps/dir/?api=1&destination=${l.latitud},${l.longitud}`));
        if (l.telefono) caja.append(enlace('Llamar', 'call', `tel:${l.telefono.replace(/[^+0-9]/g, '')}`, false));
        if (l.web) caja.append(enlace('Web', 'language', l.web));
        return caja;
    }

    function resaltar(id) {
        lista.querySelectorAll('[data-lugar]').forEach(t => t.classList.toggle('explora-activa', t.dataset.lugar === id));
        marcadores.forEach((m, clave) => m.setStyle({ radius: clave === id ? 11 : 7, weight: clave === id ? 3 : 2 }));
    }

    function tarjeta(l) {
        const filtro = filtros.get(l.filtro);
        const caja = elemento('article', 'explora-tarjeta flex cursor-pointer flex-col gap-1.5 rounded-xl border border-surface-container-high bg-surface-container-lowest p-3');
        caja.dataset.lugar = l.id;

        const cabecera = elemento('div', 'flex items-start gap-2.5');
        const icono = elemento('span', 'material-symbols-outlined flex h-8 w-8 shrink-0 items-center justify-center rounded-full text-[18px] text-white', filtro?.icono ?? 'place');
        icono.style.background = filtro?.color ?? '#475569';
        const textos = elemento('div', 'min-w-0 flex-1');
        textos.append(
            elemento('strong', 'block truncate text-sm text-on-surface', l.nombre),
            elemento('small', 'block text-xs text-on-surface-variant', [filtro?.etiqueta, l.detalle].filter(Boolean).join(' · ')));
        cabecera.append(icono, textos);
        const distancia = distanciaLegible(l.distanciaMetros);
        if (distancia) cabecera.append(elemento('span', 'shrink-0 text-xs font-semibold tabular-nums text-secondary', distancia));
        caja.append(cabecera);

        if (l.direccion) caja.append(elemento('small', 'text-xs text-on-surface-variant', l.direccion));
        if (l.horario) caja.append(elemento('small', 'text-xs text-on-surface-variant', `Horario: ${l.horario}`));
        caja.append(acciones(l));

        caja.addEventListener('click', evento => {
            if (evento.target.closest('a')) return;
            resaltar(l.id);
            const marcador = marcadores.get(l.id);
            if (marcador) {
                mapa.setView(marcador.getLatLng(), Math.max(mapa.getZoom(), 16));
                marcador.openPopup();
            }
        });
        return caja;
    }

    function ventanita(l) {
        const caja = elemento('div', 'flex flex-col gap-1');
        caja.append(elemento('strong', '', l.nombre));
        const filtro = filtros.get(l.filtro);
        caja.append(elemento('small', '', [filtro?.etiqueta, l.detalle, distanciaLegible(l.distanciaMetros)].filter(Boolean).join(' · ')));
        return caja;
    }

    function pintarMapa(l) {
        if (!hayMapa) return;
        const color = filtros.get(l.filtro)?.color ?? '#475569';
        const marcador = L.circleMarker([l.latitud, l.longitud], { radius: 7, color: '#ffffff', weight: 2, fillColor: color, fillOpacity: 0.95 })
            .bindPopup(ventanita(l));
        marcador.on('click', () => {
            resaltar(l.id);
            lista.querySelector(`[data-lugar="${CSS.escape(l.id)}"]`)?.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
        });
        marcador.addTo(capaLugares);
        marcadores.set(l.id, marcador);
    }

    function pintarZona(datos) {
        if (!hayMapa) return;
        capaZona.clearLayers();
        const centro = [datos.latitud, datos.longitud];
        L.circleMarker(centro, { radius: 8, color: '#ffffff', weight: 3, fillColor: '#2563eb', fillOpacity: 1 })
            .bindTooltip(nombreLugar || 'Su ubicación')
            .addTo(capaZona);
        const zona = L.circle(centro, { radius: datos.radioMetros, color: '#2563eb', weight: 1, fillOpacity: 0.05 }).addTo(capaZona);
        // Con «Ver todos» se encuadran los resultados: a 50 km no se distinguiría nada.
        const limites = datos.radioMetros <= 10000 || !lugares.length
            ? zona.getBounds()
            : L.latLngBounds([centro, ...lugares.map(l => [l.latitud, l.longitud])]);
        mapa.fitBounds(limites, { padding: [24, 24], maxZoom: 16 });
    }

    function actualizarResumen() {
        const quien = nombreLugar || 'su ubicación';
        resumen.textContent = lugares.length ? `${lugares.length} lugar(es) cerca de ${quien}, del más cercano al más lejano.` : '';
    }

    function botonMas() {
        lista.querySelector('[data-explora-mas]')?.remove();
        if (!hayMas) return;
        const boton = elemento('button', 'shrink-0 rounded-lg bg-surface-container px-3 py-2 text-sm font-semibold text-on-surface hover:bg-surface-container-high', 'Ver más');
        boton.type = 'button';
        boton.dataset.exploraMas = '';
        boton.addEventListener('click', () => buscar(true));
        lista.append(boton);
    }

    function filtrosElegidos() {
        return botonesFiltro.filter(b => b.getAttribute('aria-pressed') === 'true').map(b => b.dataset.exploraFiltro);
    }

    async function buscar(ampliar = false) {
        if (!punto || !disponible) return;
        const numero = ++consulta;
        const parametros = new URLSearchParams({
            handler: 'Buscar',
            lat: punto.lat,
            lon: punto.lon,
            radio: radio.value,
            filtros: filtrosElegidos().join(','),
            desplazamiento: ampliar ? desplazamiento + 20 : 0
        });
        avisar('Buscando lugares…');
        try {
            const respuesta = await fetch(`${location.pathname}?${parametros}`);
            const datos = await respuesta.json().catch(() => ({}));
            if (numero !== consulta) return;
            if (!respuesta.ok) {
                avisar(respuesta.status === 429 ? 'Demasiadas búsquedas seguidas. Espere un momento.' : (datos.error || 'No se pudieron consultar los lugares.'));
                return;
            }
            avisar('');
            if (!ampliar) {
                lugares = [];
                lista.replaceChildren();
                marcadores.clear();
                if (hayMapa) capaLugares.clearLayers();
            }
            // Un lugar puede repetirse entre páginas si cambia el orden.
            const nuevos = datos.lugares.filter(l => !marcadores.has(l.id) && !lugares.some(x => x.id === l.id));
            desplazamiento = ampliar ? desplazamiento + 20 : 0;
            hayMas = datos.hayMas && desplazamiento < 200;
            lugares.push(...nuevos);
            nuevos.forEach(l => {
                lista.append(tarjeta(l));
                pintarMapa(l);
            });
            if (!ampliar) pintarZona(datos);
            botonMas();
            actualizarResumen();
            if (!lugares.length) {
                lista.replaceChildren(elemento('p', 'py-6 text-center text-sm text-outline',
                    'No encontramos lugares con esos filtros. Pruebe con más distancia o «Ver todos».'));
            }
        } catch {
            if (numero === consulta) avisar('No se pudieron consultar los lugares. Revise su conexión.');
        }
    }

    function buscarEnUnMomento() {
        clearTimeout(espera);
        espera = setTimeout(() => buscar(), 300);
    }

    function fijarPunto(lat, lon, nombre) {
        punto = { lat, lon };
        nombreLugar = nombre;
        buscar();
    }

    async function buscarCiudad(nombre) {
        avisar('Buscando la ciudad…');
        try {
            const respuesta = await fetch(`${location.pathname}?${new URLSearchParams({ handler: 'Ciudad', ciudad: nombre })}`);
            const datos = await respuesta.json().catch(() => ({}));
            if (!respuesta.ok) {
                avisar(datos.error || 'No se pudo buscar la ciudad.');
                return;
            }
            fijarPunto(datos.latitud, datos.longitud, datos.nombre);
        } catch {
            avisar('No se pudo buscar la ciudad. Revise su conexión.');
        }
    }

    parte('ubicacion').addEventListener('click', () => {
        if (!navigator.geolocation) {
            avisar('Su navegador no permite obtener la ubicación. Escriba una ciudad.');
            return;
        }
        avisar('Obteniendo su ubicación…');
        navigator.geolocation.getCurrentPosition(
            p => {
                ciudad.value = '';
                fijarPunto(p.coords.latitude, p.coords.longitude, '');
            },
            () => avisar('No pudimos obtener su ubicación. Escriba una ciudad.'),
            { enableHighAccuracy: true, timeout: 8000, maximumAge: 300000 });
    });

    parte('formulario').addEventListener('submit', evento => {
        evento.preventDefault();
        const nombre = ciudad.value.trim();
        if (nombre) buscarCiudad(nombre);
    });
    ciudad.addEventListener('focus', () => ciudad.select());
    radio.addEventListener('change', () => buscar());

    botonesFiltro.forEach(boton => boton.addEventListener('click', () => {
        boton.setAttribute('aria-pressed', String(boton.getAttribute('aria-pressed') !== 'true'));
        botonTodo.setAttribute('aria-pressed', String(!filtrosElegidos().length));
        buscarEnUnMomento();
    }));
    botonTodo.addEventListener('click', () => {
        botonesFiltro.forEach(b => b.setAttribute('aria-pressed', 'false'));
        botonTodo.setAttribute('aria-pressed', 'true');
        buscarEnUnMomento();
    });

    if (!disponible) {
        avisar('La búsqueda de lugares no está disponible en este momento.');
        return;
    }
    resumen.textContent = 'Use su ubicación o escriba una ciudad para empezar.';
    if (hayMapa) setTimeout(() => mapa.invalidateSize(), 300);

    let recordada = null;
    try { recordada = localStorage.getItem(CLAVE_CIUDAD); } catch { /* Sin almacenamiento, se empieza en blanco. */ }
    if (recordada) {
        ciudad.value = recordada;
        buscarCiudad(recordada);
    }
})();
