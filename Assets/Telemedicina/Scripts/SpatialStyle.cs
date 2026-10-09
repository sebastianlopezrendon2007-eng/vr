using UnityEngine;using UnityEngine.UI;using UnityEngine.Events;
namespace Telemedicina {
 internal static class SpatialStyle {
  private static Sprite rounded,circle;
  public static Sprite Rounded=>rounded?rounded:(rounded=Shape(false));public static Sprite Circle=>circle?circle:(circle=Shape(true));
  private static Sprite Shape(bool circular){var t=new Texture2D(64,64,TextureFormat.RGBA32,false);t.filterMode=FilterMode.Bilinear;var a=new Color[4096];for(int y=0;y<64;y++)for(int x=0;x<64;x++){float dx=circular?x-31.5f:Mathf.Max(0,Mathf.Abs(x-31.5f)-19),dy=circular?y-31.5f:Mathf.Max(0,Mathf.Abs(y-31.5f)-19);a[y*64+x]=new Color(1,1,1,Mathf.Clamp01((circular?31:12)-Mathf.Sqrt(dx*dx+dy*dy)));}t.SetPixels(a);t.Apply(false,true);return Sprite.Create(t,new Rect(0,0,64,64),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(14,14,14,14));}
  public static RawImage Raw(Transform p,string name,float x,float y,float w,float h,Texture tex=null,bool raycast=false){var r=Ui.Rect(name,p,x,y,w,h).gameObject.AddComponent<RawImage>();r.texture=tex;r.raycastTarget=raycast;return r;}
  public static Button Icon(Transform p,string name,string glyph,float x,float y,float size,UnityAction click,Color? color=null){var box=Ui.Panel(name,p,x,y,size,size,color??new Color32(74,87,110,235),true);box.GetComponent<Image>().sprite=Circle;var b=box.gameObject.AddComponent<Button>();b.targetGraphic=box.GetComponent<Image>();b.onClick.AddListener(click);Raw(box,glyph,size*.26f,size*.26f,size*.48f,size*.48f,Resources.Load<Texture2D>("Icons/"+glyph));return b;}
  public static RectTransform Card(Transform p,string name,float x,float y,float w,float h)=>Ui.Panel(name,p,x,y,w,h,new Color32(64,77,104,225));
 }
}
