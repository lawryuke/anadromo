using UnityEngine;
using UnityEngine.Rendering;

namespace Anadromo.Locomotion
{
    /// <summary>
    /// Utility class that generates a procedural mesh for a salmon pectoral fin at runtime.
    /// Used for first-person VR visualization on Oculus Quest 2.
    /// </summary>
    public static class ProceduralFinMesh
    {
        /// <summary>
        /// Creates an elongated, slightly curved paddle/leaf shaped mesh 
        /// representing a salmon pectoral fin.
        /// Low poly (60 triangles) for Quest 2 performance.
        /// </summary>
        /// <returns>A generated Mesh object</returns>
        public static Mesh CreateFinMesh()
        {
            Mesh finMesh = new Mesh();
            finMesh.name = "ProceduralSalmonFin";

            // Grid resolution
            int widthSegments = 5;
            int lengthSegments = 6;
            
            int vertexCount = (widthSegments + 1) * (lengthSegments + 1);
            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            Vector3[] normals = new Vector3[vertexCount];
            int[] triangles = new int[widthSegments * lengthSegments * 6];

            float maxLength = 0.25f; // Z axis
            float maxWidth = 0.15f;  // X axis
            float camberDepth = 0.02f; // Y axis curvature

            int v = 0;
            for (int z = 0; z <= lengthSegments; z++)
            {
                float zPercent = (float)z / lengthSegments;
                float currentZ = zPercent * maxLength;

                // Tapering logic to create a leaf/paddle shape
                // Base (z=0) is narrow, middle is wide, tip (z=1) tapers to a point
                float widthMultiplier = Mathf.Sin(zPercent * Mathf.PI); 
                // Adjust base width so it's not exactly 0 but small (attachment point)
                if (zPercent < 0.2f)
                {
                    widthMultiplier = Mathf.Lerp(0.3f, widthMultiplier, zPercent / 0.2f);
                }
                float currentWidth = maxWidth * widthMultiplier;

                for (int x = 0; x <= widthSegments; x++)
                {
                    float xPercent = (float)x / widthSegments;
                    // Centered on X axis
                    float currentX = Mathf.Lerp(-currentWidth / 2f, currentWidth / 2f, xPercent);

                    // Camber (curvature) - inverted parabola across the width, and slightly along length
                    float xDistFromCenter = (xPercent - 0.5f) * 2f; // -1 to 1
                    float currentY = (1f - (xDistFromCenter * xDistFromCenter)) * camberDepth;
                    // Taper camber at the tip
                    currentY *= (1f - zPercent);

                    vertices[v] = new Vector3(currentX, currentY, currentZ);
                    uvs[v] = new Vector2(zPercent, xPercent);
                    normals[v] = Vector3.up; // Mostly up, simplified for low poly
                    v++;
                }
            }

            int t = 0;
            for (int z = 0; z < lengthSegments; z++)
            {
                for (int x = 0; x < widthSegments; x++)
                {
                    int current = x + z * (widthSegments + 1);
                    int next = current + widthSegments + 1;

                    // Triangle 1
                    triangles[t++] = current;
                    triangles[t++] = next;
                    triangles[t++] = current + 1;

                    // Triangle 2
                    triangles[t++] = current + 1;
                    triangles[t++] = next;
                    triangles[t++] = next + 1;
                }
            }

            finMesh.vertices = vertices;
            finMesh.uv = uvs;
            finMesh.triangles = triangles;
            finMesh.normals = normals;
            
            // Recalculate normals properly based on surface for better lighting
            finMesh.RecalculateNormals();
            finMesh.RecalculateBounds();

            return finMesh;
        }

        /// <summary>
        /// Creates a minimalist, transparent, silvery-blue URP Lit material
        /// optimized for performance (no shadows).
        /// </summary>
        /// <returns>A configured Material object</returns>
        public static Material CreateFinMaterial()
        {
            // Use URP Lit shader
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogWarning("[Anadromo] ProceduralFinMesh: URP Lit shader not found. Using standard.");
                urpLit = Shader.Find("Standard");
            }

            Material mat = new Material(urpLit);
            mat.name = "FinMaterial";

            // Set to transparent
            mat.SetFloat("_Surface", 1); // 1 = Transparent
            mat.SetFloat("_Blend", 0);   // 0 = Alpha
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            
            // Allow alpha clipping to be off
            mat.SetFloat("_AlphaClip", 0);

            // Set properties
            Color finColor = new Color(0.7f, 0.8f, 0.9f, 0.25f);
            mat.SetColor("_BaseColor", finColor);
            mat.SetColor("_Color", finColor); // Fallback for standard
            
            mat.SetFloat("_Smoothness", 0.8f);
            mat.SetFloat("_Metallic", 0.1f);
            
            // Disable shadow casting and receiving for performance
            mat.SetFloat("_ReceiveShadows", 0);
            
            // Enable transparency keywords for URP
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)RenderQueue.Transparent;

            return mat;
        }
    }
}
