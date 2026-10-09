using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
namespace Telemedicina {
 [Serializable] public class AnatomyAsset { public AnatomySection[] sections; }
 [Serializable] public class AnatomySection { public string name,system;public float[] vertices,color;public int[] triangles;public float alpha; }
 public sealed class AnatomyModel:MonoBehaviour,IDragHandler,IScrollHandler {
  private Transform pivot;private Camera camera;private RenderTexture target;private AnatomyAsset asset;private float yaw,zoom=1;
  private readonly List<Mesh> meshes=new List<Mesh>();private readonly List<Material> materials=new List<Material>();private readonly List<GameObject> parts=new List<GameObject>();
  public RenderTexture Create(){
   asset=ApiProtocol.Decode<AnatomyAsset>(Resources.Load<TextAsset>("AnatomiaV3").text);pivot=new GameObject("Modelo completo fuera del campo del visor").transform;pivot.position=new Vector3(1000,0,0);
   foreach(var s in asset.sections){var obj=new GameObject(s.name,typeof(MeshFilter),typeof(MeshRenderer));obj.layer=26;obj.transform.SetParent(pivot,false);obj.transform.localPosition=new Vector3(0,-.88f,0);
    var v=new Vector3[s.vertices.Length/3];for(int i=0;i<v.Length;i++)v[i]=new Vector3(s.vertices[i*3],s.vertices[i*3+1],s.vertices[i*3+2]);
    var mesh=new Mesh{name=s.name,indexFormat=IndexFormat.UInt32};mesh.vertices=v;mesh.triangles=s.triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);obj.GetComponent<MeshFilter>().sharedMesh=mesh;
    var mat=new Material(Resources.Load<Material>(s.system=="cuerpo"?"AnatomiaCristal":"AnatomiaSolida"));mat.SetColor("_BaseColor",new Color(s.color[0],s.color[1],s.color[2],s.alpha));materials.Add(mat);obj.GetComponent<MeshRenderer>().sharedMaterial=mat;obj.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;obj.GetComponent<MeshRenderer>().receiveShadows=false;parts.Add(obj);
   }
   target=new RenderTexture(448,832,16,RenderTextureFormat.ARGB32){name="Anatomía V3",antiAliasing=1};target.Create();camera=new GameObject("Cámara de anatomía",typeof(Camera)).GetComponent<Camera>();camera.transform.position=new Vector3(1000,0,-4);camera.orthographic=true;camera.orthographicSize=1.00f;camera.nearClipPlane=.1f;camera.farClipPlane=10;camera.cullingMask=1<<26;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.allowHDR=false;camera.allowMSAA=false;camera.stereoTargetEye=StereoTargetEyeMask.None;camera.targetTexture=target;return target;
  }
  public void Select(string system){for(int i=0;i<parts.Count;i++)parts[i].SetActive(system=="cuerpo"||asset.sections[i].system==system||asset.sections[i].system=="cuerpo");}
  public void Rotate(float degrees){yaw+=degrees;Apply();}public void Zoom(float delta){zoom=Mathf.Clamp(zoom+delta,.7f,1.4f);Apply();}public void ResetView(){yaw=0;zoom=1;Select("cuerpo");Apply();}
  private void Apply(){pivot.rotation=Quaternion.Euler(0,yaw,0);pivot.localScale=Vector3.one*zoom;}
  public void OnDrag(PointerEventData e){Rotate(-e.delta.x*.3f);}public void OnScroll(PointerEventData e){Zoom(e.scrollDelta.y*.06f);}
  private void OnDestroy(){if(camera)Destroy(camera.gameObject);if(pivot)Destroy(pivot.gameObject);if(target){target.Release();Destroy(target);}foreach(var m in meshes)Destroy(m);foreach(var m in materials)Destroy(m);}
 }
}
