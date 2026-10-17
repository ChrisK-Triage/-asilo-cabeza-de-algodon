namespace Asilo.Api.Models;

/// <summary>
/// De donde viene una donacion. Los cuatro valores son los que acepta
/// CK_Donaciones_Tipo, y el enunciado los nombra explicitamente:
/// empresas, gobierno y personas particulares.
/// </summary>
public enum TipoDonante
{
    Persona = 0,
    EmpresaNacional = 1,
    EmpresaInternacional = 2,
    Gobierno = 3
}

public class Donacion
{
    public int Id { get; set; }
    public string NombreDonante { get; set; } = string.Empty;
    public TipoDonante Tipo { get; set; }
    public decimal Monto { get; set; }
    public DateTime Fecha { get; set; }
    public string? Descripcion { get; set; }
}

public class GastoOperativo
{
    public int Id { get; set; }
    public string Concepto { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public DateTime Fecha { get; set; }
}

/// <summary>
/// Las categorias que acepta CK_Gastos_Categoria.
///
/// Las tengo aqui y no escritas en el formulario por lo mismo que los
/// roles: la pantalla las pide a la API, asi que el desplegable y la
/// restriccion de la base no se pueden desincronizar.
///
/// PagoFundacion es la que el enunciado menciona aparte, y es la que
/// alimenta su propio reporte: lo que el asilo le debe a la fundacion
/// por los servicios que presto.
/// </summary>
public static class CategoriasGasto
{
    public const string ServiciosBasicos = "ServiciosBasicos";
    public const string Alimentacion = "Alimentacion";
    public const string Mantenimiento = "Mantenimiento";
    public const string PagoFundacion = "PagoFundacion";
    public const string Personal = "Personal";
    public const string Otros = "Otros";

    public static readonly string[] Todas =
    {
        ServiciosBasicos, Alimentacion, Mantenimiento, PagoFundacion, Personal, Otros
    };

    public static string Etiqueta(string categoria) => categoria switch
    {
        ServiciosBasicos => "Servicios básicos",
        Alimentacion => "Alimentación",
        Mantenimiento => "Mantenimiento",
        PagoFundacion => "Pago a la fundación",
        Personal => "Personal",
        Otros => "Otros",
        _ => categoria
    };

    public static bool EsValida(string categoria) => Todas.Contains(categoria);
}

public static class TiposDonante
{
    public static string Etiqueta(TipoDonante tipo) => tipo switch
    {
        TipoDonante.Persona => "Persona particular",
        TipoDonante.EmpresaNacional => "Empresa nacional",
        TipoDonante.EmpresaInternacional => "Empresa internacional",
        TipoDonante.Gobierno => "Gobierno",
        _ => tipo.ToString()
    };
}
