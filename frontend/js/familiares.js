// =====================================================================
// familiares.js  -  Contactos familiares de un residente.
//
// Vive dentro del panel clinico porque un familiar no se administra
// solo: siempre se consulta en el contexto del residente que se esta
// viendo. El correo que se guarda aqui es el que usara el
// microservicio de notificaciones.
// =====================================================================

let residenteAbierto = null;   // Id del residente cuyo panel esta visible
let familiares = [];
let familiarEnEdicion = null;  // null = el formulario esta en modo alta

const panelFamiliares = document.getElementById("panel-familiares");
const formFamiliar = document.getElementById("form-familiar");
const cuerpoFamiliares = document.getElementById("cuerpo-familiares");

formFamiliar.addEventListener("submit", guardarFamiliar);

async function abrirFamiliares(residenteId) {
    residenteAbierto = residenteId;
    familiarEnEdicion = null;

    const r = residentes.find(x => x.id === residenteId);
    document.getElementById("familiares-residente").textContent =
        r ? `${r.nombres} ${r.apellidos}` : `residente ${residenteId}`;

    panelFamiliares.hidden = false;
    panelFamiliares.scrollIntoView({ behavior: "smooth", block: "nearest" });

    formFamiliar.reset();
    document.getElementById("btn-guardar-familiar").textContent = "Agregar contacto";

    await cargarFamiliares();
}

function cerrarFamiliares() {
    panelFamiliares.hidden = true;
    residenteAbierto = null;
    familiarEnEdicion = null;
    formFamiliar.reset();
}

async function cargarFamiliares() {
    cuerpoFamiliares.innerHTML = `<tr><td colspan="5" class="vacio">Cargando…</td></tr>`;

    try {
        familiares = await api.listar(`familiares/residente/${residenteAbierto}`);
        pintarFamiliares();
    } catch (error) {
        cuerpoFamiliares.innerHTML =
            `<tr><td colspan="5" class="vacio">${limpiar(error.message)}</td></tr>`;
    }
}

function pintarFamiliares() {
    if (familiares.length === 0) {
        cuerpoFamiliares.innerHTML = `<tr><td colspan="5" class="vacio">
            Este residente no tiene contactos registrados. Sin al menos uno
            no hay a quien avisarle de las citas ni a quien facturarle.
        </td></tr>`;
        return;
    }

    cuerpoFamiliares.innerHTML = familiares.map(f => `
        <tr>
            <td>${limpiar(f.nombre)}</td>
            <td>${limpiar(f.dpi) || "<span class='secundario'>—</span>"}</td>
            <td>${limpiar(f.email)}</td>
            <td>${limpiar(f.telefono) || "—"}</td>
            <td class="der">
                <button class="btn-fila" onclick="editarFamiliar(${f.id})">Editar</button>
                <button class="btn-fila peligro" onclick="eliminarFamiliar(${f.id})">Quitar</button>
            </td>
        </tr>
    `).join("");
}

function editarFamiliar(id) {
    const f = familiares.find(x => x.id === id);
    if (!f) return;

    familiarEnEdicion = id;
    formFamiliar.nombre.value = f.nombre;
    formFamiliar.dpi.value = f.dpi ?? "";
    formFamiliar.email.value = f.email;
    formFamiliar.telefono.value = f.telefono ?? "";
    document.getElementById("btn-guardar-familiar").textContent = "Guardar cambios";
    formFamiliar.nombre.focus();
}

function cancelarEdicionFamiliar() {
    familiarEnEdicion = null;
    formFamiliar.reset();
    document.getElementById("btn-guardar-familiar").textContent = "Agregar contacto";
}

async function guardarFamiliar(evento) {
    evento.preventDefault();
    if (!residenteAbierto) return;

    const datos = {
        nombre: formFamiliar.nombre.value.trim(),
        dpi: formFamiliar.dpi.value.trim() || null,
        email: formFamiliar.email.value.trim(),
        telefono: formFamiliar.telefono.value.trim() || null
    };

    try {
        if (familiarEnEdicion) {
            // El PUT no lleva ResidenteId: un contacto no se traslada de
            // residente, si se equivocaron se quita y se vuelve a crear.
            await api.editar("familiares", familiarEnEdicion, datos);
            mostrarAviso("Contacto actualizado.");
        } else {
            await api.crear("familiares", { residenteId: residenteAbierto, ...datos });
            mostrarAviso("Contacto agregado.");
        }

        cancelarEdicionFamiliar();
        await cargarFamiliares();
        // El listado de residentes muestra el conteo de familiares,
        // asi que lo recargo para que la columna quede al dia.
        await cargar();
        panelFamiliares.hidden = false;
    } catch (error) {
        mostrarAviso(error.message, "error");
    }
}

async function eliminarFamiliar(id) {
    const f = familiares.find(x => x.id === id);
    if (!confirm(`Quitar a ${f.nombre} como contacto? Dejara de recibir los avisos de este residente.`)) return;

    try {
        await api.eliminar("familiares", id);
        mostrarAviso("Contacto eliminado.");
        await cargarFamiliares();
        await cargar();
        panelFamiliares.hidden = false;
    } catch (error) {
        mostrarAviso(error.message, "error");
    }
}
