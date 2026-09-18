using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class TerrainCompatibility
{
    public static void Export()
    {
        Directory.CreateDirectory("Export");
        foreach (string name in new[] { "New Terrain", "New Terrain 1" })
        {
            string path = "Assets/" + name + ".asset";
            var terrain = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
            if (terrain == null) throw new Exception("Cannot load " + path);
            Debug.Log(name + ": heights=" + terrain.heightmapResolution + ", layers=" + terrain.alphamapLayers + ", trees=" + terrain.treeInstanceCount + ", detail prototypes=" + terrain.detailPrototypes.Length);
            if (terrain.alphamapLayers != 0 || terrain.treeInstanceCount != 0 || terrain.detailPrototypes.Length != 0)
                throw new Exception("This conversion expects terrains without painted layers, trees or details.");
            using (var writer = new BinaryWriter(File.Create("Export/" + name + ".bin")))
            {
                writer.Write(terrain.heightmapResolution);
                writer.Write(terrain.size.x); writer.Write(terrain.size.y); writer.Write(terrain.size.z);
                writer.Write(terrain.alphamapResolution); writer.Write(terrain.baseMapResolution);
                writer.Write(terrain.detailResolution); writer.Write(terrain.detailResolutionPerPatch);
                writer.Write(terrain.wavingGrassSpeed); writer.Write(terrain.wavingGrassStrength); writer.Write(terrain.wavingGrassAmount);
                var tint = terrain.wavingGrassTint;
                writer.Write(tint.r); writer.Write(tint.g); writer.Write(tint.b); writer.Write(tint.a);
                writer.Write((int)terrain.detailScatterMode);
                writer.Write(terrain.enableHolesTextureCompression);
                foreach (float height in terrain.GetHeights(0, 0, terrain.heightmapResolution, terrain.heightmapResolution)) writer.Write(height);
                writer.Write(terrain.holesResolution);
                foreach (bool hole in terrain.GetHoles(0, 0, terrain.holesResolution, terrain.holesResolution)) writer.Write(hole);
            }
        }
        AssetDatabase.SaveAssets();
    }

    public static void Import()
    {
        Directory.CreateDirectory("Assets/Converted");
        foreach (string name in new[] { "New Terrain", "New Terrain 1" })
        {
            using (var reader = new BinaryReader(File.OpenRead("Export/" + name + ".bin")))
            {
                int resolution = reader.ReadInt32();
                var terrain = new TerrainData { name = name, heightmapResolution = resolution };
                terrain.size = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                terrain.alphamapResolution = reader.ReadInt32(); terrain.baseMapResolution = reader.ReadInt32();
                terrain.SetDetailResolution(reader.ReadInt32(), reader.ReadInt32());
                terrain.wavingGrassSpeed = reader.ReadSingle(); terrain.wavingGrassStrength = reader.ReadSingle(); terrain.wavingGrassAmount = reader.ReadSingle();
                terrain.wavingGrassTint = new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                terrain.SetDetailScatterMode((DetailScatterMode)reader.ReadInt32());
                terrain.enableHolesTextureCompression = reader.ReadBoolean();
                var heights = new float[resolution, resolution];
                for (int y = 0; y < resolution; y++) for (int x = 0; x < resolution; x++) heights[y, x] = reader.ReadSingle();
                terrain.SetHeights(0, 0, heights);
                int holesResolution = reader.ReadInt32();
                var holes = new bool[holesResolution, holesResolution];
                for (int y = 0; y < holesResolution; y++) for (int x = 0; x < holesResolution; x++) holes[y, x] = reader.ReadBoolean();
                terrain.SetHoles(0, 0, holes);
                string path = "Assets/Converted/" + name + ".asset";
                AssetDatabase.CreateAsset(terrain, path);
                AssetDatabase.SaveAssets();
                var loaded = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
                var actual = loaded.GetHeights(0, 0, resolution, resolution);
                var actualHoles = loaded.GetHoles(0, 0, holesResolution, holesResolution);
                float maxError = 0;
                for (int y = 0; y < resolution; y++) for (int x = 0; x < resolution; x++) maxError = Mathf.Max(maxError, Mathf.Abs(actual[y, x] - heights[y, x]));
                if (maxError > 0.000031f) throw new Exception("Height conversion lost precision: " + maxError);
                for (int y = 0; y < holesResolution; y++) for (int x = 0; x < holesResolution; x++)
                    if (actualHoles[y, x] != holes[y, x]) throw new Exception("Holes differ");
                Debug.Log("CONVERSION PASS: " + name + "; max height error=" + maxError + "; size=" + loaded.size);
            }
        }
    }
}
