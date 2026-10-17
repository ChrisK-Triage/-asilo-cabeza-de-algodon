let ordenes = [];
let ordenSeleccionada = null;

const formResultado = document.getElementById("form-resultado");

const pagOrdenes = new Paginador("paginador-ordenes", () => pintarOrdenes());

document.addEventListener("DOMContentLoaded", cargarOrdenes);
formResultado.addEventListener("submit", cargarResultado);
document.getElementById("btn-cerrar-resultado").addEventListener("click", cerrarResultado);
document.getElementById("filtro-ordenes").addEventListener("change", () => {
    pagOrdenes.reiniciar();
    cargarOrdenes();
});

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

        cerrarResultado();
        cargarOrdenes();
    } catch (error) {
        mostrarAviso(error.message, "error");
    }
}
