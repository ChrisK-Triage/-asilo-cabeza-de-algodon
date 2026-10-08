// =====================================================================
// api.js
// Punto unico de contacto con la API. Si cambia la direccion del
// servidor o el esquema de autenticacion, lo ajusto solo aqui.
// =====================================================================

// Puerto del perfil "http" de launchSettings.json.
const API_URL = "http://localhost:5177/api";

async function apiFetch(ruta, opciones = {}) {
    let respuesta;

    try {
        respuesta = await fetch(`${API_URL}/${ruta}`, {
            headers: { "Content-Type": "application/json" },
            ...opciones
        });
    } catch {
        // Este catch salta cuando el servidor no responde: API apagada,
        // puerto equivocado o certificado de desarrollo sin aceptar.
        throw new Error(
            "No se pudo conectar con el servidor. Verificar que la API este " +
            "ejecutandose y que el puerto en api.js sea el correcto."
        );
    }

    if (respuesta.status === 204) return null;

    if (!respuesta.ok) {
        let detalle = `Error ${respuesta.status}`;
        try {
            const cuerpo = await respuesta.json();
            if (cuerpo.mensaje) {
                detalle = cuerpo.mensaje;
            } else if (cuerpo.errors) {
                // Errores de validacion de DataAnnotations: {"Nombres": ["..."]}
                detalle = Object.values(cuerpo.errors).flat().join(" ");
            }
        } catch { /* la respuesta no traia JSON */ }
        throw new Error(detalle);
    }

    return respuesta.json();
}

const api = {
    listar:   recurso => apiFetch(recurso),
    obtener:  (recurso, id) => apiFetch(`${recurso}/${id}`),
    crear:    (recurso, datos) => apiFetch(recurso, { method: "POST", body: JSON.stringify(datos) }),
    editar:   (recurso, id, datos) => apiFetch(`${recurso}/${id}`, { method: "PUT", body: JSON.stringify(datos) }),
    parchar:  (ruta, datos) => apiFetch(ruta, { method: "PATCH", body: JSON.stringify(datos) }),
    eliminar: (recurso, id) => apiFetch(`${recurso}/${id}`, { method: "DELETE" })
};

// ---------------- Utilidades compartidas ----------------

// Escapa el texto antes de insertarlo en el DOM.
// Sin esto, un residente llamado <script>...</script> se ejecutaria
// al pintar la tabla. Es la mitigacion de XSS del lado del cliente.
function limpiar(texto) {
    const div = document.createElement("div");
    div.textContent = texto ?? "";
    return div.innerHTML;
}

function fecha(iso) {
    if (!iso) return "—";
    return new Date(iso).toLocaleDateString("es-GT", {
        day: "2-digit", month: "2-digit", year: "numeric"
    });
}

function fechaInput(iso) {
    return iso ? iso.split("T")[0] : "";
}

function mostrarAviso(mensaje, tipo = "ok") {
    const caja = document.getElementById("aviso");
    if (!caja) return;
    caja.textContent = mensaje;
    caja.className = `aviso visible aviso-${tipo}`;
    clearTimeout(caja._temporizador);
    caja._temporizador = setTimeout(() => (caja.className = "aviso"), 5000);
}

// Marca como activo el enlace de la pagina actual en la navegacion lateral.
document.addEventListener("DOMContentLoaded", () => {
    const actual = location.pathname.split("/").pop();
    document.querySelectorAll("aside a").forEach(a => {
        if (a.getAttribute("href") === actual) a.classList.add("activo");
    });
});
