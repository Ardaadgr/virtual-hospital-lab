from fastapi import FastAPI

app = FastAPI()

@app.get("/")
def root():
    return {"service": "staff", "status": "running"}