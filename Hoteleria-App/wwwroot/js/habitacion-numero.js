// Número de habitación: el piso seguido de dos dígitos (piso 4 → 401…499).
// Al elegir el piso propone el siguiente número libre y muestra el código tal como se verá (por ejemplo, 4V-401).
(() => {
    const raiz = document.getElementById('numero-habitacion');
    if (!raiz) return;
    const formulario = raiz.closest('form');
    const piso = raiz.querySelector('[data-numero-piso]');
    const numero = raiz.querySelector('[data-numero-valor]');
    const codigo = formulario.querySelector('[data-numero-codigo]');
    const aviso = formulario.querySelector('[data-numero-aviso]');
    const categoria = formulario.querySelector('[name="Habitacion.TipoVipNombre"]');
    const existentes = new Set((raiz.dataset.existentes || '').split(',').filter(Boolean).map(Number));
    const siglas = Object.fromEntries((raiz.dataset.siglas || '').split(',').filter(Boolean).map(par => par.split(':')));
    let sugerido = null;
    // El formulario llega con 0 (número sin elegir): se muestra vacío para que se vea el ejemplo y la sugerencia.
    if (Number(numero.value) === 0) numero.value = '';

    function siguienteLibre(p) {
        for (let n = p * 100 + 1; n <= p * 100 + 99; n++) if (!existentes.has(n)) return n;
        return null;
    }

    function actualizar() {
        const p = Number(piso.value);
        const n = Number(numero.value);
        const sigla = siglas[categoria.value] || '?';
        codigo.textContent = p && n ? `${p}${sigla}-${String(n).padStart(3, '0')}` : '—';
        if (!p || !n) aviso.textContent = '';
        else if (Math.floor(n / 100) !== p || n % 100 < 1) aviso.textContent = `En el piso ${p} el número va del ${p * 100 + 1} al ${p * 100 + 99}.`;
        else if (existentes.has(n)) aviso.textContent = 'Ya existe una habitación con ese número.';
        else aviso.textContent = '';
    }

    piso.addEventListener('change', () => {
        const p = Number(piso.value);
        if (p) {
            numero.min = p * 100 + 1;
            numero.max = p * 100 + 99;
            // Solo se reemplaza si está vacío o si era la sugerencia anterior: nunca lo que escribió la persona.
            if (!Number(numero.value) || Number(numero.value) === sugerido) {
                sugerido = siguienteLibre(p);
                if (sugerido) numero.value = sugerido;
            }
        }
        actualizar();
    });
    numero.addEventListener('input', actualizar);
    categoria.addEventListener('change', actualizar);
    actualizar();
})();
