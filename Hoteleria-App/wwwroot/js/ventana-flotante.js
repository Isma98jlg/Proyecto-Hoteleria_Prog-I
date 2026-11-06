// Abre los formularios y detalles en una ventana flotante, sin salir de la página actual.
(() => {
    const RUTAS_FLOTANTES = /^\/(reservas\/(crear|editar|detalles)|habitaciones\/(crear|editar|detalles)|usuarios\/(crear|editar|detalles)|empleados\/editar|portal\/(reservar|perfil|detalle)|account\/cambiarcontrasena|chat)(\/|$)/i;

    let ventana = null;
    let huboCambios = false;

    const esFlotante = url => RUTAS_FLOTANTES.test(new URL(url, location.origin).pathname);

    function conModal(url) {
        const destino = new URL(url, location.origin);
        destino.searchParams.set('modal', '1');
        return destino.pathname + destino.search;
    }

    function abrir(url) {
        cerrar(false);
        if (typeof toggleDrawer === 'function') toggleDrawer(false);
        if (typeof toggleBento === 'function') toggleBento(false);

        const esChat = /^\/chat/i.test(new URL(url, location.origin).pathname);
        ventana = document.createElement('div');
        ventana.id = 'ventana-flotante';
        ventana.className = 'fixed inset-0 z-[60] flex bg-primary/50 backdrop-blur-sm p-2 md:p-6 ' + (esChat ? 'items-end justify-start' : 'items-center justify-center');
        ventana.setAttribute('role', 'dialog');
        ventana.setAttribute('aria-modal', 'true');

        const panel = document.createElement('section');
        panel.className = 'flex w-full flex-col overflow-hidden rounded-2xl border border-surface-container-high bg-background shadow-2xl ' + (esChat ? 'h-[85vh] max-w-xl' : 'h-[92vh] max-w-5xl');

        const cabecera = document.createElement('header');
        cabecera.className = 'flex shrink-0 items-center justify-between gap-3 border-b border-surface-container-high bg-surface-container-lowest px-4 py-3';
        const titulo = document.createElement('strong');
        titulo.className = 'truncate text-on-surface';
        titulo.textContent = 'Cargando…';
        const cerrarBoton = document.createElement('button');
        cerrarBoton.type = 'button';
        cerrarBoton.className = 'flex h-9 w-9 shrink-0 items-center justify-center rounded-lg text-outline hover:bg-surface-container hover:text-on-surface';
        cerrarBoton.setAttribute('aria-label', 'Cerrar');
        cerrarBoton.innerHTML = '<span class="material-symbols-outlined">close</span>';
        cerrarBoton.addEventListener('click', () => cerrar());
        cabecera.append(titulo, cerrarBoton);

        const marco = document.createElement('iframe');
        marco.className = 'w-full flex-1 border-0';
        marco.title = 'Ventana flotante';
        marco.src = conModal(url);
        marco.addEventListener('load', () => {
            try { titulo.textContent = marco.contentDocument.title || ''; } catch { titulo.textContent = ''; }
        });

        panel.append(cabecera, marco);
        ventana.append(panel);
        ventana.addEventListener('click', evento => { if (evento.target === ventana) cerrar(); });
        document.body.append(ventana);
        document.body.classList.add('overflow-hidden');
    }

    function cerrar(recargarSiHuboCambios = true) {
        if (!ventana) return;
        ventana.remove();
        ventana = null;
        document.body.classList.remove('overflow-hidden');
        if (recargarSiHuboCambios && huboCambios) location.reload();
        huboCambios = false;
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
        const { tipo, url } = evento.data || {};
        if (tipo === 'listo' && typeof url === 'string') seguir(url, true);
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
