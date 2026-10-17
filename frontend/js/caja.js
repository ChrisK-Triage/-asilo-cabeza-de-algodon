let donaciones = [];
let gastos = [];

const formDonacion = document.getElementById("form-donacion");
const formGasto = document.getElementById("form-gasto");

const pagDonaciones = new Paginador("paginador-donaciones", () => pintarDonaciones());
const pagGastos = new Paginador("paginador-gastos", () => pintarGastos());

document.addEventListener("DOMContentLoaded", iniciar);
formDonacion.addEventListener("submit", guardarDonacion);
formGasto.addEventListener("submit", guardarGasto);
document.getElementById("desde").addEventListener("change", cargar);
document.getElementById("hasta").addEventListener("change", cargar);

const DIAS_HACIA_ATRAS = 30;

async function iniciar() {
    acotarFechas();
    await cargarCategorias();
    await cargar();
}

function acotarFechas() {
    const hoy = new Date();
    const limite = new Date();
    limite.setDate(hoy.getDate() - DIAS_HACIA_ATRAS);

    const aTexto = d => d.toISOString().split("T")[0];

    for (const campo of [formDonacion.fecha, formGasto.fecha]) {
        campo.value = aTexto(hoy);
        campo.max = aTexto(hoy);
        campo.min = aTexto(limite);
    }

    document.getElementById("limite-fechas").textContent =
        `Se admiten movimientos desde el ${fecha(limite)} hasta hoy`;
}

async function cargarCategorias() {
    try {
        const categorias = await api.listar("caja/categorias");

        formGasto.categoria.innerHTML =
            `<option value="">Selecciona una categoría</option>` +
            categorias.map(c =>
                `<option value="${limpiar(c.valor)}">${limpiar(c.etiqueta)}</option>`).join("");
    } catch (error) {
        formGasto.categoria.innerHTML = `<option value="">No se pudieron cargar</option>`;
        mostrarAviso(error.message, "error");
    }
}

function rango() {
    const desde = document.getElementById("desde").value;
    const hasta = document.getElementById("hasta").value;
    const partes = [];
    if (desde) partes.push(`desde=${desde}`);
    if (hasta) partes.push(`hasta=${hasta}`);
    return partes.length ? `?${partes.join("&")}` : "";
}

async function cargar() {
    const filtro = rango();

    try {
        [donaciones, gastos] = await Promise.all([
            api.listar(`caja/donaciones${filtro}`),
            api.listar(`caja/gastos${filtro}`)
        ]);

        const totalDonaciones = donaciones.reduce((s, d) => s + d.monto, 0);
        const totalGastos = gastos.reduce((s, g) => s + g.monto, 0);

        document.getElementById("r-donaciones").textContent = moneda(totalDonaciones);
        document.getElementById("r-gastos").textContent = moneda(totalGastos);
        document.getElementById("conteo").textContent =
            `${donaciones.length} donaciones · ${gastos.length} gastos`;

        pintarDonaciones();
        pintarGastos();
    } catch (error) {
        mostrarAviso(error.message, "error");
    }
}

function pintarDonaciones() {
    const cuerpo = document.getElementById("cuerpo-donaciones");

    if (donaciones.length === 0) {
        cuerpo.innerHTML = `<tr><td colspan="6" class="vacio">
            No hay donaciones registradas en el período.
        </td></tr>`;
        return;
    }

    cuerpo.innerHTML = pagDonaciones.recortar(donaciones).map(d => `
        <tr>
            <td>${limpiar(d.nombreDonante)}</td>
            <td>${limpiar(d.tipoEtiqueta)}</td>
            <td>${limpiar(d.descripcion) || "<span class='secundario'>—</span>"}</td>
            <td>${fecha(d.fecha)}</td>
            <td class="num"><strong>${moneda(d.monto)}</strong></td>
            <td class="der">
                <button class="btn-fila peligro" onclick="eliminarDonacion(${d.id})">Quitar</button>
            </td>
        </tr>
    `).join("");
}

function pintarGastos() {
    const cuerpo = document.getElementById("cuerpo-gastos");

    if (gastos.length === 0) {
        cuerpo.innerHTML = `<tr><td colspan="5" class="vacio">
            No hay gastos registrados en el período.
        </td></tr>`;
        return;
    }

    cuerpo.innerHTML = pagGastos.recortar(gastos).map(g => `
        <tr>
            <td>${limpiar(g.concepto)}</td>
            <td>${limpiar(g.categoriaEtiqueta)}</td>
            <td>${fecha(g.fecha)}</td>
            <td class="num"><strong>${moneda(g.monto)}</strong></td>
            <td class="der">
                <button class="btn-fila peligro" onclick="eliminarGasto(${g.id})">Quitar</button>
            </td>
        </tr>
    `).join("");
}

async function guardarDonacion(evento) {
    evento.preventDefault();

    try {
        await api.crear("caja/donaciones", {
            nombreDonante: formDonacion.nombreDonante.value.trim(),
            tipo: formDonacion.tipo.value,
            monto: Number(formDonacion.monto.value),
            fecha: formDonacion.fecha.value,
            descripcion: formDonacion.descripcion.value.trim() || null
        });

        mostrarAviso("Donación registrada.");
        formDonacion.reset();
        formDonacion.fecha.value = new Date().toISOString().split("T")[0];
        cargar();
    } catch (error) {
        mostrarAviso(error.message, "error");
    }
}

async function guardarGasto(evento) {
    evento.preventDefault();

    try {
        await api.crear("caja/gastos", {
            concepto: formGasto.concepto.value.trim(),
            categoria: formGasto.categoria.value,
            monto: Number(formGasto.monto.value),
            fecha: formGasto.fecha.value
        });

        mostrarAviso("Gasto registrado.");
        formGasto.reset();
        formGasto.fecha.value = new Date().toISOString().split("T")[0];
        cargar();
    } catch (error) {
        mostrarAviso(error.message, "error");
    }
}

async function eliminarDonacion(id) {
    const d = donaciones.find(x => x.id === id);
    if (!confirm(`Quitar la donación de ${d.nombreDonante} por ${moneda(d.monto)}?`)) return;

    try {
        await api.eliminar("caja/donaciones", id);
        mostrarAviso("Donación eliminada.");
        cargar();
    } catch (error) {
        mostrarAviso(error.message, "error");
    }
}

async function eliminarGasto(id) {
    const g = gastos.find(x => x.id === id);
    if (!confirm(`Quitar el gasto "${g.concepto}" por ${moneda(g.monto)}?`)) return;

    try {
        await api.eliminar("caja/gastos", id);
        mostrarAviso("Gasto eliminado.");
        cargar();
    } catch (error) {
        mostrarAviso(error.message, "error");
    }
}
