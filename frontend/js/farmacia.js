// =====================================================================
// farmacia.js  -  Panel de farmacia (RF-05b).
//
// Separado de laboratorio: aunque los dos se cuelgan de una cita, son
// requerimientos distintos con actores distintos, y tenerlos en la
// misma pantalla obligaba al tecnico a pasar por encima del modulo
// que no le toca.
// =====================================================================

let despachos = [];
let citasEsperandoMedicamento = [];

const formDespacho = document.getElementById("form-despacho");

const pagDespachos = new Paginador("paginador-despachos", () => pintarDespachos());

document.addEventListener("DOMContentLoaded", iniciar);
formDespacho.addEventListener("submit", registrarDespacho);

async function iniciar() {
    await cargarCitas();
    await cargarDespachos();
    prepararVistaPrevia();
}

// ---------------- Catalogo de citas ----------------
// Solo las que estan esperando medicamento: con diagnostico, sin
// liquidar, todavia en "Esperando anexos" y con farmacia marcada por
// el especialista. La API rechaza las demas con 409.
async function cargarCitas() {
    try {
        const citas = await api.listar("citas");

        citasEsperandoMedicamento = citas.filter(c =>
            c.diagnostico && !c.liquidada &&
            c.estado === "Esperando anexos" && c.requiereFarmacia
        );

        formDespacho.citaId.innerHTML = citasEsperandoMedicamento.length
            ? `<option value="">Selecciona una cita</option>` +
              citasEsperandoMedicamento.map(c =>
                  `<option value="${c.id}">
                      #${c.id} — ${limpiar(c.residenteNombre)} (${limpiar(c.especialidadSolicitada)})
                   </option>`
              ).join("")
            : `<option value="">No hay citas esperando medicamentos</option>`;

        if (citas.length === 0) {
            mostrarAviso("Aún no hay citas registradas. La fundación debe asignar una cita primero.", "info");
        } else if (citasEsperandoMedicamento.length === 0) {
            mostrarAviso(
                "Ninguna cita está esperando medicamentos. El especialista debe registrar " +
                "la consulta e indicar que el residente necesita farmacia.",
                "info"
            );
        }
    } catch (error) {
        mostrarAviso(error.message, "error");
    }
}

// ---------------- Despachos ----------------

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

    cuerpo.innerHTML = pagDespachos.recortar(despachos).map(d => `
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
        document.getElementById("vista-total").textContent = moneda(0);
        cargarDespachos();
    } catch (error) {
        mostrarAviso(error.message, "error");
    }
}

// Vista previa del costo total mientras el tecnico escribe. Lo calculo
// aqui solo para mostrarlo: el valor que se guarda lo genera SQL Server
// como columna persistida, asi que no pueden desincronizarse.
function prepararVistaPrevia() {
    const actualizar = () => {
        const cant = Number(formDespacho.cantidad.value) || 0;
        const unit = Number(formDespacho.costoUnitario.value) || 0;
        document.getElementById("vista-total").textContent = moneda(cant * unit);
    };
    formDespacho.cantidad.addEventListener("input", actualizar);
    formDespacho.costoUnitario.addEventListener("input", actualizar);
}
