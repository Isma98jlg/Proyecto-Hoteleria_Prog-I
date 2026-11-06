// Abre los formularios y detalles en una ventana flotante, sin salir de la página actual.
(() => {
    const RUTAS_FLOTANTES = /^\/(reservas\/(crear|editar|detalles)|habitaciones\/(crear|editar|detalles)|usuarios\/(crear|editar|detalles)|empleados\/editar|portal\/(reservar|perfil|detalle|asistente)|account\/cambiarcontrasena|chat)(\/|$)/i;

    let ventana = null;
    let huboCambios = false;
    let foco = null;
    const DURACION_CIERRE_MS = 170;

    const esFlotante = url => RUTAS_FLOTANTES.test(new URL(url, location.origin).pathname);

    function conModal(url) {
        const destino = new URL(url, location.origin);
        destino.searchParams.set('modal', '1');
        return destino.pathname + destino.search;
    }

    // Cada ventana usa el ancho que necesita su contenido.
    function anchoPara(ruta) {
        if (/^\/portal\/asistente/i.test(ruta)) return 'max-w-lg vf-alto-fijo';
        if (/^\/(chat|portal\/perfil|account\/cambiarcontrasena)/i.test(ruta)) return 'max-w-lg';
        if (/^\/reservas\//i.test(ruta)) return 'max-w-4xl';
        return 'max-w-3xl';
    }

    function abrir(url) {
        cerrar(false);
        if (typeof toggleDrawer === 'function') toggleDrawer(false);
        if (typeof toggleBento === 'function') toggleBento(false);

        const ruta = new URL(url, location.origin).pathname;
        foco = document.activeElement;
        ventana = document.createElement('div');
        ventana.id = 'ventana-flotante';
        ventana.className = 'vf-fondo fixed inset-0 z-[60] flex ' + (/^\/(chat|portal\/asistente)/i.test(ruta) ? 'vf-izquierda' : 'vf-centro');
        ventana.setAttribute('role', 'dialog');
        ventana.setAttribute('aria-modal', 'true');
        ventana.setAttribute('aria-labelledby', 'ventana-flotante-titulo');

        const panel = document.createElement('section');
        panel.className = 'vf-panel flex w-full flex-col overflow-hidden bg-surface-container-lowest ' + anchoPara(ruta);

        const cabecera = document.createElement('header');
        cabecera.className = 'vf-cabecera relative flex shrink-0 items-center justify-between gap-3 py-3 pl-5 pr-3';
        const titulo = document.createElement('h2');
        titulo.id = 'ventana-flotante-titulo';
        titulo.className = 'truncate font-headline-sm text-headline-sm text-on-surface';
        titulo.textContent = 'Cargando…';
        const cerrarBoton = document.createElement('button');
        cerrarBoton.type = 'button';
        cerrarBoton.className = 'flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-outline transition-colors hover:bg-surface-container hover:text-on-surface';
        cerrarBoton.setAttribute('aria-label', 'Cerrar');
        cerrarBoton.innerHTML = '<span class="material-symbols-outlined text-[20px]">close</span>';
        cerrarBoton.addEventListener('click', () => cerrar());
        const carga = document.createElement('span');
        carga.className = 'vf-carga';
        cabecera.append(titulo, cerrarBoton, carga);

        const marco = document.createElement('iframe');
        marco.className = 'vf-marco w-full flex-1 border-0';
        marco.title = 'Contenido de la ventana';
        marco.src = conModal(url);
        marco.addEventListener('load', () => {
            try { titulo.textContent = marco.contentDocument.title || ''; } catch { titulo.textContent = ''; }
            panel.classList.add('vf-listo');
        });

        panel.append(cabecera, marco);
        ventana.append(panel);
        ventana.addEventListener('click', evento => { if (evento.target === ventana) cerrar(); });
        document.body.append(ventana);
        document.body.classList.add('overflow-hidden');
        panel.tabIndex = -1;
        panel.focus({ preventScroll: true });
    }

    function cerrar(recargarSiHuboCambios = true) {
        if (!ventana) return;
        const saliente = ventana;
        ventana = null;
        saliente.classList.add('vf-cerrando');
        setTimeout(() => saliente.remove(), DURACION_CIERRE_MS);
        document.body.classList.remove('overflow-hidden');
        foco?.focus?.({ preventScroll: true });
        foco = null;
        if (recargarSiHuboCambios && huboCambios) location.reload();
        huboCambios = false;
    }

    // La página interna informa de su altura: la ventana se ajusta sin pasar del máximo de la pantalla.
    function ajustarAltura(alto) {
        const panel = ventana.querySelector('.vf-panel');
        if (panel.classList.contains('vf-alto-fijo')) return;
        panel.style.height = `${Math.ceil(alto) + panel.querySelector('header').offsetHeight}px`;
    }

    // replace() evita sumar entradas al historial del navegador.
    function irDentro(url) {
        ventana.querySelector('iframe').contentWindow.location.replace(conModal(url));
    }

    function seguir(url, hayCambios) {
        const destino = new URL(url, location.origin);
        if (destino.origin !== location.origin) return;

        if (ventana && esFlotante(destino.href)) {
            huboCambios = huboCambios || hayCambios;
            irDentro(destino.pathname + destino.search);
            return;
        }
        // Volver a la misma página solo cierra la ventana, salvo que deba mostrar cambios o un mensaje.
        const mismaPagina = destino.pathname.toLowerCase() === location.pathname.toLowerCase();
        if (mismaPagina && !hayCambios) {
            cerrar();
            return;
        }
        cerrar(false);
        location.assign(destino.pathname + destino.search);
    }

    window.addEventListener('message', evento => {
        if (evento.origin !== location.origin || !ventana) return;
        const { tipo, url, alto } = evento.data || {};
        if (tipo === 'altura' && typeof alto === 'number') ajustarAltura(alto);
        else if (tipo === 'cambios') huboCambios = true;
        else if (tipo === 'listo' && typeof url === 'string') seguir(url, true);
        else if (tipo === 'navegar' && typeof url === 'string') seguir(url, false);
        else if (tipo === 'cerrar') cerrar();
    });

    document.addEventListener('click', evento => {
        if (evento.defaultPrevented || evento.button !== 0 || evento.ctrlKey || evento.metaKey || evento.shiftKey) return;
        const enlace = evento.target.closest('a[href]');
        if (!enlace || enlace.origin !== location.origin || !esFlotante(enlace.href)) return;
        evento.preventDefault();
        abrir(enlace.pathname + enlace.search);
    });

    document.addEventListener('keydown', evento => {
        if (evento.key === 'Escape') cerrar();
    });
})();
