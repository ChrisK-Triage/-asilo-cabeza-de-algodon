const selTipo = document.getElementById("tipo");
const selResidente = document.getElementById("residenteId");
const salida = document.getElementById("salida");
const btnImprimir = document.getElementById("btn-imprimir");

const PIDEN_RESIDENTE = {
    "historial-clinico": "obligatorio",
    "examenes-insumos": "opcional"
};

document.addEventListener("DOMContentLoaded", iniciar);
selTipo.addEventListener("change", ajustarFiltros);
document.getElementById("btn-generar").addEventListener("click", generar);
btnImprimir.addEventListener("click", () => window.print());

async function iniciar() {
    await cargarResidentes();
    ajustarFiltros();
}

async function cargarResidentes() {
    try {
        const residentes = await api.listar("residentes");

        selResidente.innerHTML =
            `<option value="">Todos los residentes</option>` +
            residentes.map(r =>
                `<option value="${r.id}">${limpiar(r.nombres)} ${limpiar(r.apellidos)}</option>`
            ).join("");
    } catch (error) {
        selResidente.innerHTML = `<option value="">No se pudieron cargar</option>`;
        mostrarAviso(error.message, "error");
    }
}

function ajustarFiltros() {
    const modo = PIDEN_RESIDENTE[selTipo.value];

    document.getElementById("campo-residente").hidden = !modo;
    document.getElementById("ayuda-residente").textContent =
        modo === "obligatorio" ? "Obligatorio para este reporte" : "Vacío = todos";

    salida.innerHTML = "";
    btnImprimir.disabled = true;
}

function rango() {
    const desde = document.getElementById("desde").value;
    const hasta = document.getElementById("hasta").value;
    const partes = [];
    if (desde) partes.push(`desde=${desde}`);
    if (hasta) partes.push(`hasta=${hasta}`);
    return partes;
}

async function generar() {
    const tipo = selTipo.value;
    const modo = PIDEN_RESIDENTE[tipo];
    const residenteId = selResidente.value;

    if (modo === "obligatorio" && !residenteId) {
        mostrarAviso("Este reporte necesita que elijas un residente.", "error");
        return;
    }

    const partes = rango();
    let ruta = `reportes/${tipo}`;

    if (tipo === "historial-clinico") {
        ruta += `/${residenteId}`;
    } else if (tipo === "examenes-insumos" && residenteId) {
        partes.unshift(`residenteId=${residenteId}`);
    }

    if (partes.length) ruta += `?${partes.join("&")}`;

    salida.innerHTML = `<div class="panel"><div class="panel-cuerpo vacio">Generando…</div></div>`;
    btnImprimir.disabled = true;

    try {
        const datos = await api.listar(ruta);

        salida.innerHTML = {
            "historial-clinico": pintarHistorial,
            "costos-por-cita": pintarCostos,
            "ingresos-egresos": pintarIngresosEgresos,
            "pagos-fundacion": pintarPagosFundacion,
            "examenes-insumos": pintarInsumos
        }[tipo](datos);

        btnImprimir.disabled = false;
    } catch (error) {
        salida.innerHTML = `<div class="panel"><div class="panel-cuerpo vacio">${limpiar(error.message)}</div></div>`;
    }
}

function encabezado(e) {
    const periodo = e.desde || e.hasta
        ? `${e.desde ? fecha(e.desde) : "inicio"} — ${e.hasta ? fecha(e.hasta) : "hoy"}`
        : "Todo el historial";

    return `
        <div class="cabecera-reporte">
            <div>
                <h2>${limpiar(e.titulo)}</h2>
                <div class="secundario">Asilo de Ancianos "Cabeza de Algodón"</div>
            </div>
            <div class="der secundario">
                <div><strong>Período:</strong> ${limpiar(periodo)}</div>
                ${e.residente ? `<div><strong>Residente:</strong> ${limpiar(e.residente)}</div>` : ""}
                <div>Generado ${fechaHora(e.generado)}</div>
            </div>
        </div>`;
}

function totales(filas) {
    return `<div class="totales-reporte">${filas.map(([r, v, fuerte]) => `
        <div class="renglon ${fuerte ? "total" : ""}"><span>${limpiar(r)}</span>
        <span class="num">${v}</span></div>`).join("")}</div>`;
}

// ---------------- 1. Historial clínico ----------------

function pintarHistorial(d) {
    const consultas = d.consultas.length
        ? d.consultas.map(c => `
            <tr>
                <td>
                    ${fecha(c.fecha)}
                    <div class="secundario">Cita #${c.citaId}</div>
                </td>
                <td>
                    ${limpiar(c.especialidad)}
                    <div class="secundario">${limpiar(c.especialista)}</div>
                </td>
                <td>
                    <div class="secundario">Motivo:</div> ${limpiar(c.sintomatologia)}
                    ${c.diagnostico
                        ? `<div style="margin-top:6px"><div class="secundario">Diagnóstico:</div> ${limpiar(c.diagnostico)}</div>`
                        : `<div class="secundario" style="margin-top:6px">Sin diagnóstico registrado</div>`}
                </td>
                <td>
                    ${c.examenes ? limpiar(c.examenes) : "<span class='secundario'>—</span>"}
                </td>
                <td>
                    ${c.medicamentos ? limpiar(c.medicamentos) : "<span class='secundario'>—</span>"}
                </td>
            </tr>`).join("")
        : `<tr><td colspan="5" class="vacio">Sin consultas en el período.</td></tr>`;

    return `<div class="panel reporte">
        <div class="panel-cuerpo">
            ${encabezado(d.encabezado)}
            <div class="ficha" style="margin-top:16px">
                <div class="dato"><div class="rotulo">Residente</div>
                    <div class="valor">${limpiar(d.residente)}</div></div>
                <div class="dato"><div class="rotulo">DPI</div>
                    <div class="valor">${limpiar(d.dpi) || "—"}</div></div>
                <div class="dato"><div class="rotulo">Edad</div>
                    <div class="valor">${d.edad} años</div></div>
                <div class="dato"><div class="rotulo">Ingreso al asilo</div>
                    <div class="valor">${fecha(d.fechaIngreso)}</div></div>
            </div>
            <div style="margin-top:14px">
                <div class="rotulo secundario">Psicopatologías</div>
                <div>${limpiar(d.psicopatologias) || "Sin diagnóstico previo"}</div>
                <div class="rotulo secundario" style="margin-top:10px">Medicamentos de cajón</div>
                <div>${limpiar(d.medicamentosCajon) || "Ninguno"}</div>
            </div>
        </div>
        <table>
            <thead><tr><th>Fecha</th><th>Especialidad</th><th>Motivo y diagnóstico</th>
                       <th>Exámenes</th><th>Medicamentos</th></tr></thead>
            <tbody>${consultas}</tbody>
        </table>
    </div>`;
}

// ---------------- 2. Costos por cita ----------------

function pintarCostos(d) {
    const filas = d.citas.length
        ? d.citas.map(c => `
            <tr>
                <td>${fecha(c.fecha)}<div class="secundario">Cita #${c.citaId}</div></td>
                <td>${limpiar(c.residente)}<div class="secundario">${limpiar(c.especialidad)}</div></td>
                <td class="num">${moneda(c.costoConsulta)}</td>
                <td class="num">${moneda(c.costoLaboratorio)}</td>
                <td class="num">${moneda(c.costoFarmacia)}</td>
                <td class="num"><strong>${moneda(c.total)}</strong></td>
                <td><span class="estado ${c.liquidada ? "e-completo" : "e-pendiente"}">
                    ${c.liquidada ? "Facturada" : "Sin facturar"}</span></td>
            </tr>`).join("")
        : `<tr><td colspan="7" class="vacio">Sin citas en el período.</td></tr>`;

    return `<div class="panel reporte">
        <div class="panel-cuerpo">${encabezado(d.encabezado)}</div>
        <table>
            <thead><tr><th>Fecha</th><th>Residente</th><th class="num">Consulta</th>
                       <th class="num">Laboratorio</th><th class="num">Farmacia</th>
                       <th class="num">Total</th><th>Estado</th></tr></thead>
            <tbody>${filas}</tbody>
        </table>
        <div class="panel-cuerpo">${totales([
            ["Consultas", moneda(d.totalConsultas)],
            ["Laboratorio", moneda(d.totalLaboratorio)],
            ["Farmacia", moneda(d.totalFarmacia)],
            ["Total del período", moneda(d.totalGeneral), true]
        ])}</div>
    </div>`;
}

// ---------------- 3. Ingresos y egresos ----------------

function pintarIngresosEgresos(d) {
    const filas = d.movimientos.length
        ? d.movimientos.map(m => `
            <tr>
                <td>${fecha(m.fecha)}</td>
                <td>${limpiar(m.concepto)}</td>
                <td>${limpiar(m.detalle)}</td>
                <td class="num">${m.ingreso > 0 ? moneda(m.ingreso) : "—"}</td>
                <td class="num">${m.egreso > 0 ? moneda(m.egreso) : "—"}</td>
            </tr>`).join("")
        : `<tr><td colspan="5" class="vacio">Sin movimientos en el período.</td></tr>`;

    const signo = d.resultado >= 0 ? "Superávit" : "Déficit";

    return `<div class="panel reporte">
        <div class="panel-cuerpo">${encabezado(d.encabezado)}</div>
        <table>
            <thead><tr><th>Fecha</th><th>Concepto</th><th>Detalle</th>
                       <th class="num">Ingreso</th><th class="num">Egreso</th></tr></thead>
            <tbody>${filas}</tbody>
        </table>
        <div class="panel-cuerpo">${totales([
            ["Cuotas y servicios cobrados", moneda(d.totalCuotasYServicios)],
            ["Donaciones recibidas", moneda(d.totalDonaciones)],
            ["Total de ingresos", moneda(d.totalIngresos)],
            ["Total de egresos", "− " + moneda(d.totalEgresos)],
            [`${signo} del período`, moneda(d.resultado), true]
        ])}</div>
    </div>`;
}

// ---------------- 4. Pagos a la fundación ----------------

function pintarPagosFundacion(d) {
    const filas = d.pagos.length
        ? d.pagos.map(p => `
            <tr>
                <td>${fecha(p.fecha)}</td>
                <td>${limpiar(p.concepto)}</td>
                <td class="num"><strong>${moneda(p.monto)}</strong></td>
            </tr>`).join("")
        : `<tr><td colspan="3" class="vacio">
             No hay pagos registrados a la fundación en el período.
             Se registran como gasto operativo con categoría "Pago a la fundación".
           </td></tr>`;

    return `<div class="panel reporte">
        <div class="panel-cuerpo">${encabezado(d.encabezado)}</div>
        <table>
            <thead><tr><th>Fecha</th><th>Concepto</th><th class="num">Monto</th></tr></thead>
            <tbody>${filas}</tbody>
        </table>
        <div class="panel-cuerpo">
            ${totales([
                ["Servicios facturados por la fundación", moneda(d.serviciosFacturados)],
                ["Descuento del convenio", "− " + moneda(d.descuentoConvenio)],
                ["Adeudado a la fundación", moneda(d.adeudadoALaFundacion)],
                ["Pagado en el período", "− " + moneda(d.totalPagado)],
                ["Saldo con la fundación", moneda(d.saldo), true]
            ])}
            <p class="ayuda" style="margin-top:12px">
                Lo adeudado sale de las facturas emitidas, no de los gastos: es la suma de
                consulta, laboratorio y farmacia ya descontado el convenio. La cuota mensual
                de estancia no entra, porque es ingreso del asilo y no un servicio de la fundación.
            </p>
        </div>
    </div>`;
}

// ---------------- 5. Exámenes e insumos ----------------

function pintarInsumos(d) {
    const filas = d.renglones.length
        ? d.renglones.map(x => `
            <tr>
                <td>${fecha(x.fecha)}<div class="secundario">Cita #${x.citaId}</div></td>
                <td>${limpiar(x.residente)}</td>
                <td>${limpiar(x.clase)}</td>
                <td>${limpiar(x.detalle)}</td>
                <td class="num">${x.cantidad}</td>
                <td><span class="estado ${
                    x.situacion === "Completado" || x.situacion === "Entregado"
                        ? "e-completo" : "e-pendiente"}">${limpiar(x.situacion)}</span></td>
                <td class="num">${x.costo > 0 ? moneda(x.costo) : "—"}</td>
            </tr>`).join("")
        : `<tr><td colspan="7" class="vacio">Sin exámenes ni medicamentos en el período.</td></tr>`;

    return `<div class="panel reporte">
        <div class="panel-cuerpo">${encabezado(d.encabezado)}</div>
        <table>
            <thead><tr><th>Fecha</th><th>Residente</th><th>Clase</th><th>Detalle</th>
                       <th class="num">Cantidad</th><th>Situación</th><th class="num">Costo</th></tr></thead>
            <tbody>${filas}</tbody>
        </table>
        <div class="panel-cuerpo">${totales([
            [`Exámenes solicitados (${d.totalExamenes})`, moneda(d.costoLaboratorio)],
            [`Medicamentos recetados (${d.totalMedicamentos})`, moneda(d.costoFarmacia)],
            ["Total", moneda(d.costoTotal), true]
        ])}</div>
    </div>`;
}
