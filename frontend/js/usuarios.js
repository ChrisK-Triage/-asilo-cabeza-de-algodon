// =====================================================================
// usuarios.js  -  Administracion de usuarios del sistema.
//
// Esta pantalla no estaba en mis casos de uso: el analisis daba por
// sentado que los usuarios existian sin decir quien los crea. Hasta
// ahora se llenaban desde el script de semilla, lo que sirve para
// probar pero no para entregar.
// =====================================================================

let usuarios = [];
let roles = [];

// null = alta; un Id = edicion de ese usuario. Mismo formulario para
// las dos cosas, igual que en residentes.
let usuarioEnEdicion = null;

// A quien le estoy restableciendo la contrasena en el panel de abajo.
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

// Este filtro si vuelve a pedir datos a la API: incluir o no a los
// dados de baja es una consulta distinta, no un filtro en memoria.
document.getElementById("filtro-estado").addEventListener("change", () => {
    paginador.reiniciar();
    cargarUsuarios();
});

async function iniciar() {
    // allSettled y no all: si un catalogo falla quiero que el resto de
    // la pantalla cargue igual, con el aviso del error visible, en vez
    // de quedarse en blanco sin explicacion.
    await Promise.allSettled([cargarRoles(), cargarEspecialidades()]);
    await cargarUsuarios();
}

// ---------------- Catalogos del formulario ----------------

// Los roles salen de la API y no escritos en el HTML. Son los mismos
// cinco que acepta CK_Usuarios_Rol, y el endpoint los deriva de la
// misma constante que usa el backend para validar: una sola lista para
// la base, la validacion y el desplegable.
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

// La especialidad solo tiene sentido en el rol Especialista, y el CHECK
// CK_Usuarios_EspecialidadRol la exige nula en los demas. En vez de
// dejar que el usuario la llene y recibir un error al guardar, el
// select se habilita o se bloquea segun el rol elegido.
//
// La condicion la decide el campo requiereEspecialidad que manda la
// API, no una comparacion contra la cadena "Especialista" escrita aqui.
function ajustarEspecialidad() {
    const rol = roles.find(r => r.valor === form.rol.value);
    const requiere = rol?.requiereEspecialidad === true;
    const select = form.especialidadId;

    select.disabled = !requiere;
    select.required = requiere;

    // Al salir del rol de especialista limpio el valor: si lo dejara
    // puesto, el formulario mostraria una especialidad que no se va a
    // guardar y la pantalla estaria mintiendo.
    if (!requiere) select.value = "";

    document.getElementById("ayuda-especialidad").textContent = requiere
        ? "Obligatoria para este rol"
        : "Se habilita al elegir el rol Especialista";
}

// ---------------- Listado ----------------

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

// Los usuarios de la semilla quedaron con 'PENDIENTE_HASH_BCRYPT' como
// contrasena, asi que no pueden iniciar sesion. Marcarlo en la tabla
// es lo que convierte ese detalle en algo que se puede arreglar: sin
// este distintivo, el login va a fallar el miercoles y no va a quedar
// claro por que.
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

// ---------------- Alta y edicion ----------------

async function guardar(evento) {
    evento.preventDefault();

    const datos = {
        nombreCompleto: form.nombreCompleto.value.trim(),
        email: form.email.value.trim(),
        rol: form.rol.value,
        // El select vacio da "", que no es un entero. Lo convierto a
        // null para que el modelo lo reciba como ausencia de valor y no
        // como un cero.
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

    // Primero el rol y despues la especialidad: ajustarEspecialidad
    // limpia el select cuando el rol no es especialista, asi que
    // asignar la especialidad antes la perderia.
    ajustarEspecialidad();
    form.especialidadId.value = u.especialidadId ?? "";

    // La contrasena no se edita desde aqui. Oculto el campo y le quito
    // el required, porque un input oculto pero requerido bloquea el
    // envio del formulario sin mostrar donde esta el problema.
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

// ---------------- Contrasena ----------------

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

    // La comparacion es del lado del navegador a proposito: la API
    // recibe una sola contrasena. Mandar las dos la obligaria a validar
    // algo que es un detalle del formulario, no una regla del sistema.
    if (nueva !== repetida) {
        mostrarAviso("Las dos contraseñas no coinciden.", "error");
        return;
    }

    try {
        await api.enviarPut(`usuarios/${usuarioEnPassword}/password`, { password: nueva });
        mostrarAviso("Contraseña restablecida.");
        cerrarPassword();
        // Recargo para que la columna de acceso pase a "Definida".
        cargarUsuarios();
    } catch (error) {
        mostrarAviso(error.message, "error");
    }
}

// ---------------- Baja y reactivacion ----------------

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
        // Aqui caen los dos 409 del controlador: ultimo administrador
        // activo y especialista con citas sin cerrar. El mensaje ya
        // explica que hacer, asi que lo muestro tal cual.
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
