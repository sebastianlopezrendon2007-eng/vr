using NUnit.Framework;
using Telemedicina;

public class ProtocolTests
{
    [TestCase("http://localhost:8000", true)]
    [TestCase("http://127.0.0.1:8000/api/", true)]
    [TestCase("https://ejemplo.invalid", true)]
    [TestCase("http://192.168.1.15:8000", false)]
    [TestCase("https://usuario:secreto@ejemplo.invalid", false)]
    [TestCase("https://ejemplo.invalid?token=abc", false)]
    [TestCase("https://ejemplo.invalid/otra-ruta", false)]
    [TestCase("file:///etc/passwd", false)]
    public void UrlRestringeCredencialesRutasYHttpRemoto(string value, bool valid)
    {
        Assert.That(ApiProtocol.TryBaseUrl(value, out _, out _), Is.EqualTo(valid));
    }
    [Test] public void SignosDecimalesYNulos()
    {
        var s = ApiProtocol.Decode<Signos>("{\"temperatura\":\"36.7\",\"saturacion\":98.5,\"glucosa\":null,\"peso\":\"70.2\"}");
        Assert.That(s.temperatura, Is.EqualTo(36.7m)); Assert.That(s.saturacion, Is.EqualTo(98.5m));
        Assert.That(s.glucosa, Is.Null); Assert.That(ApiProtocol.Medicion(s.glucosa, "mg/dL"), Is.EqualTo("No capturado"));
        Assert.That(ApiProtocol.Medicion(0, "%"), Is.EqualTo("0 %"));
    }
    [Test] public void NotaCerradaYOtroMedicoNoSonEditables()
    {
        var u = new Usuario { id = 1, rol = "medico", activo = true };
        var c = new Consulta { estado = "en_atencion", medico_id = 1 };
        Assert.That(ApiProtocol.CanWrite(c, u), Is.True);
        c.estado = "cerrado"; Assert.That(ApiProtocol.CanWrite(c, u), Is.False);
        c.estado = "en_atencion"; c.medico_id = 2; Assert.That(ApiProtocol.CanWrite(c, u), Is.False);
        u.rol = "administrador"; Assert.That(ApiProtocol.CanWrite(c, u), Is.False);
    }
    [Test] public void FechaConservaElInstanteYNuloNoEsCero()
    {
        var c = ApiProtocol.Decode<Consulta>("{\"fecha_apertura\":\"2026-10-03T04:00:00+00:00\",\"medico_id\":null}");
        Assert.That(c.fecha_apertura, Is.EqualTo("2026-10-03T04:00:00+00:00")); Assert.That(c.medico_id, Is.Null);
        Assert.That(ApiProtocol.Fecha(c.fecha_apertura), Does.Contain("/2026"));
    }
    [Test] public void BorradorSeComparaConVersionGuardada()
    {
        var nota = new Nota { contenido = "Texto ficticio", version = 2 };
        Assert.That(ApiProtocol.Dirty(" Texto ficticio ", nota), Is.False);
        Assert.That(ApiProtocol.Dirty("Otro texto ficticio", nota), Is.True);
    }
}
