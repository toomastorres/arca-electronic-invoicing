namespace FacturacionArca.Domain.Errores;

public static class ArcaErrorCatalog
{
    private static readonly Dictionary<int, string> Mapa = new()
    {
        // ── Autenticación y conexión ──
        [600] = "El servicio WSAA no está disponible en este momento. Reintente en unos minutos.",
        [1005] = "Token no válido o expirado. El sistema renovará el ticket de acceso automáticamente.",
        [1101] = "Punto de venta o tipo de comprobante no autorizado en el padrón de servicios web.",
        [600100] = "El servicio web está temporalmente fuera de línea. Reintente en unos minutos.",

        // ── Datos de cabecera ──
        [10000] = "Falta enviar el campo CUIT del solicitante en la cabecera.",
        [10013] = "Punto de venta no autorizado para emitir comprobantes electrónicos.",
        [10015] = "Fecha de comprobante fuera de rango admitido por AFIP (Productos: ±5 días / Servicios: ±10 días).",
        [10016] = "El número de comprobante no es correlativo con el último autorizado para ese punto de venta.",
        [10017] = "La fecha del comprobante es posterior a la fecha actual. Verifique el reloj del sistema.",
        [10018] = "Tipo de documento del receptor no se corresponde con el tipo de comprobante.",
        [10019] = "El concepto requiere fechas de servicio (Desde / Hasta / Vto. Pago).",
        [10020] = "Tipo de cambio inválido para la moneda y fecha indicadas. Verifique cotización oficial ARCA.",

        // ── Validaciones aritméticas ──
        [10048] = "Existe una diferencia de decimales entre el total y la suma de subtotales (neto + no gravado + exento + IVA + tributos).",
        [10061] = "La suma de las bases imponibles de IVA no coincide con el importe neto.",
        [10065] = "Existe una diferencia entre la suma de los importes de IVA y el total de IVA declarado.",
        [10100] = "Errores de aritmética en los subtotales del comprobante.",

        // ── Receptor y condición IVA ──
        [10063] = "Para Factura A el receptor debe ser Responsable Inscripto y el documento debe ser CUIT.",
        [10064] = "Para Factura B el receptor no puede ser Responsable Inscripto.",
        [10070] = "El CUIT del receptor no figura en el padrón de AFIP o tiene una condición frente a IVA distinta a la declarada.",
        [10071] = "El CUIT del receptor no está habilitado para recibir Factura de Crédito Electrónica MiPyME.",
        [10076] = "La fecha de servicio está fuera del rango permitido para el tipo de concepto.",
        [10096] = "La cotización de la moneda extranjera no coincide con la cotización oficial vigente en ARCA.",

        // ── Comprobantes asociados y duplicados ──
        [10154] = "El comprobante ya existe en AFIP. Use FECompConsultar para obtener su CAE.",
        [10180] = "El comprobante asociado (referencia a NC/ND) no existe en AFIP.",
        [602] = "El comprobante consultado no existe en AFIP para ese punto de venta y tipo.",

        // ── FCE MiPyME específicos ──
        [10162] = "Factura de Crédito Electrónica: falta la estructura Opcionales obligatoria (CBU/SCA/ADC). Complete los datos FCE.",
        [10163] = "Factura de Crédito Electrónica: el CBU informado no es válido (debe tener 22 dígitos numéricos).",
        [10164] = "Factura de Crédito Electrónica: el receptor no está inscripto en el Registro FCE MiPyME.",
        [10165] = "Factura de Crédito Electrónica: el emisor debe tener registrado el Domicilio Fiscal Electrónico (DFE).",
        [10166] = "Factura de Crédito Electrónica: el emisor no está categorizado como MiPyME vigente.",
    };

    public static string MensajeAmigable(int codigo, string mensajeOriginal) =>
        Mapa.TryGetValue(codigo, out var amigable)
            ? amigable
            : $"AFIP devolvió el código {codigo}: {mensajeOriginal}";
}
