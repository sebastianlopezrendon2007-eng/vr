using System;using System.Collections;using System.Collections.Concurrent;using System.Collections.Generic;using System.IO;using System.Net.WebSockets;using System.Text;using System.Threading;using System.Threading.Tasks;
using UnityEngine;using Unity.WebRTC;
namespace Telemedicina {
 [Serializable] public class IceConfig {public RTCIceServer[] iceServers;}
 [Serializable] public class IceData {public string candidate,sdpMid;public int? sdpMLineIndex;}
 [Serializable] public class CallSignal {public string type,sdp,message;public bool offerer;public int code;public IceData candidate;public Usuario peer;}
 public sealed class QuestCall:MonoBehaviour {
  public event Action<string> State;public event Action<Texture> RemoteVideo,LocalVideo;public event Action<Usuario> Peer;
  public bool SharingModel{get;private set;}public bool Active{get;private set;}public bool Muted{get;private set;}public bool VideoEnabled{get;private set;}=true;
  private ApiClient api;private RTCPeerConnection pc;private ClientWebSocket socket;private CancellationTokenSource cancel;private int generation;private bool processing,remoteSet;
  private readonly ConcurrentQueue<Action> main=new ConcurrentQueue<Action>();private readonly Queue<CallSignal> incoming=new Queue<CallSignal>();private readonly List<IceData> candidates=new List<IceData>();private readonly SemaphoreSlim sendLock=new SemaphoreSlim(1,1);
  private AudioSource microphone,speaker;private string device;private AudioClip clip;private WebCamTexture webcam;private AudioStreamTrack micTrack,remoteAudio;private VideoStreamTrack videoTrack,remoteVideo;private Coroutine pump;private float timeout;private Texture modelTexture,sourceTexture;private RenderTexture encodeTexture;
  public void Initialize(ApiClient client){api=client;}
  private void Update(){if(Active&&sourceTexture&&encodeTexture)Graphics.Blit(sourceTexture,encodeTexture);while(main.TryDequeue(out var a))a();if(Active&&!processing&&incoming.Count>0)StartCoroutine(Process(incoming.Dequeue()));if(Active&&!api.HasToken)End();if(Active&&timeout>0&&Time.realtimeSinceStartup>timeout){End();State?.Invoke("No se pudo conectar. Revisa la red o configura TURN.");}}
  private bool Current(int g)=>Active&&g==generation;
  public void Join(Consulta c,Texture model){if(Active||!ApiProtocol.CanWrite(c,api.User)){State?.Invoke("Primero toma la consulta.");return;}Active=true;StartCoroutine(Setup(c.id,model,++generation));}
  private IEnumerator Setup(int id,Texture model,int g){
   State?.Invoke("Preparando micrófono…");
#if UNITY_ANDROID && !UNITY_EDITOR
   if(!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone)){UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);float limit=Time.realtimeSinceStartup+20;while(Current(g)&&Time.realtimeSinceStartup<limit&&!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))yield return null;}
#else
   yield return Application.RequestUserAuthorization(UserAuthorization.Microphone|UserAuthorization.WebCam);
#endif
   if(!Current(g))yield break;ApiResult<IceConfig> config=null;yield return api.Send<IceConfig>("GET","/llamadas/config",null,r=>config=r);if(!Current(g))yield break;
   if(config==null||!config.Ok){End();State?.Invoke(config?.Message??"No se pudo configurar la llamada.");yield break;}
   bool ok=true;try{
    pump=StartCoroutine(WebRTC.Update());var conf=new RTCConfiguration{iceServers=config.Data?.iceServers??new RTCIceServer[0]};pc=new RTCPeerConnection(ref conf);
    pc.OnIceCandidate=c=>{if(Current(g))Send(new CallSignal{type="candidate",candidate=new IceData{candidate=c.Candidate,sdpMid=c.SdpMid,sdpMLineIndex=c.SdpMLineIndex}},g);};
    pc.OnConnectionStateChange=s=>{if(!Current(g))return;if(s==RTCPeerConnectionState.Connected){timeout=0;State?.Invoke("En llamada");}if(s==RTCPeerConnectionState.Failed||s==RTCPeerConnectionState.Disconnected){End();State?.Invoke("Conexión interrumpida. Vuelve a conectar.");}};
    pc.OnTrack=e=>{if(!Current(g))return;if(e.Track is VideoStreamTrack v){remoteVideo=v;v.OnVideoReceived+=t=>{if(Current(g))RemoteVideo?.Invoke(t);};}if(e.Track is AudioStreamTrack a){remoteAudio=a;speaker=new GameObject("Audio remoto").AddComponent<AudioSource>();speaker.transform.SetParent(transform,false);speaker.SetTrack(a);speaker.loop=true;speaker.Play();}};
   }catch(Exception){ok=false;}if(!ok){End();State?.Invoke("WebRTC no inició. Revisa las dependencias y Console.");yield break;}
   if(Microphone.devices.Length>0){device=Microphone.devices[0];clip=Microphone.Start(device,true,1,48000);float limit=Time.realtimeSinceStartup+5;while(Current(g)&&Time.realtimeSinceStartup<limit&&Microphone.GetPosition(device)<=0)yield return null;if(!Current(g))yield break;if(clip&&Microphone.GetPosition(device)>0){microphone=new GameObject("Micrófono").AddComponent<AudioSource>();microphone.transform.SetParent(transform,false);microphone.clip=clip;microphone.loop=true;micTrack=new AudioStreamTrack(microphone){Loopback=false};microphone.Play();pc.AddTrack(micTrack);}}
   if(micTrack==null){End();State?.Invoke("Concede el permiso del micrófono y vuelve a conectar.");yield break;}
   Texture outgoing=model;modelTexture=model;
   if(!QuestRig.Active&&WebCamTexture.devices.Length>0&&Application.HasUserAuthorization(UserAuthorization.WebCam)){webcam=new WebCamTexture(WebCamTexture.devices[0].name,640,480,15);webcam.Play();float limit=Time.realtimeSinceStartup+5;while(Current(g)&&Time.realtimeSinceStartup<limit&&webcam.width<=16)yield return null;if(!Current(g))yield break;if(webcam.width>16)outgoing=webcam;}
   try{sourceTexture=outgoing;SharingModel=outgoing==model;encodeTexture=new RenderTexture(QuestRig.Active?448:640,QuestRig.Active?832:480,0,WebRTC.GetSupportedGraphicsFormat(SystemInfo.graphicsDeviceType));encodeTexture.Create();Graphics.Blit(outgoing,encodeTexture);videoTrack=new VideoStreamTrack(encodeTexture);pc.AddTrack(videoTrack);LocalVideo?.Invoke(outgoing);}catch(Exception){ok=false;}
   if(!ok){End();State?.Invoke("No se pudo abrir el video. Revisa el controlador gráfico.");yield break;}
   cancel=new CancellationTokenSource();socket=new ClientWebSocket();var url=new UriBuilder(api.BaseUrl){Scheme=api.BaseUrl.StartsWith("https:")?"wss":"ws",Path="/api/llamadas/"+id+"/ws"};State?.Invoke("Esperando al otro participante…");_=Receive(url.Uri,api.SessionToken,g,socket,cancel.Token);
  }
  private async Task Receive(Uri url,string token,int g,ClientWebSocket ws,CancellationToken ct){try{
   using(var limit=CancellationTokenSource.CreateLinkedTokenSource(ct)){limit.CancelAfter(15000);await ws.ConnectAsync(url,limit.Token);}await SendText(ws,ApiProtocol.Encode(new{type="auth",client="quest",token}),ct);token=null;byte[] bytes=new byte[8192];
   using(var buffer=new MemoryStream()){while(!ct.IsCancellationRequested&&ws.State==WebSocketState.Open){var r=await ws.ReceiveAsync(new ArraySegment<byte>(bytes),ct);if(r.MessageType==WebSocketMessageType.Close)break;buffer.Write(bytes,0,r.Count);if(buffer.Length>110000)throw new InvalidOperationException();if(!r.EndOfMessage)continue;var signal=ApiProtocol.Decode<CallSignal>(Encoding.UTF8.GetString(buffer.ToArray()));buffer.SetLength(0);main.Enqueue(()=>{if(Current(g))incoming.Enqueue(signal);});}}
   main.Enqueue(()=>{if(Current(g)){End();State?.Invoke("Llamada terminada.");}});
  }catch(Exception){main.Enqueue(()=>{if(Current(g)){End();State?.Invoke("No se pudo conectar con el servidor de llamadas.");}});}}
  private async Task SendText(ClientWebSocket ws,string text,CancellationToken ct){await sendLock.WaitAsync(ct);try{var bytes=Encoding.UTF8.GetBytes(text);await ws.SendAsync(new ArraySegment<byte>(bytes),WebSocketMessageType.Text,true,ct);}finally{sendLock.Release();}}
  private async void Send(CallSignal signal,int g){var ws=socket;var ct=cancel?.Token??CancellationToken.None;if(!Current(g)||ws?.State!=WebSocketState.Open)return;try{await SendText(ws,ApiProtocol.Encode(signal),ct);}catch(Exception){main.Enqueue(()=>{if(Current(g))End();});}}
  private IEnumerator Process(CallSignal m){int g=generation;processing=true;try{
   if(m.type=="error"){End();State?.Invoke(ApiProtocol.ErrorMessage(m.code));yield break;}if(m.type=="hangup"||m.type=="peer_left"){End();State?.Invoke("El otro participante terminó.");yield break;}
   if(m.type=="ready"){Peer?.Invoke(m.peer);timeout=Time.realtimeSinceStartup+45;if(m.offerer){var offer=pc.CreateOffer();yield return offer;if(!Current(g))yield break;if(offer.IsError){End();yield break;}var d=offer.Desc;var set=pc.SetLocalDescription(ref d);yield return set;if(!Current(g))yield break;if(set.IsError){End();yield break;}Send(new CallSignal{type="offer",sdp=d.sdp},g);}}
   if(m.type=="offer"||m.type=="answer"){var d=new RTCSessionDescription{type=m.type=="offer"?RTCSdpType.Offer:RTCSdpType.Answer,sdp=m.sdp};var set=pc.SetRemoteDescription(ref d);yield return set;if(!Current(g))yield break;if(set.IsError){End();State?.Invoke("No se pudo negociar la llamada.");yield break;}remoteSet=true;foreach(var c in candidates)AddCandidate(c);candidates.Clear();if(m.type=="offer"){var answer=pc.CreateAnswer();yield return answer;if(!Current(g))yield break;if(answer.IsError){End();yield break;}d=answer.Desc;set=pc.SetLocalDescription(ref d);yield return set;if(!Current(g))yield break;if(set.IsError){End();yield break;}Send(new CallSignal{type="answer",sdp=d.sdp},g);}}
   if(m.type=="candidate"&&m.candidate!=null){if(remoteSet)AddCandidate(m.candidate);else if(candidates.Count<200)candidates.Add(m.candidate);}
  }finally{processing=false;}}
  private void AddCandidate(IceData d){if(string.IsNullOrEmpty(d.candidate))return;using(var c=new RTCIceCandidate(new RTCIceCandidateInit{candidate=d.candidate,sdpMid=d.sdpMid,sdpMLineIndex=d.sdpMLineIndex}))pc?.AddIceCandidate(c);}
  public void ShareModel(){if(!Active){State?.Invoke("Conecta una llamada primero.");return;}SharingModel=QuestRig.Active||!webcam||!SharingModel;sourceTexture=SharingModel?modelTexture:webcam;LocalVideo?.Invoke(sourceTexture);State?.Invoke(SharingModel?"Compartiendo el modelo 3D":"Compartiendo la cámara");}
  public void Mute(){Muted=!Muted;if(micTrack!=null)micTrack.Enabled=!Muted;}public void Video(){VideoEnabled=!VideoEnabled;if(videoTrack!=null)videoTrack.Enabled=VideoEnabled;}
  public void End(){Active=false;generation++;cancel?.Cancel();socket?.Abort();socket?.Dispose();socket=null;cancel?.Dispose();cancel=null;incoming.Clear();candidates.Clear();timeout=0;remoteSet=false;RemoteVideo?.Invoke(null);LocalVideo?.Invoke(null);Peer?.Invoke(null);pc?.Close();pc?.Dispose();pc=null;videoTrack?.Dispose();videoTrack=null;remoteVideo?.Dispose();remoteVideo=null;micTrack?.Dispose();micTrack=null;remoteAudio?.Dispose();remoteAudio=null;if(microphone)Destroy(microphone.gameObject);if(speaker)Destroy(speaker.gameObject);if(device!=null)Microphone.End(device);device=null;if(clip)Destroy(clip);clip=null;if(webcam){webcam.Stop();Destroy(webcam);}webcam=null;sourceTexture=null;modelTexture=null;if(encodeTexture){encodeTexture.Release();Destroy(encodeTexture);}encodeTexture=null;SharingModel=false;if(pump!=null)StopCoroutine(pump);pump=null;Muted=false;VideoEnabled=true;State?.Invoke("Sin llamada");}
  private void OnDestroy(){End();}
 }
}
