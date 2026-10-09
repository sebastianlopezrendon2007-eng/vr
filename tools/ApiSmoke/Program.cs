using System.Net;
using System.Text;
using Telemedicina;

internal static class Program
{
    private static int checks;
    private static HttpClient client;
    private static string Env(string name) => Environment.GetEnvironmentVariable(name) ?? throw new Exception("Falta " + name);
    private static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception("FALLÓ: " + label);
        checks++;
    }
    private sealed class Response<T> { public int Code; public T Data; }
    private static async Task<Response<T>> Send<T>(string method, string path, object data = null, string token = null)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), "/api" + path);
        if (data != null) request.Content = new StringContent(ApiProtocol.Encode(data), Encoding.UTF8, "application/json");
        if (token != null) request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        using var result = await client.SendAsync(request);
        string body = await result.Content.ReadAsStringAsync();
        return new Response<T> { Code = (int)result.StatusCode, Data = result.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(body) ? ApiProtocol.Decode<T>(body) : default };
    }
    private static async Task<string> Login(string email, string password)
    {
        var response = await Send<TokenData>("POST", "/auth/login", new LoginData { correo = email, password = password });
        Check(response.Code == 200 && response.Data.expires_in > 0 && response.Data.token_type == "bearer", "login con expiración");
        return response.Data.access_token;
    }
    private static async Task CreateUser(string admin, string env, string role)
    {
        var result = await Send<Usuario>("POST", "/usuarios", new { nombre = "Prueba XR " + role, correo = Env(env + "_EMAIL"), password = Env(env + "_PASS"), rol = role }, admin);
        Check(result.Code == 201 && result.Data.rol == role, "crear usuario " + role);
    }
    private static void ProtocolChecks()
    {
        foreach (var item in new[] {
            ("http://localhost:8000", true), ("http://127.0.0.1:8000/api/", true), ("https://ejemplo.invalid", true),
            ("http://192.168.0.4:8000", false), ("https://a:b@ejemplo.invalid", false),
            ("https://ejemplo.invalid?token=x", false), ("https://ejemplo.invalid/path", false), ("file:///etc/passwd", false) })
            Check(ApiProtocol.TryBaseUrl(item.Item1, out _, out _) == item.Item2, "validación de origen");
        var s = ApiProtocol.Decode<Signos>("{\"temperatura\":\"36.7\",\"saturacion\":98.5,\"peso\":\"70.2\",\"glucosa\":null}");
        Check(s.temperatura == 36.7m && s.saturacion == 98.5m && s.peso == 70.2m, "decimales como texto y números");
        Check(s.glucosa == null && ApiProtocol.Medicion(s.glucosa, "mg/dL") == "No capturado", "null no significa cero");
        Check(ApiProtocol.Medicion(0, "%") == "0 %", "cero válido no se oculta");
        var c = ApiProtocol.Decode<Consulta>("{\"fecha_apertura\":\"2026-10-03T04:00:00+00:00\",\"medico_id\":null}");
        Check(c.fecha_apertura.EndsWith("+00:00") && c.medico_id == null, "fecha con zona y asignación opcional");
        var u = new Usuario { id = 1, rol = "medico", activo = true };
        c.estado = "en_atencion"; c.medico_id = 1;
        Check(ApiProtocol.CanWrite(c, u), "médico asignado puede escribir");
        c.estado = "cerrado"; Check(!ApiProtocol.CanWrite(c, u), "consulta cerrada bloquea edición en cliente");
        c.estado = "en_atencion"; c.medico_id = 2; Check(!ApiProtocol.CanWrite(c, u), "otro médico no puede escribir");
        Check(ApiProtocol.Dirty("Cambio ficticio", new Nota { contenido = "Nota ficticia" }), "detectar cambios sin guardar");
        Check(!ApiProtocol.Dirty(" Nota ficticia ", new Nota { contenido = "Nota ficticia" }), "normalizar espacios de nota");
        Check(ApiProtocol.Encode(new LoginData { correo = "a@example.com", password = " espacios " }).Contains(" espacios "), "contraseña sin recortar");
    }
    private static async Task<int> Flow()
    {
        string admin = await Login(Env("XR_ADMIN_EMAIL"), Env("XR_ADMIN_PASS"));
        await CreateUser(admin, "XR_NURSE", "enfermeria");
        await CreateUser(admin, "XR_DOCTOR", "medico");
        await CreateUser(admin, "XR_OTHER", "medico");
        string nurse = await Login(Env("XR_NURSE_EMAIL"), Env("XR_NURSE_PASS"));
        string doctor = await Login(Env("XR_DOCTOR_EMAIL"), Env("XR_DOCTOR_PASS"));
        string other = await Login(Env("XR_OTHER_EMAIL"), Env("XR_OTHER_PASS"));
        var health = await Send<HealthData>("GET", "/health");
        Check(health.Code == 200 && health.Data.database == "ok", "salud y PostgreSQL");
        Check((await Send<object>("GET", "/consultas")).Code == 401, "rechazo sin token");
        Check((await Send<object>("POST", "/auth/login", new LoginData { correo = Env("XR_DOCTOR_EMAIL"), password = "incorrecta" })).Code == 401, "rechazo de contraseña incorrecta");
        Check((await Send<object>("GET", "/usuarios", token: doctor)).Code == 403, "médico sin acceso a usuarios");
        Check((await Send<object>("GET", "/pacientes", token: admin)).Code == 403, "administrador sin acceso clínico");
        Check((await Send<object>("GET", "/consultas?pagina=0", token: doctor)).Code == 422, "validación de página");
        var me = await Send<Usuario>("GET", "/auth/me", token: doctor);
        Check(me.Code == 200 && me.Data.rol == "medico", "usuario autenticado");
        int id = 0;
        for (int i = 0; i < 7; i++)
        {
            var p = await Send<Paciente>("POST", "/pacientes", new {
                nombres = "Paciente ficticio " + i, apellido_paterno = "Demostración", apellido_materno = "XR",
                fecha_nacimiento = "1985-04-12", sexo = "no_especificado", comunidad = "Comunidad ficticia", municipio = "Municipio de prueba", estado = "Sinaloa" }, nurse);
            Check(p.Code == 201 && !string.IsNullOrEmpty(p.Data.expediente), "expediente generado por servidor");
            var c = await Send<Consulta>("POST", "/consultas", new { paciente_id = p.Data.id, motivo = "Demostración ficticia de conexión y guardado." }, nurse);
            Check(c.Code == 201 && c.Data.estado == "en_preparacion", "apertura por enfermería");
            if (i == 0)
            {
                id = c.Data.id;
                Check((await Send<object>("GET", "/consultas/" + id, token: doctor)).Code == 403, "preparación no visible al médico");
                Check((await Send<object>("PUT", "/consultas/" + id + "/signos", new { presion_sistolica = 120 }, nurse)).Code == 422, "rechazo de presión incompleta");
                var s = await Send<Signos>("PUT", "/consultas/" + id + "/signos", new { presion_sistolica = 120, presion_diastolica = 80, temperatura = "36.7", peso = "70.2", saturacion = "98.5", glucosa = (string)null }, nurse);
                Check(s.Code == 200 && s.Data.temperatura == 36.7m && s.Data.glucosa == null, "medición parcial y decimales PostgreSQL");
            }
            else Check((await Send<Signos>("GET", "/consultas/" + c.Data.id + "/signos", token: nurse)).Data == null, "consulta sin mediciones");
            var ready = await Send<Consulta>("PATCH", "/consultas/" + c.Data.id + "/estado", new EstadoData { estado = "listo" }, nurse);
            Check(ready.Code == 200 && ready.Data.estado == "listo", "marcar listo");
        }
        var page1 = await Send<Pagina<Consulta>>("GET", "/consultas?estado=listo&pagina=1&tamano=5", token: doctor);
        var page2 = await Send<Pagina<Consulta>>("GET", "/consultas?estado=listo&pagina=2&tamano=5", token: doctor);
        Check(page1.Code == 200 && page1.Data.items.Count == 5 && page2.Data.items.Count == 2, "paginación real de consultas");
        Check((await Send<object>("POST", "/consultas/" + id + "/nota", new NotaData { contenido = "Ficticio" }, nurse)).Code == 403, "enfermería no escribe notas");
        var assigned = await Send<Consulta>("PATCH", "/consultas/" + id + "/estado", new EstadoData { estado = "en_atencion" }, doctor);
        Check(assigned.Code == 200 && assigned.Data.medico_id == me.Data.id, "toma y asignación atómica");
        Check((await Send<object>("GET", "/consultas/" + id, token: other)).Code == 403, "otro médico sin acceso a consulta tomada");
        Check((await Send<object>("PATCH", "/consultas/" + id + "/estado", new EstadoData { estado = "en_atencion" }, other)).Code == 403, "otro médico no puede tomar consulta asignada");
        var record = await Send<Expediente>("GET", "/pacientes/" + assigned.Data.paciente_id + "/expediente", token: doctor);
        Check(record.Code == 200 && record.Data.paciente.id == assigned.Data.paciente_id && record.Data.total_consultas == 1, "expediente vinculado");
        var vitals = await Send<Signos>("GET", "/consultas/" + id + "/signos", token: doctor);
        Check(vitals.Code == 200 && vitals.Data.peso == 70.2m && vitals.Data.glucosa == null, "lectura de signos por médico");
        Check((await Send<object>("PATCH", "/consultas/" + id + "/estado", new EstadoData { estado = "cerrado" }, doctor)).Code == 409, "no cerrar sin nota");
        Check((await Send<object>("POST", "/consultas/" + id + "/nota", new NotaData { contenido = "" }, doctor)).Code == 422, "no guardar nota vacía");
        var note = await Send<Nota>("POST", "/consultas/" + id + "/nota", new NotaData { contenido = "DEMOSTRACIÓN: nota ficticia para verificar el flujo." }, doctor);
        Check(note.Code == 201 && note.Data.version == 1, "guardar nota v1");
        Check((await Send<object>("PUT", "/consultas/" + id + "/nota", new NotaActualizarData { contenido = "Conflicto ficticio", version_esperada = 99 }, doctor)).Code == 409, "rechazo de versión desactualizada");
        var updated = await Send<Nota>("PUT", "/consultas/" + id + "/nota", new NotaActualizarData { contenido = "DEMOSTRACIÓN: nota ficticia actualizada y guardada.", version_esperada = 1 }, doctor);
        Check(updated.Code == 200 && updated.Data.version == 2, "guardar nota v2");
        var closed = await Send<Consulta>("PATCH", "/consultas/" + id + "/estado", new EstadoData { estado = "cerrado" }, doctor);
        Check(closed.Code == 200 && closed.Data.estado == "cerrado" && closed.Data.fecha_cierre != null, "cerrar con fecha y nota");
        Check(!ApiProtocol.CanWrite(closed.Data, me.Data), "cliente bloquea nota cerrada");
        Check((await Send<object>("PUT", "/consultas/" + id + "/nota", new NotaActualizarData { contenido = "Cambio prohibido", version_esperada = 2 }, doctor)).Code == 409, "servidor bloquea nota cerrada");
        Check((await Send<object>("PUT", "/consultas/" + id + "/signos", new { temperatura = "36.6" }, nurse)).Code == 409, "servidor bloquea signos cerrados");
        var audit = await Send<Pagina<object>>("GET", "/auditoria?tamano=100", token: admin);
        Check(audit.Code == 200 && audit.Data.total > 0, "auditoría disponible para admin");
        Check((await Send<object>("GET", "/auditoria", token: doctor)).Code == 403, "auditoría restringida");
        Check((await Send<object>("POST", "/auth/logout", token: doctor)).Code == 204, "logout revoca token");
        Check((await Send<object>("GET", "/auth/me", token: doctor)).Code == 401, "token revocado ya no accede");
        return id;
    }
    private static async Task Persistence(int id)
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("XR_EXPIRED_TOKEN")))
            Check((await Send<object>("GET", "/auth/me", token: Env("XR_EXPIRED_TOKEN"))).Code == 401, "servidor rechaza token vencido");
        string doctor = await Login(Env("XR_DOCTOR_EMAIL"), Env("XR_DOCTOR_PASS"));
        var c = await Send<Consulta>("GET", "/consultas/" + id, token: doctor);
        var n = await Send<Nota>("GET", "/consultas/" + id + "/nota", token: doctor);
        var s = await Send<Signos>("GET", "/consultas/" + id + "/signos", token: doctor);
        var p = await Send<Expediente>("GET", "/pacientes/" + c.Data.paciente_id + "/expediente", token: doctor);
        Check(c.Code == 200 && c.Data.estado == "cerrado", "consulta persiste tras reiniciar");
        Check(n.Code == 200 && n.Data.version == 2 && n.Data.contenido.EndsWith("actualizada y guardada."), "nota v2 persiste tras reiniciar");
        Check(s.Code == 200 && s.Data.temperatura == 36.7m && s.Data.glucosa == null, "signos parciales persisten");
        Check(p.Code == 200 && !string.IsNullOrWhiteSpace(p.Data.paciente.expediente), "paciente y expediente persisten");
    }
    private static async Task<int> Main(string[] args)
    {
        try
        {
            if (!ApiProtocol.TryBaseUrl(Env("XR_TEST_URL"), out var origin, out var error)) throw new Exception(error);
            client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(origin), Timeout = TimeSpan.FromSeconds(15) };
            ProtocolChecks();
            int id;
            if (args.Length == 2 && args[0] == "--persistencia") { id = int.Parse(args[1]); await Persistence(id); }
            else
            {
                if (Env("XR_TEST_ALLOW_WRITES") != "1") throw new Exception("Usa una base de pruebas desechable y XR_TEST_ALLOW_WRITES=1.");
                id = await Flow();
            }
            Console.WriteLine(ApiProtocol.Encode(new { resultado = "PASS", comprobaciones = checks, consulta_id = id }));
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
        finally { client?.Dispose(); }
    }
}
