from fastapi import FastAPI, Request
from fastapi.responses import PlainTextResponse
import hl7

app = FastAPI(title="PACS System (with HL7)")

studies = {}

@app.get("/")
def root():
    return {"service": "pacs", "status": "running"}

@app.post("/hl7/receive", response_class=PlainTextResponse)
async def receive_hl7(request: Request):
    body = await request.body()
    hl7_text = body.decode("utf-8")
    
    try:
        parsed_msg = hl7.parse(hl7_text)
        msh = parsed_msg.segment("MSH")
        pid = parsed_msg.segment("PID")
        
        patient_id = pid[3][0]
        patient_name = pid[5][0]
        msg_control_id = msh[10][0]
        
        # Simulate storing order
        studies[str(patient_id)] = {"name": str(patient_name), "status": "ORDER_RECEIVED"}
        
        # Generate ACK
        ack_msg = f"MSH|^~\\&|PACS|HOSPITAL|HBYS|HOSPITAL|20231010120000||ACK^O01|{msg_control_id}|P|2.4\rMSA|AA|{msg_control_id}"
        return ack_msg
    except Exception as e:
        return f"MSH|^~\\&|PACS|HOSPITAL|HBYS|HOSPITAL|20231010120000||ACK^O01|ERROR|P|2.4\rMSA|AE|ERROR: {str(e)}"