const API_URL = (location.protocol === "file:" ||
                 (location.hostname === "localhost" && location.port !== "5177"))
    ? "http://localhost:5177/api"
    : "/api";

async function apiFetch(ruta, opciones = {}) {
    let respuesta;

    const cabeceras = { "Content-Type": "application/json" };
    const token = typeof sesion !== "undefined" ? sesion.token : null;
    if (token) cabeceras.Authorization = `Bearer ${token}`;

    try {
        respuesta = await fetch(`${API_URL}/${ruta}`, {
            ...opciones,
            headers: { ...cabeceras, ...(opciones.headers ?? {}) }
        });
    } catch {

        throw new Error(
            "No se pudo conectar con el servidor. Verificar que la API este " +
            "ejecutandose y que el puerto en api.js sea el correcto."
        );
    }

    if (respuesta.status === 204) return null;

    if (respuesta.status === 401 && typeof sesion !== "undefined") {
        sesion.borrar();
        if (!location.pathname.endsWith("login.html")) {
            window.location.replace("login.html");
        }
        throw new Error("La sesión expiró. Hay que volver a entrar.");
    }

    if (respuesta.status === 403) {
        throw new Error("Tu rol no tiene permiso para esta operación.");
    }

    if (!respuesta.ok) {
        let detalle = `Error ${respuesta.status}`;
        try {
            const cuerpo = await respuesta.json();
            if (cuerpo.mensaje) {
                detalle = cuerpo.mensaje;
            } else if (cuerpo.errors) {

                detalle = Object.values(cuerpo.errors).flat().join(" ");
            }
        } catch {  }
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

    enviarPut: (ruta, datos) => apiFetch(ruta, { method: "PUT", body: JSON.stringify(datos) }),
    eliminar: (recurso, id) => apiFetch(`${recurso}/${id}`, { method: "DELETE" })
};

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

const FILAS_POR_PAGINA = 10;

class Paginador {

    constructor(contenedorId, alCambiar, porPagina = FILAS_POR_PAGINA) {
        this.contenedorId = contenedorId;
        this.alCambiar = alCambiar;
        this.porPagina = porPagina;
        this.pagina = 1;
    }

    reiniciar() {
        this.pagina = 1;
    }

    recortar(lista) {
        const total = lista.length;
        const ultimaPagina = Math.max(1, Math.ceil(total / this.porPagina));

        if (this.pagina > ultimaPagina) this.pagina = ultimaPagina;

        const desde = (this.pagina - 1) * this.porPagina;
        const hasta = Math.min(desde + this.porPagina, total);

        this._pintarControles(desde, hasta, total, ultimaPagina);
        return lista.slice(desde, hasta);
    }

    _pintarControles(desde, hasta, total, ultimaPagina) {
        const caja = document.getElementById(this.contenedorId);
        if (!caja) return;

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

document.addEventListener("DOMContentLoaded", () => {
    const actual = location.pathname.split("/").pop();
    document.querySelectorAll("aside a").forEach(a => {
        if (a.getAttribute("href") === actual) a.classList.add("activo");
    });
});

function etiquetaEstado(c) {
    if (c.estado === "Esperando anexos") {
        const faltan = [];
        if (c.requiereLaboratorio) faltan.push("exámenes");
        if (c.requiereFarmacia)    faltan.push("medicamento");

        return faltan.length
            ? limpiar(`Esperando ${faltan.join(" y ")}`)
            : "Esperando anexos";
    }

    const mapa = {
        "Programada":          "Sin consulta",
        "En evaluacion":       "En consulta",
        "Bitacora completada": "Consulta cerrada",
        "Costos calculados":   "Costos aplicados"
    };
    return limpiar(mapa[c.estado] || c.estado);
}

function claseEstadoCita(estado) {
    if (estado === "Programada")          return "e-pendiente";
    if (estado === "En evaluacion")       return "e-proceso";
    if (estado === "Esperando anexos")    return "e-proceso";
    if (estado === "Bitacora completada") return "e-completo";
    return "e-completo";
}
