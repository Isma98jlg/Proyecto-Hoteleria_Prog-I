// Chat de reservas del huésped. Todo el texto se pinta con textContent: nunca se interpreta como HTML.
(() => {
    const raiz = document.getElementById('chat-huesped');
    if (!raiz) return;

    const lista = document.getElementById('chat-mensajes');
    const formulario = document.getElementById('chat-formulario');
    const entrada = document.getElementById('chat-entrada');
    const enviar = document.getElementById('chat-enviar');
    const avisoError = document.getElementById('chat-error');
    const reiniciar = document.getElementById('chat-reiniciar');
    const token = raiz.querySelector('input[name="__RequestVerificationToken"]').value;
    const disponible = raiz.dataset.disponible === '1';

    const BIENVENIDA = 'Hola, soy el asistente de GrandStay. Cuénteme qué necesita: fechas, noches, cuántas personas y si prefiere alguna categoría. Puede escribirlo todo de una vez.';
    let ocupado = false;

    function elemento(etiqueta, clases, texto) {
        const nodo = document.createElement(etiqueta);
        if (clases) nodo.className = clases;
        if (texto !== undefined) nodo.textContent = texto;
        return nodo;
    }

    function burbuja(rol, texto) {
        const esHuesped = rol === 'user';
        return elemento('div',
            'max-w-[85%] whitespace-pre-wrap break-words rounded-2xl px-3.5 py-2.5 text-body-md '
            + (esHuesped ? 'self-end rounded-br-md bg-primary-container text-on-primary' : 'self-start rounded-bl-md bg-surface-container text-on-surface'),
            texto);
    }

    function dato(titulo, valor) {
        const fila = elemento('div', 'flex justify-between gap-3');
        fila.append(elemento('span', 'text-on-surface-variant', titulo), elemento('span', 'text-right font-semibold', valor));
        return fila;
    }

    function tarjeta(propuesta) {
        const caja = elemento('div', 'flex w-full flex-col gap-3 rounded-2xl border border-outline-variant p-4 text-body-sm');
        caja.append(elemento('strong', 'text-headline-sm', 'Su propuesta de reserva'));

        const datos = elemento('div', 'flex flex-col gap-1.5');
        datos.append(
            dato('Entrada', propuesta.entrada),
            dato('Salida', `${propuesta.salida} · ${propuesta.noches} noche(s)`),
            dato('Personas', String(propuesta.personas)));
        propuesta.habitaciones.forEach(h =>
            datos.append(dato(`Habitación ${h.codigo}`, `${h.categoria} · ${h.camas} cama(s) · ${h.precio}/noche${h.ubicacion ? ` · ${h.ubicacion}` : ''}`)));
        caja.append(datos);

        const pie = elemento('div', 'flex items-center justify-between gap-3 border-t border-surface-container-high pt-3');
        const total = elemento('div');
        total.append(elemento('small', 'block text-outline', 'Total'), elemento('strong', 'text-headline-md', `${propuesta.total}`));
        const confirmar = elemento('button', 'rounded-lg bg-primary px-4 py-2.5 font-semibold text-on-primary transition-opacity disabled:opacity-40', 'Confirmar reserva');
        confirmar.type = 'button';
        confirmar.disabled = ocupado;
        confirmar.addEventListener('click', () => pedir('Confirmar'));
        pie.append(total, confirmar);
        caja.append(pie);
        return caja;
    }

    function mostrar(datos) {
        lista.replaceChildren();
        if (!disponible) {
            lista.append(burbuja('assistant', 'El asistente no está disponible en este momento. Puede reservar desde «Reservar habitación».'));
        } else if (datos.mensajes.length === 0) {
            lista.append(burbuja('assistant', BIENVENIDA));
        }
        datos.mensajes.forEach(m => lista.append(burbuja(m.rol, m.texto)));
        if (datos.propuesta) lista.append(tarjeta(datos.propuesta));
        if (datos.reservaCreada) {
            const enlace = elemento('a', 'self-start rounded-lg bg-surface-container px-4 py-2 font-semibold', 'Ver mis reservas');
            enlace.href = '/Portal';
            lista.append(enlace);
            // Al cerrar la ventana, «Mis reservas» debe mostrar la nueva.
            parent.postMessage({ tipo: 'cambios' }, location.origin);
        }
        avisoError.textContent = datos.error || '';
        avisoError.classList.toggle('hidden', !datos.error);
        lista.scrollTop = lista.scrollHeight;
    }

    function ocupar(valor) {
        ocupado = valor;
        enviar.disabled = valor || !disponible;
        entrada.disabled = !disponible;
        reiniciar.disabled = valor;
        raiz.querySelectorAll('#chat-mensajes button').forEach(b => { b.disabled = valor; });
    }

    async function pedir(accion, mensaje) {
        if (ocupado) return;
        ocupar(true);
        avisoError.classList.add('hidden');
        const escribiendo = elemento('div', 'self-start rounded-2xl rounded-bl-md bg-surface-container px-3.5 py-2.5 text-body-md text-outline', 'Escribiendo…');
        lista.append(escribiendo);
        lista.scrollTop = lista.scrollHeight;

        const cuerpo = new FormData();
        cuerpo.append('__RequestVerificationToken', token);
        if (mensaje !== undefined) cuerpo.append('mensaje', mensaje);
        try {
            const respuesta = await fetch(`/Portal/Asistente?handler=${accion}`, { method: 'POST', body: cuerpo });
            if (!respuesta.ok) throw new Error(String(respuesta.status));
            mostrar(await respuesta.json());
            ocupar(false);
            return true;
        } catch {
            escribiendo.remove();
            avisoError.textContent = 'No se pudo enviar. Revise su conexión e inténtelo de nuevo.';
            avisoError.classList.remove('hidden');
            ocupar(false);
            return false;
        }
    }

    formulario.addEventListener('submit', async evento => {
        evento.preventDefault();
        const texto = entrada.value.trim();
        if (!texto || ocupado) return;
        lista.append(burbuja('user', texto));
        entrada.value = '';
        entrada.style.height = '';
        if (!await pedir('Enviar', texto)) entrada.value = texto;
        entrada.focus();
    });

    entrada.addEventListener('keydown', evento => {
        if (evento.key === 'Enter' && !evento.shiftKey) {
            evento.preventDefault();
            formulario.requestSubmit();
        }
    });
    entrada.addEventListener('input', () => {
        entrada.style.height = 'auto';
        entrada.style.height = `${entrada.scrollHeight}px`;
    });
    reiniciar.addEventListener('click', () => pedir('Reiniciar'));

    ocupar(false);
    mostrar(JSON.parse(document.getElementById('chat-inicial').textContent));
    if (disponible) entrada.focus();
})();
