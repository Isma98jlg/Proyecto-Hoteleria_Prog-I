// Registro de huéspedes: verifica el documento en cuanto está completo y rellena los datos con los oficiales.
// Sin JavaScript, todo el formulario queda visible y editable, y la cuenta se crea sin verificar.
(() => {
    const formulario = document.getElementById('registro');
    if (!formulario) return;

    const parte = nombre => formulario.querySelector(`[data-registro-${nombre}]`);
    const campo = nombre => formulario.querySelector(`[name="Persona.${nombre}"]`);
    const tipos = [...formulario.querySelectorAll('[data-registro-tipo]')];
    const oficiales = [...formulario.querySelectorAll('[data-registro-oficial]')];
    const paso2 = parte('paso2');
    const resultado = parte('resultado');
    const token = parte('token');
    const consentimiento = parte('consentimiento');
    const numero = campo('NumeroDocumento');
    const pais = campo('PaisEmisor');
    const fecha = formulario.querySelector('[name="FechaNacimiento"]');
    const nota = parte('nota');

    const AYUDAS = {
        DNI: ['8 dígitos', 'MigoAPI y RENIEC (ApisTed)', /^\d{8}$/],
        Pasaporte: ['Letras y números, tal como figuran en el pasaporte', 'Luhnify, que comprueba el formato del número', /^[A-Za-z0-9]{5,15}$/],
        CPP: ['Solo los dígitos del carné', 'Migraciones, a través de ApisTed', /^\d{6,12}$/],
        CE: ['Solo los dígitos del carné', 'Migraciones, a través de ApisTed', /^\d{6,12}$/]
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
    // El pasaporte no tiene fuente oficial de nombres: se escriben a mano.
    const vienenDeLaFuente = () => tipoActual() !== 'Pasaporte';

    // Modo de los datos personales: «espera» (bloqueados hasta verificar), «oficial» (rellenados por la fuente)
    // o «manual» (los escribe el huésped: pasaporte o continuar sin verificar).
    let modo = 'espera';
    function fijarModo(nuevo) {
        modo = nuevo;
        oficiales.forEach(c => {
            c.readOnly = nuevo !== 'manual';
            c.placeholder = nuevo === 'espera' ? 'Se completa al verificar' : '';
        });
        nota.classList.toggle('hidden', nuevo === 'manual');
        parte('nota-texto').textContent = nuevo === 'oficial'
            ? 'Datos tomados del documento. Si no son correctos, cambie el número o continúe sin verificar.'
            : 'Se completan solos con los datos oficiales al verificar su documento.';
    }

    function vaciarOficiales() {
        oficiales.forEach(c => { c.value = ''; });
    }

    function mostrarSegunTipo() {
        const tipo = tipoActual();
        formulario.querySelectorAll('[data-solo]').forEach(bloque => {
            const visible = bloque.dataset.solo.split(' ').includes(tipo);
            bloque.classList.toggle('hidden', !visible);
            bloque.querySelectorAll('input, select').forEach(control => { control.disabled = !visible; });
        });
        parte('ayuda').textContent = AYUDAS[tipo][0];
        parte('fuente').textContent = AYUDAS[tipo][1];
    }

    function mostrarResultado(texto, tono) {
        resultado.textContent = texto;
        resultado.className = 'rounded-xl p-3 text-sm ' + ({
            ok: 'bg-tertiary-fixed text-on-tertiary-fixed',
            aviso: 'bg-secondary-fixed text-on-secondary-fixed',
            error: 'bg-error-container text-on-error-container'
        }[tono]);
    }

    function ocultarResultado() { resultado.className = 'hidden'; }

    function abrirPaso2() {
        paso2.classList.remove('hidden');
        parte('crear').classList.remove('hidden');
    }

    function cerrarPaso2() {
        paso2.classList.add('hidden');
        parte('crear').classList.add('hidden');
    }

    // ¿Están todos los datos que pide la fuente de este documento?
    function completo() {
        const tipo = tipoActual();
        if (!AYUDAS[tipo][2].test(numero.value.trim())) return false;
        if (tipo === 'Pasaporte' && !pais.value) return false;
        if ((tipo === 'CE' || tipo === 'CPP') && !fecha.value) return false;
        return true;
    }

    const clave = () => [tipoActual(), numero.value.trim(), pais.value, fecha.value].join('|');
    let ultimaClave = '';
    let espera = null;
    let peticion = 0;

    // Cualquier cambio del documento anula la verificación anterior y sus datos.
    function alCambiarDocumento() {
        if (clave() === ultimaClave) return;
        token.value = '';
        ultimaClave = '';
        if (vienenDeLaFuente()) {
            if (modo === 'oficial') vaciarOficiales();
            fijarModo(modo === 'manual' && paso2Abierto ? 'manual' : 'espera');
        }
        if (!paso2Abierto) cerrarPaso2();
        programar();
    }

    let paso2Abierto = false;

    function programar() {
        clearTimeout(espera);
        if (!completo()) { ocultarResultado(); return; }
        if (!consentimiento.checked) {
            mostrarResultado('Marque la autorización de arriba para verificar su documento, o continúe sin verificar.', 'aviso');
            return;
        }
        espera = setTimeout(verificar, 450);
    }

    async function verificar() {
        const actual = clave();
        if (actual === ultimaClave) return;
        ultimaClave = actual;
        const esta = ++peticion;
        mostrarResultado(['CE', 'CPP'].includes(tipoActual()) ? 'Consultando a Migraciones; puede tardar hasta medio minuto…' : 'Verificando su documento…', 'aviso');
        const datos = new FormData(formulario);
        datos.set('consentimiento', 'true');
        try {
            const respuesta = await fetch(`${location.pathname}?handler=Verificar`, { method: 'POST', body: datos });
            if (!respuesta.ok) throw new Error(String(respuesta.status));
            const r = await respuesta.json();
            if (esta !== peticion || clave() !== actual) return; // Llegó tarde: el documento ya cambió.
            token.value = r.token || '';
            if (r.nombres) {
                campo('Nombres').value = r.nombres;
                campo('Apellidos').value = r.apellidos || '';
                if (r.nacionalidad && campo('Nacionalidad')) campo('Nacionalidad').value = r.nacionalidad;
                fijarModo('oficial');
            }
            mostrarResultado(r.mensaje, r.token ? 'ok' : 'aviso');
            if (r.token) { paso2Abierto = true; abrirPaso2(); }
            if (!r.token) ultimaClave = ''; // Permite reintentar si se corrige un dato.
        } catch {
            if (esta !== peticion) return;
            ultimaClave = '';
            mostrarResultado('No se pudo verificar ahora. Puede continuar sin verificar.', 'error');
        }
    }

    tipos.forEach(t => t.addEventListener('change', () => {
        mostrarSegunTipo();
        if (!vienenDeLaFuente()) fijarModo('manual');
        else if (modo !== 'oficial') fijarModo(paso2Abierto && modo === 'manual' ? 'manual' : 'espera');
        alCambiarDocumento();
    }));
    formulario.querySelectorAll('[data-registro-documento]').forEach(c => {
        c.addEventListener('input', alCambiarDocumento);
        c.addEventListener('change', alCambiarDocumento);
    });
    consentimiento.addEventListener('change', () => { ultimaClave = ''; programar(); });

    parte('saltar').addEventListener('click', () => {
        clearTimeout(espera);
        peticion++;
        token.value = '';
        ultimaClave = '';
        if (modo === 'oficial') vaciarOficiales();
        fijarModo('manual');
        mostrarResultado('Continuará sin verificar: escriba sus datos tal como figuran en su documento. Recepción lo revisará a su llegada.', 'aviso');
        paso2Abierto = true;
        abrirPaso2();
        campo('Nombres').focus();
    });

    mostrarSegunTipo();
    if (formulario.dataset.paso2 === '1') {
        // Volvió del servidor con un error: se conservan los datos y la verificación que hubiera.
        paso2Abierto = true;
        fijarModo(!vienenDeLaFuente() ? 'manual' : token.value ? 'oficial' : 'manual');
        if (token.value) ultimaClave = clave();
    } else {
        fijarModo(vienenDeLaFuente() ? 'espera' : 'manual');
        cerrarPaso2();
        programar();
    }
})();
