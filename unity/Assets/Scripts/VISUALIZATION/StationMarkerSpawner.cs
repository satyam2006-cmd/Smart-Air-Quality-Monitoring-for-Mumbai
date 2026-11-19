using System.Collections;
using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class StationMarkerSpawner : MonoBehaviour
{
    [Header("References")]
    public StationRegistryManager stationManager;
    public AirQualityApiManager airQualityManager;
    public GameObject stationMarkerPrefab;

    public StationInfoPanel stationInfoPanel;

    [Header("Map Projection Settings")]
    public double centerLatitude = 19.1032405;
    public double centerLongitude = 72.913974;

    public double earthRadius = 6378137.0;
    public float mapScale = 0.0018f;

    [Header("Marker Settings")]
    public float markerHeight = 0.5f;

    private readonly List<GameObject> spawnedObjects = new List<GameObject>();
    private PollutionHeatmapRenderer heatmapRenderer;

[Header("PM2.5 Visualization Thresholds")]
public float greenThreshold = 15f;
public float yellowThreshold = 35f;
public float orangeThreshold = 55f;

[Header("PM2.5 Marker Colors")]
public Color lowColor = Color.green;
public Color elevatedColor = Color.yellow;
public Color highColor = new Color(1f, 0.5f, 0f);
public Color veryHighColor = Color.red;
public Color noDataColor = Color.gray;

[Header("Pollution Heatmap")]
public bool showPollutionHeatmap = true;
[Range(0.05f, 0.8f)] public float heatmapOpacity = 0.38f;

    private void Start()
    {
        heatmapRenderer = GetComponent<PollutionHeatmapRenderer>();
        if (heatmapRenderer == null)
            heatmapRenderer = gameObject.AddComponent<PollutionHeatmapRenderer>();

        heatmapRenderer.centerLatitude = centerLatitude;
        heatmapRenderer.centerLongitude = centerLongitude;
        heatmapRenderer.earthRadius = earthRadius;
        heatmapRenderer.mapScale = mapScale;
        heatmapRenderer.showHeatmap = showPollutionHeatmap;
        heatmapRenderer.opacity = heatmapOpacity;
        heatmapRenderer.greenThreshold = greenThreshold;
        heatmapRenderer.yellowThreshold = yellowThreshold;
        heatmapRenderer.orangeThreshold = orangeThreshold;
        heatmapRenderer.lowColor = lowColor;
        heatmapRenderer.elevatedColor = elevatedColor;
        heatmapRenderer.highColor = highColor;
        heatmapRenderer.veryHighColor = veryHighColor;

        if (airQualityManager != null)
            airQualityManager.DataRefreshed += OnAirQualityRefreshed;
        StartCoroutine(WaitAndSpawnStations());
    }

    private void OnDestroy()
    {
        if (airQualityManager != null)
            airQualityManager.DataRefreshed -= OnAirQualityRefreshed;
    }

    private void OnAirQualityRefreshed()
    {
        if (stationManager != null && stationManager.IsLoaded)
            SpawnStations();
    }

    IEnumerator WaitAndSpawnStations()
    {
        Debug.Log("Waiting for station registry data...");

        if (stationManager == null)
        {
            Debug.LogError("Station Manager is not assigned!");
            yield break;
        }

        if (airQualityManager == null)
        {
            Debug.LogError("Air Quality Manager is not assigned!");
            yield break;
        }

        if (stationMarkerPrefab == null)
        {
            Debug.LogError("Station Marker Prefab is not assigned!");
            yield break;
        }

        float timeout = 60f;
        float elapsed = 0f;

        // Station coordinates are sufficient to create visible map markers.
        // Live readings may fail independently (for example, if OpenAQ is
        // unavailable or its API key is missing), so do not block the map on it.
        while (!stationManager.IsLoaded && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!stationManager.IsLoaded)
        {
            Debug.LogError("Station Registry API loading timed out!");
            yield break;
        }

        if (stationManager.stations == null ||
            stationManager.stations.Length == 0)
        {
            Debug.LogError("No station records available!");
            yield break;
        }

        if (!airQualityManager.IsLoaded)
            Debug.LogWarning("Live readings are not available yet. Spawning station markers without readings; they will update after a successful refresh.");

        SpawnStations();
    }

    Vector3 ConvertGeoToUnity(
        double latitude,
        double longitude)
    {
        double latRad = latitude * Mathf.Deg2Rad;
        double centerLatRad = centerLatitude * Mathf.Deg2Rad;

        double lonRad = longitude * Mathf.Deg2Rad;
        double centerLonRad = centerLongitude * Mathf.Deg2Rad;

        double x = earthRadius * (lonRad - centerLonRad);

        double z = earthRadius * (
            System.Math.Log(
                System.Math.Tan(System.Math.PI / 4.0 + latRad / 2.0)
            )
            -
            System.Math.Log(
                System.Math.Tan(System.Math.PI / 4.0 + centerLatRad / 2.0)
            )
        );

        return new Vector3(
            (float)(x * mapScale),
            markerHeight,
            (float)(z * mapScale)
        );
    }

    void SpawnStations()
    {
        if (heatmapRenderer != null && airQualityManager.airQualityData != null)
            heatmapRenderer.SetHeatmap(airQualityManager.airQualityData.heatmap);

        for (int i = 0; i < spawnedObjects.Count; i++)
        {
            if (spawnedObjects[i] != null)
                Destroy(spawnedObjects[i]);
        }
        spawnedObjects.Clear();

        StationInfo[] stations = stationManager.stations;

        Debug.Log("Starting geographical station spawning...");

        int matchedCount = 0;
        int unmatchedCount = 0;

        for (int i = 0; i < stations.Length; i++)
        {
            StationInfo stationInfo = stations[i];

            Vector3 position = ConvertGeoToUnity(
                stationInfo.latitude,
                stationInfo.longitude
            );

            GameObject marker = Instantiate(
                stationMarkerPrefab,
                transform
            );

            marker.transform.localPosition = position;
            marker.transform.localRotation = Quaternion.identity;

            marker.name = "Station_" + stationInfo.location_id;
            spawnedObjects.Add(marker);

            // Match air quality data using location_id.
            UnityStationAirQuality airData =
                airQualityManager.GetStationData(
                    stationInfo.location_id
                );

                // Ensure marker can receive mouse clicks.
Collider markerCollider = marker.GetComponent<Collider>();

if (markerCollider == null)
{
    SphereCollider sphereCollider =
        marker.AddComponent<SphereCollider>();

    sphereCollider.radius = 0.5f;
}

// Attach click interaction.
StationMarkerClick clickHandler =
    marker.GetComponent<StationMarkerClick>();

if (clickHandler == null)
{
    clickHandler =
        marker.AddComponent<StationMarkerClick>();
}

// Pass station and air quality information.
                clickHandler.Initialize(stationInfo, airData, stationInfoPanel,
                    airQualityManager.GetAlertMessage(stationInfo.location_id));

            AirQualityMarkerColor colorHandler =
    marker.GetComponent<AirQualityMarkerColor>();

if (colorHandler == null)
{
    colorHandler =
        marker.AddComponent<AirQualityMarkerColor>();
}

            colorHandler.SetColor(airData, lowColor, elevatedColor, highColor, veryHighColor, noDataColor,
                greenThreshold, yellowThreshold, orangeThreshold);


            if (airData != null)
            {
                matchedCount++;
            }
            else
            {
                unmatchedCount++;
            }
            ApplyPM25Color(marker, airData);

            CreateStationLabel(
                stationInfo.location_name,
                position,
                airData
            );

            Debug.Log(
                "Station: " + stationInfo.location_name +
                " | Location ID: " + stationInfo.location_id +
                " | PM2.5: " +
                GetPM25Text(airData)
            );
        }

        Debug.Log("======================================");
        Debug.Log("GEOGRAPHICAL SPAWNING COMPLETED!");
        Debug.Log("Total markers: " + stations.Length);
        Debug.Log("Air quality matches: " + matchedCount);
        Debug.Log("Air quality unmatched: " + unmatchedCount);
        Debug.Log("======================================");
    }

    string GetPM25Text(UnityStationAirQuality airData)
    {
        if (airData == null || airData.pm25 == null)
            return "No PM2.5 data";

        return airData.pm25.value + " " + airData.pm25.unit;
    }

    void CreateStationLabel(
        string stationName,
        Vector3 markerPosition,
        UnityStationAirQuality airData)
    {
        GameObject label = new GameObject(
            "Label_" + stationName
        );

        label.transform.SetParent(transform, false);
        spawnedObjects.Add(label);

        label.transform.localPosition =
            markerPosition + Vector3.up * 1.8f;

        TextMeshPro text = label.AddComponent<TextMeshPro>();

        string labelText = stationName;

        if (airData != null && airData.pm25 != null)
        {
            labelText +=
                "\nPM2.5: " +
                airData.pm25.value + " " +
                airData.pm25.unit;

            labelText +=
                "\nStatus: " +
                airData.pm25.freshness;
        }
        else
        {
            labelText += "\nPM2.5: No data";
        }

        text.text = labelText;
        text.fontSize = 0.8f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;

        text.enableAutoSizing = true;
        text.fontSizeMin = 0.35f;
        text.fontSizeMax = 0.8f;

        if (Camera.main != null)
        {
            label.transform.rotation =
                Quaternion.LookRotation(
                    Camera.main.transform.position -
                    label.transform.position
                );
        }
    }

    void ApplyPM25Color(
    GameObject marker,
    UnityStationAirQuality airData)
{
    Color markerColor;

    // No PM2.5 data
    if (airData == null || airData.pm25 == null)
    {
        markerColor = noDataColor;
    }
    else
    {
        float pm25 = airData.pm25.value;

        if (pm25 < 0)
        {
            markerColor = noDataColor;
        }
        else if (pm25 <= greenThreshold)
        {
            markerColor = lowColor;
        }
        else if (pm25 <= yellowThreshold)
        {
            markerColor = elevatedColor;
        }
        else if (pm25 <= orangeThreshold)
        {
            markerColor = highColor;
        }
        else
        {
            markerColor = veryHighColor;
        }
    }

    Renderer[] renderers =
        marker.GetComponentsInChildren<Renderer>(true);

    if (renderers.Length == 0)
    {
        Debug.LogWarning(
            "No Renderer found in marker: " + marker.name
        );
        return;
    }

    MaterialPropertyBlock propertyBlock =
        new MaterialPropertyBlock();

    foreach (Renderer markerRenderer in renderers)
    {
        markerRenderer.GetPropertyBlock(propertyBlock);

        // URP shader property
        propertyBlock.SetColor(
            "_BaseColor",
            markerColor
        );

        // Standard shader property
        propertyBlock.SetColor(
            "_Color",
            markerColor
        );

        markerRenderer.SetPropertyBlock(propertyBlock);
    }

    Debug.Log(
        marker.name +
        " | PM2.5 Color: " + markerColor
    );
}
}
