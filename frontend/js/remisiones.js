// =====================================================================
// remisiones.js  -  Creacion y consulta de remisiones a especialista.
// Carga el catalogo de residentes para alimentar el select de la FK.
// =====================================================================

let remisiones = [];

const form = document.getElementById("form-remision");
const cuerpoTabla = document.getElementById("cuerpo-tabla");

document.addEventListener("DOMContentLoaded", iniciar);
form.addEventListener("submit", guardar);
document.getElementById("filtro-estado").addEventListener("change", cargarRemisiones);

async function iniciar() {
    await cargarResidentes();
    await cargarRemisiones();
}

// El select se alimenta de la base: asi el medico no puede escribir
// un ResidenteId que no existe. Prevencion de errores antes que
// mensajes de error.
async function cargarResidentes() {
    try {
        const residentes = await api.listar("residentes");

        form.residenteId.innerHTML =
            `<option value="">Selecciona un residente</option>` +
            residentes.map(r =>
                `<option value="${r.id}">${limpiar(r.nombres)} ${limpiar(r.apellidos)} — ${r.edad} anios</option>`
            ).join("");

        // Si vengo desde el panel clinico con ?residente=N, lo preselecciono.
        const params = new URLSearchParams(location.search);
        const preseleccion = params.get("residente");
        if (preseleccion) form.residenteId.value = preseleccion;

    } catch (error) {
        mostrarAviso(error.message, "error");
    }
}

async function cargarRemisiones() {
    cuerpoTabla.innerHTML = `<tr><td colspan="5" class="vacio">Cargando…</td></tr>`;

    const estado = document.getElementById("filtro-estado").value;
    const consulta = estado ? `remisiones?estado=${estado}` : "remisiones";

    try {
        remisiones = await api.listar(consulta);
        pintarTabla();
    } catch (error) {
        cuerpoTabla.innerHTML = `<tr><td colspan="5" class="vacio">${limpiar(error.message)}</td></tr>`;
    }
}

function pintarTabla() {
    document.getElementById("conteo").textContent =
        `${remisiones.length} ${remisiones.length === 1 ? "remision" : "remisiones"}`;

    if (remisiones.length === 0) {
        cuerpoTabla.innerHTML = `<tr><td colspan="5" class="vacio">
            No hay remisiones que coincidan con el filtro.
        </td></tr>`;
        return;
    }

    cuerpoTabla.innerHTML = remisiones.map(r => `
        <tr>
            <td>
                ${limpiar(r.residenteNombre)}
                <div class="secundario">Remite: ${limpiar(r.medicoNombre)}</div>
            </td>
            <td>
                ${limpiar(r.especialidadSolicitada)}
                <div class="secundario">${limpiar(r.sintomatologia)}</div>
            </td>
            <td>
                ${fecha(r.fechaSolicitud)}
                <div class="${r.diasEspera > 5 ? "antiguedad" : "secundario"}">
                    ${r.diasEspera === 0 ? "Hoy" : `Hace ${r.diasEspera} dias`}
                </div>
            </td>
            <td><span class="estado ${claseEstado(r.estado)}">${limpiar(r.estado)}</span></td>
            <td class="der">
                ${r.estado === "Pendiente"
                    ? `<button class="btn-fila" onclick="cambiarEstado(${r.id}, 'Programada')">Programar</button>
                       <button class="btn-fila peligro" onclick="cambiarEstado(${r.id}, 'Cancelada')">Cancelar</button>`
                    : `<span class="secundario">Sin acciones</span>`}
            </td>
        </tr>
    `).join("");
}

function claseEstado(estado) {
    if (estado === "Pendiente")  return "e-pendiente";
    if (estado === "Programada") return "e-proceso";
    return "e-cancelado";
}

async function guardar(evento) {
    evento.preventDefault();

    const datos = {
        residenteId: Number(form.residenteId.value),
        // Medico general de prueba. Cuando agregue autenticacion, este Id
        // saldra del token JWT y no de un valor fijo.
        medicoGeneralId: 2,
        sintomatologia: form.sintomatologia.value.trim(),
        especialidadSolicitada: form.especialidadSolicitada.value,
        enfermeroAsignado: form.enfermeroAsignado.value.trim() || null
    };

    try {
        await api.crear("remisiones", datos);
        mostrarAviso("Remision emitida correctamente.");
        form.reset();
        cargarRemisiones();
    } catch (error) {
        mostrarAviso(error.message, "error");
    }
}

async function cambiarEstado(id, nuevoEstado) {
    try {
        await api.parchar(`remisiones/${id}/estado`, { estado: nuevoEstado });
        mostrarAviso(`Remision marcada como ${nuevoEstado.toLowerCase()}.`);
        cargarRemisiones();
    } catch (error) {
        mostrarAviso(error.message, "error");
    }
}
