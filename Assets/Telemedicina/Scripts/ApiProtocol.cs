using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;

namespace Telemedicina
{
    // El contrato coincide con telemedicina/backend/app/schemas.py. No se almacena
    // contenido clínico ni credenciales en PlayerPrefs, archivos o mensajes de consola.
    [Serializable] public class LoginData { public string correo; public string password; }
    [Serializable] public class TokenData { public string access_token; public string token_type; public int expires_in; }
    [Serializable] public class HealthData { public string status; public string database; public string version; public string entorno; }
    [Serializable] public class Usuario
    {
        public int id; public string nombre; public string correo; public string rol; public bool activo;
        public string fecha_registro;
    }
    [Serializable] public class Paciente
    {
        public int id; public string expediente; public string nombres; public string apellido_paterno;
        public string apellido_materno; public string fecha_nacimiento; public string sexo;
        public string telefono; public string comunidad; public string municipio; public string estado;
        public string tipo_sangre; public bool activo; public string fecha_registro; public string fecha_actualizacion;
        [JsonIgnore] public string NombreCompleto => string.Join(" ", new[] { nombres, apellido_paterno, apellido_materno }).Trim();
    }
    [Serializable] public class Consulta
    {
        public int id; public int paciente_id; public int registrado_por_id; public int? medico_id;
        public string motivo; public string estado; public string fecha_apertura; public string fecha_inicio; public string fecha_cierre;
    }
    [Serializable] public class Signos
    {
        public int id; public int consulta_id; public int registrado_por_id; public string fecha_medicion;
        public int? presion_sistolica; public int? presion_diastolica;
        public int? frecuencia_cardiaca; public int? frecuencia_respiratoria;
        // FastAPI envía Decimal como cadena JSON. Json.NET conserva null y acepta
        // tanto "36.7" como 36.7 sin depender de la configuración regional de Linux.
        public decimal? saturacion; public decimal? temperatura; public decimal? glucosa;
        public decimal? peso; public decimal? talla;
    }
    [Serializable] public class Nota
    {
        public int id; public int consulta_id; public int medico_id; public string contenido; public int version;
        public string fecha_creacion; public string fecha_actualizacion;
    }
    [Serializable] public class Pagina<T>
    {
        public List<T> items = new List<T>(); public int total; public int pagina; public int tamano;
    }
    [Serializable] public class Expediente
    {
        public Paciente paciente; public List<Consulta> consultas = new List<Consulta>();
        public int total_consultas; public int pagina; public int tamano;
    }
    [Serializable] public class EstadoData { public string estado; }
    [Serializable] public class NotaData { public string contenido; }
    [Serializable] public class NotaActualizarData { public string contenido; public int version_esperada; }

    public static class ApiProtocol
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            DateParseHandling = DateParseHandling.None,
            Culture = CultureInfo.InvariantCulture,
            TypeNameHandling = TypeNameHandling.None,
            MaxDepth = 32
        };
        public static string Encode(object data) => JsonConvert.SerializeObject(data, Settings);
        public static T Decode<T>(string data) => JsonConvert.DeserializeObject<T>(data, Settings);

        public static bool TryBaseUrl(string text, out string normalized, out string error)
        {
            normalized = null; error = "Dirección del servidor no válida. Ejemplo: http://localhost:8000";
            if (!Uri.TryCreate((text ?? "").Trim(), UriKind.Absolute, out var uri)) return false;
            if (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback))
            { error = "Usa HTTPS para conectar con un servidor externo."; return false; }
            if (!string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            { error = "La dirección no debe incluir usuario, contraseña, parámetros ni fragmentos."; return false; }
            if (uri.AbsolutePath != "/" && uri.AbsolutePath.TrimEnd('/') != "/api")
            { error = "Usa la dirección del servidor sin rutas. Ejemplo: http://localhost:8000"; return false; }
            normalized = uri.GetLeftPart(UriPartial.Authority).TrimEnd('/'); error = null; return true;
        }
        public static bool CanWrite(Consulta consulta, Usuario usuario) =>
            consulta != null && usuario != null && usuario.activo && usuario.rol == "medico" &&
            consulta.estado == "en_atencion" && consulta.medico_id == usuario.id;
        public static bool CanTake(Consulta consulta, Usuario usuario) =>
            consulta != null && usuario != null && usuario.activo && usuario.rol == "medico" && consulta.estado == "listo";
        public static bool Dirty(string draft, Nota saved) => (draft ?? "").Trim() != (saved?.contenido ?? "");
        public static string Medicion(decimal? value, string unit) => value.HasValue
            ? value.Value.ToString("0.##", CultureInfo.InvariantCulture) + " " + unit : "No capturado";
        public static string Dato(string value) => string.IsNullOrWhiteSpace(value) ? "No capturado" : value;
        public static string Fecha(string value)
        {
            if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return "Sin fecha";
            return date.ToLocalTime().ToString("dd/MM/yyyy HH:mm zzz", CultureInfo.InvariantCulture);
        }
        public static string Estado(string value)
        {
            switch (value)
            {
                case "listo": return "Listo para consulta";
                case "en_atencion": return "En atención";
                case "cerrado": return "Cerrado · solo lectura";
                case "en_preparacion": return "En preparación";
                default: return "Estado desconocido";
            }
        }
        public static string ErrorMessage(long code)
        {
            // No se refleja el cuerpo de error: podría contener datos clínicos o credenciales.
            switch (code)
            {
                case 0: return "No se pudo conectar con el servidor. Intenta de nuevo.";
                case 401: return "Sesión vencida o no válida. Inicia sesión de nuevo.";
                case 403: return "No tienes permiso para esta operación.";
                case 404: return "No se encontró el registro. Recarga la lista.";
                case 409: return "No se pudo completar la operación. Conserva tu borrador y recarga el expediente.";
                case 422: return "Revisa los datos ingresados.";
                case 429: return "Demasiados intentos. Espera un momento antes de volver a iniciar sesión.";
                default: return "Error del servidor. Intenta de nuevo.";
            }
        }
    }
}
