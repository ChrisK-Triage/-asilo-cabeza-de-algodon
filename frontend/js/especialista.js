let citas = [];
let citaSeleccionada = null;
let citaIndicaciones = null;

const formDiagnostico = document.getElementById("form-diagnostico");
const cuerpoTabla = document.getElementById("cuerpo-citas");
const paginador = new Paginador("paginador-citas", () => pintarCitas());

document.addEventListener("DOMContentLoaded", cargarCitas);
formDiagnostico.addEventListener("submit", guardarDiagnostico);
document.getElementById("btn-cerrar-diagnostico").addEventListener("click", cerrarDiagnostico);
document.getElementById("form-examen").addEventListener("submit", solicitarExamen);
document.getElementById("form-receta").addEventListener("submit", recetar);
document.getElementById("filtro-estado").addEventListener("change", () => {
    paginador.reiniciar();
    cargarCitas();
});

async function cargarCitas() {
    cuerpoTabla.innerHTML = `<tr><td colspan="5" class="vacio">Cargando…</td></tr>`;

    const estado = document.getElementById("filtro-estado").value;
    const ruta = estado ? `citas?estado=${estado}` : "citas";

    try {
        citas = await api.listar(ruta);
        pintarCitas();
    } catch (error) {
        cuerpoTabla.innerHTML = `<tr><td colspan="5" class="vacio">${limpiar(error.message)}</td></tr>`;
        document.getElementById("conteo").textContent = "";
    }
}

function pintarCitas() {
    document.getElementById("conteo").textContent =
        `${citas.length} ${citas.length === 1 ? "cita" : "citas"}`;

    if (citas.length === 0) {
        cuerpoTabla.innerHTML = `<tr><td colspan="5" class="vacio">
            No hay citas que coincidan con el filtro.
        </td></tr>`;
        return;
    }

    cuerpoTabla.innerHTML = paginador.recortar(citas).map(c => `
        <tr>
            <td>
                ${limpiar(c.residenteNombre)}
                <div class="secundario">${limpiar(c.especialidadSolicitada)}</div>
            </td>
            <td>${limpiar(c.sintomatologia)}</td>
            <td>${fechaHora(c.bloqueHorario)}</td>
            <td><span class="estado ${claseEstadoCita(c.estado)}">${etiquetaEstado(c)}</span></td>
            <td class="der">${acciones(c)}</td>
        </tr>
    `).join("");
}

function acciones(c) {
    if (c.estado === "Programada") {
        return `<button class="btn-fila" onclick="abrirDiagnostico(${c.id})">Registrar consulta</button>`;
    }

    if (c.estado === "Esperando anexos") {
        return `<button class="btn-fila" onclick="abrirIndicaciones(${c.id})">Exámenes y receta</button>`;
    }

    return `<span class="secundario">Consulta cerrada</span>`;
}

function abrirDiagnostico(id) {
    citaSeleccionada = citas.find(c => c.id === id);
    if (!citaSeleccionada) return;

    document.getElementById("panel-diagnostico").hidden = false;
    document.getElementById("detalle-cita").innerHTML = `
        <div class="ficha">
            <div class="dato">
                <div class="rotulo">Residente</div>
                <div class="valor">${limpiar(citaSeleccionada.residenteNombre)}</div>
            </div>
            <div class="dato">
                <div class="rotulo">Especialidad solicitada</div>
                <div class="valor">${limpiar(citaSeleccionada.especialidadSolicitada)}</div>
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
    document.getElementById("diagnostico").focus();
}

function cerrarDiagnostico() {
    document.getElementById("panel-diagnostico").hidden = true;
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

        const citaId = citaSeleccionada.id;
        const hayQueIndicar = datos.requiereLaboratorio || datos.requiereFarmacia;

        cerrarDiagnostico();
        await cargarCitas();

        if (hayQueIndicar) abrirIndicaciones(citaId);
    } catch (error) {
        mostrarAviso(error.message, "error");
    }
}

function mensajeDeCierre({ requiereLaboratorio, requiereFarmacia }) {
    if (requiereLaboratorio && requiereFarmacia)
        return "Consulta registrada. La cita cierra cuando se carguen los exámenes y se despache el medicamento.";
    if (requiereLaboratorio)
        return "Consulta registrada. La cita cierra cuando laboratorio cargue los resultados.";
    if (requiereFarmacia)
        return "Consulta registrada. La cita cierra cuando farmacia despache el medicamento.";
    return "Consulta registrada y cerrada: no quedó nada pendiente.";
}


async function abrirIndicaciones(citaId) {
    try {
        citaIndicaciones = await api.obtener("citas", citaId);
    } catch (error) {
        mostrarAviso(error.message, "error");
        return;
    }

    document.getElementById("indicaciones-residente").textContent =
        `${citaIndicaciones.residenteNombre} · cita #${citaIndicaciones.id}`;

    document.getElementById("bloque-examenes").hidden = !citaIndicaciones.requiereLaboratorio;
    document.getElementById("bloque-recetas").hidden = !citaIndicaciones.requiereFarmacia;

    document.getElementById("form-examen").reset();
    document.getElementById("form-receta").reset();

    document.getElementById("panel-indicaciones").hidden = false;
    document.getElementById("panel-indicaciones").scrollIntoView({ behavior: "smooth", block: "center" });

    await Promise.allSettled([cargarExamenes(), cargarRecetas()]);
}

function cerrarIndicaciones() {
    document.getElementById("panel-indicaciones").hidden = true;
    citaIndicaciones = null;
}

async function cargarExamenes() {
    const cuerpo = document.getElementById("cuerpo-examenes");
    if (!citaIndicaciones?.requiereLaboratorio) return;

    try {
        const ordenes = await api.listar(`laboratorio/ordenes?citaId=${citaIndicaciones.id}`);

        cuerpo.innerHTML = ordenes.length
            ? ordenes.map(o => `
                <tr>
                    <td>${limpiar(o.tipoExamen)}</td>
                    <td>${fecha(o.fechaSolicitud)}</td>
                    <td><span class="estado ${o.estado === "Completada" ? "e-completo" : "e-pendiente"}">
                        ${o.estado === "Completada" ? "Resultado cargado" : "Esperando laboratorio"}
                    </span></td>
                </tr>`).join("")
            : `<tr><td colspan="3" class="vacio">Todavía no has solicitado ningún examen.</td></tr>`;
    } catch (error) {
        cuerpo.innerHTML = `<tr><td colspan="3" class="vacio">${limpiar(error.message)}</td></tr>`;
    }
}

async function cargarRecetas() {
    const cuerpo = document.getElementById("cuerpo-recetas");
    if (!citaIndicaciones?.requiereFarmacia) return;

    try {
        const recetas = await api.listar(`farmacia/despachos?citaId=${citaIndicaciones.id}`);

        cuerpo.innerHTML = recetas.length
            ? recetas.map(d => `
                <tr>
                    <td>${limpiar(d.medicamento)}</td>
                    <td class="num">${d.cantidad}</td>
                    <td>${limpiar(d.tiempoAplicacion) || "—"}</td>
                    <td><span class="estado ${d.estado === "Despachado" ? "e-completo" : "e-pendiente"}">
                        ${d.estado === "Despachado" ? "Entregado" : "Esperando farmacia"}
                    </span></td>
                </tr>`).join("")
            : `<tr><td colspan="4" class="vacio">Todavía no has recetado nada.</td></tr>`;
    } catch (error) {
        cuerpo.innerHTML = `<tr><td colspan="4" class="vacio">${limpiar(error.message)}</td></tr>`;
    }
}

async function solicitarExamen(evento) {
    evento.preventDefault();
    if (!citaIndicaciones) return;

    const form = evento.target;

    try {
        await api.crear("laboratorio/ordenes", {
            citaId: citaIndicaciones.id,
            tipoExamen: form.tipoExamen.value.trim()
        });

        mostrarAviso("Examen solicitado. Laboratorio ya lo ve en su lista.");
        form.reset();
        await cargarExamenes();
    } catch (error) {
        mostrarAviso(error.message, "error");
    }
}

async function recetar(evento) {
    evento.preventDefault();
    if (!citaIndicaciones) return;

    const form = evento.target;

    try {
        await api.crear("farmacia/recetas", {
            citaId: citaIndicaciones.id,
            medicamento: form.medicamento.value.trim(),
            cantidad: Number(form.cantidad.value),
            tiempoAplicacion: form.tiempoAplicacion.value.trim() || null
        });

        mostrarAviso("Medicamento recetado. Farmacia lo ve pendiente de entregar.");
        form.reset();
        await cargarRecetas();
    } catch (error) {
        mostrarAviso(error.message, "error");
    }
}
