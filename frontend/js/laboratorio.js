// =====================================================================
// laboratorio.js  -  Panel de laboratorio (RF-05a).
//
// Antes esto vivia junto con farmacia en anexos.js. Los separe porque
// el analisis ya los trata como requerimientos distintos, con actores
// y momentos distintos: el tecnico de laboratorio carga resultados, el
// de farmacia despacha medicamentos.
// =====================================================================

let ordenes = [];
let citasEsperandoExamen = [];
let ordenSeleccionada = null;

const formOrden = document.getElementById("form-orden");
const formResultado = document.getElementById("form-resultado");

const pagOrdenes = new Paginador("paginador-ordenes", () => pintarOrdenes());

document.addEventListener("DOMContentLoaded", iniciar);
formOrden.addEventListener("submit", crearOrden);
formResultado.addEventListener("submit", cargarResultado);
document.getElementById("btn-cerrar-resultado").addEventListener("click", cerrarResultado);
document.getElementById("filtro-ordenes").addEventListener("change", () => {
    pagOrdenes.reiniciar();
    cargarOrdenes();
});

async function iniciar() {
    await cargarCitas();
    await cargarOrdenes();
}

// ---------------- Catalogo de citas ----------------
// Una orden se cuelga de una cita, asi que el select se alimenta de las
// citas que estan esperando examenes.
//
// Tres condiciones:
//   1. tiene diagnostico, porque el examen lo indica el especialista
//   2. sigue en "Esperando anexos", o sea la bitacora no ha cerrado
//   3. el especialista marco laboratorio en particular
//
// La API rechaza con 409 las que no cumplen, pero no tiene sentido
// ofrecer en pantalla algo que se va a rechazar.
async function cargarCitas() {
    try {
        const citas = await api.listar("citas");

        citasEsperandoExamen = citas.filter(c =>
            c.diagnostico && !c.liquidada &&
            c.estado === "Esperando anexos" && c.requiereLaboratorio
        );

        formOrden.citaId.innerHTML = citasEsperandoExamen.length
            ? `<option value="">Selecciona una cita</option>` +
              citasEsperandoExamen.map(c =>
                  `<option value="${c.id}">
                      #${c.id} — ${limpiar(c.residenteNombre)} (${limpiar(c.especialidadSolicitada)})
                   </option>`
              ).join("")
            : `<option value="">No hay citas esperando exámenes</option>`;

        if (citas.length === 0) {
            mostrarAviso("Aún no hay citas registradas. La fundación debe asignar una cita primero.", "info");
        } else if (citasEsperandoExamen.length === 0) {
            mostrarAviso(
                "Ninguna cita está esperando exámenes. El especialista debe registrar " +
                "la consulta e indicar que el residente necesita laboratorio.",
                "info"
            );
        }
    } catch (error) {
        mostrarAviso(error.message, "error");
    }
}

// ---------------- Ordenes ----------------

async function cargarOrdenes() {
    const cuerpo = document.getElementById("cuerpo-ordenes");
    cuerpo.innerHTML = `<tr><td colspan="6" class="vacio">Cargando…</td></tr>`;

    const estado = document.getElementById("filtro-ordenes").value;
    const ruta = estado ? `laboratorio/ordenes?estado=${estado}` : "laboratorio/ordenes";

    try {
        ordenes = await api.listar(ruta);
        pintarOrdenes();
    } catch (error) {
        cuerpo.innerHTML = `<tr><td colspan="6" class="vacio">${limpiar(error.message)}</td></tr>`;
    }
}

function pintarOrdenes() {
    const cuerpo = document.getElementById("cuerpo-ordenes");

    document.getElementById("conteo-ordenes").textContent =
        `${ordenes.length} ${ordenes.length === 1 ? "orden" : "ordenes"}`;

    if (ordenes.length === 0) {
        cuerpo.innerHTML = `<tr><td colspan="6" class="vacio">
            No hay ordenes que coincidan con el filtro.
        </td></tr>`;
        return;
    }

    cuerpo.innerHTML = pagOrdenes.recortar(ordenes).map(o => `
        <tr>
            <td>
                ${limpiar(o.residenteNombre)}
                <div class="secundario">Cita #${o.citaId} · ${limpiar(o.especialistaNombre)}</div>
            </td>
            <td>
                ${limpiar(o.tipoExamen)}
                ${o.resultado ? `<div class="secundario">${limpiar(o.resultado)}</div>` : ""}
            </td>
            <td>
                ${fecha(o.fechaSolicitud)}
                ${o.estado === "Pendiente" && o.diasPendiente > 3
                    ? `<div class="antiguedad">Hace ${o.diasPendiente} dias</div>` : ""}
            </td>
            <td class="num">${o.estado === "Completada" ? moneda(o.costoBruto) : "—"}</td>
            <td><span class="estado ${o.estado === "Completada" ? "e-completo" : "e-pendiente"}">
                ${limpiar(o.estado)}
            </span></td>
            <td class="der">
                ${o.estado === "Pendiente"
                    ? `<button class="btn-fila" onclick="abrirResultado(${o.id})">Cargar resultado</button>`
                    : `<span class="secundario">Completada</span>`}
            </td>
        </tr>
    `).join("");
}

async function crearOrden(evento) {
    evento.preventDefault();

    const datos = {
        citaId: Number(formOrden.citaId.value),
        tipoExamen: formOrden.tipoExamen.value.trim()
    };

    try {
        await api.crear("laboratorio/ordenes", datos);
        mostrarAviso("Orden de laboratorio solicitada.");
        await cargarCitas();
        formOrden.reset();
        cargarOrdenes();
    } catch (error) {
        mostrarAviso(error.message, "error");
    }
}

// ---------------- Carga de resultado ----------------

function abrirResultado(id) {
    ordenSeleccionada = ordenes.find(o => o.id === id);
    if (!ordenSeleccionada) return;

    document.getElementById("panel-resultado").style.display = "block";
    document.getElementById("detalle-orden").innerHTML = `
        <div class="ficha">
            <div class="dato">
                <div class="rotulo">Residente</div>
                <div class="valor">${limpiar(ordenSeleccionada.residenteNombre)}</div>
            </div>
            <div class="dato">
                <div class="rotulo">Examen solicitado</div>
                <div class="valor">${limpiar(ordenSeleccionada.tipoExamen)}</div>
            </div>
            <div class="dato">
                <div class="rotulo">Solicitado por</div>
                <div class="valor">${limpiar(ordenSeleccionada.especialistaNombre)}</div>
            </div>
        </div>
    `;

    formResultado.reset();
    document.getElementById("panel-resultado").scrollIntoView({ behavior: "smooth", block: "center" });
}

function cerrarResultado() {
    document.getElementById("panel-resultado").style.display = "none";
    ordenSeleccionada = null;
    formResultado.reset();
}

async function cargarResultado(evento) {
    evento.preventDefault();
    if (!ordenSeleccionada) return;

    const datos = {
        resultado: formResultado.resultado.value.trim(),
        costoBruto: Number(formResultado.costoBruto.value) || 0
    };

    try {
        await api.enviarPut(`laboratorio/ordenes/${ordenSeleccionada.id}/resultado`, datos);
        mostrarAviso("Resultado cargado y aplicado al expediente.");
        // Si este era el ultimo examen pendiente, la cita acaba de cerrar
        // su bitacora y ya no debe aparecer en el select.
        await cargarCitas();
        cerrarResultado();
        cargarOrdenes();
    } catch (error) {
        mostrarAviso(error.message, "error");
    }
}
