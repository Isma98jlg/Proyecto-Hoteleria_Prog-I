// Verificación de documentos en ventanilla: consulta la fuente oficial y rellena los datos en el mismo formulario.
// Los datos llegan como texto y se asignan con .value: nunca se interpretan como HTML.
(() => {
    const raiz = document.getElementById('verificar-documento');
    if (!raiz) return;
    const formulario = raiz.closest('form');
    const campo = nombre => formulario.querySelector(`[name="Persona.${nombre}"]`);
    const parte = nombre => raiz.querySelector(`[data-verificar-${nombre}]`);
    const tipo = formulario.querySelector('[data-persona-tipo]');
    const token = parte('token');
    const resultado = parte('resultado');
    const boton = parte('boton');
    const fecha = parte('fecha');

    function mostrarFecha() {
        const visible = tipo.value === 'CE' || tipo.value === 'CPP';
        fecha.classList.toggle('hidden', !visible);
        fecha.querySelector('input').disabled = !visible;
    }

    function avisar(texto, tono) {
        resultado.textContent = texto;
        resultado.className = 'rounded-lg p-3 text-sm ' + (tono === 'ok' ? 'bg-tertiary-fixed text-on-tertiary-fixed' : 'bg-secondary-fixed text-on-secondary-fixed');
    }

    // Si se cambia el documento o los nombres después de verificar, la ficha se guarda sin verificar.
    function invalidar() {
        if (!token.value) return;
        token.value = '';
        avisar('Cambió el documento o los nombres: la ficha se guardará sin verificar, salvo que vuelva a verificar.', 'aviso');
    }

    ['TipoDocumento', 'NumeroDocumento', 'Nombres', 'Apellidos', 'PaisEmisor'].forEach(n => {
        campo(n)?.addEventListener('input', invalidar);
        campo(n)?.addEventListener('change', invalidar);
    });
    tipo.addEventListener('change', mostrarFecha);

    boton.addEventListener('click', async () => {
        boton.disabled = true;
        avisar('Consultando…', 'aviso');
        const datos = new FormData(formulario);
        datos.set('consentimiento', parte('consentimiento').checked ? 'true' : 'false');
        try {
            const respuesta = await fetch(`${location.pathname}?handler=Verificar`, { method: 'POST', body: datos });
            if (!respuesta.ok) throw new Error(String(respuesta.status));
            const r = await respuesta.json();
            if (r.nombres) campo('Nombres').value = r.nombres;
            if (r.apellidos) campo('Apellidos').value = r.apellidos;
            if (r.nacionalidad && campo('Nacionalidad')) campo('Nacionalidad').value = r.nacionalidad;
            token.value = r.token || '';
            avisar(r.mensaje, r.estado === 'SIN_VERIFICAR' ? 'aviso' : 'ok');
        } catch {
            avisar('No se pudo consultar ahora. Puede guardar la ficha sin verificar.', 'aviso');
        } finally {
            boton.disabled = false;
        }
    });

    mostrarFecha();
})();
