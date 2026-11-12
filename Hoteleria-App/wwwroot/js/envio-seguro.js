// En todas las páginas (se carga desde _Head):
// 1) Al enviar un formulario, sus botones se bloquean y dicen qué está pasando («Guardando…», «Creando…»)
//    hasta que llega la respuesta: un doble clic nunca envía dos veces.
// 2) Los avisos de éxito o error del servidor (_AvisoGlobal) aparecen arriba y se pueden cerrar.
// Los botones que hacen peticiones desde JavaScript usan window.envioSeguro.bloquear(boton).
(() => {
    const GIRO = '<span class="envio-giro" aria-hidden="true"></span>';
    const MAXIMO_MS = 45000; // Si no llega respuesta (una descarga, una red caída), el botón vuelve a funcionar.

    function textoPara(boton, metodo) {
        if (boton.dataset.enviando) return boton.dataset.enviando;
        if (metodo === 'get') return 'Buscando…';
        const t = boton.textContent.toLowerCase();
        if (/eliminar|borrar|quitar/.test(t)) return 'Eliminando…';
        if (/cancelar reserva|cancelar mi reserva/.test(t)) return 'Cancelando…';
        if (/iniciar sesi|entrar|ingresar/.test(t)) return 'Entrando…';
        if (/entrenar/.test(t)) return 'Entrenando…';
        if (/verificar|consultar/.test(t)) return 'Consultando…';
        if (/crear|registrar|nuev|añadir|agregar|invitar|generar|confirmar reserva/.test(t)) return 'Creando…';
        if (/enviar/.test(t)) return 'Enviando…';
        return 'Guardando…';
    }

    // Botón solo con icono (tiene aria-label): se cambia el icono por el giro, sin texto.
    const soloIcono = boton => boton.hasAttribute('aria-label') && boton.querySelectorAll('.material-symbols-outlined').length === 1
        && boton.textContent.trim() === boton.querySelector('.material-symbols-outlined').textContent.trim();

    /**
     * Bloquea un botón hasta que se llame a la función que devuelve.
     * texto: lo que muestra mientras espera; null deja su contenido (solo se desactiva).
     */
    function bloquear(boton, texto) {
        if (!boton || boton.dataset.bloqueado === '1') return () => {};
        boton.dataset.bloqueado = '1';
        const original = boton.innerHTML;
        boton.style.minWidth = `${boton.offsetWidth}px`;
        boton.setAttribute('aria-busy', 'true');
        boton.classList.add('enviando');
        if (texto !== null && boton.tagName === 'BUTTON') boton.innerHTML = soloIcono(boton) ? GIRO : GIRO + (texto ?? textoPara(boton, 'post'));
        // Se desactiva después de que el navegador haya armado los datos del envío (si no, el botón pulsado no viajaría).
        setTimeout(() => { if (boton.dataset.bloqueado === '1') boton.disabled = true; }, 0);
        const reloj = setTimeout(liberar, MAXIMO_MS);
        function liberar() {
            clearTimeout(reloj);
            if (boton.dataset.bloqueado !== '1') return;
            boton.disabled = false;
            boton.innerHTML = original;
            boton.style.minWidth = '';
            boton.removeAttribute('aria-busy');
            boton.classList.remove('enviando');
            delete boton.dataset.bloqueado;
        }
        return liberar;
    }

    /** Para peticiones hechas con fetch: bloquea el botón mientras dura la promesa. */
    async function mientras(boton, promesa, texto) {
        const liberar = bloquear(boton, texto);
        try { return await promesa; } finally { liberar(); }
    }

    const pendientes = new Set();

    // En la fase de burbuja, la validación del formulario o un «¿Está seguro?» ya pudieron cancelar el envío.
    document.addEventListener('submit', evento => {
        const form = evento.target;
        if (!(form instanceof HTMLFormElement) || form.hasAttribute('data-sin-bloqueo')) return;
        if (form.dataset.enviando === '1') { evento.preventDefault(); return; }
        if (evento.defaultPrevented) return;

        form.dataset.enviando = '1';
        const metodo = (form.getAttribute('method') || 'get').toLowerCase();
        const botones = [...form.elements].filter(e => e.matches('button:not([type="button"]):not([type="reset"]), input[type="submit"]'));
        const principal = evento.submitter && botones.includes(evento.submitter) ? evento.submitter : botones[0];
        const liberadores = botones.map(b => bloquear(b, b === principal ? textoPara(b, metodo) : null));
        const liberarTodo = () => { liberadores.forEach(l => l()); delete form.dataset.enviando; pendientes.delete(liberarTodo); };
        pendientes.add(liberarTodo);
        setTimeout(liberarTodo, MAXIMO_MS);
    });

    // Al volver con «Atrás», el navegador puede mostrar la página tal como quedó: se desbloquea.
    window.addEventListener('pageshow', evento => {
        if (evento.persisted) [...pendientes].forEach(liberar => liberar());
    });

    // Avisos del servidor: aparecen arriba; los de éxito se van solos y los de error esperan a que se cierren.
    function prepararAvisos() {
        document.querySelectorAll('[data-aviso]').forEach(aviso => {
            const cerrar = () => {
                aviso.classList.add('aviso-saliendo');
                setTimeout(() => aviso.remove(), 220);
            };
            aviso.querySelector('[data-aviso-cerrar]')?.addEventListener('click', cerrar);
            if (aviso.dataset.aviso === 'ok') setTimeout(cerrar, 6000);
        });
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', prepararAvisos);
    else prepararAvisos();

    window.envioSeguro = { bloquear, mientras };
})();
