// =====================================================================
// anexos.js  -  Panel de laboratorio y farmacia.
// Dos bloques independientes que comparten el catalogo de citas.
// =====================================================================

let ordenes = [];
let despachos = [];
let citasDisponibles = [];
let ordenSeleccionada = null;

const formOrden = document.getElementById("form-orden");
const formDespacho = document.getElementById("form-despacho");
const formResultado = document.getElementById("form-resultado");

document.addEventListener("DOMContentLoaded", iniciar);
formOrden.addEventListener("submit", crearOrden);
formDespacho.addEventListener("submit", registrarDespacho);
formResultado.addEventListener("submit", cargarResultado);
document.getElementById("btn-cerrar-resultado").addEventListener("click", cerrarResultado);
document.getElementById("filtro-ordenes").addEventListener("change", cargarOrdenes);

async function iniciar() {
    await cargarCitas();
    await Promise.all([cargarOrdenes(), cargarDespachos()]);
}

// ---------------- Catalogo de citas ----------------
// Las ordenes y los despachos se cuelgan de una cita, asi que
// el select se alimenta de las citas existentes.
async function cargarCitas() {
    try {
        citasDisponibles = await api.listar("citas");

        // Cada select lleva su propia lista: una cita puede estar
        // esperando examenes y no medicamentos, o al reves.
        //
        // Tres condiciones para que una cita aparezca:
        //   1. tiene diagnostico, porque el anexo lo indica el especialista
        //   2. sigue en "Esperando anexos", o sea la bitacora no ha cerrado
        //   3. el especialista marco ese destino en particular
        //
        // Antes filtraba solo por diagnostico y por liquidada, y el select
        // seguia ofreciendo citas ya cerradas. La API tambien las rechaza,
        // pero no tiene sentido ofrecer algo que se va a rechazar.
        const abierta = c =>
            c.diagnostico && !c.liquidada && c.estado === "Esperando anexos";

        const paraLaboratorio = citasDisponibles.filter(c => abierta(c) && c.requiereLaboratorio);
        const paraFarmacia    = citasDisponibles.filter(c => abierta(c) && c.requiereFarmacia);

        const vacio = `<option value="">Selecciona una cita</option>`;
        const opcionesDe = lista => lista.map(c =>
            `<option value="${c.id}">
                #${c.id} — ${limpiar(c.residenteNombre)} (${limpiar(c.especialidadSolicitada)})
             </option>`
        ).join("");

        formOrden.citaId.innerHTML = paraLaboratorio.length
            ? vacio + opcionesDe(paraLaboratorio)
            : `<option value="">No hay citas esperando exámenes</option>`;

        document.getElementById("citaIdFarmacia").innerHTML = paraFarmacia.length
            ? vacio + opcionesDe(paraFarmacia)
            : `<option value="">No hay citas esperando medicamentos</option>`;

        if (citasDisponibles.length === 0) {
            mostrarAviso("Aún no hay citas registradas. La fundación debe asignar una cita primero.", "info");
        } else if (paraLaboratorio.length === 0 && paraFarmacia.length === 0) {
            mostrarAviso(
                "Ninguna cita está esperando exámenes ni medicamentos. " +
                "El especialista debe registrar la consulta e indicar qué necesita el residente.",
                "info"
            );
        }
    } catch (error) {
        mostrarAviso(error.message, "error");
    }
}

// ==================== LABORATORIO ====================

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

    cuerpo.innerHTML = ordenes.map(o => `
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
        // Recargo el catalogo: la cita pudo cambiar de estado.
        await cargarCitas();
        formOrden.reset();
        cargarOrdenes();
    } catch (error) {
        mostrarAviso(error.message, "error");
    }
}

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
        // Si este era el ultimo examen pendiente, la cita acaba de
        // cerrar su bitacora y ya no debe aparecer en los select.
        await cargarCitas();
        cerrarResultado();
        cargarOrdenes();
    } catch (error) {
        mostrarAviso(error.message, "error");
    }
}

// ==================== FARMACIA ====================

async function cargarDespachos() {
    const cuerpo = document.getElementById("cuerpo-despachos");
    cuerpo.innerHTML = `<tr><td colspan="5" class="vacio">Cargando…</td></tr>`;

    try {
        despachos = await api.listar("farmacia/despachos");
        pintarDespachos();
    } catch (error) {
        cuerpo.innerHTML = `<tr><td colspan="5" class="vacio">${limpiar(error.message)}</td></tr>`;
    }
}

function pintarDespachos() {
    const cuerpo = document.getElementById("cuerpo-despachos");
    const total = despachos.reduce((suma, d) => suma + d.costoTotal, 0);

    document.getElementById("conteo-despachos").textContent =
        `${despachos.length} despachos · ${moneda(total)} acumulado`;

    if (despachos.length === 0) {
        cuerpo.innerHTML = `<tr><td colspan="5" class="vacio">
            Aun no hay despachos registrados.
        </td></tr>`;
        return;
    }

    cuerpo.innerHTML = despachos.map(d => `
        <tr>
            <td>
                ${limpiar(d.residenteNombre)}
                <div class="secundario">Cita #${d.citaId}</div>
            </td>
            <td>
                ${limpiar(d.medicamento)}
                <div class="secundario">${limpiar(d.tiempoAplicacion) || "Sin indicaciones"}</div>
            </td>
            <td class="num">${d.cantidad}</td>
            <td class="num">${moneda(d.costoUnitario)}</td>
            <td class="num"><strong>${moneda(d.costoTotal)}</strong></td>
        </tr>
    `).join("");
}

async function registrarDespacho(evento) {
    evento.preventDefault();

    const datos = {
        citaId: Number(formDespacho.citaId.value),
        medicamento: formDespacho.medicamento.value.trim(),
        cantidad: Number(formDespacho.cantidad.value),
        tiempoAplicacion: formDespacho.tiempoAplicacion.value.trim() || null,
        costoUnitario: Number(formDespacho.costoUnitario.value)
    };

    try {
        await api.crear("farmacia/despachos", datos);
        mostrarAviso("Despacho registrado.");
        // Con el despacho hecho la cita pudo cerrar su bitacora.
        await cargarCitas();
        formDespacho.reset();
        cargarDespachos();
    } catch (error) {
        mostrarAviso(error.message, "error");
    }
}

// Vista previa del costo total mientras el tecnico escribe.
// Lo calculo aqui solo para mostrarlo: el valor real lo genera
// SQL Server como columna persistida.
document.addEventListener("DOMContentLoaded", () => {
    const actualizar = () => {
        const cant = Number(formDespacho.cantidad.value) || 0;
        const unit = Number(formDespacho.costoUnitario.value) || 0;
        document.getElementById("vista-total").textContent = moneda(cant * unit);
    };
    formDespacho.cantidad.addEventListener("input", actualizar);
    formDespacho.costoUnitario.addEventListener("input", actualizar);
});
