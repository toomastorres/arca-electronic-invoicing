# Manual de Usuario — Facturación Electrónica ARCA

**Versión 1.1** | Motor de reemplazo de software de facturación electrónica para AFIP/ARCA  
**Empresa:** Responsable Inscripto + Convenio Multilateral CM05  
**Stack:** C# .NET 8 + WPF + SQLite  
**Última actualización:** 2026-04-30

---

## 1. Instalación y primer arranque

### Requisitos
- **Windows 10/11/Server 2019+**
- **.NET 8 Runtime** (o .NET 10, versiones superiores)
- **Certificado X.509 digital** (`.pfx`) emitido por AFIP/ARCA
- **Carpetas de red** accesibles (si está en entorno multiusuario)

### Pasos iniciales

1. **Descargar la aplicación**
   - EXE compilado está en: `src\FacturacionArca.Wpf\bin\Release\net8.0-windows\FacturacionArca.Wpf.exe`
   - Opcionalmente copiar a `C:\Programas\FacturacionArca\` o similar

2. **Crear acceso directo** (opcional pero recomendado)
   - Click derecho en el EXE → Crear acceso directo → mover al Escritorio

3. **Primer arranque**
   - Doble-click en `FacturacionArca.Wpf.exe`
   - La app crea automáticamente:
     - Base de datos SQLite: `%LocalAppData%\FacturacionArca\facturacion.db`
     - Carpetas de logs: `%LocalAppData%\FacturacionArca\logs\`
     - Configuración inicial con valores por defecto (o de variables de entorno)

---

## 2. Configuración inicial

Al primer arranque, la aplicación carga una configuración por defecto. **Tenés que completarla con tus datos reales.**

### Abrir Configuración

En la ventana principal, pestaña **"Configuración"**

### Datos de la Empresa
| Campo | Ejemplo | Notas |
|---|---|---|
| **CUIT** | `30712345678` | 11 dígitos, sin guiones |
| **Razón social** | `LOGISTICA NAVAL SA` | Sin acentos ni ñ |
| **Domicilio comercial** | `Av. Rivadavia 1500, CABA` | Aparecerá en el PDF |
| **Cond. frente IVA** | `Responsable Inscripto` | Ej: RI, MT, EX, CF |
| **Inicio actividades** | `2020-01-15` | Fecha de habilitación |
| **Convenio Multilateral** | ✓ Habilitado | Marca si usás CM05 (típicamente sí) |

### Certificado X.509

| Campo | Qué poner |
|---|---|
| **Ruta del .pfx** | Path completo, ej: `C:\certs\empresa.pfx` |
| **Contraseña** | La contraseña que protege el `.pfx` |

> **⚠️ Nota:** Si no tenés certificado aún, ver sección 8 "Obtener certificado digital"

### Carpetas

| Carpeta | Propósito | Ejemplo |
|---|---|---|
| **Proformas IN** (vigilada) | Entrada de XMLs del ERP Nápoles | `C:\AppFacturacion\InvoiceArgentina\Invoice\In` |
| **Proformas OUT** (procesadas) | XMLs ya emitidos (auto-movidos) | `C:\AppFacturacion\InvoiceArgentina\Invoice\Out` |
| **Archivo fiscal AFIP** (RG 1361) | Respuestas XML de ARCA (10 años) | `C:\AppFacturacion\InvoiceArgentina\Invoice\AFIP` |
| **Salida PDF** | PDFs de facturas emitidas | `C:\AppFacturacion\Facturas` |

### Impresión

| Campo | Descripción |
|---|---|
| **Impresora por defecto** | Seleccionar de la lista detectada |
| **Modo silencioso** | ✓ = imprime sin mostrar diálogo |

### Guardar y Sincronizar

1. Completá todos los campos
2. Click en **"Guardar"**
3. Click en **"Sincronizar maestros AFIP"**
   - Primera llamada a ARCA (valida el certificado + conexión)
   - Si OK → "Sincronizados X tipos de cbte, X alícuotas IVA, X monedas"
   - Si Error → revisar certificado y conexión de red

---

## 3. Flujo de emisión de facturas

### 3.1 — Lista de proformas (pantalla principal)

1. El sistema vigila automáticamente la carpeta `Proformas IN`
2. Cuando llega un XML nuevo desde el ERP Nápoles:
   - Aparece en la grilla
   - Estado: **"Pendiente"**
   - Datos parseados: cliente, total, moneda, items

3. Seleccionar una fila → Click derecho "Consulta" (o doble-click)

### 3.2 — Detalle y emisión

Se abre ventana con todos los datos:

**Encabezado**
- Cliente
- Número proforma
- Tipo de comprobante (FacturaA, FacturaB, NotaCredito, FCE, etc.)
- Documento receptor
- Condición IVA
- Moneda + cotización
- Buque/Viaje (si aplica)
- Total

**Datos de emisión** (editables)
- **Ingresos Brutos**: elegir
  - `Convenio Multilateral` → aplica percepciones CM05
  - `Local / No Inscripto` → sin percepciones
  - `Exento` → sin IVA
- **Concepto**: Productos / Servicios / Ambos
- **Fechas de servicio**: desde/hasta (solo si servicios)
- **Vencimiento de pago**: fecha

**Paneles especiales** (se muestran según corresponda):
- 🟠 **Forzar como FCE** — aparece si el XML vino con tipo normal pero se puede convertir a FCE (ver sección 4)
- 🟢 **FCE MiPyME** — datos de CBU, SCA/ADC (ver sección 4)
- 🔵 **Tipo de saldo P/S** — para moneda extranjera
- 🟡 **Comprobante asociado** — para NC/ND

**Items** (grilla de solo lectura)
- Código, descripción, cantidad, precio unitario, IVA, importe

**Botón principal**
- Click **"Envía Factura Electrónica"** cuando estés listo

### 3.3 — Proceso de emisión

Cuando clickeás "Envía...":

1. **Validación local** (aritmética + reglas negocio + validaciones FCE)
   - Si falla → muestra errores en rojo, **no llama a ARCA**
   - Revisar montos, IVA, documentación, datos FCE

2. **Consulta último número autorizado** (WSFEv1)
   - Obtiene PtoVta + Tipo → número siguiente

3. **Solicita CAE a ARCA** (FECAESolicitar)
   - Si OK → CAE obtenido, número asignado
   - Si error → muestra mensaje amigable traducido

4. **Guarda XML respuesta** en `Archivo fiscal AFIP`
   - Estructura: `YYYY/MM/CAE_TIPO_PTOVTA_NUMERO_FECHA.xml`

5. **Genera PDF**
   - Con CAE + QR escaneable
   - Guarda en `Salida PDF`

6. **Imprime** (silencioso o con diálogo, según config)

7. **Mueve XML origen** a `Proformas OUT`
   - El XML de entrada se mueve a carpeta procesados

8. **Actualiza estado**
   - Estado pasa de "Pendiente" a "Autorizada"

---

## 4. Factura de Crédito Electrónica MiPyME (FCE)

### ¿Qué es la FCE?

La Factura de Crédito Electrónica es un comprobante que funciona como **título ejecutivo** negociable. Permite a las MiPyMEs financiar sus cuentas por cobrar.

### Códigos de comprobante FCE

| Código | Tipo | Letra |
|--------|------|-------|
| **201** | Factura de Crédito Electrónica MiPyME | A |
| **202** | Nota de Débito Electrónica MiPyME | A |
| **203** | Nota de Crédito Electrónica MiPyME | A |
| **206** | Factura de Crédito Electrónica MiPyME | B |
| **207** | Nota de Débito Electrónica MiPyME | B |
| **208** | Nota de Crédito Electrónica MiPyME | B |
| **211** | Factura de Crédito Electrónica MiPyME | C |
| **212** | Nota de Débito Electrónica MiPyME | C |
| **213** | Nota de Crédito Electrónica MiPyME | C |

> **⚠️ IMPORTANTE:** El código **203** es una **Nota de Crédito** FCE (no una factura). La **factura** FCE es el código **201** (letra A).

### ¿Cuándo usar FCE?

- Cuando la operación supera **$5.549.862** (monto vigente desde 14/04/2026)
- Y el receptor es una **"Empresa Grande"** según el listado oficial de ARCA
- O cuando el receptor optó por recibir FCE voluntariamente

### Datos requeridos para FCE

Cuando el tipo de comprobante es FCE, la app muestra un panel verde con:

| Campo | Descripción | Obligatorio |
|---|---|---|
| **SCA/ADC** | Sistema de Circulación Abierta o Agente Depósito Colectivo | ✅ Para facturas |
| **CBU** | 22 dígitos numéricos del beneficiario | ✅ Para facturas |
| **Alias** | Alias bancario (opcional) | ❌ |

### 4.1 — Forzar una factura normal como FCE

**Escenario:** El ERP Nápoles envió el XML con código de tipo normal (ej: 1 = Factura A) pero la operación debería ser FCE (ej: 201 = FCE A).

**Cómo actuar:**

1. Al abrir el detalle de la proforma, si el tipo es normal y convertible a FCE, aparece un **panel naranja** con el texto:

   > ⚠️ Forzar emisión como Factura de Crédito Electrónica MiPyME (FCE)

2. **Marcar el checkbox** para activar el modo FCE
3. Se desplegará automáticamente el panel FCE (verde) para completar CBU y SCA/ADC
4. Al enviar, el sistema reclasifica automáticamente:
   - Factura A (1) → FCE A (201)
   - Factura B (6) → FCE B (206)
   - NC A (3) → NC FCE A (203)
   - etc.

> **NOTA:** Si la factura ya fue emitida como normal y debería haber sido FCE, se debe emitir una Nota de Crédito para anularla y re-emitir como FCE.

### 4.2 — ¿Qué pasa si no fuerzo como FCE?

Si el XML viene con código 1 (Factura A normal) y no se marca "Forzar como FCE":
- ARCA **acepta** el comprobante como factura normal
- El PDF se imprime como "Factura" (sin cartel FCE)
- El receptor **no podrá negociarla** como título ejecutivo MiPyME
- Si la operación requería FCE obligatoriamente, se incumple la normativa

---

## 5. Mensajes de error y notificaciones

### 5.1 — Errores de validación local (antes de enviar a ARCA)

Estos errores se detectan **antes** de llamar a ARCA y aparecen en texto rojo en la pantalla:

| Error | Qué hacer |
|---|---|
| "ImpTotal ≠ ImpTotConc + ..." | Revisar montos de items + IVA en el ERP |
| "Suma bases imponibles ≠ ImpNeto" | Revisar agrupación de IVA |
| "FacturaA requiere RI + CUIT" | Cambiar tipo comprobante o verificar receptor |
| "Fechas de servicio obligatorias" | Completar Desde/Hasta/Vto.Pago |
| "FCE: CBU obligatorio" | Completar el CBU en el panel FCE |
| "FCE: seleccionar SCA o ADC" | Elegir SCA o ADC en el panel FCE |
| "CBU debe tener 22 dígitos" | Verificar formato del CBU |

### 5.2 — Errores de ARCA (después de enviar)

| Código | Error | Qué hacer |
|---|---|---|
| 10048 | Diferencia decimales total | Revisar montos de items + IVA |
| 10061 | Base imponible IVA | Revisar agrupación de IVA |
| 10063 | Receptor RI + CUIT | Cambiar tipo comprobante o receptor |
| 10016 | Número no correlativo | Reintentar (se resuelve automáticamente) |
| 10020 | Cotización inválida | Verificar cotización con ARCA |
| 10162 | FCE: falta Opcionales | Completar CBU y SCA/ADC |
| 10180 | Cbte asociado inexistente | Verificar datos del comprobante asociado |
| Timeout | Error de red | Reintentar en unos segundos |

### 5.3 — Estados del comprobante

| Estado | Significado | Acción |
|---|---|---|
| **Pendiente** | XML importado, aún no enviado | Abrir detalle y emitir |
| **EnProceso** | Enviado a ARCA, esperando respuesta | Esperar (contingencia automática) |
| **Autorizada** | CAE obtenido, comprobante válido | ✅ Listo |
| **Rechazada** | ARCA rechazó | Revisar errores y corregir |

---

## 6. Historial y búsqueda

Pestaña **"Historial de comprobantes"**

Buscar por:
- **CAE**: número de autorización (14 dígitos)
- **Rango de fechas**: desde/hasta
- **Cliente**: nombre o parte del nombre
- **Tipo**: FacturaA, FacturaB, NC, FCE, etc.

Resultados muestran: tipo, punto venta, número, fecha, cliente, monto, estado

---

## 7. Troubleshooting

### La aplicación no arranca
- **Síntoma**: "No se ejecuta" o "archivo no encontrable"
- **Solución**: Verificar que .NET 8+ está instalado
  ```powershell
  dotnet --version
  ```

### FileWatcher no detecta XMLs nuevos
- **Síntoma**: XMLs llegan a la carpeta pero no aparecen en la grilla
- **Solución**:
  1. Verificar ruta en Configuración → "Proformas IN"
  2. Probar con un XML de prueba (copiar uno de `BORRADORES\`)
  3. Revisar logs: `%LocalAppData%\FacturacionArca\logs\`

### Error de certificado
- **Síntoma**: "El certificado no es válido" o "No se puede cargar el .pfx"
- **Solución**:
  1. Verificar ruta completa del `.pfx` en Configuración
  2. Verificar contraseña correcta (case-sensitive)
  3. Si el `.pfx` es muy viejo, solicitar renovación a ARCA

### Sincronizar maestros falla
- **Síntoma**: Al hacer "Sincronizar maestros AFIP", error de conexión
- **Solución**:
  1. Verificar conexión a Internet
  2. Verificar que el certificado está autorizado en ARCA para el servicio WSFEv1
  3. En homologación: asegurarse de haber hecho la relación en el portal ARCA

### No puedo abrir carpetas `P:\` (red)
- **Síntoma**: "Acceso denegado" al guardar PDFs/XMLs en ruta de red
- **Solución**:
  1. Verificar que la carpeta existe y que el usuario tiene permisos
  2. Probar manualmente: `\\servidor\share\carpeta` desde Explorer
  3. Si usa credenciales distintas, mapear unidad con esas credenciales

### PDF no se ve bien o QR no funciona
- **Síntoma**: PDF abierto pero datos cortados, QR no escanea
- **Solución**:
  1. Abrir PDF en Adobe Reader (no en navegador)
  2. Probar QR con app móvil de cámara (toca el QR)
  3. Si persiste, revisar logs para errores de renderizado

### FCE sin cartel: factura se emitió como normal
- **Síntoma**: El PDF dice "Factura" en lugar de "Factura de Crédito Electrónica MiPyME"
- **Causa**: El ERP envió un código de tipo normal (1) en lugar de FCE (201)
- **Solución**: Para futuras facturas, marcar "Forzar como FCE" antes de emitir. Si ya se emitió, se debe anular con NC y re-emitir como FCE.

---

## 8. Obtener certificado digital (primeros pasos)

Si aún no tenés certificado:

### 8.1 — Generar CSR (Certificate Signing Request)

Con **OpenSSL** instalado (https://slproweb.com/products/Win32OpenSSL.html):

```powershell
# Crear carpeta de trabajo
mkdir C:\certificados_arca
cd C:\certificados_arca

# 1. Generar clave privada RSA 2048
openssl genrsa -out empresa.key 2048

# 2. Generar CSR
openssl req -new -key empresa.key -out empresa.csr `
  -subj "/C=AR/O=RAZON SOCIAL/CN=FacturacionArca/serialNumber=CUIT 30712345678"

# 3. Ver contenido del CSR (para validar)
openssl req -text -noout -in empresa.csr
```

### 8.2 — Enviar CSR a AFIP

1. Ingresar en https://auth.afip.gob.ar con **Clave Fiscal**
2. Buscar servicio **"WSASS"** o **"Autogestión Certificados Homologación"**
3. Opción **"Nuevo certificado"**
4. **Alias**: `FacturacionArca`
5. **CSR**: pegar contenido completo del archivo `empresa.csr` (desde `-----BEGIN...` hasta `-----END...`)
6. Confirmar → ARCA emite el certificado

### 8.3 — Crear archivo `.pfx`

Una vez que ARCA devuelve el `.crt`:

```powershell
openssl pkcs12 -export -in empresa.crt -inkey empresa.key `
  -out empresa.pfx -name "FacturacionArca"

# Te pide contraseña: ingresar y confirmar
# Ejemplo: "MiContraseña2024"
```

### 8.4 — Cargar en la aplicación

- Configuración → **"Ruta del .pfx"**: `C:\certificados_arca\empresa.pfx`
- **"Contraseña"**: la que ingreses en paso anterior
- **Guardar** → **"Sincronizar maestros"** (prueba la conexión)

---

## 9. Multimoneda y cotización

La aplicación soporta USD, PES, EUR, BRL y otras:

### En el XML de origen
- Cada proforma viene con `CodigoMoneda` (ej: DOL, PES)
- Y `CotizacionMoneda` (ej: 1180.50)

### En la emisión
- La app valida que la cotización coincida con la de AFIP (RG 5616)
- Si no coincide → error antes de enviar a ARCA

### Tipo de saldo P/S (solo moneda extranjera)

Cuando la factura es en moneda extranjera, aparece un panel azul con dos opciones:
- **Tipo P — Pesificada**: El saldo se fija en ARS al obtener el CAE
- **Tipo S — Saldos**: El saldo permanece en moneda original

Esta es una política interna que no se transmite a ARCA.

---

## 10. Convenio Multilateral CM05

Si habilitás **"Convenio Multilateral"** en Configuración:

- Al emitir, la app aplica **percepciones de IIBB por jurisdicción**
- En el PDF aparece un desglose: CABA, Bs.As., Córdoba, etc., con sus alícuotas
- El padrón AGIP se consulta automáticamente para obtener la alícuota

### Configurar alícuotas CM05
- **Futuro**: panel de administración en Configuración
- **Por ahora**: contactar soporte para editar base de datos

---

## 11. Contingencia y reproceso (sin conexión)

Si la red falla mientras se emite:

1. **FECompConsultar**: la app intenta averiguar si el CAE fue asignado
   - Si sí → comprobante se marca como Autorizado (sin duplicar número)
   - Si no → queda como "Pendiente reproceso"

2. **Reproceso manual**:
   - Pestaña "Historial" → buscar comprobante con estado "Pendiente"
   - Click derecho → "Reintentar" (futuro)
   - O, abrir nuevamente desde la lista de proformas

---

## 12. FAQ

**P: ¿Qué pasa con los XMLs después de emitir?**  
R: Se mueven de `Proformas IN` a `Proformas OUT`. El XML respuesta de ARCA se guarda en `Archivo fiscal AFIP` (RG 1361, indefinidamente).

**P: ¿Puedo editar los datos de la proforma antes de emitir?**  
R: Sí, en la pantalla "Detalle": Concepto, fechas de servicio, condición IIBB, tipo de saldo, y si corresponde, forzar como FCE.

**P: ¿Qué pasa si emito la misma proforma dos veces?**  
R: ARCA rechaza ("no correlativo"). Si fue aceptada la primera vez, la app detecta que ya fue emitida (estado "Autorizada") y no la intenta de nuevo.

**P: ¿Cómo hago backup de los comprobantes emitidos?**  
R: Copiar la carpeta `Archivo fiscal AFIP` (contiene todos los XMLs de ARCA) y `Salida PDF` (todos los PDFs). La base de datos (`facturacion.db`) también contiene metadata.

**P: ¿Funciona en homologación antes de tener certificado de producción?**  
R: Sí. Generá un CSR de prueba, obtené certificado de homologación ARCA, usá ese. Después, cuando tengas el de producción, simplemente cargá el `.pfx` nuevo y cambiá Modo a "Producción".

**P: ¿Se puede usar en 2 PCs simultáneamente?**  
R: No recomendado. La base SQLite no está diseñada para escritura concurrente. Usar en una sola PC.

**P: ¿Qué es el código 203 y cuándo se usa?**  
R: El código 203 es la **Nota de Crédito Electrónica MiPyME A**. Se usa para anular o modificar una FCE previamente emitida. La **factura** FCE es el código **201**.

**P: ¿Qué hago si el ERP envía código 1 pero necesito FCE?**  
R: Marcá "Forzar como FCE" en la pantalla de detalle. El sistema reclasifica automáticamente de 1→201 y solicita los datos FCE.

**P: ¿Qué pasa si una factura se emitió como normal cuando debía ser FCE?**  
R: No se puede cambiar una factura ya emitida. Debés emitir una NC para anularla y luego re-emitir como FCE con el tipo correcto.

---

## 13. Contacto y soporte

- **Documentación técnica**: ver `README.md` y `MANUAL_TECNICO.md` en el repo
- **Reporte de errores frecuentes**: ver `REPORTE_ERRORES.md`
- **Logs**: `%LocalAppData%\FacturacionArca\logs\` (Serilog, debug + info)
- **Errores AFIP**: consultar `ArcaErrorCatalog.cs` en el código para interpretación de códigos

---

**Versión**: 1.1  
**Última actualización**: 2026-04-30  
**Estado**: Producción Homologación (certificado de prueba)
