let residenteAbierto = null;
let familiares = [];
let familiarEnEdicion = null;

const panelFamiliares = document.getElementById("panel-familiares");
const formFamiliar = document.getElementById("form-familiar");
const cuerpoFamiliares = document.getElementById("cuerpo-familiares");
const formularioFamiliar = formFamiliar.closest(".panel-cuerpo");

formFamiliar.addEventListener("submit", guardarFamiliar);

async function abrirFamiliares(residenteId) {
    residenteAbierto = residenteId;
    familiarEnEdicion = null;

    const r = residentes.find(x => x.id === residenteId);
    document.getElementById("familiares-residente").textContent =
        r ? `${r.nombres} ${r.apellidos}` : `residente ${residenteId}`;

    panelFamiliares.hidden = false;
    formularioFamiliar.hidden = !puedeAdmitir;
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
            <td class="der">${puedeAdmitir ? `
                <button class="btn-fila" onclick="editarFamiliar(${f.id})">Editar</button>
                <button class="btn-fila peligro" onclick="eliminarFamiliar(${f.id})">Quitar</button>
            ` : `<span class="secundario">Solo lectura</span>`}</td>
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

            await api.editar("familiares", familiarEnEdicion, datos);
            mostrarAviso("Contacto actualizado.");
        } else {
            await api.crear("familiares", { residenteId: residenteAbierto, ...datos });
            mostrarAviso("Contacto agregado.");
        }

        cancelarEdicionFamiliar();
        await cargarFamiliares();

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
