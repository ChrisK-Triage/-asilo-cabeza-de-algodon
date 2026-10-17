let despachos = [];
let recetaSeleccionada = null;

const formEntrega = document.getElementById("form-entrega");
const pagDespachos = new Paginador("paginador-despachos", () => pintarDespachos());

document.addEventListener("DOMContentLoaded", iniciar);
formEntrega.addEventListener("submit", confirmarEntrega);
document.getElementById("btn-cerrar-entrega").addEventListener("click", cerrarEntrega);
document.getElementById("filtro-despachos").addEventListener("change", () => {
    pagDespachos.reiniciar();
    pintarDespachos();
});

async function iniciar() {
    await cargarDespachos();
    prepararVistaPrevia();
}

async function cargarDespachos() {
    const cuerpo = document.getElementById("cuerpo-despachos");
    cuerpo.innerHTML = `<tr><td colspan="7" class="vacio">Cargando…</td></tr>`;

    try {
        despachos = await api.listar("farmacia/despachos");
        pintarDespachos();
    } catch (error) {
        cuerpo.innerHTML = `<tr><td colspan="7" class="vacio">${limpiar(error.message)}</td></tr>`;
        document.getElementById("conteo-despachos").textContent = "";
    }
}

function pintarDespachos() {
    const cuerpo = document.getElementById("cuerpo-despachos");
    const filtro = document.getElementById("filtro-despachos").value;
    const lista = filtro ? despachos.filter(d => d.estado === filtro) : despachos;

    const entregado = despachos
        .filter(d => d.estado === "Despachado")
        .reduce((suma, d) => suma + d.costoTotal, 0);

    const porEntregar = despachos.filter(d => d.estado === "Recetado").length;

    document.getElementById("conteo-despachos").textContent =
        porEntregar > 0
            ? `${porEntregar} por entregar · ${moneda(entregado)} despachado`
            : `${moneda(entregado)} despachado`;

    if (lista.length === 0) {
        cuerpo.innerHTML = `<tr><td colspan="7" class="vacio">
            ${filtro ? "Ninguna receta coincide con el filtro." : "Aun no hay recetas registradas."}
        </td></tr>`;
        return;
    }

    cuerpo.innerHTML = pagDespachos.recortar(lista).map(d => `
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
            <td class="num">${d.estado === "Despachado" ? moneda(d.costoUnitario) : "—"}</td>
            <td class="num">${d.estado === "Despachado" ? `<strong>${moneda(d.costoTotal)}</strong>` : "—"}</td>
            <td>
                <span class="estado ${d.estado === "Despachado" ? "e-completo" : "e-pendiente"}">
                    ${d.estado === "Despachado" ? "Entregado" : "Por entregar"}
                </span>
                ${d.estado === "Recetado" && d.diasPendiente > 3
                    ? `<div class="antiguedad">Recetado hace ${d.diasPendiente} dias</div>` : ""}
            </td>
            <td class="der">
                ${d.estado === "Recetado"
                    ? `<button class="btn-fila" onclick="abrirEntrega(${d.id})">Entregar</button>`
                    : `<span class="secundario">${fecha(d.fechaDespacho)}</span>`}
            </td>
        </tr>
    `).join("");
}

function abrirEntrega(id) {
    recetaSeleccionada = despachos.find(d => d.id === id);
    if (!recetaSeleccionada) return;

    document.getElementById("panel-entrega").hidden = false;
    document.getElementById("detalle-receta").innerHTML = `
        <div class="ficha">
            <div class="dato">
                <div class="rotulo">Residente</div>
                <div class="valor">${limpiar(recetaSeleccionada.residenteNombre)}</div>
            </div>
            <div class="dato">
                <div class="rotulo">Medicamento</div>
                <div class="valor">${limpiar(recetaSeleccionada.medicamento)}</div>
            </div>
            <div class="dato">
                <div class="rotulo">Cantidad</div>
                <div class="valor">${recetaSeleccionada.cantidad}</div>
            </div>
            <div class="dato">
                <div class="rotulo">Indicación</div>
                <div class="valor">${limpiar(recetaSeleccionada.tiempoAplicacion) || "Sin indicaciones"}</div>
            </div>
        </div>
    `;

    formEntrega.reset();
    document.getElementById("vista-total").textContent = moneda(0);
    document.getElementById("panel-entrega").scrollIntoView({ behavior: "smooth", block: "center" });
    formEntrega.costoUnitario.focus();
}

function cerrarEntrega() {
    document.getElementById("panel-entrega").hidden = true;
    recetaSeleccionada = null;
    formEntrega.reset();
}

async function confirmarEntrega(evento) {
    evento.preventDefault();
    if (!recetaSeleccionada) return;

    try {
        await api.enviarPut(`farmacia/despachos/${recetaSeleccionada.id}/entrega`, {
            costoUnitario: Number(formEntrega.costoUnitario.value)
        });

        mostrarAviso("Medicamento entregado y aplicado al expediente.");
        cerrarEntrega();
        cargarDespachos();
    } catch (error) {
        mostrarAviso(error.message, "error");
    }
}

function prepararVistaPrevia() {
    formEntrega.costoUnitario.addEventListener("input", () => {
        const unit = Number(formEntrega.costoUnitario.value) || 0;
        const cant = recetaSeleccionada?.cantidad ?? 0;
        document.getElementById("vista-total").textContent = moneda(cant * unit);
    });
}
