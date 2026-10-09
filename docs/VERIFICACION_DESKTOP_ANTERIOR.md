# Verificación de esta entrega

Actualización de textos: la interfaz muestra **Beta 0.1**, campos de acceso neutrales y mensajes breves. Se revisó la sintaxis C# de los archivos actualizados. Las reglas de acceso, las peticiones, el guardado y las transiciones no se modificaron. Las pruebas de integración detalladas abajo corresponden a la entrega funcional anterior; no se repitieron para este cambio de textos. La revisión visual de esta actualización debe hacerse en Unity.

## Ejecutado en este entorno

Se extrajo y revisó el ZIP original y se preparó una copia con la misma versión de Unity: **6000.6.4f1**. Para las pruebas de conexión se usó el backend real del proyecto `telemedicina`, sus migraciones Alembic y PostgreSQL 16, en una base de pruebas separada y exclusivamente ficticia. No se accedió a tu laptop ni se modificaron tus datos.

El contrato `ApiProtocol.cs` se **compiló y ejecutó como C#** con Roslyn/.NET 8 y el DLL de Newtonsoft 13.0.2 del paquete oficial de Unity `com.unity.nuget.newtonsoft-json` 3.2.2. El ejecutable de verificación usa HttpClient para poder correr fuera del motor; la app de Unity utiliza UnityWebRequest.

| Verificación | Resultado |
|---|---|
| Contrato JSON, decimales como cadena/número, nulos, fechas, validación de URL y permisos de presentación | Pasó |
| Salud con PostgreSQL y login con tokens que expiran | Pasó |
| Creación de usuarios por administrador, pacientes y consultas por enfermería | Pasó |
| Preparación, signos parciales, marcar listo y paginación de siete consultas | Pasó |
| Médico abre expediente y signos, toma consulta, guarda nota v1 y actualiza a v2 | Pasó |
| Conflicto de versión, nota vacía y cierre sin nota | Rechazados como se esperaba |
| Acceso sin token, contraseña incorrecta y acceso por otro médico | Rechazados como se esperaba |
| Médico sin permisos de usuarios/auditoría y administrador sin permisos clínicos | Rechazados como se esperaba |
| Enfermería no puede escribir una nota ni alterar signos cerrados | Rechazados como se esperaba |
| Cierre de consulta y rechazo de edición posterior de la nota | Pasó |
| Logout revoca el token y un token ya vencido no accede | Pasó |
| Detener y reiniciar **PostgreSQL y FastAPI**, volver a autenticar y leer paciente, signos, consulta cerrada y nota v2 | Pasó; datos persistieron |

El primer recorrido produjo **82 comprobaciones satisfactorias**. La segunda ejecución después del reinicio produjo **24**, incluyendo otra comprobación del contrato y las lecturas de persistencia. Son comprobaciones del contrato y del servidor; no se deben confundir con 106 pruebas de interfaz de Unity.

También se compiló `ApiClient.cs` junto al contrato contra referencias reales de los módulos UnityEngine 2021.3.33 para detectar errores de C# en las llamadas comunes de HTTP/corutinas. Esto **no sustituye** una compilación del proyecto con Unity 6000.6.4f1. El parser Roslyn revisó los siete archivos C# de `Assets/Telemedicina` con sintaxis C# 9 sin errores. Se revisaron los GUID, referencias de escena/material, JSON de paquetes y contenido del ZIP.

## Pendiente de ejecutar en tu laptop

Este entorno **no tiene instalado Unity**, una licencia de editor, una GPU equivalente a tu Intel UHD ni unos Quest 3. Por ello no se pudo ejecutar:

- Importación y compilación completa en Unity 6000.6.4f1.
- Play/Game: renderizado del shader, tipografía, campos de entrada, desplazamiento, confirmaciones y giro del modelo.
- Peticiones desde **UnityWebRequest** dentro del motor. Las pruebas de integración usaron el mismo contrato con HttpClient.
- Medición de rendimiento o comprobación de los cuadros de colores de tu vista Scene.
- Compilación del ejecutable Linux mediante el menú incluido.
- APK Android, OpenXR, passthrough, manos o controles Quest. Estas funciones todavía no están implementadas.

No se presenta un render simulado como captura de la aplicación. El ZIP contiene el proyecto de prueba y el código; todavía debes comprobar la ejecución en tu editor siguiendo el README.

## Repetir las pruebas de contrato en Unity

Abre **Window → General → Test Runner**, selecciona **EditMode** y ejecuta `ProtocolTests`. Incluye 8 casos de dirección del servidor y pruebas de signos, fechas, permisos y borradores. Se entregan para ejecutar en el editor; no se afirma que el Test Runner se haya ejecutado aquí.

Después sigue el flujo manual del README. Comprueba especialmente que cerrar bloquea el campo de nota, que cambiar de consulta pide confirmar el descarte de un borrador y que volver a Play conserva los datos del servidor.

## Runner independiente de integración

`tools/ApiSmoke` permite repetir el recorrido con .NET 8 y una **API/base de datos desechable de pruebas**. Crea siete pacientes ficticios y tres cuentas, así que no lo ejecutes apuntando a tu base de trabajo. No es necesario instalar .NET para usar la demo en Unity.

El runner requiere estas variables de entorno, elegidas por quien crea la base de pruebas: `XR_TEST_URL`, `XR_ADMIN_EMAIL`, `XR_ADMIN_PASS`, `XR_NURSE_EMAIL`, `XR_NURSE_PASS`, `XR_DOCTOR_EMAIL`, `XR_DOCTOR_PASS`, `XR_OTHER_EMAIL`, `XR_OTHER_PASS` y `XR_TEST_ALLOW_WRITES=1`. Solo el administrador debe existir al comienzo; el runner crea las otras cuentas con la API. Usa correos de ejemplo nuevos y contraseñas propias de al menos 12 caracteres; no guardes secretos en el proyecto.

Desde la carpeta del proyecto, con esas variables configuradas:

```bash
dotnet run --project tools/ApiSmoke/ApiSmoke.csproj
```

Devuelve una línea con `resultado: PASS`, el número de comprobaciones y `consulta_id`. Para verificar persistencia, reinicia tu servidor y PostgreSQL de pruebas sin borrar el volumen y usa el identificador devuelto:

```bash
dotnet run --project tools/ApiSmoke/ApiSmoke.csproj -- --persistencia NUMERO_DE_CONSULTA
```

El script no muestra credenciales, tokens ni expedientes en consola. Un token vencido de pruebas puede pasarse opcionalmente en `XR_EXPIRED_TOKEN` para repetir ese caso. La expiración de pantalla por tiempo y la limpieza visual de sesión deben verificarse dentro de Unity.
