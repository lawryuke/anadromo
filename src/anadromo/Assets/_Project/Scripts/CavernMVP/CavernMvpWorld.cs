using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Anadromo.CavernMVP
{
    public sealed class CavernMvpWorld : MonoBehaviour
    {
        public CavernPlayer player;
        public Transform geometry;
        public Material stone, floor, creature, marker;
        public const float Cell = 3;
        public static readonly Vector2Int[] Rooms = {
            new Vector2Int(0, 0), new Vector2Int(-6, 8), new Vector2Int(6, 8), new Vector2Int(0, 16),
            new Vector2Int(7, 26), new Vector2Int(-7, 23), new Vector2Int(0, 34), new Vector2Int(0, 42)
        };
        public static readonly string[] Names = { "A · Umbral", "B · Sala de las mareas", "C · Cámara del derrumbe", "D · Jardín ciego", "E · Catedral sumergida", "F · Fosa del eco", "G · Bóveda de las raíces", "H · Sifón de salida" };
        // Ten undirected edges, eight rooms: three independent circuits.
        public static readonly int[,] Edges = { {0,1}, {0,2}, {1,3}, {2,3}, {1,5}, {5,3}, {3,4}, {5,6}, {4,6}, {6,7} };
        public static readonly Vector2Int[] DeadEnds = { new Vector2Int(13, 8), new Vector2Int(14, 28), new Vector2Int(-14, 25) };
        readonly HashSet<int> visited = new HashSet<int>();
        readonly List<SharkPass> passes = new List<SharkPass>();
        float elapsed;
        GUIStyle titleStyle, textStyle;
        Texture2D white;
        sealed class SharkPass { public Vector3 from, to; public float warning = -1, cooldown; public CavernShark shark; }

        public static Vector3 RoomPosition(int index) => new Vector3(Rooms[index].x * Cell, 3, Rooms[index].y * Cell);
        void Awake() { if (!player) Build(); SetupPasses(); }
        void SetupPasses()
        {
            passes.Clear();
            passes.Add(new SharkPass { from = RoomPosition(1), to = RoomPosition(5) });
            passes.Add(new SharkPass { from = RoomPosition(4), to = RoomPosition(6) });
        }

        public static HashSet<Vector2Int> WalkableCells()
        {
            var cells = new HashSet<Vector2Int>();
            for (int i = 0; i < Rooms.Length; i++)
                for (int x = -2; x <= 2; x++) for (int z = -2; z <= 2; z++) cells.Add(Rooms[i] + new Vector2Int(x,z));
            for (int e = 0; e < Edges.GetLength(0); e++) Carve(cells, Rooms[Edges[e,0]], Rooms[Edges[e,1]]);
            Carve(cells, Rooms[2], DeadEnds[0]); Carve(cells, Rooms[4], DeadEnds[1]); Carve(cells, Rooms[5], DeadEnds[2]);
            return cells;
        }
        static void Carve(HashSet<Vector2Int> cells, Vector2Int a, Vector2Int b)
        {
            int steps = Mathf.CeilToInt(Vector2Int.Distance(a,b) * 4);
            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(a,b, i / (float)steps);
                var cell = new Vector2Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y));
                for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++) cells.Add(cell + new Vector2Int(x,z));
            }
        }

        public void Build()
        {
            EnsureMaterials();
            geometry = new GameObject("Cueva · geometría primitiva").transform; geometry.SetParent(transform, false);
            var cells = WalkableCells();
            var directions = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
            foreach (var cell in cells)
            {
                Vector3 p = new Vector3(cell.x * Cell, 0, cell.y * Cell);
                Primitive("Suelo", PrimitiveType.Cube, p + Vector3.down * .25f, new Vector3(Cell, .5f, Cell), floor, geometry, true);
                Primitive("Techo", PrimitiveType.Cube, p + Vector3.up * 6.25f, new Vector3(Cell,.5f,Cell), stone, geometry, true);
                foreach (var d in directions)
                    if (!cells.Contains(cell + d)) Primitive("Pared", PrimitiveType.Cube, p + new Vector3(d.x * Cell * .5f,3,d.y * Cell * .5f), new Vector3(d.x != 0 ? .4f : Cell,6,d.y != 0 ? .4f : Cell), stone, geometry, true);
            }
            for (int i = 0; i < Rooms.Length; i++)
            {
                var beacon = Primitive(Names[i], PrimitiveType.Cylinder, RoomPosition(i) + Vector3.down * 2.85f, new Vector3(1.6f,.1f,1.6f), marker, geometry, false);
                var light = beacon.AddComponent<Light>(); light.type = LightType.Point; light.color = new Color(.2f,.8f,.75f); light.range = 12; light.intensity = 2;
            }
            foreach (var end in DeadEnds) Primitive("Callejón sin salida", PrimitiveType.Cube, new Vector3(end.x * Cell,1.2f,end.y * Cell), new Vector3(2,2,2), stone, geometry, true);
            // Small columns provide local cover without sealing the routes.
            foreach (int i in new[] {2,3,4}) Primitive("Columna · cobertura", PrimitiveType.Cylinder, RoomPosition(i) + Vector3.right * 3, new Vector3(1.4f,3,1.4f), stone, geometry, true);
            BuildPlayer();
            var piranhas = new List<CavernPiranha>();
            foreach (int room in new[] {1,4}) for (int i = 0; i < 4; i++)
            {
                var p = Enemy<CavernPiranha>("Piraña", RoomPosition(room) + new Vector3(-2 + i * 1.2f,.2f,1.5f), new Vector3(.65f,.5f,1.1f));
                p.radius = 7; p.disturbanceDelay = 2.5f; piranhas.Add(p);
            }
            foreach (var p in piranhas) p.school = piranhas.ToArray();
            foreach (int room in new[] {2,6})
            {
                var a = Enemy<CavernAngler>("Pez linterna", RoomPosition(room) + new Vector3(-2,0,2), new Vector3(1.6f,1.1f,1.7f));
                a.radius = 8; a.disturbanceDelay = 1.8f;
                a.lureBulb = Primitive("Señuelo", PrimitiveType.Sphere, Vector3.zero, Vector3.one * .25f, marker, a.transform, false);
                a.lureBulb.transform.localPosition = new Vector3(0,1,.6f);
                a.lure = a.lureBulb.AddComponent<Light>(); a.lure.color = Color.cyan; a.lure.range = 5; a.lure.intensity = 3;
            }
            foreach (int room in new[] {3,5}) for (int i = 0; i < 2; i++)
            {
                var l = Enemy<CavernLamprey>("Lamprea", RoomPosition(room) + new Vector3(-2 + i * 3,0,2), new Vector3(.25f,.25f,1.3f));
                l.radius = 7; l.attachOffset = new Vector3(i == 0 ? -.38f : .38f,-.23f,.85f);
            }
            var exit = Primitive("Salida · llega al aro verde", PrimitiveType.Cylinder, RoomPosition(7), new Vector3(3,.12f,3), marker, geometry, false);
            exit.transform.rotation = Quaternion.Euler(90,0,0);
            RenderSettings.skybox = null; RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.22f,.3f,.35f); RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(.015f,.055f,.075f); RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = .028f;
        }

        void BuildPlayer()
        {
            var go = new GameObject("Jugador MVP"); go.transform.SetParent(geometry); go.transform.position = RoomPosition(0); go.layer = 2; go.tag = "Player";
            var cc = go.AddComponent<CharacterController>(); cc.height = 1.2f; cc.radius = .4f; cc.stepOffset = .1f; cc.minMoveDistance = 0;
            player = go.AddComponent<CavernPlayer>();
            var cameraObject = new GameObject("Cámara MVP"); cameraObject.transform.SetParent(go.transform,false); cameraObject.tag = "MainCamera";
            player.view = cameraObject.AddComponent<Camera>(); player.view.nearClipPlane = .08f; player.view.farClipPlane = 100; player.view.fieldOfView = 75;
            player.view.clearFlags = CameraClearFlags.SolidColor; player.view.backgroundColor = new Color(.015f,.055f,.075f);
            cameraObject.AddComponent<AudioListener>();
        }
        T Enemy<T>(string label, Vector3 position, Vector3 scale) where T : CavernEnemy
        {
            var root = new GameObject(label); root.transform.SetParent(geometry); root.transform.position = position; root.layer = 2;
            var body = Primitive("Cuerpo · placeholder", PrimitiveType.Sphere, position, scale, creature, root.transform, false);
            var enemy = root.AddComponent<T>(); enemy.target = player; enemy.body = body.GetComponent<Renderer>();
            return enemy;
        }
        public static GameObject Primitive(string label, PrimitiveType type, Vector3 position, Vector3 scale, Material material, Transform parent, bool solid)
        {
            var go = GameObject.CreatePrimitive(type); go.name = label; go.transform.SetParent(parent); go.transform.position = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            go.layer = solid ? 0 : 2;
            if (!solid) { var collider = go.GetComponent<Collider>(); collider.enabled = false; if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider); }
            return go;
        }
        void EnsureMaterials()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit"); if (!shader) shader = Shader.Find("Standard");
            if (!stone) stone = NewMaterial(shader,"MVP Piedra",new Color(.14f,.22f,.27f));
            if (!floor) floor = NewMaterial(shader,"MVP Suelo",new Color(.09f,.17f,.2f));
            if (!creature) creature = NewMaterial(shader,"MVP Enemigo",new Color(.4f,.75f,.75f));
            if (!marker) { marker = NewMaterial(shader,"MVP Señal",new Color(.2f,.95f,.7f)); marker.EnableKeyword("_EMISSION"); marker.SetColor("_EmissionColor",new Color(.1f,.7f,.4f)); }
        }
        static Material NewMaterial(Shader shader, string label, Color color) { var m = new Material(shader); m.name = label; m.color = color; return m; }

        public void Restart()
        {
            if (geometry) { geometry.gameObject.SetActive(false); Destroy(geometry.gameObject); }
            player = null; visited.Clear(); elapsed = 0; Build(); SetupPasses();
        }
        void Update()
        {
            if (!player || !player.Active) return;
            elapsed += Time.deltaTime;
            int closest = 0; float distance = float.MaxValue;
            for (int i = 0; i < Rooms.Length; i++)
            {
                float d = Vector3.Distance(player.transform.position,RoomPosition(i));
                if (d < distance) { distance = d; closest = i; }
            }
            player.Zone = distance < 9 ? Names[closest] : "Galería · entre cavernas";
            if (distance < 9) visited.Add(closest);
            if (Vector3.Distance(player.transform.position, RoomPosition(7)) < 2.5f) player.Complete();
            foreach (var pass in passes)
            {
                pass.cooldown -= Time.deltaTime;
                if (pass.warning >= 0)
                {
                    pass.warning -= Time.deltaTime;
                    if (pass.warning <= 0)
                    {
                        pass.warning = -1; pass.cooldown = 18;
                        pass.shark = Enemy<CavernShark>("Tiburón · paso letal", pass.to, new Vector3(1.8f,1.4f,4));
                        pass.shark.waypoints = new[] {pass.to, pass.from};
                    }
                }
                else if (!pass.shark && pass.cooldown <= 0 && SegmentDistance(player.transform.position,pass.from,pass.to) < 5)
                    pass.warning = 3;
            }
        }
        static float SegmentDistance(Vector3 p, Vector3 a, Vector3 b) => Vector3.Distance(p,a + (b-a) * Mathf.Clamp01(Vector3.Dot(p-a,b-a)/(b-a).sqrMagnitude));

        void OnGUI()
        {
            if (!player) return;
            if (textStyle == null)
            {
                textStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
                titleStyle = new GUIStyle(textStyle) { fontSize = 22, fontStyle = FontStyle.Bold };
                white = Texture2D.whiteTexture;
            }
            float scale = Mathf.Min(Screen.width / 1100f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(scale,scale,1));
            float width = Screen.width / scale, height = Screen.height / scale;
            GUI.color = new Color(.02f,.05f,.07f,.9f); GUI.DrawTexture(new Rect(16,16,510,194),white); GUI.color = Color.white;
            GUI.Label(new Rect(30,24,480,32),"ANÁDROMO / LABERINTO · MVP",titleStyle);
            GUI.Label(new Rect(30,61,480,27),player.Zone,textStyle);
            GUI.Label(new Rect(30,91,480,27),$"Ruido {player.Velocity.magnitude:0.0} m/s",textStyle);
            GUI.Label(new Rect(30,121,480,65),$"Lampreas {player.AttachedCount} · Velocidad {player.SpeedMultiplier:P0}\nCavernas {visited.Count}/8 · Tiempo {elapsed:0}s · Objetivo: llegar a H",textStyle);
            GUI.Label(new Rect(25,height-88,width-50,80),"WASD nadar · Ratón mirar · Q/E bajar/subir · Shift nadar rápido (ruido)\nAlterna A/D rápido o sacude el ratón para soltar lampreas · R reiniciar · Esc cursor\nPirañas: pasa despacio. Pez linterna: al apagarse el señuelo, esquiva. Tiburón: sal del eje del túnel o cambia de altura.",textStyle);
            foreach (var pass in passes) if (pass.warning >= 0 || pass.shark)
            {
                GUI.color = new Color(1,.45f,.2f);
                GUI.Label(new Rect(width*.5f-230,220,460,60),pass.warning >= 0 ? $"¡TIBURÓN EN {pass.warning:0.0}s! Busca refugio." : "¡TIBURÓN EN EL TÚNEL!",titleStyle); GUI.color = Color.white;
            }
            // A compact graph shows connections, dead ends and the player's location.
            Rect map = new Rect(width-245,16,225,325);
            GUI.color = new Color(.02f,.05f,.07f,.9f); GUI.DrawTexture(map,white); GUI.color = Color.white;
            GUI.Label(new Rect(map.x+12,map.y+8,205,25),"MAPA · A → H",textStyle);
            for (int e = 0; e < Edges.GetLength(0); e++) DrawLine(MapPoint(Rooms[Edges[e,0]],map),MapPoint(Rooms[Edges[e,1]],map),new Color(.3f,.55f,.6f));
            for (int i = 0; i < DeadEnds.Length; i++)
            {
                Vector2 end = MapPoint(DeadEnds[i],map); DrawLine(MapPoint(Rooms[new[]{2,4,5}[i]],map),end,Color.gray);
                GUI.Label(new Rect(end.x-5,end.y-10,20,22),"×",textStyle);
            }
            for (int i = 0; i < Rooms.Length; i++)
            {
                Vector2 p = MapPoint(Rooms[i],map); GUI.color = visited.Contains(i) ? Color.cyan : Color.white;
                GUI.Label(new Rect(p.x-6,p.y-11,22,24),((char)('A'+i)).ToString(),textStyle);
            }
            Vector2 dot = MapPoint(new Vector2(player.transform.position.x/Cell,player.transform.position.z/Cell),map);
            GUI.color = Color.yellow; GUI.DrawTexture(new Rect(dot.x-3,dot.y-3,6,6),white); GUI.color = Color.white;
            if (!player.Active)
            {
                GUI.color = new Color(.02f,.05f,.07f,.95f); GUI.DrawTexture(new Rect(width/2-240,height/2-70,480,140),white); GUI.color = Color.white;
                GUI.Label(new Rect(width/2-220,height/2-50,440,90),player.Finished ? "SALIDA ALCANZADA\nR para volver a probar las mecánicas" : "ENERGIA AGOTADA\nR para reiniciar desde el umbral",titleStyle);
            }
            GUI.matrix = Matrix4x4.identity;
        }
        static Vector2 MapPoint(Vector2 p, Rect r) => new Vector2(r.x+r.width*.5f+p.x*6,r.y+r.height-20-p.y*6);
        void DrawLine(Vector2 a, Vector2 b, Color color)
        {
            // Work in the same logical coordinates as the HUD, including on scaled Game views.
            // Rotating GUI.matrix around a pivot would apply the HUD scale twice.
            GUI.color = color;
            int steps = Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)));
            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(a,b,i/(float)steps);
                GUI.DrawTexture(new Rect(p.x-1,p.y-1,2,2),white);
            }
            GUI.color = Color.white;
        }
    }
}
