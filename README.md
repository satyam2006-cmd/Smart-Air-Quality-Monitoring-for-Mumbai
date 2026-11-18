# Smart AQ

Mumbai air quality monitoring project with a FastAPI backend and a Unity map client.

## Repository layout

- `backend/` — FastAPI service and its station dataset.
- `unity/` — Unity client project (Assets, Packages, and ProjectSettings).

## Run the backend

From the repository root, install the Python dependencies and start FastAPI:

```powershell
python -m venv backend/venv
backend/venv/Scripts/Activate.ps1
pip install -r backend/requirements.txt
$env:OPENAQ_API_KEY = "your OpenAQ API key"
python -m uvicorn app.main:app --app-dir backend --reload
```

API docs are served at `http://127.0.0.1:8000/docs`. The Unity client uses the local API endpoints on port 8000.

## Open the Unity client

Open the `unity/` directory in Unity Hub. Unity regenerates editor-specific files such as `Library/` and the solution files locally.
