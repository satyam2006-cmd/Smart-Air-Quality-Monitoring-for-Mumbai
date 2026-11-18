using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.Networking;

public class AirQualityApiManager : MonoBehaviour
{
    [Header("FastAPI Configuration")]
    public string apiUrl =
        "http://127.0.0.1:8000/api/v1/air-quality/unity";

    [Min(10f)]
    public float refreshIntervalSeconds = 300f;

    [Header("Automatic PM2.5 Alerts (same units as API)")]
    public float alertThreshold = 55f;

    [Header("API Status")]
    public bool isLoading;
    public bool isConnected;

    public UnityAirQualityResponse airQualityData;

    public bool IsLoaded { get; private set; }
    public bool HasFailed { get; private set; }

    private Dictionary<int, UnityStationAirQuality> stationLookup =
        new Dictionary<int, UnityStationAirQuality>();

    public event Action DataRefreshed;
    public string LastRefreshUtc { get; private set; }
    private bool requestInProgress;
    private readonly Dictionary<int, bool> alertState = new Dictionary<int, bool>();

    private void Start()
    {
        StartCoroutine(RefreshLoop());
    }

    private IEnumerator RefreshLoop()
    {
        while (true)
        {
            yield return FetchAirQualityData();
            yield return new WaitForSeconds(Mathf.Max(10f, refreshIntervalSeconds));
        }
    }

    public IEnumerator FetchAirQualityData()
    {
        if (requestInProgress)
            yield break;

        requestInProgress = true;
        isLoading = true;
        HasFailed = false;

        Debug.Log("Fetching air quality data from FastAPI...");

        using (UnityWebRequest request = UnityWebRequest.Get(apiUrl))
        {
            request.timeout = 60;

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Air Quality API Error: " + request.error);
                Debug.LogError("Response: " + request.downloadHandler.text);

                HasFailed = true;
                isLoading = false;
                requestInProgress = false;
                yield break;
            }

            string jsonResponse = request.downloadHandler.text;

            airQualityData =
                JsonUtility.FromJson<UnityAirQualityResponse>(jsonResponse);

            if (airQualityData == null ||
                airQualityData.stations == null)
            {
                Debug.LogError("Could not parse air quality station data.");

                HasFailed = true;
                isLoading = false;
                requestInProgress = false;
                yield break;
            }

            stationLookup.Clear();

            foreach (UnityStationAirQuality station in airQualityData.stations)
            {
                if (station == null)
                    continue;

                if (!stationLookup.ContainsKey(station.location_id))
                {
                    stationLookup.Add(station.location_id, station);
                }
            }

            UpdateAlerts();

            isConnected = true;
            IsLoaded = true;
            LastRefreshUtc = airQualityData.retrieved_at_utc;

            Debug.Log("========== AIR QUALITY API ==========");
            Debug.Log("Total stations: " + airQualityData.total_stations);
            Debug.Log("Received stations: " + stationLookup.Count);
            Debug.Log("Total readings: " + airQualityData.total_readings);

            Debug.Log("Air quality data indexed by location_id.");
            Debug.Log("=====================================");
            DataRefreshed?.Invoke();
        }

        isLoading = false;
        requestInProgress = false;
    }

    public void RefreshNow()
    {
        if (!requestInProgress)
            StartCoroutine(FetchAirQualityData());
    }

    private void UpdateAlerts()
    {
        foreach (UnityStationAirQuality station in stationLookup.Values)
        {
            if (station.pm25 == null)
                continue;

            bool isAlert = station.pm25.value >= alertThreshold;
            bool wasAlert;
            bool hadPreviousState = alertState.TryGetValue(station.location_id, out wasAlert);
            alertState[station.location_id] = isAlert;

            if (isAlert && (!hadPreviousState || !wasAlert))
            {
                Debug.LogWarning("AIR QUALITY ALERT: " + station.location_name +
                    " PM2.5 is " + station.pm25.value + " " + station.pm25.unit +
                    " (threshold " + alertThreshold + ").");
            }
        }
    }

    public string GetAlertMessage(int locationId)
    {
        UnityStationAirQuality station = GetStationData(locationId);
        if (station == null || station.pm25 == null || station.pm25.value < alertThreshold)
            return null;

        return "AIR QUALITY ALERT: PM2.5 is " + station.pm25.value.ToString("F1") +
            " " + station.pm25.unit + " (threshold " + alertThreshold.ToString("F1") + ").";
    }

    public UnityStationAirQuality GetStationData(int locationId)
    {
        if (stationLookup.TryGetValue(
            locationId,
            out UnityStationAirQuality station))
        {
            return station;
        }

        return null;
    }
}
