using UnityEngine;

public class AirQualityMarkerColor : MonoBehaviour
{
    public void SetColor(
        UnityStationAirQuality airData,
        Color lowColor,
        Color elevatedColor,
        Color highColor,
        Color veryHighColor,
        Color noDataColor,
        float greenThreshold,
        float yellowThreshold,
        float orangeThreshold)
    {
        Renderer[] renderers =
            GetComponentsInChildren<Renderer>();

        Color markerColor = GetPM25Color(airData, lowColor, elevatedColor, highColor,
            veryHighColor, noDataColor, greenThreshold, yellowThreshold, orangeThreshold);

        foreach (Renderer rend in renderers)
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            rend.GetPropertyBlock(block);
            block.SetColor("_BaseColor", markerColor);
            block.SetColor("_Color", markerColor);
            rend.SetPropertyBlock(block);
        }
    }

    private Color GetPM25Color(UnityStationAirQuality airData, Color lowColor,
        Color elevatedColor, Color highColor, Color veryHighColor, Color noDataColor,
        float greenThreshold, float yellowThreshold, float orangeThreshold)
    {
        if (airData == null || airData.pm25 == null)
            return noDataColor;

        float value = airData.pm25.value;

        if (value < 0f)
            return noDataColor;

        if (value <= greenThreshold)
            return lowColor;

        if (value <= yellowThreshold)
            return elevatedColor;

        if (value <= orangeThreshold)
            return highColor;

        return veryHighColor;
    }
}
