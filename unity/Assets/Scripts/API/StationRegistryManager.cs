using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class StationRegistryManager : MonoBehaviour
{
    private string apiUrl =
        "http://127.0.0.1:8000/api/v1/stations";

    public StationInfo[] stations;

    public bool IsLoaded { get; private set; } = false;

    void Start()
    {
        StartCoroutine(GetStations());
    }

    IEnumerator GetStations()
    {
        Debug.Log("Fetching Mumbai monitoring stations...");

        using (UnityWebRequest request = UnityWebRequest.Get(apiUrl))
        {
            request.timeout = 30;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string jsonResponse = request.downloadHandler.text;

                StationRegistryResponse response =
                    JsonUtility.FromJson<StationRegistryResponse>(
                        jsonResponse
                    );

                if (response != null && response.stations != null)
                {
                    stations = response.stations;
                    IsLoaded = true;

                    Debug.Log("STATION DATA RECEIVED SUCCESSFULLY!");
                    Debug.Log("Total Stations: " + stations.Length);

                    foreach (StationInfo station in stations)
                    {
                        Debug.Log(
                            "Station: " + station.location_name +
                            " | Latitude: " + station.latitude +
                            " | Longitude: " + station.longitude
                        );
                    }
                }
                else
                {
                    Debug.LogError("Invalid station JSON response.");
                }
            }
            else
            {
                Debug.LogError(
                    "Station API Failed: " + request.error
                );

                Debug.LogError(
                    "Response: " + request.downloadHandler.text
                );
            }
        }
    }
}