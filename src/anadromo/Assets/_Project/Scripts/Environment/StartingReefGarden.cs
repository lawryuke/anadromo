using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Anadromo.Environment
{
    /// <summary>Self-contained, deterministic dressing for the shallow starting shelf.
    /// Generated children are transient; the serialized recipe works in editor and builds.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class StartingReefGarden : MonoBehaviour
    {
        public Material seabedMaterial, vegetationMaterial;
        public Material coralMaterial, seaFanMaterial;
        public GameObject flatRock, rockScatter, redCoral, seaFan;
        public GameObject foundationBoulder, foundationCliff;
        public Material foundationMaterial;
        [Min(2)] public float foundationDepth = 20.5f;
        public Vector2 size = new Vector2(21.111416f, 9.884663f);
        public int seed = 731;
        GameObject generated;
        readonly List<Mesh> meshes = new List<Mesh>();
        bool pending;
        System.Random random;
        float Next(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
        void OnEnable() => pending = true;
        void OnValidate() => pending = true;
        void Update() { if (pending) { pending = false; Rebuild(); } }
        void OnDisable() => Clear();

        [ContextMenu("Reconstruir jardin de arrecife")]
        public void Rebuild()
        {
            Clear();
            if (!seabedMaterial || !vegetationMaterial) return;
            random = new System.Random(seed);
            generated = new GameObject("Arrecife generado (roca, coral y algas)");
            generated.transform.SetParent(transform, false);
            generated.hideFlags = HideFlags.DontSave;
            BuildShelf();
            BuildFoundation();

            // Asymmetric islands, with a generous open swim lane through the centre.
            Vector2[] islands = {
                new Vector2(-7.9f,-3.0f), new Vector2(-5.6f,-1.25f),
                new Vector2(-8.1f,2.6f), new Vector2(-4.0f,3.35f),
                new Vector2(7.6f,-2.6f), new Vector2(5.4f,.7f),
                new Vector2(8.2f,3.15f), new Vector2(3.55f,3.6f)
            };
            var grassV = new List<Vector3>(); var grassUV = new List<Vector2>();
            var grassC = new List<Color>(); var grassT = new List<int>();
            for (int i = 0; i < islands.Length; i++)
            {
                Vector2 p = islands[i];
                Place(flatRock, "Roca erosionada " + (i + 1), p,
                    new Vector3(Next(1.5f,2.6f), Next(.18f,.36f), Next(1.1f,1.8f)), -.045f);
                Place(i % 2 == 0 ? seaFan : redCoral, "Coral principal " + (i + 1),
                    p + new Vector2(.1f,.15f), Vector3.one * Next(.65f,1.15f), .13f);
                for (int j = 0; j < 3; j++)
                {
                    Vector2 q = p + new Vector2(Next(-.85f,.85f),Next(-.65f,.65f));
                    Place(redCoral, "Coral pequeno", q, Vector3.one * Next(.22f,.48f), .035f);
                }
                for (int j = 0; j < 24; j++)
                {
                    float a = Next(0, Mathf.PI * 2), r = Next(.25f,1.3f);
                    Vector2 q = p + new Vector2(Mathf.Cos(a)*r,Mathf.Sin(a)*r*.65f);
                    AddTuft(q, Next(.18f,.54f), grassV, grassUV, grassC, grassT);
                }
                // Taller, broad undulating fronds sit behind the rock islands.
                for (int j = 0; j < 5; j++)
                {
                    Vector2 q = p + new Vector2(Next(-.65f,.65f),Next(.25f,.7f));
                    for (int k = 0; k < 4; k++)
                        AddBlade(q, Next(.65f,1.5f), Next(.065f,.12f), Next(0,6.28f),
                            grassV, grassUV, grassC, grassT);
                }
            }
            for (int i = 0; i < 42; i++)
            {
                Vector2 p = new Vector2(Next(-9.7f,9.7f),Next(-4.2f,4.2f));
                if (Mathf.Abs(p.x) < 2.1f) continue;
                Place(rockScatter, "Grava y fragmentos", p,
                    new Vector3(Next(.3f,.85f),Next(.045f,.13f),Next(.25f,.6f)), -.015f);
                AddTuft(p, Next(.09f,.22f), grassV, grassUV, grassC, grassT);
            }
            MeshObject("Praderas y frondas de algas", grassV, grassUV, grassC, grassT, vegetationMaterial, false);
            foreach (Transform child in generated.GetComponentsInChildren<Transform>(true))
                child.gameObject.hideFlags = HideFlags.DontSave;
        }

        float Height(float x, float z)
        {
            float lane = Mathf.SmoothStep(0,1,Mathf.Abs(x)/3f);
            return .025f + Mathf.PerlinNoise(x*.65f+31,z*.65f+17) * (.045f + .105f*lane);
        }

        // Rounded, scalloped coastline rather than a rectangular slab. The same
        // contour drives the top, the cliff and placement of every plant.
        Vector2 Coast(float angle)
        {
            float x=Mathf.Cos(angle), z=Mathf.Sin(angle);
            float r=1/Mathf.Pow(Mathf.Pow(Mathf.Abs(x),4)+Mathf.Pow(Mathf.Abs(z),4),.25f);
            r*=1+.045f*Mathf.Sin(angle*3+.7f)+.025f*Mathf.Sin(angle*7+1.3f)+.014f*Mathf.Cos(angle*13);
            return new Vector2(x*r*size.x*.5f,z*r*size.y*.5f);
        }

        Vector2 OnShelf(Vector2 point)
        {
            float angle=Mathf.Atan2(point.y/size.y,point.x/size.x);
            Vector2 edge=Coast(angle);
            float fraction=point.magnitude/Mathf.Max(.001f,edge.magnitude-.35f);
            return fraction>1?point/fraction:point;
        }

        void BuildShelf()
        {
            const int sides=128, rings=30;
            var v = new List<Vector3>(); var uv = new List<Vector2>();
            var colors = new List<Color>(); var triangles = new List<int>();
            v.Add(new Vector3(0,Height(0,0),0));uv.Add(Vector2.zero);colors.Add(new Color(0,0,0,1));
            for(int ring=1;ring<=rings;ring++)
            for(int side=0;side<sides;side++)
            {
                Vector2 p=Coast(side*2*Mathf.PI/sides)*(ring/(float)rings);
                float px=p.x, pz=p.y;
                float noise = Mathf.PerlinNoise(px*.38f+14,pz*.38f+9);
                float lane = Mathf.Exp(-Mathf.Pow((px-.6f*Mathf.Sin(pz*.65f))/1.55f,2));
                v.Add(new Vector3(px,Height(px,pz),pz));
                uv.Add(new Vector2(px/2.2f,pz/2.2f));
                colors.Add(new Color(Mathf.Clamp01((noise-.32f)*2.8f)*(1-lane*.88f),
                    Mathf.Clamp01((Mathf.PerlinNoise(px*.7f+58,pz*.7f+24)-.46f)*3),0,1));
                int a=1+(ring-1)*sides+side, b=1+(ring-1)*sides+(side+1)%sides;
                if(ring==1) triangles.AddRange(new[]{0,b,a});
                else { int c=a-sides,d=b-sides;triangles.AddRange(new[]{c,b,a,c,d,b}); }
            }
            MeshObject("Cornisa irregular - roca y arena",v,uv,colors,triangles,seabedMaterial,true);
        }

        void BuildFoundation()
        {
            if(!foundationMaterial) return;
            const int sides=128, levels=28;
            var v=new List<Vector3>();var uv=new List<Vector2>();
            var c=new List<Color>();var t=new List<int>();
            for(int level=0;level<=levels;level++)
            for(int side=0;side<sides;side++)
            {
                float angle=side*2*Mathf.PI/sides, depth=level/(float)levels;
                Vector2 edge=Coast(angle);
                float shape=1+Mathf.Sin(depth*Mathf.PI)*.08f+depth*depth*.22f;
                float relief=Mathf.Sin(depth*Mathf.PI)*(.075f*Mathf.Sin(angle*9+depth*14)+.035f*Mathf.Cos(angle*17-depth*23));
                Vector2 p=edge*(shape+relief);
                float y=level==0?Height(edge.x,edge.y):Mathf.Lerp(-.12f,-foundationDepth,depth);
                v.Add(new Vector3(p.x,y,p.y));uv.Add(new Vector2(angle*3,y*.3f));c.Add(Color.white);
                if(level==levels) continue;
                int a=level*sides+side,b=level*sides+(side+1)%sides;
                t.AddRange(new[]{a,b,a+sides,b,b+sides,a+sides});
            }
            int bottom=v.Count;v.Add(new Vector3(0,-foundationDepth,0));uv.Add(Vector2.zero);c.Add(Color.white);
            for(int s=0;s<sides;s++)t.AddRange(new[]{bottom,levels*sides+s,levels*sides+(s+1)%sides});
            MeshObject("Macizo rocoso continuo hasta el fondo",v,uv,c,t,foundationMaterial,true);
            // OceanViz's scanned cliff faces preserve their original UVs and normal maps.
            for(int level=0;level<3;level++)
            for(int i=0;i<14;i++)
            {
                float angle=(i+.34f*level)*Mathf.PI*2/14;
                Vector2 edge=Coast(angle)*(level==0?.98f:1.02f);
                float height=level==0?Next(2.3f,3.8f):Next(7,10);
                float y=level==0?-height*.55f-.2f:-5.8f-(level-1)*7.3f;
                AddFoundationRock(i%3==0?foundationBoulder:foundationCliff,
                    new Vector3(edge.x,y,edge.y),new Vector3(Next(3.3f,5.8f),height,Next(2.2f,3.7f)),
                    new Vector3(Next(-9,9),-angle*Mathf.Rad2Deg+90,Next(-12,12)));
            }
        }

        void AddFoundationRock(GameObject prefab,Vector3 center,Vector3 dimensions,Vector3 euler)
        {
            if(!prefab) return;
            var go=Instantiate(prefab,generated.transform);go.name="OceanViz - contrafuerte erosionado";
            go.transform.localPosition=center;go.transform.localRotation=Quaternion.identity;go.transform.localScale=Vector3.one;
            var rs=go.GetComponentsInChildren<Renderer>();if(rs.Length==0)return;
            Bounds bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);
            go.transform.localScale=new Vector3(dimensions.x/Mathf.Max(.001f,bounds.size.x),dimensions.y/Mathf.Max(.001f,bounds.size.y),dimensions.z/Mathf.Max(.001f,bounds.size.z));
            go.transform.localRotation=Quaternion.Euler(euler);
            bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);
            go.transform.position+=transform.TransformPoint(center)-bounds.center;
            foreach(var behaviour in go.GetComponentsInChildren<MonoBehaviour>())if(behaviour)behaviour.enabled=false;
            // One stable collision mesh per outcrop, independent of visual LOD switching.
            var colliders=go.GetComponentsInChildren<MeshCollider>();
            for(int i=0;i<colliders.Length;i++)colliders[i].enabled=i==0;
        }

        void AddTuft(Vector2 p,float height,List<Vector3> v,List<Vector2> uv,List<Color> c,List<int> t)
        {
            for(int i=0;i<7;i++) AddBlade(p+new Vector2(Next(-.08f,.08f),Next(-.08f,.08f)),
                height*Next(.6f,1.2f),Next(.014f,.033f),Next(0,6.28f),v,uv,c,t);
        }

        void AddBlade(Vector2 p,float height,float width,float angle,List<Vector3> v,List<Vector2> uv,List<Color> c,List<int> t)
        {
            p=OnShelf(p);
            int start=v.Count;
            Vector3 side=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
            Vector3 bend=new Vector3(-side.z,0,side.x)*Next(.15f,.4f)*height;
            Color baseColor=Color.Lerp(new Color(.065f,.15f,.10f),new Color(.16f,.25f,.10f),Next(0,1));
            for(int s=0;s<=7;s++)
            {
                float f=s/7f;
                Vector3 center=new Vector3(p.x,Height(p.x,p.y)-.015f+height*f,p.y)+bend*f*f;
                center+=side*Mathf.Sin(f*9+angle)*width*f;
                float w=width*Mathf.Sin(Mathf.PI*(.08f+.92f*f));
                v.Add(center-side*w);v.Add(center+side*w);
                uv.Add(new Vector2(0,f));uv.Add(new Vector2(1,f));
                Color tint=Color.Lerp(baseColor,new Color(.26f,.39f,.19f),f*.65f);
                c.Add(tint);c.Add(tint);
                if(s==7) continue;
                int a=start+s*2;
                t.AddRange(new[]{a,a+2,a+1,a+1,a+2,a+3});
            }
        }

        void Place(GameObject source,string label,Vector2 p,Vector3 targetSize,float lift)
        {
            if(!source) return;
            p=OnShelf(p);
            GameObject go=Instantiate(source,generated.transform);
            go.name=label;
            go.transform.localPosition=Vector3.zero;
            go.transform.localRotation=Quaternion.identity;
            go.transform.localScale=Vector3.one;
            var renderers=go.GetComponentsInChildren<Renderer>();
            if(renderers.Length==0) { Dispose(go);return; }
            Material dressingMaterial=source==redCoral?coralMaterial:source==seaFan?seaFanMaterial:null;
            if(dressingMaterial)
                foreach(var r in renderers)
                {
                    var slots=r.sharedMaterials;
                    for(int i=0;i<slots.Length;i++) slots[i]=dressingMaterial;
                    r.sharedMaterials=slots;
                }
            Bounds bounds=renderers[0].bounds;
            foreach(var r in renderers) bounds.Encapsulate(r.bounds);
            Vector3 actual=bounds.size;
            go.transform.localScale=source==redCoral || source==seaFan
                ? Vector3.one*(targetSize.y/Mathf.Max(actual.y,.001f))
                : new Vector3(targetSize.x/Mathf.Max(actual.x,.001f),
                    targetSize.y/Mathf.Max(actual.y,.001f),targetSize.z/Mathf.Max(actual.z,.001f));
            go.transform.localRotation=Quaternion.Euler(0,Next(0,360),0);
            bounds=renderers[0].bounds;
            foreach(var r in renderers) bounds.Encapsulate(r.bounds);
            Vector3 baseWorld=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            go.transform.position+=transform.TransformPoint(new Vector3(p.x,Height(p.x,p.y)+lift,p.y))-baseWorld;
            // Dressing is not an obstacle; the continuous shelf provides collision.
            foreach(var collider in go.GetComponentsInChildren<Collider>()) collider.enabled=false;
            foreach(var behaviour in go.GetComponentsInChildren<MonoBehaviour>())
                if(behaviour) behaviour.enabled=false;
        }

        void MeshObject(string label,List<Vector3> v,List<Vector2> uv,List<Color> c,List<int> t,Material material,bool collision)
        {
            var mesh=new Mesh {name=label,hideFlags=HideFlags.DontSave,indexFormat=IndexFormat.UInt32};
            mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetColors(c);mesh.SetTriangles(t,0);
            mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
            meshes.Add(mesh);
            var go=new GameObject(label);go.transform.SetParent(generated.transform,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=collision?ShadowCastingMode.On:ShadowCastingMode.Off;
            if(collision) go.AddComponent<MeshCollider>().sharedMesh=mesh;
        }

        void Clear()
        {
            if(generated) { generated.SetActive(false);Dispose(generated);generated=null; }
            foreach(var mesh in meshes) if(mesh) Dispose(mesh);
            meshes.Clear();
        }
        static void Dispose(Object value)
        { if(Application.isPlaying) Destroy(value);else DestroyImmediate(value); }
    }
}
