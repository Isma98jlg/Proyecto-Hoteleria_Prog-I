// Avisos en vivo con SignalR: muestra un aviso y actualiza las pantallas de consulta.
(() => {
    if (!window.signalR) return;

    const avisos = document.getElementById('avisos-vivo');
    const etiquetas = { PENDIENTE: 'pendiente', CONFIRMADA: 'confirmada', EN_CURSO: 'en curso', FINALIZADA: 'finalizada', CANCELADA: 'cancelada', ELIMINADA: 'eliminada', LIBRE: 'libre', OCUPADO: 'ocupada', MANTENIMIENTO: 'en mantenimiento' };
    const pantallasEnVivo = ['/', '/index', '/habitaciones', '/reservas', '/portal'];
    let recarga = null;

    function mostrar(texto) {
        const aviso = document.createElement('div');
        aviso.className = 'rounded-xl bg-primary-container text-on-primary px-4 py-3 shadow-xl text-sm';
        aviso.textContent = texto;
        avisos.appendChild(aviso);
        setTimeout(() => aviso.remove(), 5000);
    }

    function hayFormularioEnUso() {
        const foco = document.activeElement;
        const editando = foco && ['INPUT', 'SELECT', 'TEXTAREA'].includes(foco.tagName);
        const modalAbierto = ['reservationModal', 'floorModalBackdrop', 'ventana-flotante'].some(id => {
            const modal = document.getElementById(id);
            return modal && !modal.classList.contains('hidden');
        });
        return editando || modalAbierto;
    }

    function actualizarPantalla() {
        const ruta = window.location.pathname.toLowerCase().replace(/\/$/, '') || '/';
        if (!pantallasEnVivo.includes(ruta) || hayFormularioEnUso()) return;
        clearTimeout(recarga);
        recarga = setTimeout(() => { if (!hayFormularioEnUso()) window.location.reload(); }, 2500);
    }

    const conexion = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/hotel')
        .withAutomaticReconnect()
        .build();

    conexion.on('reservaActualizada', m => {
        mostrar(`Reserva #${m.reservaId}: ${etiquetas[m.estado] ?? m.estado}`);
        actualizarPantalla();
    });
    conexion.on('reservaPropiaActualizada', m => {
        mostrar(`Su reserva #${m.reservaId} está ${etiquetas[m.estado] ?? m.estado}`);
        actualizarPantalla();
    });
    conexion.on('habitacionActualizada', m => {
        mostrar(`Habitación ${m.habitacionId}: ${etiquetas[m.estado] ?? m.estado}`);
        actualizarPantalla();
    });

    conexion.start().catch(() => { /* Sin tiempo real, la aplicación sigue funcionando. */ });
})();
