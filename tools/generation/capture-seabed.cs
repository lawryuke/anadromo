// Neutral overview for inspecting the geometry; restores scene render settings afterwards.
var temp=new UnityEngine.GameObject("Seabed_Preview_Temporary");
var cam=temp.AddComponent<UnityEngine.Camera>();
var rt=new UnityEngine.RenderTexture(1440,900,24);
var previous=UnityEngine.RenderTexture.active;
bool fog=UnityEngine.RenderSettings.fog;
UnityEngine.Texture2D image=null;
try {
    cam.transform.position=new UnityEngine.Vector3(187,135,20);
    cam.transform.LookAt(new UnityEngine.Vector3(125,21,99));
    cam.clearFlags=UnityEngine.CameraClearFlags.SolidColor;cam.backgroundColor=new UnityEngine.Color(.025f,.065f,.08f);
    cam.fieldOfView=58;cam.farClipPlane=400;cam.targetTexture=rt;
    UnityEngine.RenderSettings.fog=false;
    cam.Render();UnityEngine.RenderTexture.active=rt;
    image=new UnityEngine.Texture2D(1440,900,UnityEngine.TextureFormat.RGB24,false);
    image.ReadPixels(new UnityEngine.Rect(0,0,1440,900),0,0);image.Apply();
    string dir=System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath,"../../../docs/images"));
    System.IO.Directory.CreateDirectory(dir);
    System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir,"seabed-overview.png"),image.EncodeToPNG());
    return "Saved docs/images/seabed-overview.png";
} finally {
    UnityEngine.RenderSettings.fog=fog;UnityEngine.RenderTexture.active=previous;
    cam.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);
    if(image!=null) UnityEngine.Object.DestroyImmediate(image);
    UnityEngine.Object.DestroyImmediate(temp);
}
