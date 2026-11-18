import os
import math
import requests

from datetime import datetime, timezone
from fastapi import APIRouter, HTTPException

from .stations import get_stations


router = APIRouter()

OPENAQ_BASE_URL = "https://api.openaq.org/v3"
REQUEST_TIMEOUT = 30


# =========================================================
# SENSOR METADATA CACHE
# =========================================================

# Stores sensor metadata grouped by OpenAQ location ID.
LOCATION_SENSOR_CACHE = {}

# Stores sensor metadata individually by sensor ID.
SENSOR_METADATA_CACHE = {}


# =========================================================
# COMMON API FUNCTIONS
# =========================================================

def get_api_headers():

    api_key = os.getenv("OPENAQ_API_KEY")

    if not api_key:
        raise HTTPException(
            status_code=500,
            detail="OPENAQ_API_KEY environment variable is missing."
        )

    return {
        "X-API-Key": api_key,
        "Accept": "application/json"
    }


def openaq_get(session, url):

    try:
        response = session.get(
            url,
            timeout=REQUEST_TIMEOUT
        )

        if response.status_code in (401, 403):
            raise HTTPException(
                status_code=401,
                detail="OpenAQ authentication failed. Check your API key."
            )

        if response.status_code == 404:
            raise HTTPException(
                status_code=404,
                detail="Requested OpenAQ resource was not found."
            )

        if response.status_code == 429:
            raise HTTPException(
                status_code=429,
                detail="OpenAQ API rate limit reached. Please wait before retrying."
            )

        response.raise_for_status()

        return response.json()

    except requests.exceptions.Timeout:
        raise HTTPException(
            status_code=504,
            detail="OpenAQ API request timed out."
        )

    except requests.exceptions.RequestException as error:
        raise HTTPException(
            status_code=502,
            detail=f"OpenAQ API request failed: {str(error)}"
        )


# =========================================================
# SENSOR METADATA FUNCTIONS
# =========================================================

def get_location_sensor_metadata(session, location_id):

    # Return cached metadata if already available.
    if location_id in LOCATION_SENSOR_CACHE:
        return LOCATION_SENSOR_CACHE[location_id]

    sensors_url = (
        f"{OPENAQ_BASE_URL}/locations/{location_id}/sensors"
    )

    data = openaq_get(session, sensors_url)

    sensor_results = data.get("results", [])

    location_sensor_map = {}

    for sensor in sensor_results:

        sensor_id = sensor.get("id")

        parameter = sensor.get("parameter") or {}

        if not sensor_id:
            continue

        metadata = {
            "sensor_id": sensor_id,
            "parameter": parameter.get("name"),
            "display_name": parameter.get("displayName"),
            "unit": parameter.get("units")
        }

        location_sensor_map[sensor_id] = metadata

        SENSOR_METADATA_CACHE[sensor_id] = metadata

    LOCATION_SENSOR_CACHE[location_id] = location_sensor_map

    return location_sensor_map


def get_single_sensor_metadata(session, sensor_id):

    # First check individual sensor cache.
    if sensor_id in SENSOR_METADATA_CACHE:
        return SENSOR_METADATA_CACHE[sensor_id]

    sensor_url = (
        f"{OPENAQ_BASE_URL}/sensors/{sensor_id}"
    )

    data = openaq_get(session, sensor_url)

    results = data.get("results", [])

    if not results:
        return {}

    sensor = results[0]

    parameter = sensor.get("parameter") or {}

    metadata = {
        "sensor_id": sensor_id,
        "parameter": parameter.get("name"),
        "display_name": parameter.get("displayName"),
        "unit": parameter.get("units")
    }

    SENSOR_METADATA_CACHE[sensor_id] = metadata

    return metadata


# =========================================================
# DATA FRESHNESS CALCULATION
# =========================================================

def calculate_freshness(datetime_data):

    utc_value = (datetime_data or {}).get("utc")

    if not utc_value:
        return {
            "status": "unknown",
            "age_hours": None
        }

    try:

        observation_time = datetime.fromisoformat(
            utc_value.replace("Z", "+00:00")
        )

        # If timezone is missing, assume UTC.
        if observation_time.tzinfo is None:
            observation_time = observation_time.replace(
                tzinfo=timezone.utc
            )

        now = datetime.now(timezone.utc)

        age_seconds = (
            now - observation_time
        ).total_seconds()

        # Future timestamps cannot be classified.
        if age_seconds < 0:
            return {
                "status": "unknown",
                "age_hours": None
            }

        age_hours = round(
            age_seconds / 3600,
            2
        )

        if age_hours <= 3:
            freshness_status = "fresh"

        elif age_hours <= 24:
            freshness_status = "recent"

        else:
            freshness_status = "stale"

        return {
            "status": freshness_status,
            "age_hours": age_hours
        }

    except (ValueError, TypeError):

        return {
            "status": "unknown",
            "age_hours": None
        }


# =========================================================
# FORMAT ONE AIR QUALITY READING
# =========================================================

def format_air_quality_reading(
    reading,
    sensor_metadata
):

    sensor_id = reading.get("sensorsId")

    coordinates = reading.get("coordinates") or {}

    datetime_data = reading.get("datetime") or {}

    freshness = calculate_freshness(datetime_data)

    return {

        "sensor_id": sensor_id,

        "parameter": sensor_metadata.get("parameter"),

        "display_name": sensor_metadata.get("display_name"),

        "unit": sensor_metadata.get("unit"),

        "value": reading.get("value"),

        "datetime": {
            "utc": datetime_data.get("utc"),
            "local": datetime_data.get("local")
        },

        "freshness": freshness["status"],

        "age_hours": freshness["age_hours"],

        "metadata_missing": not bool(
            sensor_metadata.get("parameter")
        ),

        "latitude": coordinates.get("latitude"),

        "longitude": coordinates.get("longitude")
    }


# =========================================================
# 1. TEST LATEST READINGS FOR ONE LOCATION
# =========================================================

@router.get("/api/v1/air-quality/test/{location_id}")
def test_live_air_quality(location_id: int):

    headers = get_api_headers()

    url = (
        f"{OPENAQ_BASE_URL}/locations/"
        f"{location_id}/latest"
    )

    with requests.Session() as session:

        session.headers.update(headers)

        data = openaq_get(session, url)

    results = data.get("results", [])

    return {
        "status": "success",
        "source": "OpenAQ API v3",
        "location_id": location_id,
        "total_readings": len(results),
        "data": results
    }


# =========================================================
# 2. GET DETAILS FOR ONE SENSOR
# =========================================================

@router.get("/api/v1/air-quality/sensor/{sensor_id}")
def get_sensor_details(sensor_id: int):

    headers = get_api_headers()

    with requests.Session() as session:

        session.headers.update(headers)

        sensor_url = (
            f"{OPENAQ_BASE_URL}/sensors/{sensor_id}"
        )

        data = openaq_get(session, sensor_url)

    results = data.get("results", [])

    if not results:
        raise HTTPException(
            status_code=404,
            detail="Sensor not found."
        )

    sensor = results[0]

    return {
        "status": "success",
        "sensor": sensor
    }


# =========================================================
# 3. DETAILED READINGS FOR ONE LOCATION
# =========================================================

@router.get("/api/v1/air-quality/test-detailed/{location_id}")
def get_detailed_air_quality(location_id: int):

    headers = get_api_headers()

    with requests.Session() as session:

        session.headers.update(headers)

        # Fetch latest readings.
        latest_url = (
            f"{OPENAQ_BASE_URL}/locations/"
            f"{location_id}/latest"
        )

        latest_data = openaq_get(
            session,
            latest_url
        )

        latest_results = latest_data.get("results", [])

        # Fetch sensor metadata.
        sensor_map = get_location_sensor_metadata(
            session,
            location_id
        )

        detailed_readings = []

        for reading in latest_results:

            sensor_id = reading.get("sensorsId")

            if not sensor_id:
                continue

            metadata = sensor_map.get(sensor_id)

            if not metadata:
                metadata = get_single_sensor_metadata(
                    session,
                    sensor_id
                )

            detailed_readings.append(
                format_air_quality_reading(
                    reading,
                    metadata
                )
            )

    return {

        "status": "success",

        "source": "OpenAQ API v3",

        "location_id": location_id,

        "total_readings": len(detailed_readings),

        "readings": detailed_readings

    }


# =========================================================
# 4. LATEST AIR QUALITY FOR ALL 31 STATIONS
# =========================================================

@router.get("/api/v1/air-quality/latest")
def get_all_stations_air_quality():

    headers = get_api_headers()

    # Get station registry from existing CSV-based API.
    station_response = get_stations()

    stations = station_response["stations"]

    all_station_data = []

    failed_stations = []

    total_readings = 0

    freshness_summary = {
        "fresh": 0,
        "recent": 0,
        "stale": 0,
        "unknown": 0
    }

    with requests.Session() as session:

        session.headers.update(headers)

        for station in stations:

            location_id = station["location_id"]

            try:

                # -------------------------------------
                # A. Fetch latest observations
                # -------------------------------------

                latest_url = (
                    f"{OPENAQ_BASE_URL}/locations/"
                    f"{location_id}/latest"
                )

                latest_data = openaq_get(
                    session,
                    latest_url
                )

                latest_results = (
                    latest_data.get("results", [])
                )

                # -------------------------------------
                # B. Get sensor metadata from cache
                # -------------------------------------

                sensor_map = get_location_sensor_metadata(
                    session,
                    location_id
                )

                # -------------------------------------
                # C. Combine readings and metadata
                # -------------------------------------

                station_readings = []

                for reading in latest_results:

                    sensor_id = reading.get("sensorsId")

                    if not sensor_id:
                        continue

                    metadata = sensor_map.get(sensor_id)

                    if not metadata:

                        metadata = get_single_sensor_metadata(
                            session,
                            sensor_id
                        )

                    formatted_reading = (
                        format_air_quality_reading(
                            reading,
                            metadata
                        )
                    )

                    station_readings.append(
                        formatted_reading
                    )

                    freshness = formatted_reading["freshness"]

                    freshness_summary[freshness] += 1

                total_readings += len(station_readings)

                # -------------------------------------
                # D. Build station response
                # -------------------------------------

                all_station_data.append({

                    "location_id": location_id,

                    "location_name": station["location_name"],

                    "latitude": station["latitude"],

                    "longitude": station["longitude"],

                    "data_source": "OpenAQ",

                    "total_readings": len(station_readings),

                    "readings": station_readings

                })

            except HTTPException as error:

                # Authentication and rate-limit errors
                # affect the entire OpenAQ request.
                if error.status_code in (401, 429):
                    raise

                failed_stations.append({

                    "location_id": location_id,

                    "location_name": station["location_name"],

                    "error": error.detail

                })

            except Exception as error:

                failed_stations.append({

                    "location_id": location_id,

                    "location_name": station["location_name"],

                    "error": str(error)

                })

    # -------------------------------------
    # E. Final response
    # -------------------------------------

    successful_count = len(all_station_data)

    failed_count = len(failed_stations)

    return {

        "status": (
            "success"
            if failed_count == 0
            else "partial_success"
        ),

        "source": "OpenAQ API v3",

        "data_type": "latest_available_observations",

        "retrieved_at_utc": datetime.now(
            timezone.utc
        ).isoformat(),

        "total_stations": len(stations),

        "successful_stations": successful_count,

        "failed_stations_count": failed_count,

        "total_readings": total_readings,

        "freshness_summary": freshness_summary,

        "cached_sensor_locations": len(
            LOCATION_SENSOR_CACHE
        ),

        "cached_sensors": len(
            SENSOR_METADATA_CACHE
        ),

        "stations": all_station_data,

        "errors": failed_stations

    }


# =========================================================
# 5. UNITY-FRIENDLY AIR QUALITY API
# =========================================================

def get_latest_parameter_reading(readings, parameter_name):

    matching_readings = [
        reading
        for reading in readings
        if reading.get("parameter") == parameter_name
    ]

    if not matching_readings:
        return None

    def get_timestamp(reading):

        timestamp = (reading.get("datetime") or {}).get("utc")

        if not timestamp:
            return datetime.min.replace(tzinfo=timezone.utc)

        try:
            parsed = datetime.fromisoformat(
                timestamp.replace("Z", "+00:00")
            )

            if parsed.tzinfo is None:
                parsed = parsed.replace(tzinfo=timezone.utc)

            return parsed

        except (ValueError, TypeError):
            return datetime.min.replace(tzinfo=timezone.utc)

    latest = max(
        matching_readings,
        key=get_timestamp
    )

    return {
        "value": latest.get("value"),
        "unit": latest.get("unit"),
        "freshness": latest.get("freshness"),
        "age_hours": latest.get("age_hours"),
        "datetime_utc": (latest.get("datetime") or {}).get("utc")
    }


def build_pm25_heatmap(stations, width=64, height=64):
    """Interpolate station PM2.5 readings into a small Unity-ready raster."""
    points = []
    for station in stations:
        reading = station.get("pm25")
        if not reading or reading.get("value") is None:
            continue
        try:
            value = float(reading["value"])
            latitude = float(station["latitude"])
            longitude = float(station["longitude"])
        except (TypeError, ValueError, KeyError):
            continue
        if (math.isfinite(value) and math.isfinite(latitude) and math.isfinite(longitude)
                and -90 <= latitude <= 90 and -180 <= longitude <= 180 and value >= 0):
            points.append((latitude, longitude, value))

    if not points:
        return None

    min_lat = min(point[0] for point in points)
    max_lat = max(point[0] for point in points)
    min_lon = min(point[1] for point in points)
    max_lon = max(point[1] for point in points)

    # Add a small border so the interpolated surface does not end abruptly
    # exactly at the outer monitoring stations.
    lat_padding = max((max_lat - min_lat) * 0.08, 0.005)
    lon_padding = max((max_lon - min_lon) * 0.08, 0.005)
    min_lat -= lat_padding
    max_lat += lat_padding
    min_lon -= lon_padding
    max_lon += lon_padding

    center_latitude = sum(point[0] for point in points) / len(points)
    longitude_scale = max(abs(math.cos(math.radians(center_latitude))), 0.01)
    values = []

    for y in range(height):
        latitude = min_lat + (max_lat - min_lat) * y / (height - 1)
        for x in range(width):
            longitude = min_lon + (max_lon - min_lon) * x / (width - 1)
            weighted_value = 0.0
            weight_total = 0.0
            exact_value = None

            for point_lat, point_lon, value in points:
                dx = (longitude - point_lon) * longitude_scale
                dy = latitude - point_lat
                distance_squared = dx * dx + dy * dy
                if distance_squared < 1e-12:
                    exact_value = value
                    break
                weight = 1.0 / (distance_squared ** 1.5)
                weighted_value += value * weight
                weight_total += weight

            values.append(exact_value if exact_value is not None else weighted_value / weight_total)

    return {
        "parameter": "pm25",
        "unit": "µg/m³",
        "width": width,
        "height": height,
        "min_latitude": min_lat,
        "max_latitude": max_lat,
        "min_longitude": min_lon,
        "max_longitude": max_lon,
        "values": values,
    }


@router.get("/api/v1/air-quality/unity")
def get_unity_air_quality():

    # Reuse the existing all-stations API.
    # This also reuses the sensor metadata cache.

    source_data = get_all_stations_air_quality()

    unity_stations = []

    for station in source_data["stations"]:

        readings = station["readings"]

        unity_stations.append({

            "location_id": station["location_id"],

            "location_name": station["location_name"],

            "latitude": station["latitude"],

            "longitude": station["longitude"],

            "total_readings": station["total_readings"],

            "pm25": get_latest_parameter_reading(
                readings, "pm25"
            ),

            "pm10": get_latest_parameter_reading(
                readings, "pm10"
            ),

            "no2": get_latest_parameter_reading(
                readings, "no2"
            ),

            "so2": get_latest_parameter_reading(
                readings, "so2"
            ),

            "co": get_latest_parameter_reading(
                readings, "co"
            ),

            "o3": get_latest_parameter_reading(
                readings, "o3"
            ),

            "nox": get_latest_parameter_reading(
                readings, "nox"
            ),

            "no": get_latest_parameter_reading(
                readings, "no"
            ),

            "temperature": get_latest_parameter_reading(
                readings, "temperature"
            ),

            "relativehumidity": get_latest_parameter_reading(
                readings, "relativehumidity"
            ),

            "wind_speed": get_latest_parameter_reading(
                readings, "wind_speed"
            ),

            "wind_direction": get_latest_parameter_reading(
                readings, "wind_direction"
            )

        })

    heatmap = build_pm25_heatmap(unity_stations)

    return {

        "status": source_data["status"],

        "source": "OpenAQ API v3",

        "data_type": "latest_available_observations",

        "retrieved_at_utc": source_data["retrieved_at_utc"],

        "total_stations": source_data["total_stations"],

        "successful_stations": source_data["successful_stations"],

        "failed_stations_count": source_data["failed_stations_count"],

        "total_readings": source_data["total_readings"],

        "stations": unity_stations,

        "heatmap": heatmap,

        "errors": source_data["errors"]

    }
