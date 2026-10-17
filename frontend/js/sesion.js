const CLAVE_SESION = "asilo.sesion";

const sesion = {
    guardar(datos) {
        try {
            localStorage.setItem(CLAVE_SESION, JSON.stringify(datos));
        } catch {
            window._sesionEnMemoria = datos;
        }
    },

    leer() {
        try {
            const crudo = localStorage.getItem(CLAVE_SESION);
            return crudo ? JSON.parse(crudo) : (window._sesionEnMemoria ?? null);
        } catch {
            return window._sesionEnMemoria ?? null;
        }
    },

    borrar() {
        try {
            localStorage.removeItem(CLAVE_SESION);
        } catch {  }
        window._sesionEnMemoria = null;
    },

    get token() {
        return this.leer()?.token ?? null;
    },

    get vigente() {
        const s = this.leer();
        return Boolean(s?.token) && new Date(s.expira) > new Date();
    }
};

function cerrarSesion() {
    sesion.borrar();
    window.location.replace("login.html");
}

function paginaActual() {
    return window.location.pathname.split("/").pop() || "login.html";
}

function exigirSesion() {
    const s = sesion.leer();

    if (!s || !sesion.vigente) {
        sesion.borrar();
        window.location.replace("login.html");
        return null;
    }

    const actual = paginaActual();
    if (!s.paginasPermitidas.includes(actual)) {
        window.location.replace(s.paginaInicio);
        return null;
    }

    return s;
}

const sesionActiva = paginaActual() === "login.html" ? null : exigirSesion();

function pintarIdentidad(s) {
    const caja = document.querySelector("header .usuario");
    if (!caja) return;

    const detalle = s.especialidad ? `${s.rolEtiqueta} · ${s.especialidad}` : s.rolEtiqueta;
    caja.innerHTML = `<strong>${limpiar(s.nombreCompleto)}</strong>${limpiar(detalle)}`;
}

function filtrarNavegacion(s) {
    document.querySelectorAll("aside a").forEach(enlace => {
        const destino = enlace.getAttribute("href");

        if (destino === "login.html") {
            enlace.textContent = "Cerrar sesión";
            enlace.addEventListener("click", evento => {
                evento.preventDefault();
                cerrarSesion();
            });
            return;
        }

        if (!s.paginasPermitidas.includes(destino)) enlace.hidden = true;
    });

    document.querySelectorAll("aside .grupo-nav").forEach(grupo => {
        const visibles = [];
        let nodo = grupo.nextElementSibling;

        while (nodo && !nodo.classList.contains("grupo-nav")) {
            if (nodo.tagName === "A" && !nodo.hidden) visibles.push(nodo);
            nodo = nodo.nextElementSibling;
        }

        if (visibles.length === 0) grupo.hidden = true;
    });
}

if (sesionActiva) {
    document.addEventListener("DOMContentLoaded", () => {
        pintarIdentidad(sesionActiva);
        filtrarNavegacion(sesionActiva);
    });
}
