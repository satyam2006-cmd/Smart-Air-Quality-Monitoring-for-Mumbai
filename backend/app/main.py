from fastapi import FastAPI
from datetime import datetime, timezone

from app.api.stations import router as stations_router
from app.api.air_quality import router as air_quality_router

app = FastAPI(
    title="Smart City Environmental Intelligence API",
    description="Mumbai PM2.5 Monitoring and Digital Twin API",
    version="1.0.0"
)

app.include_router(stations_router)
app.include_router(air_quality_router)

@app.get("/")
def home():
    return {
        "status": "success",
        "message": "Smart City API is running successfully",
        "version": "1.0.0",
        "documentation": "/docs"
    }


@app.get("/health")
def health_check():
    return {
        "status": "healthy",
        "service": "Smart City API",
        "timestamp": datetime.now(timezone.utc).isoformat()
    }