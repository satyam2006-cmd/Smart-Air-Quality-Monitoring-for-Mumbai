using UnityEngine;

public class StationMarkerClick : MonoBehaviour
{
    private StationInfo stationInfo;
    private UnityStationAirQuality airData;
    private StationInfoPanel infoPanel;
    private string alertMessage;

    public void Initialize(
        StationInfo info,
        UnityStationAirQuality qualityData,
        StationInfoPanel panel,
        string activeAlert = null)
    {
        stationInfo = info;
        airData = qualityData;
        infoPanel = panel;
        alertMessage = activeAlert;
    }

    private void OnMouseDown()
    {
        if (stationInfo == null)
        {
            Debug.LogWarning("Station information not assigned!");
            return;
        }

        Debug.Log("Selected station: " + stationInfo.location_name);

        if (infoPanel != null)
        {
            infoPanel.Show(stationInfo, airData, alertMessage);
        }
        else
        {
            Debug.LogError("Station Info Panel is not assigned!");
        }
    }
}
