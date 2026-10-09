import importlib.util, tempfile, os, zipfile, pathlib, json
from unittest.mock import patch
from subprocess import CompletedProcess
spec=importlib.util.spec_from_file_location('quest_launcher',str(pathlib.Path(__file__).resolve().with_name('quest.py'))); q=importlib.util.module_from_spec(spec);spec.loader.exec_module(q)
checks=[]
def expect_error(fn, message):
 try: fn()
 except RuntimeError as e:
  if message.lower() not in str(e).lower(): raise AssertionError(str(e))
 else: raise AssertionError('No rechazó: '+message)
def apk(path, bits='arm64-v8a'):
 with zipfile.ZipFile(path,'w') as z:
  z.writestr('AndroidManifest.xml',b'test fixture');z.writestr('lib/'+bits+'/libunity.so',b'test fixture');z.writestr('lib/'+bits+'/libil2cpp.so',b'test fixture')
with tempfile.TemporaryDirectory(prefix='Quest con espacios ñ ') as tmp:
 root=pathlib.Path(tmp);(root/'ProjectSettings').mkdir();(root/'ProjectSettings/ProjectVersion.txt').write_text('m_EditorVersion: 6000.6.4f1\n');editor=root/'Unity';editor.touch();sdk=root/'Data/PlaybackEngines/AndroidPlayer';sdk.mkdir(parents=True)
 with patch.object(q,'editor_path',return_value=editor), patch.object(q,'PROJECT',root),patch.object(q,'APK',root/'test.apk'):
  with patch.object(q,'run') as run:
   expect_error(q.compile_android,'Faltan módulos');assert run.call_count==0
  checks.append('SDK ausente se rechaza antes de iniciar compilación')
  for n in ['SDK','NDK','OpenJDK']:(sdk/n).mkdir()
  with patch.object(q,'run',side_effect=RuntimeError('Fallo Unity')) as run:
   expect_error(q.compile_android,'Fallo Unity');assert run.call_count==1
  checks.append('Preparación fallida no continúa al build')
  with patch.object(q,'run',return_value=''):
   expect_error(q.compile_android,'no generó')
  checks.append('Unity con código cero sin APK se rechaza')
  apk(q.APK);os.utime(q.APK,(1,1))
  with patch.object(q,'run',return_value=''):
   expect_error(q.compile_android,'APK nuevo')
  checks.append('Un APK anterior no se informa como nuevo')
  q.APK.write_bytes(b'not an apk');expect_error(q.check_apk,'válido')
  checks.append('Archivo corrupto no se acepta como APK')
  apk(q.APK,'armeabi-v7a');expect_error(q.check_apk,'ARM64')
  checks.append('APK sin ARM64/IL2CPP no se instala')
  def build_fixture(command,**kwargs):
   assert command[command.index('-projectPath')+1]==str(root)
   if 'Telemedicina.Editor.QuestEditor.BuildAndroidBatch' in command:
    assert command[command.index('-buildTarget')+1]=='Android';apk(q.APK)
   return ''
  with patch.object(q,'run',side_effect=build_fixture):q.compile_android()
  info=json.loads((root/'build-info.json').read_text());assert info['probado_en_quest']==False and info['sha256']==q.check_apk()
  checks.append('Ruta con espacios conservada; comando Android y hash estructural verificados con fixture, no Unity')
  with patch.object(q,'adb_path',return_value='adb'):
   with patch.object(q,'run',return_value='List of devices attached\nABC unauthorized\n') as run:
    expect_error(q.device,'depuración');assert run.call_count==1
   checks.append('Dispositivo no autorizado no recibe comandos')
   with patch.object(q,'run',return_value='List of devices attached\nABC device\nXYZ device\n') as run:
    expect_error(q.device,'exactamente un');assert run.call_count==1
   checks.append('Varios dispositivos se rechazan')
   with patch.object(q,'run',side_effect=['List of devices attached\nABC device\n','Phone']) as run:
    expect_error(q.device,'no es un Quest');assert run.call_count==2
   checks.append('Un teléfono Android se rechaza')
   commands=[]
   def adb(command,**kwargs):
    commands.append(command)
    if command[-1]=='devices':return 'List of devices attached\nQUESTSERIAL device\n'
    if 'getprop' in command:return 'Quest 3\n'
    if 'install' in command:return 'Success\n'
    return ''
   with patch.object(q,'run',side_effect=adb):q.install()
   assert any(c[1:3]==['-s','QUESTSERIAL'] and 'reverse' in c for c in commands)
   assert any('install' in c and c[-1]==str(q.APK) for c in commands)
   checks.append('Instalador dirige APK y reverse al Quest seleccionado; adb simulado')
   with patch.object(q,'device',return_value=['adb','-s','QUESTSERIAL']),patch.object(q,'run',return_value='Failure\n') as run:
    expect_error(q.install,'no confirmó');assert run.call_count==1
   checks.append('Instalación sin Success no abre app ni conecta puerto')
print('Comprobaciones launcher:',len(checks),'aprobadas')
pathlib.Path('launcher-results.json').write_text(json.dumps({'checks':checks,'unity_real':False,'adb_real':False,'apk_fixture_no_entregado':True},ensure_ascii=False,indent=2)+'\n')
