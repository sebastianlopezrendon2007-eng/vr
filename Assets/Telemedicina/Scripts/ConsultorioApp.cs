using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Telemedicina
{
    public sealed class ConsultorioApp : MonoBehaviour
    {
        private const int PageSize = 5;
        private ApiClient api;
        private Camera view;
        private EducationalModel model;
        private RectTransform root, login, workspace, listEntries, bodyContent;
        private CanvasGroup loginGroup, workspaceGroup;
        private InputField serverInput, emailInput, passwordInput, noteInput;
        private Text status, userLabel, title, body, noteState, pager, listTitle;
        private ScrollRect detailScroll;
        private Button previous, next, take, save, close, reload;
        private Consulta selected;
        private Expediente record;
        private Signos vitals;
        private Nota savedNote;
        private bool busy;
        private int page = 1;
        private string filter = "listo";
        private GameObject modal;
        private float lastAspect;
        private QuestRig quest;

        private void Start()
        {
            Application.targetFrameRate = QuestRig.Active ? 72 : 30;
            QualitySettings.vSyncCount = 0; QualitySettings.antiAliasing = 0;
            QualitySettings.shadows = ShadowQuality.Disable;
            if (QuestRig.Active)
            {
                quest = new GameObject("Origen Quest").AddComponent<QuestRig>();
                view = quest.Create(transform);
            }
            else
            {
            view = Camera.main;
            if (!view) view = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            view.gameObject.tag = "MainCamera";
            view.transform.SetPositionAndRotation(new Vector3(0, 0, -10), Quaternion.identity);
            view.orthographic = true; view.nearClipPlane = .1f; view.farClipPlane = 30;
            view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = new Color32(8, 21, 31, 255);
            view.allowHDR = false; view.allowMSAA = false; view.useOcclusionCulling = false;
            FitCamera();
            }
            if (!EventSystem.current)
            {
                var events = new GameObject("Interacción de escritorio", typeof(EventSystem));
                events.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            api = gameObject.AddComponent<ApiClient>(); api.SessionExpired += OnSessionExpired;
            BuildUi();
            if (QuestRig.Active)
            {
                root.localScale = Vector3.one * .0016f;
                new GameObject("Teclado Quest", typeof(RectTransform)).AddComponent<QuestKeyboard>().Create(transform, view);
                quest.Recenter();
            }
            ShowStatus("");
        }
        private void FitCamera()
        {
            if (!view) return;
            lastAspect = view.aspect;
            view.orthographicSize = Mathf.Max(2f, 3.2f / Mathf.Max(.1f, lastAspect)) * 1.025f;
        }
        private void Update()
        {
            if (!QuestRig.Active && view && Mathf.Abs(view.aspect - lastAspect) > .001f) FitCamera();
            var keys = Keyboard.current;
            if (keys == null) return;
            if (!busy && !modal && login && login.gameObject.activeSelf && keys.enterKey.wasPressedThisFrame) LoginClick();
            if (modal && keys.escapeKey.wasPressedThisFrame) DismissModal();
        }
        private void BuildUi()
        {
            var canvasObj = new GameObject("Paneles del consultorio", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            canvasObj.transform.SetParent(transform, false);
            var canvas = canvasObj.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = view;
            root = (RectTransform)canvasObj.transform; root.sizeDelta = new Vector2(1280, 800); root.localScale = Vector3.one * .005f;
            Ui.Text(root, "Marca", "CONSULTORIO · XR", 26, 22, 430, 42, 29, Ui.Teal);
            Ui.Text(root, "Versión", "Beta 0.2", 26, 66, 910, 26, 17, Ui.Muted);
            userLabel = Ui.Text(root, "Usuario", "Acceso médico", 900, 26, 350, 34, 18);
            status = Ui.Text(root, "Estado de operación", "", 26, 742, 1228, 52, 18);
            BuildLogin(); BuildWorkspace(); BuildModel();
            workspace.gameObject.SetActive(false);
        }
        private void BuildLogin()
        {
            login = Ui.Panel("Acceso", root, 26, 120, 808, 594);
            loginGroup = login.gameObject.AddComponent<CanvasGroup>();
            Ui.Text(login, "Título", "Iniciar sesión", 28, 26, 752, 42, 27);
            Ui.Text(login, "Servidor", "Servidor", 28, 150, 500, 26);
            serverInput = Ui.Input(login, "Dirección", "http://localhost:8000", 28, 180, 752, 43);
            serverInput.text = "http://localhost:8000";
            Ui.Text(login, "Correo", "Correo electrónico", 28, 244, 500, 26);
            emailInput = Ui.Input(login, "Correo", "correo@ejemplo.com", 28, 274, 752, 43);
            Ui.Text(login, "Contraseña", "Contraseña", 28, 338, 500, 26);
            passwordInput = Ui.Input(login, "Contraseña", "Contraseña", 28, 368, 752, 43, true);
            Ui.Button(login, "Iniciar sesión", 28, 445, 355, 46, LoginClick, true);
            Ui.Button(login, "Abrir módulo web", 405, 445, 375, 46, OpenWeb);
        }
        private void BuildWorkspace()
        {
            workspace = Ui.Rect("Trabajo del médico", root, 0, 0, 850, 730);
            workspaceGroup = workspace.gameObject.AddComponent<CanvasGroup>();
            var left = Ui.Panel("Consultas", workspace, 26, 120, 242, 594);
            listTitle = Ui.Text(left, "Título", "Consultas", 14, 16, 216, 28, 23);
            Ui.Button(left, "Listos", 14, 58, 67, 37, () => ChooseFilter("listo"));
            Ui.Button(left, "En atención", 87, 58, 141, 37, () => ChooseFilter("en_atencion"));
            Ui.Button(left, "Mis cerradas", 14, 104, 214, 37, () => ChooseFilter("cerrado"));
            Ui.Button(left, "Actualizar lista", 14, 150, 214, 36, () => Begin(LoadList()));
            listEntries = Ui.Rect("Registros", left, 14, 201, 214, 333);
            previous = Ui.Button(left, "Anterior", 14, 543, 102, 36, () => ChangePage(-1));
            next = Ui.Button(left, "Siguiente", 126, 543, 102, 36, () => ChangePage(1));
            pager = Ui.Text(left, "Paginación", "", 14, 509, 214, 26, 16, Ui.Muted);
            var center = Ui.Panel("Expediente", workspace, 284, 120, 550, 594);
            title = Ui.Text(center, "Título", "Selecciona una consulta", 16, 16, 518, 32, 24);
            detailScroll = Ui.Scroll(center, "Lectura del expediente", 16, 58, 518, 291, out bodyContent);
            body = Ui.Text(bodyContent, "Datos", "Selecciona una consulta.", 12, 10, 481, 270, 19);
            Ui.Text(center, "Nota", "Nota médica", 16, 363, 150, 28, 20);
            reload = Ui.Button(center, "Recargar expediente", 280, 357, 254, 35, ReloadClick);
            noteInput = Ui.Input(center, "Nota del médico", "Toma la consulta para escribir una nota.", 16, 399, 518, 108, false, true);
            noteInput.onValueChanged.AddListener(_ => UpdateState());
            noteState = Ui.Text(center, "Estado de la nota", "", 16, 514, 518, 26, 16, Ui.Muted);
            take = Ui.Button(center, "Tomar consulta", 16, 549, 518, 36, () => Begin(TakeConsult()), true);
            save = Ui.Button(center, "Guardar nota", 16, 549, 249, 36, SaveClick, true);
            close = Ui.Button(center, "Cerrar consulta", 280, 549, 254, 36, CloseClick);
            Ui.Button(root, "Cerrar sesión", 1094, 67, 160, 34, LogoutClick);
            ClearConsult();
        }
        private void BuildModel()
        {
            var panel = Ui.Panel("Modelo educativo", root, 850, 120, 404, 594);
            Ui.Text(panel, "Título", "Explorar en 3D", 20, 20, 365, 34, 25, Ui.Teal);
            Ui.Text(panel, "Descripción", "Columna lumbar · modelo educativo", 20, 64, 365, 30, 19);
            Ui.Text(panel, "Ayuda", "Usa Girar, Alejar y Acercar\nMenú del control izquierdo: centrar", 20, 108, 365, 55, 17, Ui.Muted);
            var modelObject = new GameObject("Modelo educativo independiente del paciente");
            modelObject.transform.SetParent(transform, false); modelObject.transform.localPosition = new Vector3(2.06f, -.04f, -.7f);
            modelObject.transform.localScale = Vector3.one * .57f;
            if (QuestRig.Active)
            {
                modelObject.transform.localPosition *= .32f;
                modelObject.transform.localScale *= .32f;
            }
            model = modelObject.AddComponent<EducationalModel>(); model.Create(view);
            Ui.Text(panel, "Leyenda", "Hueso     Discos     Trayecto nervioso", 20, 411, 365, 26, 16, Ui.Teal);
            Ui.Button(panel, "Girar ←", 20, 448, 113, 34, () => model.Rotate(-20));
            Ui.Button(panel, "Girar →", 145, 448, 113, 34, () => model.Rotate(20));
            Ui.Button(panel, "Reiniciar", 270, 448, 114, 34, () => model.ResetView());
            Ui.Button(panel, "Alejar −", 20, 492, 176, 34, () => model.Zoom(-.1f));
            Ui.Button(panel, "Acercar +", 208, 492, 176, 34, () => model.Zoom(.1f));
        }
        private void Begin(IEnumerator operation)
        {
            if (busy || modal) return;
            StartCoroutine(Run(operation));
        }
        private IEnumerator Run(IEnumerator operation)
        {
            SetBusy(true);
            try { yield return operation; }
            finally { SetBusy(false); }
        }
        private void SetBusy(bool value)
        {
            busy = value;
            if (loginGroup) loginGroup.interactable = !busy && !modal;
            if (workspaceGroup) workspaceGroup.interactable = !busy && !modal;
        }
        private void ShowStatus(string message, bool error = false)
        {
            if (!status) return;
            status.text = message; status.color = error ? new Color32(255, 169, 146, 255) : Ui.Ink;
        }
        private bool Failed<T>(ApiResult<T> result)
        {
            if (result != null && result.Ok) return false;
            ShowStatus(result?.Message ?? "No se pudo completar la operación.", true); return true;
        }
        private void LoginClick()
        {
            if (busy || modal) return;
            if (!ApiProtocol.TryBaseUrl(serverInput.text, out var url, out var error)) { ShowStatus(error, true); return; }
            if (string.IsNullOrWhiteSpace(emailInput.text) || string.IsNullOrEmpty(passwordInput.text))
            { ShowStatus("Ingresa correo y contraseña.", true); return; }
            QuestKeyboard.Close();
            api.Configure(url);
            var credentials = new LoginData { correo = emailInput.text.Trim(), password = passwordInput.text };
            passwordInput.SetTextWithoutNotify("");
            Begin(Login(credentials));
        }
        private IEnumerator Login(LoginData credentials)
        {
            ShowStatus("Conectando…");
            ApiResult<HealthData> health = null;
            yield return api.Send<HealthData>("GET", "/health", null, result => health = result, false);
            if (Failed(health)) { credentials.password = null; yield break; }
            if (health.Data == null || health.Data.status != "ok" || health.Data.database != "ok")
            { credentials.password = null; ShowStatus("El servidor no está disponible.", true); yield break; }
            ApiResult<TokenData> token = null;
            yield return api.Send<TokenData>("POST", "/auth/login", credentials, result => token = result, false);
            credentials.password = null;
            if (token != null && token.Code == 401) { ShowStatus("Correo o contraseña incorrectos, o usuario inactivo.", true); yield break; }
            if (Failed(token)) yield break;
            bool valid = true;
            try { api.Authenticate(token.Data); } catch (Exception) { valid = false; }
            if (!valid) { api.ClearSession(); ShowStatus("No se pudo iniciar sesión.", true); yield break; }
            ApiResult<Usuario> me = null;
            yield return api.Send<Usuario>("GET", "/auth/me", null, result => me = result);
            if (Failed(me)) { api.ClearSession(); yield break; }
            if (me.Data == null || !me.Data.activo || me.Data.rol != "medico")
            { api.ClearSession(); ShowStatus("Se requiere una cuenta de médico.", true); yield break; }
            api.SetUser(me.Data); login.gameObject.SetActive(false); workspace.gameObject.SetActive(true);
            userLabel.text = me.Data.nombre + " · Médico";
            page = 1; filter = "listo"; ClearConsult();
            yield return LoadList();
        }
        private void OpenWeb()
        {
            if (!ApiProtocol.TryBaseUrl(serverInput.text, out var url, out var error)) { ShowStatus(error, true); return; }
            Application.OpenURL(url);
        }
        private void ChooseFilter(string value)
        {
            if (busy || modal || api.User == null) return;
            ConfirmDiscard(() => { filter = value; page = 1; ClearConsult(); Begin(LoadList()); });
        }
        private void ChangePage(int direction)
        {
            if (busy || modal) return;
            page = Mathf.Max(1, page + direction); Begin(LoadList());
        }
        private IEnumerator LoadList()
        {
            ShowStatus("Cargando consultas…"); previous.interactable = next.interactable = false;
            Ui.Clear(listEntries);
            ApiResult<Pagina<Consulta>> result = null;
            yield return api.Send<Pagina<Consulta>>("GET", "/consultas?estado=" + filter + "&pagina=" + page + "&tamano=" + PageSize, null, r => result = r);
            if (Failed(result)) yield break;
            var data = result.Data;
            if (data?.items == null) { ShowStatus("No se pudieron cargar las consultas.", true); yield break; }
            if (data.items.Count == 0 && page > 1) { page--; yield return LoadList(); yield break; }
            listTitle.text = filter == "listo" ? "Listos" : filter == "cerrado" ? "Mis cerradas" : "En atención";
            pager.text = "Página " + page + " · " + data.total + " consultas";
            previous.interactable = page > 1; next.interactable = page * PageSize < data.total;
            int i = 0;
            foreach (var consulta in data.items)
            {
                int id = consulta.id;
                string label = "Consulta #" + id + " · Paciente #" + consulta.paciente_id + "\n" + ApiProtocol.Fecha(consulta.fecha_apertura);
                Ui.Button(listEntries, label, 0, i++ * 65, 214, 57, () => ConfirmDiscard(() => Begin(LoadConsult(id))));
            }
            if (data.items.Count == 0)
                Ui.Text(listEntries, "Sin registros", filter == "listo" ? "No hay consultas listas." : "No hay consultas en este estado.", 4, 6, 206, 240, 19, Ui.Muted);
            ShowStatus(data.total == 0 ? "Sin consultas para mostrar." : "Selecciona una consulta.");
        }
        private void ReloadClick()
        {
            if (busy || modal || selected == null) return;
            int id = selected.id; ConfirmDiscard(() => Begin(LoadConsult(id)));
        }
        private IEnumerator LoadConsult(int id)
        {
            ClearConsult(); ShowStatus("Cargando expediente…");
            ApiResult<Consulta> c = null;
            yield return api.Send<Consulta>("GET", "/consultas/" + id, null, r => c = r);
            if (Failed(c)) yield break;
            if (c.Data == null || c.Data.id != id) { ShowStatus("No se pudo cargar la consulta.", true); yield break; }
            ApiResult<Expediente> p = null;
            yield return api.Send<Expediente>("GET", "/pacientes/" + c.Data.paciente_id + "/expediente?pagina=1&tamano=20", null, r => p = r);
            if (Failed(p)) yield break;
            ApiResult<Signos> s = null;
            yield return api.Send<Signos>("GET", "/consultas/" + id + "/signos", null, r => s = r);
            if (Failed(s)) yield break;
            ApiResult<Nota> n = null;
            yield return api.Send<Nota>("GET", "/consultas/" + id + "/nota", null, r => n = r);
            if (Failed(n)) yield break;
            if (p.Data?.paciente == null || p.Data.paciente.id != c.Data.paciente_id)
            { ShowStatus("El expediente recibido no corresponde a la consulta.", true); yield break; }
            selected = c.Data; record = p.Data; vitals = s.Data; savedNote = n.Data;
            noteInput.SetTextWithoutNotify(savedNote?.contenido ?? "");
            RenderRecord(); UpdateState();
            ShowStatus(selected.estado == "cerrado" ? "Consulta cerrada · Solo lectura." : "Expediente cargado.");
        }
        private void RenderRecord()
        {
            var p = record.paciente;
            title.text = "Expediente " + p.expediente;
            var text = new StringBuilder();
            text.AppendLine(p.NombreCompleto).AppendLine("Nacimiento: " + p.fecha_nacimiento + " · Sexo: " + p.sexo);
            text.AppendLine("Comunidad: " + ApiProtocol.Dato(p.comunidad));
            text.AppendLine("Municipio: " + ApiProtocol.Dato(p.municipio) + " · Estado: " + ApiProtocol.Dato(p.estado));
            text.AppendLine("Teléfono: " + ApiProtocol.Dato(p.telefono) + " · Sangre: " + ApiProtocol.Dato(p.tipo_sangre));
            text.AppendLine().AppendLine("CONSULTA #" + selected.id + " · " + ApiProtocol.Estado(selected.estado));
            text.AppendLine("Apertura: " + ApiProtocol.Fecha(selected.fecha_apertura));
            if (!string.IsNullOrEmpty(selected.fecha_inicio)) text.AppendLine("Inicio de atención: " + ApiProtocol.Fecha(selected.fecha_inicio));
            if (!string.IsNullOrEmpty(selected.fecha_cierre)) text.AppendLine("Cierre: " + ApiProtocol.Fecha(selected.fecha_cierre));
            text.AppendLine("Motivo: " + selected.motivo).AppendLine().AppendLine("SIGNOS VITALES");
            if (vitals == null) text.AppendLine("Sin signos vitales registrados.");
            else
            {
                text.AppendLine("Medición: " + ApiProtocol.Fecha(vitals.fecha_medicion));
                text.AppendLine("Presión sistólica: " + ApiProtocol.Medicion(vitals.presion_sistolica, "mmHg"));
                text.AppendLine("Presión diastólica: " + ApiProtocol.Medicion(vitals.presion_diastolica, "mmHg"));
                text.AppendLine("Frecuencia cardiaca: " + ApiProtocol.Medicion(vitals.frecuencia_cardiaca, "lat/min"));
                text.AppendLine("Frecuencia respiratoria: " + ApiProtocol.Medicion(vitals.frecuencia_respiratoria, "resp/min"));
                text.AppendLine("Saturación: " + ApiProtocol.Medicion(vitals.saturacion, "%"));
                text.AppendLine("Temperatura: " + ApiProtocol.Medicion(vitals.temperatura, "°C"));
                text.AppendLine("Glucosa: " + ApiProtocol.Medicion(vitals.glucosa, "mg/dL"));
                text.AppendLine("Peso: " + ApiProtocol.Medicion(vitals.peso, "kg"));
                text.AppendLine("Talla: " + ApiProtocol.Medicion(vitals.talla, "cm"));
            }
            text.AppendLine().AppendLine("HISTORIAL DE CONSULTAS: " + record.total_consultas);
            if (record.consultas != null) foreach (var history in record.consultas)
                text.AppendLine("#" + history.id + " · " + ApiProtocol.Estado(history.estado) + " · " + ApiProtocol.Fecha(history.fecha_apertura));
            if (record.total_consultas > 20) text.AppendLine("Últimas 20 consultas. Historial completo en el módulo web.");
            body.text = text.ToString();
            float height = Mathf.Max(271, body.preferredHeight + 12);
            body.rectTransform.sizeDelta = new Vector2(481, height);
            bodyContent.sizeDelta = new Vector2(bodyContent.sizeDelta.x, height + 20);
            detailScroll.verticalNormalizedPosition = 1;
        }
        private void ClearConsult()
        {
            selected = null; record = null; vitals = null; savedNote = null;
            if (!noteInput) return;
            noteInput.SetTextWithoutNotify(""); title.text = "Selecciona una consulta";
            body.text = "Selecciona una consulta.";
            body.rectTransform.sizeDelta = new Vector2(481, 271); bodyContent.sizeDelta = new Vector2(bodyContent.sizeDelta.x, 291);
            UpdateState();
        }
        private void UpdateState()
        {
            if (!noteInput) return;
            bool writable = ApiProtocol.CanWrite(selected, api.User);
            bool canTake = ApiProtocol.CanTake(selected, api.User);
            noteInput.readOnly = !writable;
            take.gameObject.SetActive(canTake); save.gameObject.SetActive(writable); close.gameObject.SetActive(writable);
            reload.interactable = selected != null;
            save.interactable = writable && !string.IsNullOrWhiteSpace(noteInput.text) && ApiProtocol.Dirty(noteInput.text, savedNote);
            close.interactable = writable && savedNote != null && !ApiProtocol.Dirty(noteInput.text, savedNote);
            if (selected == null) noteState.text = "Selecciona una consulta.";
            else if (selected.estado == "cerrado") noteState.text = "Solo lectura · Nota v" + (savedNote?.version.ToString() ?? "—");
            else if (!writable) noteState.text = "Toma la consulta para escribir la nota.";
            else if (ApiProtocol.Dirty(noteInput.text, savedNote)) noteState.text = "Borrador sin guardar · " + noteInput.text.Length + "/10000 caracteres";
            else noteState.text = savedNote == null ? "Escribe una nota antes de cerrar." : "Nota guardada · Versión " + savedNote.version;
        }
        private IEnumerator TakeConsult()
        {
            if (!ApiProtocol.CanTake(selected, api.User)) yield break;
            int id = selected.id; ShowStatus("Iniciando atención…");
            ApiResult<Consulta> result = null;
            yield return api.Send<Consulta>("PATCH", "/consultas/" + id + "/estado", new EstadoData { estado = "en_atencion" }, r => result = r);
            if (Failed(result)) yield break;
            yield return LoadConsult(id);
            if (selected != null) yield return LoadList();
            if (selected != null) ShowStatus("Consulta en atención.");
        }
        private void SaveClick()
        {
            if (busy || modal) return;
            if (!ApiProtocol.CanWrite(selected, api.User)) { ShowStatus("Esta consulta no permite escribir una nota.", true); return; }
            if (string.IsNullOrWhiteSpace(noteInput.text)) { ShowStatus("Escribe el contenido de la nota.", true); return; }
            Begin(SaveNote());
        }
        private IEnumerator SaveNote()
        {
            int id = selected.id; string content = noteInput.text.Trim();
            if (content.Length == 0 || content.Length > 10000) { ShowStatus("La nota debe tener de 1 a 10000 caracteres.", true); yield break; }
            ShowStatus("Guardando nota…");
            ApiResult<Nota> result = null;
            object data = savedNote == null ? (object)new NotaData { contenido = content } :
                new NotaActualizarData { contenido = content, version_esperada = savedNote.version };
            yield return api.Send<Nota>(savedNote == null ? "POST" : "PUT", "/consultas/" + id + "/nota", data, r => result = r);
            if (Failed(result)) yield break;
            if (result.Data == null || result.Data.consulta_id != id) { ShowStatus("No se pudo confirmar el guardado. Recarga el expediente.", true); yield break; }
            savedNote = result.Data; noteInput.SetTextWithoutNotify(savedNote.contenido); UpdateState();
            ShowStatus("Nota guardada · Versión " + savedNote.version + ".");
        }
        private void CloseClick()
        {
            if (busy || modal || !ApiProtocol.CanWrite(selected, api.User)) return;
            if (savedNote == null || ApiProtocol.Dirty(noteInput.text, savedNote)) { ShowStatus("Guarda la nota y sus cambios antes de cerrar.", true); return; }
            ShowModal("Cerrar consulta", "¿Cerrar esta consulta? La nota quedará en solo lectura.", "Confirmar cierre", () => Begin(CloseConsult()));
        }
        private IEnumerator CloseConsult()
        {
            if (!ApiProtocol.CanWrite(selected, api.User) || savedNote == null || ApiProtocol.Dirty(noteInput.text, savedNote)) yield break;
            int id = selected.id; ShowStatus("Cerrando consulta…");
            ApiResult<Consulta> result = null;
            yield return api.Send<Consulta>("PATCH", "/consultas/" + id + "/estado", new EstadoData { estado = "cerrado" }, r => result = r);
            if (Failed(result)) yield break;
            filter = "cerrado"; page = 1;
            yield return LoadConsult(id);
            if (selected != null) yield return LoadList();
            if (selected?.estado == "cerrado") ShowStatus("Consulta cerrada · Nota guardada.");
        }
        private bool HasDirtyNote() => ApiProtocol.CanWrite(selected, api.User) && ApiProtocol.Dirty(noteInput.text, savedNote);
        private void ConfirmDiscard(Action proceed)
        {
            if (busy || modal) return;
            if (!HasDirtyNote()) { proceed(); return; }
            ShowModal("Cambios sin guardar", "¿Descartar los cambios de la nota?", "Descartar cambios", proceed);
        }
        private void LogoutClick()
        {
            if (busy || modal || api.User == null) return;
            ConfirmDiscard(() => Begin(Logout()));
        }
        private IEnumerator Logout()
        {
            ApiResult<object> result = null;
            yield return api.Send<object>("POST", "/auth/logout", null, r => result = r);
            ResetSession();
            ShowStatus(result != null && result.Ok ? "Sesión cerrada." : "Sesión cerrada en este equipo. No se pudo confirmar el cierre en el servidor.", result == null || !result.Ok);
        }
        private void OnSessionExpired()
        {
            ResetSession(); ShowStatus(ApiProtocol.ErrorMessage(401), true);
        }
        private void ResetSession()
        {
            QuestKeyboard.Close();
            api.ClearSession();
            if (modal) DismissModal();
            ClearConsult(); Ui.Clear(listEntries); pager.text = "";
            passwordInput.SetTextWithoutNotify(""); userLabel.text = "Acceso médico";
            workspace.gameObject.SetActive(false); login.gameObject.SetActive(true);
            EventSystem.current?.SetSelectedGameObject(null);
        }
        private void ShowModal(string heading, string message, string confirm, Action action)
        {
            if (modal) return;
            modal = new GameObject("Confirmación", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            modal.transform.SetParent(transform, false);
            var canvas = modal.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
            var scaler = modal.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 800); scaler.matchWidthOrHeight = .5f;
            if (QuestRig.Active)
            {
                QuestKeyboard.Close(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = view;
                var rect = (RectTransform)modal.transform; rect.sizeDelta = new Vector2(1280, 800);
                rect.localScale = Vector3.one * .0016f; rect.localPosition = new Vector3(0, 0, -.04f);
                scaler.enabled = false;
            }
            var cover = Ui.Panel("Fondo", modal.transform, 0, 0, 1280, 800, new Color(0, .035f, .06f, .86f), true);
            cover.anchorMin = Vector2.zero; cover.anchorMax = Vector2.one; cover.offsetMin = cover.offsetMax = Vector2.zero;
            var box = Ui.Panel("Diálogo", modal.transform, 0, 0, 570, 270);
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(.5f, .5f); box.anchoredPosition = Vector2.zero;
            Ui.Text(box, "Título", heading, 24, 22, 522, 38, 25);
            Ui.Text(box, "Mensaje", message, 24, 80, 522, 106, 20);
            Ui.Button(box, "Cancelar", 24, 212, 210, 38, DismissModal);
            Ui.Button(box, confirm, 250, 212, 296, 38, () => { DismissModal(); action(); }, true);
            model.InteractionEnabled = false; SetBusy(busy);
        }
        private void DismissModal()
        {
            if (!modal) return;
            modal.SetActive(false); Destroy(modal); modal = null;
            model.InteractionEnabled = true; SetBusy(busy);
        }
        private void OnApplicationPause(bool paused)
        {
            if (paused && QuestRig.Active && api && login && listEntries) ResetSession();
        }
        private void OnDestroy()
        {
            if (api) api.SessionExpired -= OnSessionExpired;
            if (quest) Destroy(quest.gameObject);
        }
    }
}
