// Registro de huéspedes: el formulario cambia según el documento y la verificación ocurre sin recargar.
// Sin JavaScript, todo el formulario queda visible y la cuenta se crea sin verificar.
(() => {
    const formulario = document.getElementById('registro');
    if (!formulario) return;

    const parte = nombre => formulario.querySelector(`[data-registro-${nombre}]`);
    const tipos = [...formulario.querySelectorAll('[data-registro-tipo]')];
    const paso2 = parte('paso2');
    const resultado = parte('resultado');
    const verificar = parte('verificar');
    const token = parte('token');
    const ayuda = parte('ayuda');
    const fuente = parte('fuente');

    const AYUDAS = {
        DNI: ['8 dígitos', 'RENIEC, a través de PeruAPI'],
        Pasaporte: ['Letras y números, tal como figuran en el pasaporte', 'Luhnify, que comprueba el formato del número'],
        CPP: ['Solo los dígitos del carné', 'Migraciones, a través de ApisTed'],
        CE: ['Solo los dígitos del carné', 'Migraciones, a través de ApisTed']
    };

    // Nombres de países en español, generados por el navegador a partir del código ISO.
    try {
        const nombres = new Intl.DisplayNames(['es'], { type: 'region' });
        const select = parte('paises');
        const opciones = [...select.options].slice(1);
        opciones.forEach(o => { o.textContent = nombres.of(o.value) || o.value; });
        opciones.sort((a, b) => a.textContent.localeCompare(b.textContent, 'es')).forEach(o => select.append(o));
    } catch { /* Sin Intl.DisplayNames se muestran los códigos. */ }

    const tipoActual = () => tipos.find(t => t.checked)?.value || 'DNI';

    function mostrarSegunTipo() {
        const tipo = tipoActual();
        formulario.querySelectorAll('[data-solo]').forEach(campo => {
            const visible = campo.dataset.solo.split(' ').includes(tipo);
            campo.classList.toggle('hidden', !visible);
            campo.querySelectorAll('input, select').forEach(control => { control.disabled = !visible; });
        });
        ayuda.textContent = AYUDAS[tipo][0];
        fuente.textContent = AYUDAS[tipo][1];
    }

    function mostrarResultado(texto, tono) {
        resultado.textContent = texto;
        resultado.className = 'rounded-xl p-3 text-sm ' + ({
            ok: 'bg-tertiary-fixed text-on-tertiary-fixed',
            aviso: 'bg-secondary-fixed text-on-secondary-fixed',
            error: 'bg-error-container text-on-error-container'
        }[tono]);
    }

    function abrirPaso2() {
        paso2.classList.remove('hidden');
        parte('crear').classList.remove('hidden');
    }

    // Cambiar el documento después de verificarlo anula la verificación.
    function invalidar() {
        if (!token.value) return;
        token.value = '';
        mostrarResultado('Cambió los datos del documento: verifíquelo de nuevo o continúe sin verificar.', 'aviso');
    }

    tipos.forEach(t => t.addEventListener('change', () => { mostrarSegunTipo(); invalidar(); }));
    formulario.querySelectorAll('[data-registro-documento]').forEach(c => c.addEventListener('input', invalidar));

    verificar.addEventListener('click', async () => {
        verificar.disabled = true;
        mostrarResultado('Verificando…', 'aviso');
        const datos = new FormData(formulario);
        datos.set('consentimiento', parte('consentimiento').checked ? 'true' : 'false');
        try {
            const respuesta = await fetch(`${location.pathname}?handler=Verificar`, { method: 'POST', body: datos });
            if (!respuesta.ok) throw new Error(String(respuesta.status));
            const r = await respuesta.json();
            token.value = r.token || '';
            mostrarResultado(r.mensaje, r.estado === 'CONFIRMADO' ? 'ok' : r.estado === 'FORMATO_VALIDO' ? 'ok' : 'aviso');
            if (r.token) abrirPaso2();
        } catch {
            mostrarResultado('No se pudo verificar ahora. Puede continuar sin verificar.', 'error');
        } finally {
            verificar.disabled = false;
        }
    });

    parte('saltar').addEventListener('click', () => {
        token.value = '';
        mostrarResultado('Continuará sin verificar: recepción revisará su documento a su llegada.', 'aviso');
        abrirPaso2();
        paso2.querySelector('input')?.focus();
    });

    mostrarSegunTipo();
    if (formulario.dataset.paso2 !== '1') {
        paso2.classList.add('hidden');
        parte('crear').classList.add('hidden');
    }
})();
