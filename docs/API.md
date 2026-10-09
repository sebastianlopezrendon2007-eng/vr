# Contrato utilizado por el cliente Unity

Origen local de desarrollo: `http://localhost:8000`. Todas las rutas usan `/api`. Este proyecto consume el backend existente `telemedicina` v0.1; no incorpora un servidor adicional ni una copia de PostgreSQL. No necesita una clave de OpenAI.

| Operación | Método y ruta | Datos |
|---|---|---|
| Conexión | `GET /api/health` | `status`, `database`, `version`, `entorno` |
| Acceso | `POST /api/auth/login` | JSON `correo` y `password` |
| Perfil | `GET /api/auth/me` | Usuario autenticado, rol y estado activo |
| Cerrar sesión | `POST /api/auth/logout` | Revoca los tokens del usuario; devuelve 204 |
| Lista | `GET /api/consultas?estado=listo&pagina=1&tamano=5` | `items`, `total`, `pagina`, `tamano` |
| Consulta | `GET /api/consultas/{id}` | Paciente, estado, médico asignado, motivo y fechas |
| Expediente | `GET /api/pacientes/{id}/expediente?pagina=1&tamano=20` | Paciente y consultas visibles para este médico |
| Signos | `GET /api/consultas/{id}/signos` | Mediciones o `null` cuando no se capturaron |
| Tomar | `PATCH /api/consultas/{id}/estado` | `{"estado":"en_atencion"}` |
| Nota | `GET /api/consultas/{id}/nota` | Nota o `null` |
| Crear nota | `POST /api/consultas/{id}/nota` | `{"contenido":"Nota ficticia de prueba"}` |
| Actualizar nota | `PUT /api/consultas/{id}/nota` | `{"contenido":"Nota ficticia actualizada","version_esperada":1}` |
| Cerrar consulta | `PATCH /api/consultas/{id}/estado` | `{"estado":"cerrado"}` |

Después del acceso se envía `Authorization: Bearer <token>` en cada petición protegida. El token permanece solamente en memoria; no se registra el contenido de las peticiones o respuestas. La contraseña se oculta en el campo y no se recorta antes de enviarla. Las redirecciones HTTP están deshabilitadas y los certificados HTTPS se validan con el comportamiento normal de Unity.

Se acepta HTTP solamente en direcciones de loopback para desarrollo local. Para otro origen se exige HTTPS. CORS no sustituye la autorización: este cliente nativo usa los mismos permisos de servidor y no requiere ampliar los orígenes del navegador.

Los decimales de FastAPI pueden llegar como cadenas (`"36.7"`); se interpretan como `decimal?`, también si vienen como número. `null` conserva el significado de no medido. Las fechas ISO conservan su zona; se muestran en la zona local de la laptop con el desplazamiento `-07:00`, `+00:00`, etc. La fecha de nacimiento se muestra como fecha sin convertirla a un instante.

| Medición | Unidad |
|---|---|
| Presión sistólica y diastólica | mmHg |
| Frecuencia cardiaca | lat/min |
| Frecuencia respiratoria | resp/min |
| Saturación | % |
| Temperatura | °C |
| Glucosa | mg/dL |
| Peso | kg |
| Talla | cm |

El cliente guía el estado `listo → en_atencion → cerrado`. Enfermería prepara y marca listo en la web. La app solo permite escribir durante la atención por el médico asignado, usa versiones para evitar sobrescribir otra sesión y pide confirmar el cierre. El backend verifica estas reglas nuevamente aunque se cambie la URL o se use otro cliente.

Un error de red no produce una respuesta simulada ni un mensaje de guardado exitoso. Si un guardado llegó al servidor pero se perdió su respuesta, se debe recargar el expediente para confirmar el resultado; volver a crear la nota podría devolver 409. Los errores muestran mensajes generales sin reflejar texto clínico ni secretos.
