if (!UnityEngine.Application.isPlaying) throw new System.Exception("Play Mode required.");
var passage = UnityEngine.Object.FindAnyObjectByType<Anadromo.AI.SharkPassage>();
if (passage == null || passage.sharks.Length != 5) throw new System.Exception("Expected five sharks.");
var sample = typeof(Anadromo.AI.SharkPassage).GetMethod("ApplyTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
void Check(float time, int count)
{
    sample.Invoke(passage, new object[] { time });
    if (passage.sharks.Count(s => s.gameObject.activeSelf) != count)
        throw new System.Exception("Incorrect wave count at " + time);
}
Check(0f, 2);
foreach (var shark in passage.sharks)
    if (UnityEngine.Vector3.Distance(shark.position, passage.startPoint) > 0.001f)
        throw new System.Exception("Incorrect start position.");
Check(1.999f, 2);
if (passage.sharks[0].position.y <= passage.startPoint.y)
    throw new System.Exception("First wave did not move upward.");
Check(2f, 5);
for (int i = 2; i < 5; i++)
    if (UnityEngine.Vector3.Distance(passage.sharks[i].position, passage.startPoint) > 0.001f)
        throw new System.Exception("Second wave did not start at the specified point.");
Check(3f, 5);
if (passage.sharks[2].position.y <= passage.startPoint.y)
    throw new System.Exception("Second wave did not move.");
Check(100f, 5);
foreach (var shark in passage.sharks)
    if (UnityEngine.Vector3.Distance(shark.position, passage.endPoint) > 0.001f)
        throw new System.Exception("Incorrect end position.");
return "PASS: five capsules; two at t=0, three more at t=2; upward movement; exact start and end coordinates.";
