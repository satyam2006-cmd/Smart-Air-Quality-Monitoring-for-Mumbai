from fastapi import APIRouter, HTTPException
from pathlib import Path
import pandas as pd

router = APIRouter()

# Locate the CSV inside Backend/data
BASE_DIR = Path(__file__).resolve().parents[2]
DATASET_PATH = BASE_DIR / "data" / "mumbai_pm25_cleaned.csv"


@router.get("/api/v1/stations")
def get_stations():

    try:
        required_columns = [
            "location_id",
            "location_name",
            "sensor_id",
            "latitude",
            "longitude",
            "data_source"
        ]

        # Read only the columns required for the station registry
        df = pd.read_csv(
            DATASET_PATH,
            usecols=required_columns
        )

        # Remove records without essential station information
        df = df.dropna(
            subset=[
                "location_id",
                "location_name",
                "sensor_id",
                "latitude",
                "longitude"
            ]
        )

        # Keep one record per sensor
        # stations_df = (
        #     df.drop_duplicates(
        #         subset=["sensor_id"],
        #         keep="first"
        #     )
        #     .sort_values("location_name")
        # )
        stations_df = (
    df.drop_duplicates(
        subset=["location_id"],
        keep="first"
    )
    .sort_values("location_name")
)
        stations = []

        for _, row in stations_df.iterrows():

            stations.append({
                "location_id": int(row["location_id"]),
                "location_name": str(row["location_name"]),
                "sensor_id": int(row["sensor_id"]),
                "latitude": float(row["latitude"]),
                "longitude": float(row["longitude"]),
                "data_source": str(row["data_source"])
            })

        return {
            "status": "success",
            "total_stations": len(stations),
            "data_source": "OpenAQ",
            "stations": stations
        }

    except FileNotFoundError:
        raise HTTPException(
            status_code=500,
            detail=f"Dataset not found: {DATASET_PATH}"
        )

    except Exception as error:
        raise HTTPException(
            status_code=500,
            detail=str(error)
        )