import hl7
import requests
from fastapi import FastAPI, Depends, HTTPException
from sqlalchemy import create_engine, Column, Integer, String
from sqlalchemy.orm import sessionmaker, declarative_base, Session

DATABASE_URL = "postgresql://admin:adminpassword@postgres:5432/hospital_db"
engine = create_engine(DATABASE_URL)
SessionLocal = sessionmaker(autocommit=False, autoflush=False, bind=engine)
Base = declarative_base()

class Patient(Base):
    __tablename__ = "patients"
    id = Column(Integer, primary_key=True, index=True)
    first_name = Column(String, index=True)
    last_name = Column(String, index=True)
    tc_no = Column(String, unique=True, index=True)

app = FastAPI(title="HBYS System (with HL7 & DB)")

def get_db():
    db = SessionLocal()
    try:
        yield db
    finally:
        db.close()

@app.on_event("startup")
def startup():
    Base.metadata.create_all(bind=engine)

@app.get("/")
def root():
    return {"service": "hbys", "status": "running"}

@app.post("/patients/")
def create_patient(first_name: str, last_name: str, tc_no: str, db: Session = Depends(get_db)):
    db_patient = Patient(first_name=first_name, last_name=last_name, tc_no=tc_no)
    db.add(db_patient)
    db.commit()
    db.refresh(db_patient)
    return db_patient

@app.post("/order-pacs/{patient_id}")
def order_pacs(patient_id: int, db: Session = Depends(get_db)):
    patient = db.query(Patient).filter(Patient.id == patient_id).first()
    if not patient:
        raise HTTPException(status_code=404, detail="Patient not found")

    # Generate HL7 ORM^O01 Message
    msg = f"MSH|^~\\&|HBYS|HOSPITAL|PACS|HOSPITAL|20231010120000||ORM^O01|MSG00001|P|2.4\rPID|1||{patient.id}||{patient.last_name}^{patient.first_name}|||M\rORC|NW|1234|||||^^^20231010120000\rOBR|1|1234||MRI^MRI Head|||20231010120000"
    
    # Send to PACS
    try:
        r = requests.post("http://pacs:8000/hl7/receive", data=msg, headers={"Content-Type": "text/plain"})
        ack = r.text
        return {
            "message": "Order sent to PACS via HL7",
            "hl7_sent": msg,
            "hl7_ack_received": ack
        }
    except Exception as e:
        return {"error": str(e)}