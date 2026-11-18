using UnityEngine;

public class PollutionHeatmapRenderer : MonoBehaviour
{
    [Header("Layer")]
    public bool showHeatmap = true;
    [Range(0.05f, 0.8f)] public float opacity = 0.38f;
    public float heightAboveMap = 0.22f;

    [Header("Projection (match StationMarkerSpawner)")]
    public double centerLatitude = 19.1032405;
    public double centerLongitude = 72.913974;
    public double earthRadius = 6378137.0;
    public float mapScale = 0.0018f;

    [Header("PM2.5 Color Scale")]
    public float greenThreshold = 15f;
    public float yellowThreshold = 35f;
    public float orangeThreshold = 55f;
    public Color lowColor = Color.green;
    public Color elevatedColor = Color.yellow;
    public Color highColor = new Color(1f, 0.5f, 0f);
    public Color veryHighColor = Color.red;

    private GameObject heatmapObject;
    private Mesh heatmapMesh;
    private Material heatmapMaterial;

    public void SetHeatmap(UnityPollutionHeatmap data)
    {
        if (!showHeatmap || !IsValid(data))
        {
            ClearHeatmap();
            return;
        }

        ClearHeatmap();

        heatmapObject = new GameObject("PM2.5 Pollution Heatmap");
        heatmapObject.transform.SetParent(transform, false);
        heatmapObject.transform.localPosition = Vector3.up * heightAboveMap;
        heatmapObject.transform.localRotation = Quaternion.identity;

        MeshFilter meshFilter = heatmapObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = heatmapObject.AddComponent<MeshRenderer>();

        heatmapMesh = BuildMesh(data);
        meshFilter.sharedMesh = heatmapMesh;

        Shader shader = Shader.Find("AirQuality/Heatmap");
        if (shader == null)
        {
            Debug.LogError("AirQuality/Heatmap shader is missing. The pollution heatmap cannot render.");
            ClearHeatmap();
            return;
        }

        heatmapMaterial = new Material(shader);
        heatmapMaterial.SetFloat("_Opacity", opacity);
        meshRenderer.sharedMaterial = heatmapMaterial;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
    }

    private bool IsValid(UnityPollutionHeatmap data)
    {
        return data != null && data.width >= 2 && data.height >= 2 &&
            data.values != null && data.values.Length == data.width * data.height &&
            data.max_latitude > data.min_latitude && data.max_longitude > data.min_longitude;
    }

    private Mesh BuildMesh(UnityPollutionHeatmap data)
    {
        int vertexCount = data.width * data.height;
        Vector3[] vertices = new Vector3[vertexCount];
        Color[] colors = new Color[vertexCount];
        int[] triangles = new int[(data.width - 1) * (data.height - 1) * 6];

        for (int y = 0; y < data.height; y++)
        {
            double latitude = data.min_latitude +
                (data.max_latitude - data.min_latitude) * y / (data.height - 1);

            for (int x = 0; x < data.width; x++)
            {
                double longitude = data.min_longitude +
                    (data.max_longitude - data.min_longitude) * x / (data.width - 1);
                int index = y * data.width + x;
                Vector3 point = Project(latitude, longitude);
                vertices[index] = new Vector3(point.x, 0f, point.z);
                colors[index] = ColorForValue(data.values[index]);
            }
        }

        int triangleIndex = 0;
        for (int y = 0; y < data.height - 1; y++)
        {
            for (int x = 0; x < data.width - 1; x++)
            {
                int lowerLeft = y * data.width + x;
                int lowerRight = lowerLeft + 1;
                int upperLeft = lowerLeft + data.width;
                int upperRight = upperLeft + 1;

                // Order the vertices so the surface normal faces upward.
                triangles[triangleIndex++] = lowerLeft;
                triangles[triangleIndex++] = upperLeft;
                triangles[triangleIndex++] = lowerRight;
                triangles[triangleIndex++] = lowerRight;
                triangles[triangleIndex++] = upperLeft;
                triangles[triangleIndex++] = upperRight;
            }
        }

        Mesh mesh = new Mesh { name = "PM2.5 Heatmap Grid" };
        if (vertexCount > 65535)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private Vector3 Project(double latitude, double longitude)
    {
        double latRadians = latitude * Mathf.Deg2Rad;
        double centerLatRadians = centerLatitude * Mathf.Deg2Rad;
        double x = earthRadius * (longitude - centerLongitude) * Mathf.Deg2Rad;
        double z = earthRadius * (
            System.Math.Log(System.Math.Tan(System.Math.PI / 4.0 + latRadians / 2.0)) -
            System.Math.Log(System.Math.Tan(System.Math.PI / 4.0 + centerLatRadians / 2.0))
        );
        return new Vector3((float)(x * mapScale), 0f, (float)(z * mapScale));
    }

    private Color ColorForValue(float value)
    {
        Color color;
        if (value <= greenThreshold)
            color = lowColor;
        else if (value <= yellowThreshold)
            color = Color.Lerp(lowColor, elevatedColor,
                Mathf.InverseLerp(greenThreshold, yellowThreshold, value));
        else if (value <= orangeThreshold)
            color = Color.Lerp(elevatedColor, highColor,
                Mathf.InverseLerp(yellowThreshold, orangeThreshold, value));
        else
            color = Color.Lerp(highColor, veryHighColor,
                Mathf.Clamp01((value - orangeThreshold) / Mathf.Max(orangeThreshold, 1f)));

        color.a = 1f;
        return color;
    }

    private void ClearHeatmap()
    {
        if (heatmapObject != null)
            Destroy(heatmapObject);
        if (heatmapMesh != null)
            Destroy(heatmapMesh);
        if (heatmapMaterial != null)
            Destroy(heatmapMaterial);

        heatmapObject = null;
        heatmapMesh = null;
        heatmapMaterial = null;
    }

    private void OnDestroy()
    {
        ClearHeatmap();
    }
}
