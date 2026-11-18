using System;

[Serializable]
public class StationInfo
{
    public int location_id;
    public string location_name;
    public int sensor_id;

    public double latitude;
    public double longitude;

    public string data_source;
}

[Serializable]
public class StationRegistryResponse
{
    public string status;
    public int total_stations;
    public string data_source;

    public StationInfo[] stations;
}