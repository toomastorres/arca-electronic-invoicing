# Manual Técnico — Facturación Electrónica ARCA

**Versión 1.1** | Guía para administradores y equipo de desarrollo  
**Última actualización:** 2026-04-30

---

## 1. Arquitectura del Sistema

### 1.1 — Capas

```
┌──────────────────────────────────────────────┐
│          FacturacionArca.Wpf (UI)            │
│  MVVM: Views (XAML) ↔ ViewModels (C#)        │
│  DI Bootstrap: App.xaml.cs                   │
├──────────────────────────────────────────────┤
│       FacturacionArca.Application            │
│  UseCases: EmitirComprobante, Mapeo, etc.    │
│  Validacion: Aritmetica + Reglas Negocio     │
│  Abstractions: Interfaces (IArcaWsfev1, etc.)│
├──────────────────────────────────────────────┤
│      FacturacionArca.Infrastructure          │
│  Arca: Wsaa, Wsfev1, Wsfecred (SOAP)        │
│  Persistence: EF Core + SQLite               │
│  Pdf: QuestPDF + QR                          │
│  FileWatching, Printing, XmlParser           │
├──────────────────────────────────────────────┤
│        FacturacionArca.Domain                │
│  Entidades: Comprobante, Proforma, Config    │
│  Enums: TipoComprobante, CondicionIva        │
│  Errores: ArcaErrorCatalog                   │
└──────────────────────────────────────────────┘
```

### 1.2 — Flujo de datos

```
XML ERP (Nápoles) 
    → FileWatcher detecta archivo en carpeta IN
    → ProformaXmlParser → ProformaNapoles (entidad)
    → UI: ListaProformasView (grilla)
    → Usuario abre: DetalleProformaView
    → MapearProformaAComprobante → Comprobante
    → ValidadorReglasNegocio + ValidadorAritmeticoAfip
    → EmitirComprobante:
        → WSAA → TicketAcceso (cache 12h)
        → WSFEv1.FECompUltimoAutorizado → número siguiente
        → WSFEv1.FECAESolicitar → CAE (o error)
        → [contingencia si timeout]
    → ArchivoFiscal: XML respuesta → YYYY/MM/
    → QuestPdfRenderer → PDF con QR
    → SilentPdfPrinter → impresora
    → EscribirCallbackOut → XML confirmación a carpeta OUT
    → FileWatcher: mueve XML original a OUT
```

### 1.3 — Servicios ARCA utilizados

| Servicio | Endpoint | Uso |
|---|---|---|
| **WSAA** | `wsaa.afip.gob.ar` | Autenticación, obtener Token+Sign |
| **WSFEv1** | `servicios1.afip.gob.ar/wsfev1/service.asmx` | Emisión de comprobantes (FC, FCE, NC, ND) |
| **WSFECRED** | *(pendiente integración completa)* | Consulta estado FCE (aceptación/rechazo) |

---

## 2. FCE — Factura de Crédito Electrónica MiPyME

### 2.1 — Códigos de tipo de comprobante

| Código | Tipo | Enum | Método EsFce() |
|--------|------|------|----------------|
| 1 | Factura A (normal) | `FacturaA` | ❌ false |
| 201 | FCE A | `FacturaCreditoMipymeA` | ✅ true |
| 202 | ND FCE A | `NotaDebitoElectronicaMipymeA` | ✅ true |
| 203 | NC FCE A | `NotaCreditoElectronicaMipymeA` | ✅ true |
| 206 | FCE B | `FacturaCreditoMipymeB` | ✅ true |
| 211 | FCE C | `FacturaCreditoMipymeC` | ✅ true |

### 2.2 — Opcionales FCE en FECAESolicitar

```xml
<ar:Opcionales>
  <ar:Opcional>
    <ar:Id>27</ar:Id>            <!-- SCA="S" o ADC="N" -->
    <ar:Valor>S</ar:Valor>
  </ar:Opcional>
  <ar:Opcional>
    <ar:Id>2101</ar:Id>          <!-- CBU beneficiario (22 dígitos) -->
    <ar:Valor>0070999020000012345678</ar:Valor>
  </ar:Opcional>
  <ar:Opcional>
    <ar:Id>2102</ar:Id>          <!-- Alias bancario (opcional) -->
    <ar:Valor>MI.EMPRESA.FCE</ar:Valor>
  </ar:Opcional>
</ar:Opcionales>
```

### 2.3 — Override tipo comprobante (FC→FCE)

**Archivo:** `MapearProformaAComprobante.cs`

El sistema permite reclasificar un comprobante normal como FCE mediante `OpcionesMapeo.TipoComprobanteOverride`:

```csharp
// Si hay un override de tipo (ej: reclasificar FC→FCE), usar ese
if (opciones.TipoComprobanteOverride is { } overrideTipo)
    tipo = overrideTipo;
else
    tipo = (TipoComprobante)proforma.CodigoTipoComprobanteOrigen;
```

**Mapeo automático:**

```csharp
// TipoComprobanteExtensions.EquivalenteFce()
FacturaA (1)     → FacturaCreditoMipymeA (201)
NotaCreditoA (3) → NotaCreditoElectronicaMipymeA (203)
FacturaB (6)     → FacturaCreditoMipymeB (206)
// etc.
```

### 2.4 — Normativa vigente (abril 2026)

| Aspecto | Valor | Fuente |
|---|---|---|
| **Monto mínimo FCE** | $5.549.862 | Res. 1/2026 Sec. PyME |
| **Plazo aceptación** | 21 días corridos | Res. 219/2025 (hasta 31/10/2026) |
| **DFE obligatorio** | Sí, emisor y receptor | RG 4291 |
| **CBU obligatorio** | Sí, para facturas FCE | ARCA |

---

## 3. Validaciones implementadas

### 3.1 — Validaciones aritméticas (ValidadorAritmeticoAfip)

| Validación | Código ARCA | Tolerancia |
|---|---|---|
| `ImporteTotal ≠ suma subtotales` | 10048 | ±$0.01 o ±0.01% |
| `Σ BaseImponible ≠ ImporteNeto` | 10061 | ±$0.01 o ±0.01% |
| `Σ ImporteIVA ≠ ImporteIva` | 10065 | ±$0.01 o ±0.01% |
| `Σ Tributos ≠ ImporteTributos` | 10100 | ±$0.01 o ±0.01% |

### 3.2 — Validaciones de reglas de negocio (ValidadorReglasNegocio)

| Validación | Código ARCA |
|---|---|
| Factura A → receptor RI + CUIT | 10063 |
| Factura B → receptor NO RI | 10064 |
| Servicios → fechas obligatorias | 10019 |
| Documento receptor obligatorio | — |
| Punto de venta > 0 | — |
| Importe total > 0 (excepto NC) | — |
| Moneda reconocida | — |
| Cotización > 0 para ME | 10020 |
| **FCE factura → CBU obligatorio** | 10162 |
| **FCE factura → SCA/ADC obligatorio** | 10162 |
| **CBU formato 22 dígitos** | 10163 |

---

## 4. Catálogo de errores ARCA

El catálogo está en `ArcaErrorCatalog.cs` y contiene 29 códigos de error con mensajes amigables en español. Los errores se clasifican en:

- **Autenticación:** 600, 1005, 1101, 600100
- **Cabecera:** 10000, 10013, 10015, 10016, 10017, 10018, 10019, 10020
- **Aritméticos:** 10048, 10061, 10065, 10100
- **Receptor/IVA:** 10063, 10064, 10070, 10071, 10076, 10096
- **Asociados:** 10154, 10180, 602
- **FCE específicos:** 10162, 10163, 10164, 10165, 10166

Si ARCA devuelve un código no catalogado, el sistema muestra: `"AFIP devolvió el código {N}: {mensaje_original}"`.

---

## 5. Base de datos (SQLite)

### Ubicación
`%LocalAppData%\FacturacionArca\facturacion.db`

### Tablas principales

| Tabla | Propósito |
|---|---|
| Configuracion | CUIT, RazonSocial, rutas, certificado, modo |
| Proformas | XML importados, estado, vínculo a comprobante |
| Comprobantes | Datos fiscales + CAE + saldo P/S + FCE |
| ComprobanteItems | Detalle de ítems |
| SubtotalesIva | Agrupación IVA por alícuota |
| Tributos | IIBB, percepciones |
| TicketsAcceso | Cache WSAA (Token+Sign, expiración 12h) |
| ParametrosMaestros | Tipos cbte, doc, IVA, monedas (sync ARCA) |
| LogLlamadasArca | Request/Response XML, timestamps |
| PadronAgip | Alícuotas IIBB CABA por CUIT |

### Campos nuevos v1.1 (Comprobante)

| Campo | Tipo | Descripción |
|---|---|---|
| `TipoSaldo` | int | 0=Pesificado, 1=EnMonedaOriginal |
| `SaldoArsFijado` | decimal | Total × Cotización al momento del CAE |
| `SaldoMoneda` | string | "PES" o código moneda original |
| `EsSCA` | bool? | true=SCA, false=ADC, null=N/A |
| `CbuFce` | string? | CBU 22 dígitos (FCE) |
| `AliasFce` | string? | Alias bancario (FCE) |

---

## 6. Logging

### Configuración
```csharp
// App.xaml.cs
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
    .WriteTo.Debug()
    .CreateLogger();
```

### Ubicación
`%LocalAppData%\FacturacionArca\logs\facturacion-YYYYMMDD.log`

### Qué se loguea

| Evento | Nivel | Ejemplo |
|---|---|---|
| XML parseado | Information | "Proforma {Num} importada" |
| Validación fallida | Warning | "Validación local falló: {Resumen}" |
| Emisión exitosa | Information | "CAE {Cae} obtenido" |
| Error de red | Warning | "Error de red al solicitar CAE" |
| Rechazo ARCA | Error | "ARCA rechazó la emisión" |
| Contingencia | Warning | "AFIP ya tenía el CAE" |
| HTTP timing | Information | "WSFEv1.{Método} → HTTP {Status} en {Ms}ms" |

---

## 7. Procedimientos de mantenimiento

### 7.1 — Renovar certificado

1. Verificar fecha de expiración:
```powershell
openssl pkcs12 -in empresa.pfx -nodes -passin pass:CLAVE | openssl x509 -noout -dates
```

2. Si falta < 30 días:
   - Generar nuevo CSR (misma clave o nueva)
   - Enviar a ARCA portal
   - Crear nuevo .pfx
   - Actualizar en Configuración

### 7.2 — Backup

| Qué respaldar | Frecuencia | Ubicación |
|---|---|---|
| Archivo fiscal AFIP | Diario | Carpeta configurada |
| Base de datos | Diario | `%LocalAppData%\FacturacionArca\facturacion.db` |
| PDFs emitidos | Semanal | Carpeta configurada |
| Certificado .pfx | Una vez | Ubicación segura |

### 7.3 — Sincronizar maestros

Ejecutar semanalmente desde Configuración → "Sincronizar maestros AFIP" para actualizar:
- Tipos de comprobante
- Tipos de documento
- Alícuotas IVA
- Monedas y cotizaciones

### 7.4 — Actualizar padrón AGIP

El padrón AGIP (percepciones IIBB CABA) se debe importar mensualmente:
- Exportaciones → "Importar padrón AGIP"
- Archivo fuente: descarga desde portal AGIP

---

## 8. Contingencia de red

### Flujo automático

```
FECAESolicitar → Timeout/Error de red
    ↓
EsErrorDeRed() → true
    ↓
ResolverContingenciaRed.EjecutarAsync()
    ↓
FECompConsultar(PtoVta, Tipo, Nro)
    ↓
¿ARCA tiene el CAE?
    Sí → Recuperar CAE, marcar como Autorizada
    No → Marcar como "Pendiente reproceso"
```

### Errores de red detectados
- `SocketException`
- `TaskCanceledException`
- `TimeoutException`
- `HttpRequestException`
- `CommunicationException` (SOAP WCF)

---

## 9. Testing

### Ejecutar tests
```bash
dotnet test FacturacionArca.sln
```

### Cobertura actual: 34 tests

| Archivo | Tests | Qué valida |
|---|---|---|
| XmlParser_Tests | 7 | Parseo de 7 XMLs reales |
| ValidadorAritmetico_Tests | 5 | Validaciones aritméticas |
| Mapeo_Tests | 4 | Proforma → Comprobante |
| TraBuilder_Tests | 1 | Firma CMS/PKCS#7 |
| Wsfev1RequestBuilder_Tests | 3 | Snapshot XML FECAESolicitar |
| FceYSaldosTests | 5 | FCE mapeo, saldos P/S |
| NuevasFeatures_Tests | 9 | Callback, percepciones, etc. |

---

**Versión**: 1.1  
**Última actualización**: 2026-04-30
