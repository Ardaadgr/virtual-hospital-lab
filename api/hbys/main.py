import requests
from fastapi import FastAPI

app = FastAPI(title="HBYS System")

studies = {}

@app.get("/")
def root():
    return {"service": "hbys", "status": "running"}

# 1. PACS'e order gönder
@app.post("/order-study/{patient_id}")
def order_study(patient_id: int):

    r = requests.post(
        "http://pacs:8000/study/create",
        params={"patient_id": patient_id}
    )

    response = r.json()

    studies[response["study_id"]] = response

    return {
        "message": "study ordered",
        "pacs_response": response
    }

# 2. PACS'ten status sorgula
@app.get("/study/{study_id}")
def get_study(study_id: str):

    r = requests.get(f"http://pacs:8000/study/{study_id}")

    return r.json()