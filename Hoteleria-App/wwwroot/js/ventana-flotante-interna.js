// Se ejecuta dentro de una ventana flotante: mantiene los formularios en ella y consulta a la
// página principal qué hacer con los enlaces.
(() => {
    if (window.parent === window) return;

    document.addEventListener('submit', evento => {
        const form = evento.target;
        if ((form.getAttribute('method') || 'get').toLowerCase() === 'post') {
            const url = new URL(form.getAttribute('action') || location.href, location.href);
            url.searchParams.set('modal', '1');
            form.setAttribute('action', url.pathname + url.search);
        } else if (!form.querySelector('input[name="modal"]')) {
            const campo = document.createElement('input');
            campo.type = 'hidden';
            campo.name = 'modal';
            campo.value = '1';
            form.appendChild(campo);
        }
    }, true);

    document.addEventListener('click', evento => {
        const enlace = evento.target.closest('a[href]');
        if (!enlace || enlace.origin !== location.origin) return;
        if (enlace.getAttribute('href').startsWith('#')) return;
        evento.preventDefault();
        parent.postMessage({ tipo: 'navegar', url: enlace.pathname + enlace.search }, location.origin);
    });

    document.addEventListener('keydown', evento => {
        if (evento.key === 'Escape') parent.postMessage({ tipo: 'cerrar' }, location.origin);
    });
})();
