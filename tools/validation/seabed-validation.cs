// Run in Edit Mode via unity command eval_file. Checks persisted geometry and clear routes.
if (UnityEditor.EditorApplication.isPlaying) throw new System.Exception("Run outside Play Mode.");
var root=UnityEngine.GameObject.Find("Seabed_RockyRift");
if(root==null) throw new System.Exception("Seabed missing.");
UnityEngine.Physics.SyncTransforms();
var hits=new System.Collections.Generic.List<string>();
foreach(var f in root.GetComponentsInChildren<UnityEngine.MeshFilter>()) {
    if(f.sharedMesh==null || !UnityEditor.AssetDatabase.Contains(f.sharedMesh)) throw new System.Exception("Unpersisted mesh: "+f.name);
    if(f.GetComponent<UnityEngine.MeshCollider>().sharedMesh!=f.sharedMesh) throw new System.Exception("Collider mismatch: "+f.name);
}
foreach(float z in new[]{50f,65f,85f}) {
    UnityEngine.RaycastHit h;
    if(!UnityEngine.Physics.Raycast(new UnityEngine.Vector3(125,55,z),UnityEngine.Vector3.down,out h,20)) throw new System.Exception("Missing approach floor.");
    if(h.point.y>47 || h.normal.y<.7f) throw new System.Exception("Approach obstructed or bad surface normals.");
    hits.Add("Approach z="+z+", floor y="+h.point.y);
}
var anchor=root.transform.Find("04_Encounter_Anchors/Bloop_Ascent");
// Sample a 10-metre diameter vertical corridor above the Bloop anchor.
foreach(float x in new[]{-5f,0f,5f}) foreach(float z in new[]{-5f,0f,5f}) {
    var origin=anchor.position+new UnityEngine.Vector3(x,0,z);
    if(UnityEngine.Physics.Raycast(origin,UnityEngine.Vector3.up,100)) throw new System.Exception("Blocked ascent corridor: "+origin);
}
UnityEngine.RaycastHit bottom;
if(!UnityEngine.Physics.Raycast(new UnityEngine.Vector3(125,60,103),UnityEngine.Vector3.down,out bottom,100) || bottom.point.y> -14)
    throw new System.Exception("Rift is not deep enough.");
var camera=UnityEngine.Camera.main;
if(camera.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>().enabled) throw new System.Exception("XR tracking active.");
var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if(scene.isDirty) throw new System.Exception("Scene is not saved.");
return new {result="PASS", approach=hits, clearAscentDiameter=10, bottomY=bottom.point.y, meshColliders=root.GetComponentsInChildren<UnityEngine.MeshCollider>().Length, saved=!scene.isDirty};
