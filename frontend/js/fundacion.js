let pendientes = [];
let citas = [];
let especialistas = [];
let remisionSeleccionada = null;

const form = document.getElementById("form-asignar");

const pagPendientes = new Paginador("paginador-pendientes", () => pintarPendientes());
const pagCitas      = new Paginador("paginador-citas",      () => pintarCitas());

document.addEventListener("DOMContentLoaded", iniciar);
form.addEventListener("submit", asignar);
document.getElementById("btn-cerrar-form").addEventListener("click", cerrarFormulario);
document.getElementById("filtro-estado").addEventListener("change", () => {
    pagCitas.reiniciar();
    cargarCitas();
});

async function iniciar() {
    await cargarEspecialistas();
    await Promise.all([cargarPendientes(), cargarCitas()]);
}

async function cargarEspecialistas() {
    try {
        especialistas = await api.listar("citas/especialistas");

        pintarEspecialistas(null, null);
    } catch (error) {
        mostrarAviso(error.message, "error");
    }
}

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

function accionesCita(c) {
    return c.estado === "Programada"
        ? `<span class="secundario">Esperando al especialista</span>`
        : `<span class="secundario">Consulta registrada</span>`;
}
