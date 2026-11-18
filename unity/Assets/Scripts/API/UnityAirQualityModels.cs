using System;

[Serializable]
public class UnityAirQualityResponse
{
    public string status;
    public string source;
    public string data_type;
    public string retrieved_at_utc;

    public int total_stations;
    public int successful_stations;
    public int failed_stations_count;
    public int total_readings;

    public UnityStationAirQuality[] stations;
    public UnityPollutionHeatmap heatmap;
    public UnityApiError[] errors;
}

[Serializable]
public class UnityPollutionHeatmap
{
    public string parameter;
    public string unit;
    public int width;
    public int height;
    public double min_latitude;
    public double max_latitude;
    public double min_longitude;
    public double max_longitude;
    public float[] values;
}

[Serializable]
public class UnityStationAirQuality
{
    public int location_id;
    public string location_name;
    public float latitude;
    public float longitude;
    public int total_readings;

    public AirQualityReading pm25;
    public AirQualityReading pm10;
    public AirQualityReading no2;
    public AirQualityReading so2;
    public AirQualityReading co;
    public AirQualityReading o3;
    public AirQualityReading nox;
    public AirQualityReading no;
    public AirQualityReading temperature;
    public AirQualityReading relativehumidity;
    public AirQualityReading wind_speed;
    public AirQualityReading wind_direction;
}

[Serializable]
public class AirQualityReading
{
    public float value;
    public string unit;
    public string freshness;
    public float age_hours;
    public string datetime_utc;
}

[Serializable]
public class UnityApiError
{
    public int location_id;
    public string location_name;
    public string error;
}
