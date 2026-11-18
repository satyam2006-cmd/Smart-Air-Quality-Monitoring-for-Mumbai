using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Text;

public class StationInfoPanel : MonoBehaviour
{
    [Header("UI References")]
    public GameObject panelRoot;
    public TMP_Text stationTitle;
    public TMP_Text stationDetails;
    public Button closeButton;

    [Header("Scroll References")]
    public ScrollRect scrollRect;

    private void Start()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (closeButton != null)
            closeButton.onClick.AddListener(ClosePanel);
    }

    public void Show(
        StationInfo station,
        UnityStationAirQuality airData,
        string alertMessage = null)
    {
        if (station == null)
            return;

        panelRoot.SetActive(true);

        stationTitle.text = station.location_name;

        StringBuilder details = new StringBuilder();

        details.AppendLine("LOCATION ID: " + station.location_id);
        details.AppendLine();
        details.AppendLine("AIR QUALITY OBSERVATIONS");
        details.AppendLine("------------------------------");
        if (!string.IsNullOrEmpty(alertMessage))
        {
            details.AppendLine(alertMessage);
            details.AppendLine();
        }

        AddReading(details, "PM2.5", airData?.pm25);
        AddReading(details, "PM10", airData?.pm10);
        AddReading(details, "NO2", airData?.no2);
        AddReading(details, "SO2", airData?.so2);
        AddReading(details, "CO", airData?.co);
        AddReading(details, "O3", airData?.o3);
        AddReading(details, "NOX", airData?.nox);
        AddReading(details, "NO", airData?.no);
        AddReading(details, "Temperature", airData?.temperature);
        AddReading(details, "Humidity", airData?.relativehumidity);
        AddReading(details, "Wind Speed", airData?.wind_speed);
        AddReading(details, "Wind Direction", airData?.wind_direction);

        stationDetails.text = details.ToString();

        // Reset scroll position whenever a station is selected.
        if (scrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 1f;
        }
    }

    private void AddReading(
        StringBuilder builder,
        string parameter,
        AirQualityReading reading)
    {
        if (reading == null)
        {
            builder.AppendLine(parameter + ": No data");
            builder.AppendLine();
            return;
        }

        builder.AppendLine(
            parameter + ": " +
            reading.value.ToString("F2") + " " +
            reading.unit
        );

        builder.AppendLine(
            "Freshness: " + reading.freshness
        );

        if (!string.IsNullOrEmpty(reading.datetime_utc))
        {
            builder.AppendLine(
                "Observed: " + reading.datetime_utc
            );
        }

        builder.AppendLine();
    }

    public void ClosePanel()
    {
        panelRoot.SetActive(false);
    }
}
