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
    // Para subrutas como laboratorio/ordenes/5/resultado
    enviarPut: (ruta, datos) => apiFetch(ruta, { method: "PUT", body: JSON.stringify(datos) }),
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

function fechaHora(iso) {
    if (!iso) return "—";
    const d = new Date(iso);
    return d.toLocaleDateString("es-GT", { day: "2-digit", month: "2-digit", year: "numeric" })
         + " · " + d.toLocaleTimeString("es-GT", { hour: "2-digit", minute: "2-digit" });
}

function moneda(valor) {
    return new Intl.NumberFormat("es-GT", {
        style: "currency", currency: "GTQ", minimumFractionDigits: 2
    }).format(valor ?? 0);
}

// ---------------- Paginacion ----------------
//
// Corta la lista en paginas y dibuja los controles. Lo hago aqui y no
// en cada pantalla para que todas las tablas se comporten igual.
//
// Limitacion conocida: la API sigue devolviendo la lista completa y el
// corte ocurre en el navegador. Con los volumenes del asilo (decenas de
// residentes, cientos de citas al ano) no se nota, pero no escala: lo
// correcto es paginar en SQL con OFFSET/FETCH y que el endpoint reciba
// pagina y tamano. Lo dejo anotado como pendiente, no como resuelto.

const FILAS_POR_PAGINA = 10;

class Paginador {
    /**
     * contenedorId: el div donde van los controles.
     * alCambiar: que hacer cuando el usuario cambia de pagina; normalmente
     *            la misma funcion que pinta la tabla.
     */
    constructor(contenedorId, alCambiar, porPagina = FILAS_POR_PAGINA) {
        this.contenedorId = contenedorId;
        this.alCambiar = alCambiar;
        this.porPagina = porPagina;
        this.pagina = 1;
    }

    // Vuelvo a la primera pagina. Lo llamo cuando cambia un filtro: si
    // estoy en la pagina 4 y el filtro deja 3 resultados, sin esto la
    // tabla sale vacia y parece un error.
    reiniciar() {
        this.pagina = 1;
    }

    /**
     * Devuelve el trozo de la lista que toca mostrar y dibuja los
     * controles. La tabla solo tiene que pintar lo que esto devuelve.
     */
    recortar(lista) {
        const total = lista.length;
        const ultimaPagina = Math.max(1, Math.ceil(total / this.porPagina));

        // Si borraron filas y la pagina actual ya no existe, retrocedo.
        if (this.pagina > ultimaPagina) this.pagina = ultimaPagina;

        const desde = (this.pagina - 1) * this.porPagina;
        const hasta = Math.min(desde + this.porPagina, total);

        this._pintarControles(desde, hasta, total, ultimaPagina);
        return lista.slice(desde, hasta);
    }

    _pintarControles(desde, hasta, total, ultimaPagina) {
        const caja = document.getElementById(this.contenedorId);
        if (!caja) return;

        // Con una sola pagina los controles sobran.
        if (total <= this.porPagina) {
            caja.innerHTML = "";
            return;
        }

        const primera = this.pagina === 1;
        const ultima = this.pagina === ultimaPagina;

        caja.innerHTML = `
            <span class="rango">${desde + 1}–${hasta} de ${total}</span>
            <button type="button" class="btn-pagina" ${primera ? "disabled" : ""}
                    aria-label="Página anterior">‹</button>
            <button type="button" class="btn-pagina" ${ultima ? "disabled" : ""}
                    aria-label="Página siguiente">›</button>
        `;

        const [anterior, siguiente] = caja.querySelectorAll("button");
        anterior.addEventListener("click", () => this._ir(this.pagina - 1, ultimaPagina));
        siguiente.addEventListener("click", () => this._ir(this.pagina + 1, ultimaPagina));
    }

    _ir(pagina, ultimaPagina) {
        if (pagina < 1 || pagina > ultimaPagina) return;
        this.pagina = pagina;
        this.alCambiar();
    }
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
