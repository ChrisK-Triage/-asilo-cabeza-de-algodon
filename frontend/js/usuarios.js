let usuarios = [];
let roles = [];

let usuarioEnEdicion = null;

let usuarioEnPassword = null;

const form = document.getElementById("form-usuario");
const formPassword = document.getElementById("form-password");
const cuerpoTabla = document.getElementById("cuerpo-tabla");
const paginador = new Paginador("paginador-usuarios", () => pintarTabla());

document.addEventListener("DOMContentLoaded", iniciar);
form.addEventListener("submit", guardar);
formPassword.addEventListener("submit", restablecerPassword);
form.rol.addEventListener("change", ajustarEspecialidad);
document.getElementById("btn-cancelar-edicion").addEventListener("click", cancelarEdicion);

document.getElementById("buscar").addEventListener("input", () => {
    paginador.reiniciar();
    pintarTabla();
});

document.getElementById("filtro-rol").addEventListener("change", () => {
    paginador.reiniciar();
    pintarTabla();
});

document.getElementById("filtro-estado").addEventListener("change", () => {
    paginador.reiniciar();
    cargarUsuarios();
});

async function iniciar() {

    await Promise.allSettled([cargarRoles(), cargarEspecialidades()]);
    await cargarUsuarios();
}

async function cargarRoles() {
    try {
        roles = await api.listar("usuarios/roles");

        form.rol.innerHTML =
            `<option value="">Selecciona un rol</option>` +
            roles.map(r => `<option value="${limpiar(r.valor)}">${limpiar(r.etiqueta)}</option>`).join("");

        document.getElementById("filtro-rol").innerHTML =
            `<option value="">Todos los roles</option>` +
            roles.map(r => `<option value="${limpiar(r.valor)}">${limpiar(r.etiqueta)}</option>`).join("");

    } catch (error) {
        form.rol.innerHTML = `<option value="">No se pudieron cargar los roles</option>`;
        mostrarAviso(error.message, "error");
    }
}

async function cargarEspecialidades() {
    const select = form.especialidadId;

    try {
        const especialidades = await api.listar("catalogos/especialidades");

        if (especialidades.length === 0) {
            select.innerHTML = `<option value="">No hay especialidades en el catálogo</option>`;
            return;
        }

        select.innerHTML =
            `<option value="">Selecciona una especialidad</option>` +
            especialidades.map(e => `<option value="${e.id}">${limpiar(e.nombre)}</option>`).join("");

    } catch (error) {
        select.innerHTML = `<option value="">No se pudieron cargar las especialidades</option>`;
        mostrarAviso(error.message, "error");
    }
}

function ajustarEspecialidad() {
    const rol = roles.find(r => r.valor === form.rol.value);
    const requiere = rol?.requiereEspecialidad === true;
    const select = form.especialidadId;

    select.disabled = !requiere;
    select.required = requiere;

    if (!requiere) select.value = "";

    document.getElementById("ayuda-especialidad").textContent = requiere
        ? "Obligatoria para este rol"
        : "Se habilita al elegir el rol Especialista";
}

async function cargarUsuarios() {
    cuerpoTabla.innerHTML = `<tr><td colspan="7" class="vacio">Cargando…</td></tr>`;

    const incluirInactivos = document.getElementById("filtro-estado").value === "todos";

    try {
        usuarios = await api.listar(`usuarios?incluirInactivos=${incluirInactivos}`);
        pintarTabla();
    } catch (error) {
        cuerpoTabla.innerHTML = `<tr><td colspan="7" class="vacio">${limpiar(error.message)}</td></tr>`;
        document.getElementById("conteo").textContent = "";
    }
}

function pintarTabla() {
    const texto = document.getElementById("buscar").value.toLowerCase().trim();
    const rolFiltro = document.getElementById("filtro-rol").value;

    const lista = usuarios.filter(u => {
        const coincideTexto = !texto
            || u.nombreCompleto.toLowerCase().includes(texto)
            || u.email.toLowerCase().includes(texto);
        const coincideRol = !rolFiltro || u.rol === rolFiltro;
        return coincideTexto && coincideRol;
    });

    document.getElementById("conteo").textContent =
        `${lista.length} ${lista.length === 1 ? "usuario" : "usuarios"}`;

    if (lista.length === 0) {
        cuerpoTabla.innerHTML = `<tr><td colspan="7" class="vacio">
            ${texto || rolFiltro ? "Ningún usuario coincide con el filtro." : "Aún no hay usuarios registrados."}
        </td></tr>`;
        return;
    }

    cuerpoTabla.innerHTML = paginador.recortar(lista).map(u => `
        <tr>
            <td>
                ${limpiar(u.nombreCompleto)}
                <div class="secundario">${limpiar(u.email)}</div>
            </td>
            <td>${limpiar(u.rolEtiqueta)}</td>
            <td>${limpiar(u.especialidad) || "<span class='secundario'>—</span>"}</td>
            <td class="num">${u.registrosAsociados}</td>
            <td>${distintivoAcceso(u)}</td>
            <td><span class="estado ${u.activo ? "e-completo" : "e-cancelado"}">
                ${u.activo ? "Activo" : "Dado de baja"}
            </span></td>
            <td class="der">${accionesFila(u)}</td>
        </tr>
    `).join("");
}

function distintivoAcceso(u) {
    return u.tieneContrasenaDefinida
        ? `<span class="estado e-completo">Definida</span>`
        : `<span class="estado e-pendiente">Sin contraseña</span>`;
}

function accionesFila(u) {
    if (!u.activo) {
        return `<button class="btn-fila" onclick="reactivar(${u.id})">Activar</button>`;
    }

    return `
        <button class="btn-fila" onclick="editarUsuario(${u.id})">Editar</button>
        <button class="btn-fila" onclick="abrirPassword(${u.id})">Contraseña</button>
        <button class="btn-fila peligro" onclick="darDeBaja(${u.id})">Baja</button>
    `;
}

async function guardar(evento) {
    evento.preventDefault();

    const datos = {
        nombreCompleto: form.nombreCompleto.value.trim(),
        email: form.email.value.trim(),
        rol: form.rol.value,

        especialidadId: form.especialidadId.value ? Number(form.especialidadId.value) : null
    };

    try {
        if (usuarioEnEdicion) {
            await api.editar("usuarios", usuarioEnEdicion, datos);
            mostrarAviso("Datos del usuario actualizados.");
        } else {
            datos.password = form.password.value;
            await api.crear("usuarios", datos);
            mostrarAviso(`${datos.nombreCompleto} registrado. Ya puede iniciar sesión con su correo.`);
        }

        cancelarEdicion();
        cargarUsuarios();
    } catch (error) {
        mostrarAviso(error.message, "error");
    }
}

function editarUsuario(id) {
    const u = usuarios.find(x => x.id === id);
    if (!u) return;

    usuarioEnEdicion = id;

    form.nombreCompleto.value = u.nombreCompleto;
    form.email.value = u.email;
    form.rol.value = u.rol;

    ajustarEspecialidad();
    form.especialidadId.value = u.especialidadId ?? "";

    document.getElementById("campo-password").hidden = true;
    form.password.required = false;
    form.password.value = "";

    document.getElementById("titulo-form").textContent = `Editando a ${u.nombreCompleto}`;
    document.getElementById("btn-guardar").textContent = "Guardar cambios";

    form.scrollIntoView({ behavior: "smooth", block: "center" });
    form.nombreCompleto.focus();
}

function cancelarEdicion() {
    usuarioEnEdicion = null;
    form.reset();

    document.getElementById("campo-password").hidden = false;
    form.password.required = true;

    ajustarEspecialidad();

    document.getElementById("titulo-form").textContent = "Registrar usuario";
    document.getElementById("btn-guardar").textContent = "Registrar usuario";
}

function abrirPassword(id) {
    const u = usuarios.find(x => x.id === id);
    if (!u) return;

    usuarioEnPassword = id;

    document.getElementById("password-usuario").textContent = u.nombreCompleto;
    document.getElementById("panel-password").hidden = false;
    formPassword.reset();
    document.getElementById("password-nueva").focus();
    document.getElementById("panel-password").scrollIntoView({ behavior: "smooth", block: "center" });
}

function cerrarPassword() {
    usuarioEnPassword = null;
    formPassword.reset();
    document.getElementById("panel-password").hidden = true;
}

async function restablecerPassword(evento) {
    evento.preventDefault();
    if (!usuarioEnPassword) return;

    const nueva = document.getElementById("password-nueva").value;
    const repetida = document.getElementById("password-repetir").value;

    if (nueva !== repetida) {
        mostrarAviso("Las dos contraseñas no coinciden.", "error");
        return;
    }

    try {
        await api.enviarPut(`usuarios/${usuarioEnPassword}/password`, { password: nueva });
        mostrarAviso("Contraseña restablecida.");
        cerrarPassword();

        cargarUsuarios();
    } catch (error) {
        mostrarAviso(error.message, "error");
    }
}

async function darDeBaja(id) {
    const u = usuarios.find(x => x.id === id);

    const advertencia = u.registrosAsociados > 0
        ? ` Tiene ${u.registrosAsociados} registro${u.registrosAsociados === 1 ? "" : "s"} en el historial; ese historial se conserva.`
        : "";

    if (!confirm(`Dar de baja a ${u.nombreCompleto}? Deja de poder iniciar sesión.${advertencia}`)) return;

    try {
        await api.eliminar("usuarios", id);
        mostrarAviso(`${u.nombreCompleto} dado de baja.`);
        cargarUsuarios();
    } catch (error) {

        mostrarAviso(error.message, "error");
    }
}

async function reactivar(id) {
    const u = usuarios.find(x => x.id === id);

    try {
        await api.enviarPut(`usuarios/${id}/reactivacion`, {});
        mostrarAviso(`${u.nombreCompleto} habilitado de nuevo.`);
        cargarUsuarios();
    } catch (error) {
        mostrarAviso(error.message, "error");
    }
}
