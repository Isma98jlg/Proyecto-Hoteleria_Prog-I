// Panel del clima del huésped. La ubicación solo se pide al pulsar el botón y no se guarda en el navegador.
(() => {
    const raiz = document.getElementById('clima-huesped');
    if (!raiz) return;

    const parte = nombre => raiz.querySelector(`[data-clima-${nombre}]`);
    const panel = parte('panel');
    const abrir = parte('abrir');
    const resultado = parte('resultado');
    const vacio = parte('vacio');
    const aviso = parte('aviso');
    const ciudad = parte('ciudad');
    const CLAVE_CIUDAD = 'clima-ciudad';

    function alternar(abierto) {
        panel.classList.toggle('clima-abierto', abierto);
        abrir.classList.toggle('clima-oculto', abierto);
        abrir.setAttribute('aria-expanded', String(abierto));
        if (abierto) parte('cerrar').focus({ preventScroll: true });
        else if (panel.contains(document.activeElement)) abrir.focus({ preventScroll: true });
    }

    function avisar(texto) {
        aviso.textContent = texto || '';
        aviso.classList.toggle('hidden', !texto);
    }

    function mostrar(c) {
        const temperatura = `${Math.round(c.temperatura)}°`;
        parte('icono').textContent = c.icono;
        parte('lugar').textContent = c.lugar;
        parte('temperatura').textContent = temperatura;
        parte('descripcion').textContent = c.descripcion;
        parte('sensacion').textContent = `${Math.round(c.sensacion)}°`;
        parte('humedad').textContent = `${c.humedad} %`;
        parte('viento').textContent = `${Math.round(c.viento)} km/h`;
        parte('boton-icono').textContent = c.icono;
        parte('boton-temperatura').textContent = temperatura;
        vacio.classList.add('hidden');
        resultado.classList.remove('hidden');
        resultado.classList.add('flex');
    }

    async function consultar(parametros) {
        avisar('Consultando el clima…');
        try {
            const respuesta = await fetch(`/Portal/Clima?${new URLSearchParams(parametros)}`);
            const datos = await respuesta.json().catch(() => ({}));
            if (!respuesta.ok) {
                avisar(datos.error || 'No se pudo consultar el clima.');
                return false;
            }
            mostrar(datos);
            avisar('');
            return true;
        } catch {
            avisar('No se pudo consultar el clima. Revise su conexión.');
            return false;
        }
    }

    function guardarCiudad(valor) {
        try {
            if (valor) localStorage.setItem(CLAVE_CIUDAD, valor);
            else localStorage.removeItem(CLAVE_CIUDAD);
        } catch { /* Sin almacenamiento, simplemente no se recuerda. */ }
    }

    function leerCiudad() {
        try { return localStorage.getItem(CLAVE_CIUDAD); } catch { return null; }
    }

    // Al pulsar el campo se selecciona la ciudad recordada: lo que se escriba la reemplaza.
    ciudad.addEventListener('focus', () => ciudad.select());
    abrir.addEventListener('click', () => alternar(true));
    parte('cerrar').addEventListener('click', () => alternar(false));
    document.addEventListener('keydown', evento => {
        if (evento.key === 'Escape' && panel.classList.contains('clima-abierto')) alternar(false);
    });

    parte('ubicacion').addEventListener('click', () => {
        if (!navigator.geolocation) {
            avisar('Su navegador no permite obtener la ubicación. Escriba su ciudad.');
            return;
        }
        avisar('Obteniendo su ubicación…');
        const liberar = window.envioSeguro?.bloquear(parte('ubicacion'), 'Buscando…') ?? (() => {});
        navigator.geolocation.getCurrentPosition(
            async posicion => {
                guardarCiudad(null);
                await consultar({ lat: posicion.coords.latitude, lon: posicion.coords.longitude });
                liberar();
            },
            () => { liberar(); avisar('No pudimos obtener su ubicación. Escriba su ciudad.'); },
            { enableHighAccuracy: false, timeout: 8000, maximumAge: 600000 });
    });

    parte('formulario').addEventListener('submit', async evento => {
        evento.preventDefault();
        const nombre = ciudad.value.trim();
        if (!nombre) return;
        const liberar = window.envioSeguro?.bloquear(evento.target.querySelector('button[type="submit"]')) ?? (() => {});
        try { if (await consultar({ ciudad: nombre })) guardarCiudad(nombre); } finally { liberar(); }
    });

    const recordada = leerCiudad();
    if (recordada) {
        ciudad.value = recordada;
        consultar({ ciudad: recordada });
    }
})();
