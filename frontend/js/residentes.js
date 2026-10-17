let residentes = [];

let residenteEnEdicion = null;

const puedeAdmitir = sesionActiva?.rol === "Administrador";

const form = document.getElementById("form-residente");
const panelForm = document.getElementById("panel-form-residente");
const bloqueAdmision = document.getElementById("bloque-admision");
const cuerpoTabla = document.getElementById("cuerpo-tabla");
const paginador = new Paginador("paginador-residentes", () => pintarTabla());

document.addEventListener("DOMContentLoaded", iniciar);
form.addEventListener("submit", guardar);
document.getElementById("btn-cancelar-edicion").addEventListener("click", cancelarEdicion);
document.getElementById("buscar").addEventListener("input", () => {
    paginador.reiniciar();
    pintarTabla();
});

async function iniciar() {
    if (!puedeAdmitir) panelForm.hidden = true;
    await cargar();
}

function mostrarBloqueAdmision(visible) {
    bloqueAdmision.hidden = !visible;

    bloqueAdmision.querySelectorAll("input").forEach(campo => {
        if (visible) {
            if (campo.dataset.obligatorio === "si") campo.required = true;
        } else {
            if (campo.required) campo.dataset.obligatorio = "si";
            campo.required = false;
        }
    });
}

async function cargar() {
    cuerpoTabla.innerHTML = `<tr><td colspan="8" class="vacio">Cargando…</td></tr>`;

    try {
        residentes = await api.listar("residentes");
        pintarTabla();
    } catch (error) {
        cuerpoTabla.innerHTML = `<tr><td colspan="8" class="vacio">${limpiar(error.message)}</td></tr>`;
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
        cuerpoTabla.innerHTML = `<tr><td colspan="8" class="vacio">
            ${filtro ? "Ningun residente coincide con la busqueda." : "Aun no hay residentes registrados."}
        </td></tr>`;
        return;
    }

    cuerpoTabla.innerHTML = paginador.recortar(lista).map(r => `
        <tr>
            <td>
                ${limpiar(r.nombres)} ${limpiar(r.apellidos)}
                <div class="secundario">${limpiar(r.psicopatologias) || "Sin diagnostico previo"}</div>
            </td>
            <td class="dpi">${limpiar(r.dpi) || "<span class='secundario'>Sin DPI</span>"}</td>
            <td class="num">${r.edad}</td>
            <td class="num">${moneda(r.cuotaMensual)}</td>
            <td>${fecha(r.fechaIngreso)}</td>
            <td class="num">${r.totalFamiliares}</td>
            <td><span class="estado ${r.activo ? "e-completo" : "e-cancelado"}">
                ${r.activo ? "Activo" : "Inactivo"}
            </span></td>
            <td class="der">${acciones(r)}</td>
        </tr>
    `).join("");
}

function acciones(r) {
    const editar = puedeAdmitir
        ? `<button class="btn-fila" onclick="editarResidente(${r.id})">Editar</button>`
        : `<button class="btn-fila" onclick="editarResidente(${r.id})">Datos clínicos</button>`;

    const baja = puedeAdmitir
        ? `<button class="btn-fila peligro" onclick="darDeBaja(${r.id})">Baja</button>`
        : "";

    return `
        ${editar}
        <button class="btn-fila" onclick="abrirFamiliares(${r.id})">Familiares</button>
        <a class="btn-fila" href="crear-remision.html?residente=${r.id}">Remitir</a>
        ${baja}
    `;
}

async function guardar(evento) {
    evento.preventDefault();

    try {
        if (!puedeAdmitir) {
            await api.enviarPut(`residentes/${residenteEnEdicion}/clinico`, {
                psicopatologias: form.psicopatologias.value.trim() || null,
                medicamentosCajon: form.medicamentosCajon.value.trim() || null
            });
            mostrarAviso("Datos clínicos actualizados.");
        } else {
            const datos = {
                nombres: form.nombres.value.trim(),
                apellidos: form.apellidos.value.trim(),
                dpi: form.dpi.value.trim() || null,
                fechaNacimiento: form.fechaNacimiento.value,
                cuotaMensual: Number(form.cuotaMensual.value),
                psicopatologias: form.psicopatologias.value.trim() || null,
                medicamentosCajon: form.medicamentosCajon.value.trim() || null
            };

            if (residenteEnEdicion) {
                await api.editar("residentes", residenteEnEdicion, datos);
                mostrarAviso("Datos del residente actualizados.");
            } else {
                await api.crear("residentes", datos);
                mostrarAviso("Residente registrado correctamente.");
            }
        }

        cancelarEdicion();
        cargar();
    } catch (error) {
        mostrarAviso(error.message, "error");
    }
}

function editarResidente(id) {
    const r = residentes.find(x => x.id === id);
    if (!r) return;

    residenteEnEdicion = id;
    panelForm.hidden = false;

    form.psicopatologias.value   = r.psicopatologias ?? "";
    form.medicamentosCajon.value = r.medicamentosCajon ?? "";

    if (puedeAdmitir) {
        mostrarBloqueAdmision(true);

        form.nombres.value         = r.nombres;
        form.apellidos.value       = r.apellidos;
        form.dpi.value             = r.dpi ?? "";
        form.fechaNacimiento.value = fechaInput(r.fechaNacimiento);
        form.cuotaMensual.value    = r.cuotaMensual;

        document.getElementById("titulo-form").textContent = `Editando a ${r.nombres} ${r.apellidos}`;
        document.getElementById("btn-guardar").textContent = "Guardar cambios";
    } else {
        mostrarBloqueAdmision(false);

        document.getElementById("titulo-form").textContent =
            `Datos clínicos de ${r.nombres} ${r.apellidos}`;
        document.getElementById("btn-guardar").textContent = "Guardar datos clínicos";
    }

    form.scrollIntoView({ behavior: "smooth", block: "center" });
    form.psicopatologias.focus();
}

function cancelarEdicion() {
    residenteEnEdicion = null;
    form.reset();

    if (puedeAdmitir) {
        mostrarBloqueAdmision(true);
        document.getElementById("titulo-form").textContent = "Registrar residente";
        document.getElementById("btn-guardar").textContent = "Registrar residente";
    } else {
        panelForm.hidden = true;
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
