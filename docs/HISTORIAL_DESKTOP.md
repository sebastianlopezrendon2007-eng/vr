# Consultorio XR · Beta 0.1

La interfaz usa textos breves y la etiqueta **Beta 0.1**. Se retiraron los avisos de desarrollo y las instrucciones de creación de cuenta de las pantallas.

Para actualizar el proyecto que ya abriste: detén **Play**, cierra Unity, extrae este ZIP en una carpeta temporal y copia el contenido de `TelemedicinaXR/Assets/Telemedicina/Scripts` a `~/Escritorio/TelemedicinaXR/Assets/Telemedicina/Scripts`, reemplazando los archivos. Abre Unity y vuelve a pulsar **Play**. Esta actualización modifica textos; conserva la conexión, las cuentas y la base de datos.

Proyecto Unity para probar en tu laptop Zorin/Ubuntu **antes de tener los Quest 3**. Se conecta al backend de telemedicina que ya tienes: FastAPI, Python y PostgreSQL. No hay otro backend, IA ni API de ChatGPT.

**Beta de pruebas: usa exclusivamente pacientes ficticios.** El esquema 3D es educativo, genérico y no representa al paciente. Este proyecto no está certificado ni preparado para atender pacientes reales.

## Abrir y probar en tu laptop

1. Descarga `TelemedicinaXR_v0.1.zip`. En el administrador de archivos, extrae el ZIP en tu **Escritorio**. Debe quedar la carpeta `~/Escritorio/TelemedicinaXR`, con `Assets`, `Packages` y `ProjectSettings` dentro. Es una copia nueva: conserva tu proyecto anterior.
2. Inicia el backend que ya funciona. Abre una terminal y pega:

   ```bash
   cd ~/Escritorio/telemedicina
   sudo docker compose up -d
   ```

3. Abre **Unity Hub → Projects → Add → Add project from disk** y elige la carpeta `TelemedicinaXR`. Usa **Unity 6000.6.4f1**, la misma versión de tu ZIP. Espera la importación inicial de paquetes.
4. Abre el proyecto. En el menú superior, pulsa **Telemedicina → Abrir consultorio**. También puedes abrir `Assets/Scenes/Consultorio.unity` con doble clic.
5. Pulsa **▶ Play** y selecciona la pestaña **Game**. La escena crea la interfaz al iniciar Play; verla vacía antes de Play es normal. No tienes que colocar objetos ni configurar componentes.
6. Deja la dirección en **`http://localhost:8000`**. Introduce **el correo y la contraseña de tu médico**, los mismos que usas en el módulo web. El ZIP no incluye credenciales predeterminadas.

Si ya estás dentro de Unity, detener y volver a pulsar Play reinicia la pantalla de prueba; los datos clínicos quedan en PostgreSQL.

También puedes abrir Unity desde la terminal con:

```bash
cd ~/Escritorio/TelemedicinaXR
bash abrir_demo.sh
```

El script busca `~/Unity/Hub/Editor/6000.6.4f1/Editor/Unity` e inicia con OpenGL para esta laptop. Si tu instalación está en otra carpeta, puedes usar Unity Hub o indicar la ruta:

```bash
UNITY_EDITOR="/ruta/al/Editor/Unity" bash abrir_demo.sh
```

## Demostración completa

1. Abre **http://localhost:8000** en el navegador. Entra con enfermería.
2. Registra un paciente ficticio o busca uno de ejemplo. Abre una **consulta nueva**, captura las mediciones de prueba y márcala **lista**. Si ese paciente ya tiene una consulta abierta, continúa esa consulta.
3. En Unity, entra con tu médico y pulsa **Listos → Actualizar lista**. Cada fila muestra el número de consulta y de paciente; al abrirla aparecen los nombres y el expediente.
4. Selecciona la consulta. Desplaza el expediente con la rueda o la barra de la derecha: incluye motivo, fechas, signos con sus unidades e historial visible para tu usuario. Una medición ausente dice **No capturado**.
5. Pulsa **Tomar consulta**. El servidor la asigna a tu médico y pasa a **En atención**. Otro médico ya no puede tomarla ni abrir esa consulta.
6. Escribe una nota de ejemplo, sin información real:

   > DEMOSTRACIÓN CON DATOS FICTICIOS. Revisión del expediente de ejemplo. Nota de prueba para verificar el guardado y el cierre del flujo.

7. Pulsa **Guardar nota**. Verás la versión guardada. Puedes actualizarla mientras esté en atención; los cambios usan la versión esperada para detectar conflictos.
8. Pulsa **Cerrar consulta → Confirmar cierre**. Se guarda la fecha de cierre y la nota queda **solo lectura**.
9. Detén Play, vuelve a iniciarlo, entra con el mismo médico y selecciona **Mis cerradas**. Reabre la consulta: debe conservar paciente, signos y nota.

Si tu ejemplo anterior ya estaba cerrado, estará en **Mis cerradas** del médico que lo atendió. Para mostrar el flujo desde el principio prepara una consulta nueva con enfermería. **En atención** permite retomar las consultas asignadas a tu usuario que aún no cerraste. Las listas tienen cinco registros por página y botones Anterior/Siguiente.

El modelo se gira arrastrándolo o con los botones y las flechas; la rueda encima del modelo lo acerca. **R** restablece la vista. Es un esquema original de columna lumbar hecho con figuras simples, sin relación con el motivo, signos o nota. No calcula diagnósticos ni recomendaciones.

## Si no entra o no aparecen consultas

- Comprueba que `http://localhost:8000/api/health` abra en tu navegador y devuelva `status: ok` y `database: ok`.
- Usa una cuenta con rol **médico**, no la del administrador ni enfermería. Los usuarios se crean en el módulo web como administrador; no necesitas crear de nuevo los que ya tienes.
- No hay consultas listas hasta que enfermería marque una como lista. Las cerradas solo se muestran al médico que las atendió.
- Si recibes un conflicto, conserva el texto de tu borrador y pulsa **Recargar expediente**. La recarga pide confirmar el descarte de cambios sin guardar.
- Si la sesión vence o el servidor revoca el token, la app vuelve al acceso y borra los datos de la pantalla. Guarda la nota antes de dejar la sesión abierta por mucho tiempo.
- **Cerrar sesión** usa el logout del backend: revoca todas las sesiones de ese usuario, también la del navegador si está abierta con el mismo médico.
- Si Unity muestra cuadros extraños en **Scene**, prueba en **Game**. El proyecto desactiva GPU Resident Drawer, sombras, HDR y SSAO. Puedes abrir con `bash abrir_demo.sh`, cerrar navegador/otras apps y bajar el tamaño de Game. No se ha medido el rendimiento en tu Intel UHD.
- El proyecto usa el sistema de entrada nuevo y uGUI. Si Unity muestra errores al importar paquetes, verifica que estás usando `6000.6.4f1` y que terminó la descarga; no cambies de versión durante esta primera prueba.

## Crear una aplicación Linux desde Unity

Cuando ya hayas probado Play: detén la prueba, pulsa **Telemedicina → Crear aplicación Linux** y elige una carpeta, por ejemplo `~/Escritorio/TelemedicinaXR_Linux`. Unity compilará `TelemedicinaXR.x86_64` y sus carpetas acompañantes. Conserva todo el contenido generado junto.

No se entrega un ejecutable precompilado ni un APK: este entorno no tiene el editor de Unity ni una licencia para generarlos. La primera ejecución y esa compilación deben comprobarse en tu editor. La app Linux podrá iniciarse con:

```bash
cd ~/Escritorio/TelemedicinaXR_Linux
chmod +x TelemedicinaXR.x86_64
./TelemedicinaXR.x86_64 -force-glcore
```

## Qué incluye y qué falta para Quest

Incluye escena, cliente HTTP real con tokens en memoria, expedientes, signos, notas con control de versión, cierre protegido, confirmación de descarte de borradores, listas paginadas y modelo 3D manipulable. La interfaz es en español y usa paneles en el espacio 3D. Los permisos y la persistencia los aplica el backend existente.

Esta versión se controla con **mouse y teclado**. **Todavía no tiene passthrough, seguimiento de manos, controles Quest ni una sesión OpenXR**; no se presenta como realidad mixta ya verificada. El modelo, los paneles y el cliente están separados para añadir esas entradas después. El siguiente paso con el visor es integrar OpenXR/Meta, probar interacción y construir en Android. Los módulos Android que instalaste se usarán entonces.

En los Quest, `localhost` designa el visor, no la laptop. El acceso desde otro equipo requerirá una dirección HTTPS válida y configuración del servidor y la red; esta demo permite HTTP solamente en el mismo equipo. No incluye configuración de red para el visor ni modifica tu Docker o tu base de datos.

Lee `docs/VERIFICACION.md` para ver las pruebas ejecutadas y las pendientes. El contrato usado está en `docs/API.md`.

## Estructura

| Archivo | Función |
|---|---|
| `Assets/Scenes/Consultorio.unity` | Escena de entrada con creación automática de la interfaz al pulsar Play |
| `Assets/Telemedicina/Scripts/ApiProtocol.cs` | Contrato JSON, unidades, fechas y reglas de presentación |
| `Assets/Telemedicina/Scripts/ApiClient.cs` | HTTP real con UnityWebRequest, expiración, aborto y logout |
| `Assets/Telemedicina/Scripts/ConsultorioApp.cs` | Acceso, listas, expediente y flujo del médico |
| `Assets/Telemedicina/Scripts/Ui.cs` | Controles y paneles |
| `Assets/Telemedicina/Scripts/EducationalModel.cs` | Esquema 3D y controles de escritorio |
| `Assets/Telemedicina/Resources/` | Shader/material ligeros incluidos en la compilación |
| `Assets/Telemedicina/Editor/ConsultorioEditor.cs` | Menú para abrir la escena y compilar Linux |
| `Assets/Telemedicina/Tests/Editor/` | Pruebas de contrato para el Test Runner de Unity |
| `tools/ApiSmoke/` | Verificación independiente del contrato C# contra una API de pruebas real |

La base original que enviaste se conserva con su `SampleScene`; la escena habilitada para compilar es `Consultorio`. Se quitaron los paquetes opcionales de IA, colaboración y tutorial de bienvenida del proyecto de prueba para reducir trabajo de importación. URP, Input System, uGUI y sus versiones vienen de tu proyecto original; Newtonsoft JSON se declara directamente. La lógica clínica sigue en Python.
