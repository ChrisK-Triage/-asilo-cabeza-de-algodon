// =====================================================================
// fundacion.js  -  Consola de la fundacion.
// Dos bloques: remisiones pendientes de asignar, y agenda de citas.
// =====================================================================

let pendientes = [];
let citas = [];
let especialistas = [];
let remisionSeleccionada = null;
let citaSeleccionada = null;

const form = document.getElementById("form-asignar");
const formDiagnostico = document.getElementById("form-diagnostico");

const pagPendientes = new Paginador("paginador-pendientes", () => pintarPendientes());
const pagCitas      = new Paginador("paginador-citas",      () => pintarCitas());

document.addEventListener("DOMContentLoaded", iniciar);
form.addEventListener("submit", asignar);
document.getElementById("btn-cerrar-form").addEventListener("click", cerrarFormulario);
formDiagnostico.addEventListener("submit", guardarDiagnostico);
document.getElementById("btn-cerrar-diagnostico").addEventListener("click", cerrarDiagnostico);
document.getElementById("filtro-estado").addEventListener("change", () => {
    pagCitas.reiniciar();
    cargarCitas();
});

async function iniciar() {
    await cargarEspecialistas();
    await Promise.all([cargarPendientes(), cargarCitas()]);
}

// ---------------- Catalogo de especialistas ----------------
async function cargarEspecialistas() {
    try {
        especialistas = await api.listar("citas/especialistas");

        pintarEspecialistas(null, null);
    } catch (error) {
        mostrarAviso(error.message, "error");
    }
}

// Reconstruyo el select completo en cada apertura. Antes iba agregando
// el sufijo al texto existente y se acumulaba en todas las opciones.
//
// Filtro por EspecialidadId. Antes comparaba dos cadenas de texto
// quitandoles tildes, porque la especialidad era texto libre en dos
// tablas distintas; con el catalogo es una comparacion de enteros y no
// hay forma de que una tilde rompa el emparejamiento.
function pintarEspecialistas(especialidadId, nombreEspecialidad) {
    const disponibles = especialidadId
        ? especialistas.filter(e => e.especialidadId === especialidadId)
        : especialistas;

    if (especialidadId && disponibles.length === 0) {
        form.especialistaId.innerHTML =
            `<option value="">Sin especialistas de ${limpiar(nombreEspecialidad)}</option>`;
        mostrarAviso(
            `No hay ningun especialista activo de ${nombreEspecialidad}. ` +
            `Hay que registrar uno antes de asignar esta remision.`,
            "error"
        );
        return;
    }

    const opciones = disponibles.map(e =>
        `<option value="${e.id}">${limpiar(e.nombreCompleto)} — ${limpiar(e.especialidad)}</option>`
    ).join("");

    form.especialistaId.innerHTML =
        `<option value="">Selecciona un especialista</option>` + opciones;
}

// ---------------- Remisiones pendientes ----------------
async function cargarPendientes() {
    const cuerpo = document.getElementById("cuerpo-pendientes");
    cuerpo.innerHTML = `<tr><td colspan="5" class="vacio">Cargando…</td></tr>`;

    try {
        pendientes = await api.listar("remisiones?estado=Pendiente");
        pintarPendientes();
    } catch (error) {
        cuerpo.innerHTML = `<tr><td colspan="5" class="vacio">${limpiar(error.message)}</td></tr>`;
    }
}

function pintarPendientes() {
    const cuerpo = document.getElementById("cuerpo-pendientes");

    document.getElementById("conteo-pendientes").textContent =
        `${pendientes.length} ${pendientes.length === 1 ? "solicitud espera" : "solicitudes esperan"} asignacion`;

    if (pendientes.length === 0) {
        cuerpo.innerHTML = `<tr><td colspan="5" class="vacio">
            No hay remisiones pendientes de asignar.
        </td></tr>`;
        return;
    }

    // Las ordeno por antiguedad: el coordinador necesita priorizar y sin
    // ese dato tendria que calcularlo mentalmente.
    const lista = [...pendientes].sort((a, b) => b.diasEspera - a.diasEspera);

    cuerpo.innerHTML = pagPendientes.recortar(lista).map(r => `
        <tr>
            <td>
                ${limpiar(r.residenteNombre)}
                <div class="secundario">Remite: ${limpiar(r.medicoNombre)}</div>
            </td>
            <td>${limpiar(r.especialidadSolicitada)}</td>
            <td>
                ${limpiar(r.sintomatologia)}
            </td>
            <td>
                ${fecha(r.fechaSolicitud)}
                <div class="${r.diasEspera > 5 ? "antiguedad" : "secundario"}">
                    ${r.diasEspera === 0 ? "Hoy" : `Hace ${r.diasEspera} dias`}
                </div>
            </td>
            <td class="der">
                <button class="btn-fila" onclick="abrirFormulario(${r.id})">Asignar</button>
            </td>
        </tr>
    `).join("");
}

// ---------------- Formulario de asignacion ----------------
function abrirFormulario(remisionId) {
    remisionSeleccionada = pendientes.find(r => r.id === remisionId);
    if (!remisionSeleccionada) return;

    document.getElementById("panel-asignar").style.display = "block";
    document.getElementById("detalle-remision").innerHTML = `
        <div class="ficha">
            <div class="dato">
                <div class="rotulo">Residente</div>
                <div class="valor">${limpiar(remisionSeleccionada.residenteNombre)}</div>
            </div>
            <div class="dato">
                <div class="rotulo">Especialidad solicitada</div>
                <div class="valor">${limpiar(remisionSeleccionada.especialidadSolicitada)}</div>
            </div>
            <div class="dato">
                <div class="rotulo">Medico remitente</div>
                <div class="valor">${limpiar(remisionSeleccionada.medicoNombre)}</div>
            </div>
            <div class="dato">
                <div class="rotulo">Enfermero acompanante</div>
                <div class="valor">${limpiar(remisionSeleccionada.enfermeroAsignado) || "Sin asignar"}</div>
            </div>
        </div>
        <div style="margin-top:16px">
            <div class="rotulo secundario">Sintomatologia reportada</div>
            <p>${limpiar(remisionSeleccionada.sintomatologia)}</p>
        </div>
    `;

    form.reset();

    // Marco los especialistas que no corresponden a la especialidad pedida.
    // El backend valida la regla; avisarlo aqui le evita al coordinador
    // recibir un error que pudo verse antes de enviar.
    pintarEspecialistas(remisionSeleccionada.especialidadId,
                        remisionSeleccionada.especialidadSolicitada);

    document.getElementById("panel-asignar").scrollIntoView({ behavior: "smooth", block: "center" });
}

function cerrarFormulario() {
    document.getElementById("panel-asignar").style.display = "none";
    remisionSeleccionada = null;
    form.reset();
    pintarEspecialistas(null, null);
}

async function asignar(evento) {
    evento.preventDefault();
    if (!remisionSeleccionada) return;

    const datos = {
        remisionId: remisionSeleccionada.id,
        especialistaId: Number(form.especialistaId.value),
        bloqueHorario: form.bloqueHorario.value,
        costoConsulta: Number(form.costoConsulta.value) || 0
    };

    try {
        await api.crear("citas", datos);
        mostrarAviso("Cita asignada correctamente.");
        cerrarFormulario();
        await Promise.all([cargarPendientes(), cargarCitas()]);
    } catch (error) {
        mostrarAviso(error.message, "error");
    }
}

// ---------------- Agenda de citas ----------------
async function cargarCitas() {
    const cuerpo = document.getElementById("cuerpo-citas");
    cuerpo.innerHTML = `<tr><td colspan="5" class="vacio">Cargando…</td></tr>`;

    const estado = document.getElementById("filtro-estado").value;
    const ruta = estado ? `citas?estado=${estado}` : "citas";

    try {
        citas = await api.listar(ruta);
        pintarCitas();
    } catch (error) {
        cuerpo.innerHTML = `<tr><td colspan="5" class="vacio">${limpiar(error.message)}</td></tr>`;
    }
}

function pintarCitas() {
    const cuerpo = document.getElementById("cuerpo-citas");

    document.getElementById("conteo-citas").textContent =
        `${citas.length} ${citas.length === 1 ? "cita" : "citas"}`;

    if (citas.length === 0) {
        cuerpo.innerHTML = `<tr><td colspan="5" class="vacio">
            No hay citas que coincidan con el filtro.
        </td></tr>`;
        return;
    }

    cuerpo.innerHTML = pagCitas.recortar(citas).map(c => `
        <tr>
            <td>
                ${limpiar(c.residenteNombre)}
                <div class="secundario">${limpiar(c.especialidadSolicitada)}</div>
            </td>
            <td>
                ${limpiar(c.especialistaNombre)}
                <div class="secundario">${limpiar(c.especialistaEspecialidad)}</div>
            </td>
            <td>${fechaHora(c.bloqueHorario)}</td>
            <td class="num">${moneda(c.costoConsulta)}</td>
            <td><span class="estado ${claseEstadoCita(c.estado)}">${etiquetaEstado(c)}</span></td>
            <td class="der">${accionesCita(c)}</td>
        </tr>
    `).join("");
}

// El diagnostico solo se registra una vez: despues de asentado, el
// expediente clinico es historico y no se sobrescribe.
function accionesCita(c) {
    if (c.estado === "Programada") {
        return `<button class="btn-fila" onclick="abrirDiagnostico(${c.id})">Registrar consulta</button>`;
    }
    return `<span class="secundario">Consulta registrada</span>`;
}

function abrirDiagnostico(id) {
    citaSeleccionada = citas.find(c => c.id === id);
    if (!citaSeleccionada) return;

    document.getElementById("panel-diagnostico").style.display = "block";
    document.getElementById("detalle-cita").innerHTML = `
        <div class="ficha">
            <div class="dato">
                <div class="rotulo">Residente</div>
                <div class="valor">${limpiar(citaSeleccionada.residenteNombre)}</div>
            </div>
            <div class="dato">
                <div class="rotulo">Especialista</div>
                <div class="valor">${limpiar(citaSeleccionada.especialistaNombre)}</div>
            </div>
            <div class="dato">
                <div class="rotulo">Horario</div>
                <div class="valor">${fechaHora(citaSeleccionada.bloqueHorario)}</div>
            </div>
        </div>
        <div style="margin-top:16px">
            <div class="rotulo secundario">Motivo de la remisión</div>
            <p>${limpiar(citaSeleccionada.sintomatologia)}</p>
        </div>
    `;

    formDiagnostico.reset();
    document.getElementById("panel-diagnostico").scrollIntoView({ behavior: "smooth", block: "center" });
}

function cerrarDiagnostico() {
    document.getElementById("panel-diagnostico").style.display = "none";
    citaSeleccionada = null;
    formDiagnostico.reset();
}

async function guardarDiagnostico(evento) {
    evento.preventDefault();
    if (!citaSeleccionada) return;

    const datos = {
        diagnostico: formDiagnostico.diagnostico.value.trim(),
        requiereLaboratorio: formDiagnostico.requiereLaboratorio.checked,
        requiereFarmacia: formDiagnostico.requiereFarmacia.checked
    };

    try {
        await api.enviarPut(`citas/${citaSeleccionada.id}/diagnostico`, datos);
        mostrarAviso(mensajeDeCierre(datos));
        cerrarDiagnostico();
        cargarCitas();
    } catch (error) {
        mostrarAviso(error.message, "error");
    }
}

// Le digo al usuario exactamente que queda pendiente, no un generico
// "esperando anexos" que no dice cual de los dos.
function mensajeDeCierre({ requiereLaboratorio, requiereFarmacia }) {
    if (requiereLaboratorio && requiereFarmacia)
        return "Consulta registrada. La cita cierra cuando se carguen los exámenes y se despache el medicamento.";
    if (requiereLaboratorio)
        return "Consulta registrada. La cita cierra cuando laboratorio cargue los resultados.";
    if (requiereFarmacia)
        return "Consulta registrada. La cita cierra cuando farmacia despache el medicamento.";
    return "Consulta registrada y cerrada: no quedó nada pendiente.";
}

// La API devuelve el nombre tecnico del estado. En pantalla uso
// terminos que el personal del asilo entiende sin explicacion.
//
// "Esperando anexos" es el caso especial: el nombre del estado es uno
// solo, pero lo que la cita espera puede ser examenes, medicamento o
// los dos. Decia siempre "Esperando examenes", asi que una cita que
// solo esperaba medicamento mostraba una etiqueta falsa. Por eso
// recibo la cita entera y no nada mas la cadena del estado.
function etiquetaEstado(c) {
    if (c.estado === "Esperando anexos") {
        const faltan = [];
        if (c.requiereLaboratorio) faltan.push("exámenes");
        if (c.requiereFarmacia)    faltan.push("medicamento");

        return faltan.length
            ? limpiar(`Esperando ${faltan.join(" y ")}`)
            : "Esperando anexos";
    }

    const mapa = {
        "Programada":          "Sin consulta",
        "En evaluacion":       "En consulta",
        "Bitacora completada": "Consulta cerrada",
        "Costos calculados":   "Costos aplicados"
    };
    return limpiar(mapa[c.estado] || c.estado);
}

function claseEstadoCita(estado) {
    if (estado === "Programada")          return "e-pendiente";
    if (estado === "En evaluacion")       return "e-proceso";
    if (estado === "Esperando anexos")    return "e-proceso";
    if (estado === "Bitacora completada") return "e-completo";
    return "e-completo";
}
