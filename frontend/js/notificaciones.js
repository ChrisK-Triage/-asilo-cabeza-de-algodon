let avisos = [];
let temporizador = null;

const cuerpoTabla = document.getElementById("cuerpo-tabla");
const paginador = new Paginador("paginador-avisos", () => pintarTabla());

document.addEventListener("DOMContentLoaded", iniciar);

document.getElementById("filtro-situacion").addEventListener("change", () => {
    paginador.reiniciar();
    cargar();
});

document.getElementById("auto").addEventListener("change", ajustarAutomatico);

async function iniciar() {
    await cargar();
    ajustarAutomatico();
}

function ajustarAutomatico() {
    clearInterval(temporizador);

    if (document.getElementById("auto").value === "si") {
        temporizador = setInterval(cargar, 5000);
    }
}

async function cargar() {
    const situacion = document.getElementById("filtro-situacion").value;
    const ruta = situacion ? `notificaciones?situacion=${situacion}` : "notificaciones";

    if (avisos.length === 0) {
        cuerpoTabla.innerHTML = `<tr><td colspan="6" class="vacio">Cargando…</td></tr>`;
    }

    try {
        const [lista, resumen] = await Promise.all([
            api.listar(ruta),
            api.listar("notificaciones/resumen")
        ]);

        avisos = lista;

        document.getElementById("r-pendientes").textContent = resumen.pendientes;
        document.getElementById("r-entregados").textContent = resumen.entregados;
        document.getElementById("r-fallidos").textContent = resumen.fallidos;

        pintarTabla();
    } catch (error) {
        clearInterval(temporizador);
        cuerpoTabla.innerHTML = `<tr><td colspan="6" class="vacio">${limpiar(error.message)}</td></tr>`;
    }
}

function pintarTabla() {
    document.getElementById("conteo").textContent =
        `${avisos.length} ${avisos.length === 1 ? "aviso" : "avisos"}`;

    if (avisos.length === 0) {
        cuerpoTabla.innerHTML = `<tr><td colspan="6" class="vacio">
            No hay avisos en la bandeja. Se encolan al asignar una cita,
            emitir una factura o registrar un abono.
        </td></tr>`;
        return;
    }

    cuerpoTabla.innerHTML = paginador.recortar(avisos).map(a => `
        <tr>
            <td>
                ${limpiar(a.tipoEtiqueta)}
                <div class="secundario">#${a.id}</div>
            </td>
            <td>${limpiar(a.destinatarios)}</td>
            <td>
                ${fechaHora(a.fechaCreacion)}
                ${a.fechaProcesado
                    ? `<div class="secundario">Entregado ${fechaHora(a.fechaProcesado)}</div>` : ""}
            </td>
            <td>
                <span class="estado ${claseSituacion(a.situacion)}">${limpiar(a.situacion)}</span>
                ${a.ultimoError
                    ? `<div class="antiguedad">${limpiar(a.ultimoError)}</div>` : ""}
            </td>
            <td class="num">${a.intentos}</td>
            <td class="der">
                <button class="btn-fila" onclick="verPayload(${a.id})">Ver contenido</button>
            </td>
        </tr>
    `).join("");
}

function claseSituacion(situacion) {
    if (situacion === "Entregado") return "e-completo";
    if (situacion === "Fallido") return "e-cancelado";
    return "e-pendiente";
}

function verPayload(id) {
    const a = avisos.find(x => x.id === id);
    if (!a) return;

    let texto = a.payload;

    try {
        texto = JSON.stringify(JSON.parse(a.payload), null, 2);
    } catch {
        // Si no es JSON valido lo muestro crudo: es justamente lo que
        // hay que poder leer para entender por que fallo.
    }

    document.getElementById("payload").textContent = texto;
    document.getElementById("panel-payload").hidden = false;
    document.getElementById("panel-payload").scrollIntoView({ behavior: "smooth", block: "center" });
}

function cerrarPayload() {
    document.getElementById("panel-payload").hidden = true;
}
