// =====================================================================
// estado-cuenta.js  -  Calculadora financiera y cobros (RF-06, RF-10).
//
// Reemplaza la maqueta de la fase de diseno, que tenia las cifras
// escritas a mano en el HTML. Aqui todo sale del calculo de la API.
// =====================================================================

let facturas = [];
let calculoActual = null;
let facturaSeleccionada = null;

const pagFacturas = new Paginador("paginador-facturas", () => pintarFacturas());

const formPago = document.getElementById("form-pago");
const selResidente = document.getElementById("residenteId");

document.addEventListener("DOMContentLoaded", iniciar);
document.getElementById("btn-calcular").addEventListener("click", calcular);
document.getElementById("btn-emitir").addEventListener("click", emitir);
document.getElementById("btn-cerrar-pago").addEventListener("click", cerrarPago);
formPago.addEventListener("submit", registrarPago);
selResidente.addEventListener("change", () => {
    cerrarCalculo();
    cerrarPago();
    cargarFacturas();
});

async function iniciar() {
    // El mes por omision es el anterior al actual: lo normal es liquidar
    // un periodo ya terminado, no el que todavia esta corriendo.
    const hoy = new Date();
    const mesAnterior = new Date(hoy.getFullYear(), hoy.getMonth() - 1, 1);
    document.getElementById("mes").value = mesAnterior.getMonth() + 1;
    document.getElementById("anio").value = mesAnterior.getFullYear();

    await cargarResidentes();
    await cargarFacturas();
}

async function cargarResidentes() {
    try {
        const residentes = await api.listar("residentes");

        selResidente.innerHTML = residentes.map(r =>
            `<option value="${r.id}">${limpiar(r.nombres)} ${limpiar(r.apellidos)}</option>`
        ).join("");

        if (residentes.length === 0) {
            selResidente.innerHTML = `<option value="">No hay residentes registrados</option>`;
            selResidente.disabled = true;
        }
    } catch (error) {
        mostrarAviso(error.message, "error");
    }
}

// ---------------- Calculo del periodo ----------------

async function calcular() {
    const residenteId = Number(selResidente.value);
    const mes = Number(document.getElementById("mes").value);
    const anio = Number(document.getElementById("anio").value);

    if (!residenteId) return;

    try {
        calculoActual = await api.listar(
            `facturacion/calculo?residenteId=${residenteId}&anio=${anio}&mes=${mes}`);
        pintarCalculo();
    } catch (error) {
        cerrarCalculo();
        mostrarAviso(error.message, "error");
    }
}

function pintarCalculo() {
    const c = calculoActual;
    const panel = document.getElementById("panel-calculo");
    panel.hidden = false;

    document.getElementById("rotulo-periodo").textContent =
        `${String(c.mes).padStart(2, "0")}/${c.anio} · ${c.residenteNombre}`;

    // Detalle de las citas que entran. Sin esto el total es un numero
    // que hay que creer; con esto se puede verificar renglon por renglon.
    const detalle = document.getElementById("detalle-citas");
    if (c.citas.length === 0) {
        detalle.innerHTML = `<p class="ayuda">
            No hay citas liquidables en este periodo. Solo se cobraría la cuota mensual.
        </p>`;
    } else {
        detalle.innerHTML = `
            <table>
                <thead>
                    <tr>
                        <th>Cita</th><th>Especialista</th>
                        <th class="num">Consulta</th><th class="num">Laboratorio</th>
                        <th class="num">Farmacia</th><th class="num">Subtotal</th>
                    </tr>
                </thead>
                <tbody>
                    ${c.citas.map(x => `
                        <tr>
                            <td>
                                ${fechaHora(x.bloqueHorario)}
                                <div class="secundario">${limpiar(x.especialidad)}</div>
                            </td>
                            <td>${limpiar(x.especialistaNombre)}</td>
                            <td class="num">${moneda(x.costoConsulta)}</td>
                            <td class="num">${moneda(x.costoLaboratorio)}</td>
                            <td class="num">${moneda(x.costoFarmacia)}</td>
                            <td class="num"><strong>${moneda(x.subtotal)}</strong></td>
                        </tr>
                    `).join("")}
                </tbody>
            </table>`;
    }

    document.getElementById("d-consultas").textContent   = moneda(c.costoConsultas);
    document.getElementById("d-laboratorio").textContent = moneda(c.costoLaboratorio);
    document.getElementById("d-farmacia").textContent    = moneda(c.costoFarmacia);
    document.getElementById("d-servicios").textContent   = moneda(c.totalServicios);
    document.getElementById("d-cuota").textContent       = moneda(c.cuotaMensual);
    document.getElementById("d-neto").textContent        = moneda(c.totalNeto);

    // El descuento va en negativo y con el porcentaje a la vista: es la
    // cifra que el familiar va a querer entender.
    document.getElementById("d-rotulo-descuento").textContent =
        `Descuento del convenio (${c.porcentajeDescuento}% sobre servicios)`;
    document.getElementById("d-descuento").textContent = "− " + moneda(c.descuentoAplicado);

    // En una complementaria la cuota viene en cero porque ya se cobro.
    // Lo digo en el rotulo en vez de dejar un "Q 0.00" suelto, que se
    // leeria como que el residente no paga estancia.
    document.getElementById("d-rotulo-cuota").textContent = c.esComplementaria
        ? `Cuota mensual de estancia (ya cobrada en la factura #${c.facturaExistenteId})`
        : "Cuota mensual de estancia";

    document.getElementById("d-rotulo-neto").textContent = c.esComplementaria
        ? "Total de la complementaria"
        : "Total a pagar";

    pintarBoton(c);
}

// Antes esta pantalla bloqueaba el boton en cuanto el periodo tenia
// factura. Eso dejaba a la vista un desglose con cargos que nadie podia
// cobrar: la cita cerrada despues del corte sumaba al total y el unico
// boton que la habria liquidado estaba deshabilitado.
//
// Ahora el periodo facturado no cierra nada por si mismo. Lo que decide
// es si hay citas nuevas: si las hay, se emite una complementaria; si no
// las hay, no queda nada que cobrar y ahi si el boton se apaga.
function pintarBoton(c) {
    const btn = document.getElementById("btn-emitir");
    const aviso = document.getElementById("aviso-complementaria");

    if (!c.hayQueFacturar) {
        btn.disabled = true;
        btn.textContent = c.esComplementaria ? "Periodo cobrado por completo" : "Nada que facturar";
        aviso.hidden = !c.esComplementaria;
        if (c.esComplementaria) {
            aviso.textContent =
                `Este periodo se cobro en la factura #${c.facturaExistenteId} y no hay citas ` +
                `cerradas despues de esa emision.`;
        }
        return;
    }

    btn.disabled = false;

    if (c.esComplementaria) {
        btn.textContent = "Emitir factura complementaria";
        aviso.hidden = false;
        aviso.textContent =
            `El periodo ya se cobro en la factura #${c.facturaExistenteId}. Estas ` +
            `${c.citas.length} ${c.citas.length === 1 ? "cita se cerro" : "citas se cerraron"} ` +
            `despues de esa emision, asi que se cobran aparte. La cuota de estancia no se vuelve a cobrar.`;
    } else {
        btn.textContent = "Emitir factura";
        aviso.hidden = true;
    }
}

function cerrarCalculo() {
    document.getElementById("panel-calculo").hidden = true;
    calculoActual = null;
}

async function emitir() {
    if (!calculoActual) return;

    const total = moneda(calculoActual.totalNeto);
    const tipo = calculoActual.esComplementaria ? "la factura complementaria" : "la factura";

    const nota = calculoActual.esComplementaria
        ? `\n\nNo incluye la cuota de estancia: esa ya se cobró en la factura ` +
          `#${calculoActual.facturaExistenteId}.`
        : "";

    if (!confirm(
        `Emitir ${tipo} de ${calculoActual.residenteNombre} por ${total}?\n\n` +
        `Las ${calculoActual.citas.length} citas incluidas quedarán liquidadas y ` +
        `no podrán volver a facturarse.${nota}`)) return;

    try {
        await api.crear("facturacion/facturas", {
            residenteId: calculoActual.residenteId,
            anio: calculoActual.anio,
            mes: calculoActual.mes
        });
        mostrarAviso(
            calculoActual.esComplementaria
                ? `Factura complementaria emitida por ${total}. Los cargos que estaban sin cobrar ya quedaron liquidados.`
                : `Factura emitida por ${total}.`);
        cerrarCalculo();
        await cargarFacturas();
    } catch (error) {
        mostrarAviso(error.message, "error");
    }
}

// ---------------- Facturas ----------------

async function cargarFacturas() {
    const residenteId = Number(selResidente.value);
    const cuerpo = document.getElementById("cuerpo-facturas");

    if (!residenteId) {
        cuerpo.innerHTML = `<tr><td colspan="9" class="vacio">Selecciona un residente.</td></tr>`;
        return;
    }

    cuerpo.innerHTML = `<tr><td colspan="9" class="vacio">Cargando…</td></tr>`;

    try {
        const estado = await api.listar(`facturacion/estado-cuenta/${residenteId}`);
        facturas = estado.facturas;

        document.getElementById("conteo").textContent =
            estado.saldoPendiente > 0
                ? `Saldo pendiente: ${moneda(estado.saldoPendiente)}`
                : "Sin saldo pendiente";

        pintarFacturas();
    } catch (error) {
        cuerpo.innerHTML = `<tr><td colspan="9" class="vacio">${limpiar(error.message)}</td></tr>`;
    }
}

function pintarFacturas() {
    const cuerpo = document.getElementById("cuerpo-facturas");

    document.getElementById("conteo-facturas").textContent =
        `${facturas.length} ${facturas.length === 1 ? "factura" : "facturas"}`;

    if (facturas.length === 0) {
        cuerpo.innerHTML = `<tr><td colspan="9" class="vacio">
            Este residente no tiene facturas emitidas.
        </td></tr>`;
        return;
    }

    // Cual es la primera factura de cada periodo: las demas del mismo
    // mes son complementarias. Lo deduzco del Id mas bajo y no de que
    // la cuota venga en cero, porque un residente sin cuota tendria su
    // primera factura tambien en cero y saldria mal etiquetada.
    const primeraDelPeriodo = new Set();
    const vistos = new Map();
    for (const f of facturas) {
        const clave = f.periodo.slice(0, 7);          // aaaa-mm
        if (!vistos.has(clave) || f.id < vistos.get(clave)) vistos.set(clave, f.id);
    }
    vistos.forEach(id => primeraDelPeriodo.add(id));

    cuerpo.innerHTML = pagFacturas.recortar(facturas).map(f => `
        <tr>
            <td>
                ${periodo(f.periodo)}
                ${primeraDelPeriodo.has(f.id) ? "" : `<span class="antiguedad">· Complementaria</span>`}
                <div class="secundario">${f.citasIncluidas} ${f.citasIncluidas === 1 ? "cita" : "citas"}</div>
            </td>
            <td class="num">${moneda(f.totalServicios)}</td>
            <td class="num descuento">− ${moneda(f.descuentoAplicado)}</td>
            <td class="num">${moneda(f.cuotaMensual)}</td>
            <td class="num"><strong>${moneda(f.totalNeto)}</strong></td>
            <td class="num">${moneda(f.totalPagado)}</td>
            <td class="num">${f.saldo > 0 ? `<strong>${moneda(f.saldo)}</strong>` : moneda(0)}</td>
            <td><span class="estado ${claseEstadoFactura(f.estado)}">${limpiar(f.estado)}</span></td>
            <td class="der">
                <button class="btn-fila" onclick="abrirPago(${f.id})">
                    ${f.saldo > 0 ? "Registrar abono" : "Ver abonos"}
                </button>
            </td>
        </tr>
    `).join("");
}

function claseEstadoFactura(estado) {
    if (estado === "Liquidada")       return "e-completo";
    if (estado === "Saldo pendiente") return "e-pendiente";
    return "e-proceso";
}

// El periodo es un primero de mes: muestro mes y anio, no la fecha.
function periodo(iso) {
    const d = new Date(iso);
    return d.toLocaleDateString("es-GT", { month: "long", year: "numeric" });
}

// ---------------- Abonos ----------------

async function abrirPago(facturaId) {
    facturaSeleccionada = facturas.find(f => f.id === facturaId);
    if (!facturaSeleccionada) return;

    const f = facturaSeleccionada;
    document.getElementById("panel-pago").hidden = false;
    document.getElementById("pago-factura").textContent = `#${f.id} · ${periodo(f.periodo)}`;

    document.getElementById("detalle-factura").innerHTML = `
        <div class="ficha">
            <div class="dato"><div class="rotulo">Total de la factura</div>
                <div class="valor">${moneda(f.totalNeto)}</div></div>
            <div class="dato"><div class="rotulo">Abonado</div>
                <div class="valor">${moneda(f.totalPagado)}</div></div>
            <div class="dato"><div class="rotulo">Saldo pendiente</div>
                <div class="valor">${moneda(f.saldo)}</div></div>
        </div>`;

    // El formulario solo tiene sentido si queda algo por cobrar.
    formPago.hidden = f.saldo <= 0;
    if (f.saldo > 0) {
        formPago.montoRecibido.value = "";
        formPago.montoRecibido.max = f.saldo;
        document.getElementById("ayuda-saldo").textContent =
            `Máximo ${moneda(f.saldo)}. Un abono menor deja la factura con saldo pendiente.`;
    }

    await cargarPagos(f.id);
    document.getElementById("panel-pago").scrollIntoView({ behavior: "smooth", block: "nearest" });
}

function cerrarPago() {
    document.getElementById("panel-pago").hidden = true;
    facturaSeleccionada = null;
}

async function cargarPagos(facturaId) {
    const cuerpo = document.getElementById("cuerpo-pagos");

    try {
        const pagos = await api.listar(`facturacion/facturas/${facturaId}/pagos`);

        cuerpo.innerHTML = pagos.length === 0
            ? `<tr><td colspan="4" class="vacio">Sin abonos registrados.</td></tr>`
            : pagos.map(p => `
                <tr>
                    <td>${fechaHora(p.fechaPago)}</td>
                    <td>${limpiar(p.metodoPago)}</td>
                    <td>${limpiar(p.registradoPorNombre) || "—"}</td>
                    <td class="num">${moneda(p.montoRecibido)}</td>
                </tr>`).join("");
    } catch (error) {
        cuerpo.innerHTML = `<tr><td colspan="4" class="vacio">${limpiar(error.message)}</td></tr>`;
    }
}

async function registrarPago(evento) {
    evento.preventDefault();
    if (!facturaSeleccionada) return;

    const datos = {
        montoRecibido: Number(formPago.montoRecibido.value),
        metodoPago: formPago.metodoPago.value,
        // Administradora de prueba. Con autenticacion saldra del token.
        registradoPor: 1
    };

    try {
        const actualizada = await api.crear(
            `facturacion/facturas/${facturaSeleccionada.id}/pagos`, datos);

        mostrarAviso(actualizada.saldo > 0
            ? `Abono registrado. Queda un saldo de ${moneda(actualizada.saldo)}.`
            : "Abono registrado. La factura queda liquidada.");

        await cargarFacturas();
        facturaSeleccionada = facturas.find(f => f.id === actualizada.id);
        if (facturaSeleccionada) await abrirPago(facturaSeleccionada.id);
    } catch (error) {
        mostrarAviso(error.message, "error");
    }
}
