let remisiones = [];
let especialidades = [];

const form = document.getElementById("form-remision");
const cuerpoTabla = document.getElementById("cuerpo-tabla");
const paginador = new Paginador("paginador-remisiones", () => pintarTabla());

document.addEventListener("DOMContentLoaded", iniciar);
form.addEventListener("submit", guardar);
form.especialidadId.addEventListener("change", revisarCobertura);
document.getElementById("filtro-estado").addEventListener("change", () => {

    paginador.reiniciar();
    cargarRemisiones();
});

async function iniciar() {

    await Promise.allSettled([cargarResidentes(), cargarEspecialidades(), cargarEnfermeros()]);
    await cargarRemisiones();
}

async function cargarResidentes() {
    try {
        const residentes = await api.listar("residentes");

        form.residenteId.innerHTML =
            `<option value="">Selecciona un residente</option>` +
            residentes.map(r =>
                `<option value="${r.id}">${limpiar(r.nombres)} ${limpiar(r.apellidos)} — ${r.edad} anios</option>`
            ).join("");

        const params = new URLSearchParams(location.search);
        const preseleccion = params.get("residente");
        if (preseleccion) form.residenteId.value = preseleccion;

    } catch (error) {
        mostrarAviso(error.message, "error");
    }
}

async function cargarEspecialidades() {
    let select;

    try {

        select = form.especialidadId;
        especialidades = await api.listar("catalogos/especialidades");

        if (especialidades.length === 0) {
            select.innerHTML = `<option value="">No hay especialidades en el catálogo</option>`;
            select.disabled = true;
            return;
        }

        select.disabled = false;
        select.innerHTML =
            `<option value="">Selecciona una especialidad</option>` +
            especialidades.map(e =>
                `<option value="${e.id}">${limpiar(e.nombre)}${e.tieneCobertura ? "" : " — sin cobertura"}</option>`
            ).join("");

    } catch (error) {
        if (select) select.innerHTML = `<option value="">No se pudo cargar el catálogo</option>`;
        mostrarAviso(error.message, "error");
    }
}

function revisarCobertura() {
    const nota = document.getElementById("aviso-cobertura");
    const elegida = especialidades.find(e => e.id === Number(form.especialidadId.value));

    if (!elegida) {
        nota.textContent = "Catálogo de especialidades";
        nota.classList.remove("ayuda-alerta");
        return;
    }

    if (elegida.tieneCobertura) {
        nota.textContent = elegida.especialistasActivos === 1
            ? "1 especialista disponible"
            : `${elegida.especialistasActivos} especialistas disponibles`;
        nota.classList.remove("ayuda-alerta");
    } else {
        nota.textContent =
            "La fundación no tiene especialista de esta especialidad. " +
            "La remisión queda pendiente hasta que haya uno.";
        nota.classList.add("ayuda-alerta");
    }
}

async function cargarEnfermeros() {
    let select;

    try {
        select = form.enfermeroId;
        const enfermeros = await api.listar("catalogos/enfermeros");

        select.innerHTML =
            `<option value="">Sin asignar</option>` +
            enfermeros.map(e => `<option value="${e.id}">${limpiar(e.nombre)}</option>`).join("");

    } catch (error) {
        if (select) select.innerHTML = `<option value="">No se pudo cargar el catálogo</option>`;
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

    cuerpoTabla.innerHTML = paginador.recortar(remisiones).map(r => `
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
                ${accionesFila(r)}
            </td>
        </tr>
    `).join("");
}

function accionesFila(r) {
    if (r.estado === "Pendiente") {
        return `<button class="btn-fila peligro" onclick="cancelar(${r.id})">Cancelar</button>`;
    }
    if (r.estado === "Programada") {
        return `<span class="secundario">Cita asignada</span>`;
    }
    return `<span class="secundario">Sin acciones</span>`;
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

        medicoGeneralId: 2,
        sintomatologia: form.sintomatologia.value.trim(),
        especialidadId: Number(form.especialidadId.value),

        enfermeroId: form.enfermeroId.value ? Number(form.enfermeroId.value) : null
    };

    try {
        await api.crear("remisiones", datos);
        mostrarAviso("Remision emitida correctamente.");
        form.reset();
        revisarCobertura();
        cargarRemisiones();
    } catch (error) {
        mostrarAviso(error.message, "error");
    }
}

async function cancelar(id) {
    const r = remisiones.find(x => x.id === id);

    const confirmado = confirm(
        `Cancelar la remision de ${r.residenteNombre}?\n\n` +
        `Especialidad: ${r.especialidadSolicitada}\n` +
        `Solicitada: ${fecha(r.fechaSolicitud)}\n\n` +
        `La remision dejara de aparecer como pendiente para la fundacion ` +
        `y no se podra reactivar. Si el residente sigue necesitando la ` +
        `consulta, habra que emitir una remision nueva.`
    );

    if (!confirmado) return;

    try {
        await api.parchar(`remisiones/${id}/estado`, { estado: "Cancelada" });
        mostrarAviso("Remision cancelada.");
        cargarRemisiones();
    } catch (error) {
        mostrarAviso(error.message, "error");
    }
}
