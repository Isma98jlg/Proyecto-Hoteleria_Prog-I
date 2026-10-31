// ============================================================================
// GrandStay HMS v3.2 — Shell interactivo (drawer + Bento ⌘K)
// Migrado desde el code.html de referencia (Stitch).
// ============================================================================

// 1. Flyout Drawer Interactions
function toggleDrawer(open) {
    const container = document.getElementById('flyout-drawer-container');
    const backdrop = document.getElementById('drawer-backdrop');
    const panel = document.getElementById('drawer-panel');

    if (!container || !backdrop || !panel) return;

    if (open) {
        container.classList.remove('pointer-events-none');
        backdrop.classList.remove('opacity-0', 'pointer-events-none');
        backdrop.classList.add('opacity-100', 'pointer-events-auto');
        panel.classList.remove('-translate-x-full');
        panel.classList.add('translate-x-0');
    } else {
        backdrop.classList.remove('opacity-100', 'pointer-events-auto');
        backdrop.classList.add('opacity-0', 'pointer-events-none');
        panel.classList.remove('translate-x-0');
        panel.classList.add('-translate-x-full');
        setTimeout(() => {
            container.classList.add('pointer-events-none');
        }, 300);
    }
}

// 2. Bento Modal (⌘K) Interactions
function toggleBento(open) {
    const modalContainer = document.getElementById('bento-modal-container');
    const backdrop = document.getElementById('bento-backdrop');
    const panel = document.getElementById('bento-panel');
    const input = document.getElementById('bento-search-input');

    if (!modalContainer || !backdrop || !panel) return;

    if (open) {
        modalContainer.classList.remove('hidden', 'pointer-events-none');
        modalContainer.classList.add('flex', 'pointer-events-auto');
        setTimeout(() => {
            backdrop.classList.remove('opacity-0');
            backdrop.classList.add('opacity-100');
            panel.classList.remove('scale-95', 'opacity-0');
            panel.classList.add('scale-100', 'opacity-100');
            if (input) input.focus();
        }, 10);
    } else {
        backdrop.classList.remove('opacity-100');
        backdrop.classList.add('opacity-0');
        panel.classList.remove('scale-100', 'opacity-100');
        panel.classList.add('scale-95', 'opacity-0');
        setTimeout(() => {
            modalContainer.classList.remove('flex', 'pointer-events-auto');
            modalContainer.classList.add('hidden', 'pointer-events-none');
        }, 200);
    }
}

// 3. Keyboard Shortcuts (Cmd+K / Ctrl+K and ESC)
document.addEventListener('keydown', function (e) {
    if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault();
        const modalContainer = document.getElementById('bento-modal-container');
        const isVisible = modalContainer && !modalContainer.classList.contains('hidden');
        toggleBento(!isVisible);
    }

    if (e.key === 'Escape') {
        toggleBento(false);
        toggleDrawer(false);
    }
});
