from fastapi import FastAPI
import uuid

app = FastAPI(title="PACS System")

studies = {}

@app.get("/")
def root():
    return {"service": "pacs", "status": "running"}

# 1. HBYS study order oluşturur
@app.post("/study/create")
def create_study(patient_id: int):
    study_id = str(uuid.uuid4())

    studies[study_id] = {
        "patient_id": patient_id,
        "status": "CREATED"
    }

    # simulate processing
    studies[study_id]["status"] = "PROCESSING"
    studies[study_id]["status"] = "READY"

    return {
        "study_id": study_id,
        "status": studies[study_id]["status"]
    }

# 2. HBYS status sorabilir
@app.get("/study/{study_id}")
def get_study(study_id: str):
    return studies.get(study_id, {"error": "not found"})