// =====================================================================
// residentes.js  -  Panel clinico: listado y alta de residentes.
// =====================================================================

let residentes = [];

const form = document.getElementById("form-residente");
const cuerpoTabla = document.getElementById("cuerpo-tabla");

document.addEventListener("DOMContentLoaded", cargar);
form.addEventListener("submit", guardar);
document.getElementById("buscar").addEventListener("input", pintarTabla);

async function cargar() {
    cuerpoTabla.innerHTML = `<tr><td colspan="6" class="vacio">Cargando…</td></tr>`;

    try {
        residentes = await api.listar("residentes");
        pintarTabla();
    } catch (error) {
        cuerpoTabla.innerHTML = `<tr><td colspan="6" class="vacio">${limpiar(error.message)}</td></tr>`;
        document.getElementById("conteo").textContent = "";
    }
}

function pintarTabla() {
    const filtro = document.getElementById("buscar").value.toLowerCase().trim();
    const lista = filtro
        ? residentes.filter(r => `${r.nombres} ${r.apellidos}`.toLowerCase().includes(filtro))
        : residentes;

    document.getElementById("conteo").textContent =
        `${lista.length} ${lista.length === 1 ? "residente activo" : "residentes activos"}`;

    if (lista.length === 0) {
        cuerpoTabla.innerHTML = `<tr><td colspan="6" class="vacio">
            ${filtro ? "Ningun residente coincide con la busqueda." : "Aun no hay residentes registrados."}
        </td></tr>`;
        return;
    }

    cuerpoTabla.innerHTML = lista.map(r => `
        <tr>
            <td>
                ${limpiar(r.nombres)} ${limpiar(r.apellidos)}
                <div class="secundario">${limpiar(r.psicopatologias) || "Sin diagnostico previo"}</div>
            </td>
            <td class="num">${r.edad}</td>
            <td>${fecha(r.fechaIngreso)}</td>
            <td class="num">${r.totalFamiliares}</td>
            <td><span class="estado ${r.activo ? "e-completo" : "e-cancelado"}">
                ${r.activo ? "Activo" : "Inactivo"}
            </span></td>
            <td class="der">
                <button class="btn-fila" onclick="abrirFamiliares(${r.id})">Familiares</button>
                <a class="btn-fila" href="crear-remision.html?residente=${r.id}">Crear remision</a>
                <button class="btn-fila peligro" onclick="darDeBaja(${r.id})">Dar de baja</button>
            </td>
        </tr>
    `).join("");
}

async function guardar(evento) {
    evento.preventDefault();

    const datos = {
        nombres: form.nombres.value.trim(),
        apellidos: form.apellidos.value.trim(),
        fechaNacimiento: form.fechaNacimiento.value,
        psicopatologias: form.psicopatologias.value.trim() || null,
        medicamentosCajon: form.medicamentosCajon.value.trim() || null
    };

    try {
        await api.crear("residentes", datos);
        mostrarAviso("Residente registrado correctamente.");
        form.reset();
        cargar();
    } catch (error) {
        mostrarAviso(error.message, "error");
    }
}

async function darDeBaja(id) {
    const r = residentes.find(x => x.id === id);
    if (!confirm(`Dar de baja a ${r.nombres} ${r.apellidos}? El expediente se conserva, solo deja de aparecer en el listado activo.`)) return;

    try {
        await api.eliminar("residentes", id);
        mostrarAviso("Residente dado de baja.");
        cargar();
    } catch (error) {
        mostrarAviso(error.message, "error");
    }
}
