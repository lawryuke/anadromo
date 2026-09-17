if (UnityEditor.EditorApplication.isPlaying)
    throw new System.Exception("Stop Play Mode before setting up the passage.");
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (scene.path != "Assets/_Project/Scenes/Blockout_Test.unity")
    throw new System.Exception("Expected Blockout_Test, got " + scene.path);
if (UnityEngine.Object.FindAnyObjectByType<Anadromo.AI.SharkPassage>() != null)
    throw new System.Exception("A shark passage already exists; inspect it before creating another.");
var root = new UnityEngine.GameObject("Shark Passage - 2 + 3");
UnityEditor.Undo.RegisterCreatedObjectUndo(root, "Create shark passage");
root.transform.position = new UnityEngine.Vector3(119.07f, 7.93f, 102.76f);
var passage = UnityEditor.Undo.AddComponent<Anadromo.AI.SharkPassage>(root);
passage.flock = UnityEngine.Object.FindAnyObjectByType<Anadromo.AI.FlockManager>();
for (int i = 0; i < 5; i++)
{
    var capsule = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Capsule);
    UnityEditor.Undo.RegisterCreatedObjectUndo(capsule, "Create shark capsule");
    capsule.name = "Shark " + (i + 1) + (i < 2 ? " - Wave 1" : " - Wave 2");
    capsule.transform.SetParent(root.transform, false);
    capsule.transform.localScale = new UnityEngine.Vector3(1f, 2f, 1f);
    UnityEngine.Object.DestroyImmediate(capsule.GetComponent<UnityEngine.Collider>());
    passage.sharks[i] = capsule.transform;
    capsule.SetActive(i < 2);
}
UnityEditor.EditorUtility.SetDirty(passage);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
UnityEditor.Selection.activeGameObject = root;
return new { scene = scene.path, capsules = passage.sharks.Length, delay = passage.secondGroupDelay, speed = passage.speed };
