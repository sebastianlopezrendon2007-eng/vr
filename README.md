# Unity · Consultorio V3

Lee **`../README.md`** para iniciar el backend y preparar el Quest.

Agrega esta carpeta `Unity_Quest3` en Unity Hub con **6000.6.4f1**. Abre **Telemedicina → Abrir consultorio**, pulsa **Play** y entra con una cuenta de médico. La escena `Assets/Scenes/Consultorio.unity` utiliza `SpatialClinicApp`: tres Canvas en espacio 3D, modelo anatómico, videollamada y expediente. El contenido se construye en Play.

`ConsultorioApp` y `EducationalModel` conservan el cliente anterior como referencia; la escena V3 usa `SpatialClinicApp` y `AnatomyModel`.

Para Android, usa **Telemedicina → Quest 3 → 1. Preparar Android**, espera y después **2. Crear APK**. El APK se genera en `Builds/Quest3/ConsultorioQuest3.apk`. No se incluye un APK precompilado; la importación y plugins nativos requieren la primera prueba en tu editor y visor.

En Quest, el fondo usa passthrough a color bajo las tres pantallas. Los controles Touch Plus y las manos apuntan con rayo; gatillo y pellizco seleccionan, y las listas se desplazan arrastrando. Los controles conservan el scroll con palanca. La entrada activa cambia entre manos y controles y los rayos inactivos se ocultan. El botón de menú de cualquiera de los dos controles centra las pantallas.

Para crear e instalar desde Zorin/Ubuntu, cierra el Editor de Unity y conecta un Quest con modo desarrollador y depuración USB autorizada:

```bash
cd ~/Escritorio/Consultorio_Quest3_V3/Unity_Quest3
bash crear_apk.sh
bash instalar_quest.sh
```

También se puede abrir el proyecto con `bash abrir_demo.sh`. El primer script requiere Unity **6000.6.4f1** con Android Build Support, SDK, NDK y OpenJDK. El APK se guarda en `Builds/Quest3/ConsultorioQuest3.apk`. Para el backend local por USB, la instalación crea `adb reverse tcp:8000 tcp:8000`; usa `http://localhost:8000` en el inicio de sesión.

Requisitos, resultados y límites: `../docs/VERIFICACION.md`. Prueba física: `../docs/PRUEBA_QUEST.md`.
