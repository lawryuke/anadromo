// Execute with unity command eval_file --file <absolute path>, outside Play Mode.
// Creates persisted meshes/materials through Unity. Refuses to overwrite an existing version.
if (UnityEditor.EditorApplication.isPlaying) throw new System.Exception("Stop Play Mode first.");
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (scene.name != "Blockout_Test") throw new System.Exception("Open Blockout_Test first.");
const string folder = "Assets/_Project/Art/Environment/Seabed";
if (UnityEngine.GameObject.Find("Seabed_RockyRift") != null || UnityEditor.AssetDatabase.IsValidFolder(folder))
    throw new System.Exception("Seabed already exists; edit the existing version instead.");
void Folder(string path) {
    if (UnityEditor.AssetDatabase.IsValidFolder(path)) return;
    int split = path.LastIndexOf('/'); Folder(path.Substring(0, split));
    UnityEditor.AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
}
Folder(folder);
var shader = UnityEngine.Shader.Find("Universal Render Pipeline/Lit");
if (shader == null) throw new System.Exception("URP Lit shader missing.");
UnityEngine.Material Material(string name, UnityEngine.Color color, bool sand) {
    var tex = new UnityEngine.Texture2D(128, 128, UnityEngine.TextureFormat.RGBA32, true);
    tex.name = name + "_Grain";
    var pixels = new UnityEngine.Color[128 * 128];
    for (int y=0; y<128; y++) for(int x=0; x<128; x++) {
        float n=UnityEngine.Mathf.PerlinNoise(x*.13f, y*.13f)*.2f + UnityEngine.Mathf.PerlinNoise(x*.61f+32,y*.61f)*.1f;
        float layer=sand ? 0 : .06f*UnityEngine.Mathf.Sin(y*.32f+UnityEngine.Mathf.PerlinNoise(x*.045f, y*.04f)*5);
        pixels[y*128+x]=new UnityEngine.Color(.72f+n+layer,.72f+n+layer,.72f+n+layer,1);
    }
    tex.SetPixels(pixels); tex.Apply(); tex.wrapMode=UnityEngine.TextureWrapMode.Repeat;
    UnityEditor.AssetDatabase.CreateAsset(tex, folder + "/" + tex.name + ".asset");
    var mat = new UnityEngine.Material(shader); mat.name=name;
    mat.SetColor("_BaseColor", color); mat.SetTexture("_BaseMap",tex); mat.SetFloat("_Smoothness",.08f);
    UnityEditor.AssetDatabase.CreateAsset(mat,folder+"/"+name+".mat"); return mat;
}
var sandMat=Material("Seafloor_Silt",new UnityEngine.Color(.42f,.48f,.43f),true);
var rockMat=Material("Rock_Slate",new UnityEngine.Color(.25f,.33f,.34f),false);
var paleMat=Material("Rock_Weathered",new UnityEngine.Color(.39f,.44f,.41f),false);
var deepMat=Material("Rock_Abyss",new UnityEngine.Color(.12f,.20f,.23f),false);
var root=new UnityEngine.GameObject("Seabed_RockyRift");
UnityEditor.Undo.RegisterCreatedObjectUndo(root,"Create rocky seabed");
root.transform.position=new UnityEngine.Vector3(125,44,25);
UnityEngine.GameObject Group(string name) { var g=new UnityEngine.GameObject(name); g.transform.SetParent(root.transform,false); return g; }
var floorGroup=Group("01_Seafloor_110x125m");
var rockGroup=Group("02_Fractured_Rock_Ledges");
var rubbleGroup=Group("03_Scattered_Stones");
var markers=Group("04_Encounter_Anchors");
float Edge(float x) { return 78 + 2.4f*UnityEngine.Mathf.Sin(x*.08f) + 1.3f*UnityEngine.Mathf.Sin(x*.23f); }
float Surface(float x,float z) { return (UnityEngine.Mathf.PerlinNoise((x+90)*.055f,z*.055f)-.5f)*2.5f + .35f*UnityEngine.Mathf.Sin(x*.23f+z*.17f); }
UnityEngine.GameObject MeshObject(string name,UnityEngine.Mesh mesh,UnityEngine.Material[] mats,UnityEngine.Transform parent) {
    mesh.name=name; mesh.RecalculateNormals(); mesh.RecalculateBounds();
    UnityEditor.AssetDatabase.CreateAsset(mesh,folder+"/"+name+".asset");
    var go=new UnityEngine.GameObject(name); go.transform.SetParent(parent,false);
    go.AddComponent<UnityEngine.MeshFilter>().sharedMesh=mesh;
    go.AddComponent<UnityEngine.MeshRenderer>().sharedMaterials=mats;
    go.AddComponent<UnityEngine.MeshCollider>().sharedMesh=mesh;
    go.isStatic=true; return go;
}
// Cross-section: broad sediment shelf, broken near-vertical walls, deep channel, opposite shelf.
// The abyss has 24+ metres of clear width at the lip; its bottom is 62 metres below the shelf.
float[] offsets={-78,-70,-62,-54,-46,-38,-30,-22,-18,-15,-13,-12,-11,-10,-8,-5,0,5,8,10,11,12,13,15,18,22,28,36,47};
float[] heights={0,0,0,0,0,0,0,0,-.4f,-.8f,-1.5f,-4,-10,-20,-36,-54,-62,-54,-36,-20,-10,-4,-1.5f,-.8f,-.4f,0,0,0,0};
int nx=74, nz=offsets.Length;
var v=new UnityEngine.Vector3[(nx+1)*nz]; var uv=new UnityEngine.Vector2[v.Length];
for(int ix=0;ix<=nx;ix++) for(int iz=0;iz<nz;iz++) {
    float x=-55+110f*ix/nx;
    float z=Edge(x)+offsets[iz];
    // Keep the exterior boundary rectangular, allow the fissure edges to meander.
    if(iz==0) z=0; if(iz==nz-1) z=125;
    float y=heights[iz]+Surface(x,z)*(heights[iz]<-4 ? 1.5f:1);
    int i=ix*nz+iz; v[i]=new UnityEngine.Vector3(x,y,z); uv[i]=new UnityEngine.Vector2(x*.25f,(z-y*.55f)*.25f);
}
var sandTris=new System.Collections.Generic.List<int>(); var wallTris=new System.Collections.Generic.List<int>();
for(int ix=0;ix<nx;ix++) for(int iz=0;iz<nz-1;iz++) {
    int a=ix*nz+iz,b=a+nz; var tris=(iz<9 || iz>=23) ? sandTris:wallTris;
    tris.AddRange(new[]{a,a+1,b,b,a+1,b+1});
}
var mesh=new UnityEngine.Mesh(); mesh.vertices=v; mesh.uv=uv; mesh.subMeshCount=2;
mesh.SetTriangles(sandTris,0); mesh.SetTriangles(wallTris,1);
MeshObject("Seabed_Shelves_And_Rift",mesh,new[]{sandMat,rockMat},floorGroup.transform);
// Six reusable low-poly, irregular stone meshes. Flat normals retain fractured faces.
var stoneMeshes=new UnityEngine.Mesh[6];
for(int variant=0;variant<6;variant++) {
    var points=new System.Collections.Generic.List<UnityEngine.Vector3>();
    int rings=7, sides=11;
    for(int ring=0;ring<=rings;ring++) for(int side=0;side<sides;side++) {
        float phi=UnityEngine.Mathf.PI*ring/rings, theta=2*UnityEngine.Mathf.PI*side/sides;
        float noise=UnityEngine.Mathf.PerlinNoise(side*.73f+variant*4.3f,ring*.67f+variant)*.36f+.80f;
        points.Add(new UnityEngine.Vector3(UnityEngine.Mathf.Sin(phi)*UnityEngine.Mathf.Cos(theta)*noise,UnityEngine.Mathf.Cos(phi)*.85f*noise,UnityEngine.Mathf.Sin(phi)*UnityEngine.Mathf.Sin(theta)*noise));
    }
    var vertices=new System.Collections.Generic.List<UnityEngine.Vector3>(); var uvs=new System.Collections.Generic.List<UnityEngine.Vector2>();
    var triangles=new System.Collections.Generic.List<int>();
    void Triangle(int a,int b,int c) { foreach(int k in new[]{a,b,c}) { var p=points[k]; triangles.Add(vertices.Count); vertices.Add(p); uvs.Add(new UnityEngine.Vector2(p.x+p.z*.4f,p.y)*2); } }
    for(int r=0;r<rings;r++) for(int s=0;s<sides;s++) {
        int a=r*sides+s,b=r*sides+(s+1)%sides,c=(r+1)*sides+s,d=(r+1)*sides+(s+1)%sides;
        if(r>0) Triangle(a,b,c); if(r<rings-1) Triangle(b,d,c);
    }
    var stone=new UnityEngine.Mesh(); stone.name="FracturedStone_"+variant; stone.SetVertices(vertices);stone.SetUVs(0,uvs);stone.SetTriangles(triangles,0);stone.RecalculateNormals();stone.RecalculateBounds();
    UnityEditor.AssetDatabase.CreateAsset(stone,folder+"/"+stone.name+".asset");stoneMeshes[variant]=stone;
}
var random=new System.Random(1701);
float Range(float a,float b) { return a+(b-a)*(float)random.NextDouble(); }
int stones=0;
void Stone(UnityEngine.Transform parent,UnityEngine.Vector3 pos,UnityEngine.Vector3 scale,UnityEngine.Material mat) {
    var g=new UnityEngine.GameObject("Rock_"+(stones++).ToString("D3"));g.transform.SetParent(parent,false);g.transform.localPosition=pos;
    g.transform.localRotation=UnityEngine.Quaternion.Euler(Range(-15,15),Range(0,360),Range(-12,12));g.transform.localScale=scale;
    g.AddComponent<UnityEngine.MeshFilter>().sharedMesh=stoneMeshes[random.Next(6)];g.AddComponent<UnityEngine.MeshRenderer>().sharedMaterial=mat;
    g.AddComponent<UnityEngine.MeshCollider>().sharedMesh=g.GetComponent<UnityEngine.MeshFilter>().sharedMesh;g.isStatic=true;
}
for(int side=-1;side<=1;side+=2) for(int i=0;i<21;i++) {
    float x=-52+i*5.2f+Range(-1,1); float z=Edge(x)+side*Range(14,18);
    Stone(rockGroup.transform,new UnityEngine.Vector3(x,Surface(x,z)-.6f,z),new UnityEngine.Vector3(Range(3,5),Range(1.8f,4),Range(2.3f,4)),i%3==0?paleMat:rockMat);
    for(int tier=0;tier<3;tier++) {
        float depth=9+tier*14;float wallZ=Edge(x)+side*(11-tier*2);
        Stone(rockGroup.transform,new UnityEngine.Vector3(x,-depth,wallZ),new UnityEngine.Vector3(Range(3,5),Range(5,9),Range(1.4f,2.7f)),tier==2?deepMat:rockMat);
    }
}
for(int i=0;i<90;i++) {
    float x=Range(-53,53),z=Range(3,122);if(UnityEngine.Mathf.Abs(z-Edge(x))<20) continue;
    // Open approach around the existing player, flock and feeding patch.
    if(UnityEngine.Mathf.Abs(x)<8 && z<65) continue;
    float size=Range(.3f,1.6f);
    Stone(rubbleGroup.transform,new UnityEngine.Vector3(x,Surface(x,z)-size*.15f,z),new UnityEngine.Vector3(size, size*Range(.5f,1),size*Range(.7f,1.5f)),i%3==0?paleMat:rockMat);
}
void Anchor(string name,UnityEngine.Vector3 position) { var g=new UnityEngine.GameObject(name);g.transform.SetParent(markers.transform,false);g.transform.localPosition=position; }
Anchor("Predator_Emergence",new UnityEngine.Vector3(-18,-24,Edge(-18)));
Anchor("Bloop_Ascent",new UnityEngine.Vector3(0,-50,Edge(0)));
Anchor("Rift_Observation",new UnityEngine.Vector3(0,6,57));
Anchor("Refuge_Approach",new UnityEngine.Vector3(35,5,54));
var old=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="Terrain");
if(old!=null) {UnityEditor.Undo.RecordObject(old,"Replace oversized terrain"); old.SetActive(false);}
UnityEngine.RenderSettings.fogDensity=.022f;
var playerCamera=UnityEngine.Camera.main;
if(playerCamera!=null) {
    UnityEditor.Undo.RecordObject(playerCamera,"Underwater background");
    playerCamera.clearFlags=UnityEngine.CameraClearFlags.SolidColor;
    playerCamera.backgroundColor=UnityEngine.RenderSettings.fogColor;
}
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.AssetDatabase.SaveAssets();
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
UnityEditor.Selection.activeGameObject=root;
var view=UnityEditor.SceneView.lastActiveSceneView;
if(view!=null) {
    view.sceneViewState.showFog=false;
    view.LookAt(new UnityEngine.Vector3(125,29,103),UnityEngine.Quaternion.Euler(45,25,0),78);
}
return new {scene=scene.path,stones,meshVertices=v.Length,size="110 x 125 m",depth="62 m",root=root.name};
