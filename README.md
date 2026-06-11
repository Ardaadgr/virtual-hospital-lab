# Virtual Hospital Lab (Health IT & DevSecOps Test Bed)

This project is a fully isolated virtual simulation of a real Hospital Network and its clinical systems (HBYS, PACS, etc.). It is designed with a professional DevSecOps approach to test cybersecurity analysis, network management (VLAN/Routing), and clinical integration (HL7/FHIR) scenarios.

## System Architecture

The architecture consists of completely isolated virtual networks (VLAN simulation) running on Docker:

- clinical-net (10.10.10.x): An isolated network containing the Hospital Information System (HBYS), PACS, PostgreSQL, and Redis.
- security-net (10.10.20.x): Network for future Suricata (IDS) and Wazuh (SIEM) deployments.
- management-net (10.10.30.x): The network for staff computers and monitoring dashboards.
- core-router: A virtual router connecting all isolated networks and enforcing firewall rules.

## Running the Project

You can start the entire infrastructure with a single command. All databases, background services, and network isolation rules are automatically configured by Docker.

Requirements:
- Docker Desktop must be installed and running on your machine.

Start Command:
Open a terminal in the project directory and run the following command:

```bash
docker-compose up --build -d
```

## Accessing Services

Once the system is running, you can access the following services via your web browser:

| Service Name | Address | Function |
|--------------|---------|----------|
| HBYS API | http://localhost:8000 | Manages patient registration and sends HL7 orders to PACS. |
| PACS API | http://localhost:8001 | Receives, parses, and acknowledges HL7 formatted requests from HBYS. |
| Staff API | http://localhost:8002 | Simulates the staff network. Cannot directly access HBYS due to VLAN isolation. |
| pgAdmin | http://localhost:5050 | Web browser interface for managing the PostgreSQL database. |

### Database Access (pgAdmin)
To manage the database visually via your browser:
1. Go to `http://localhost:5050`
2. Login with Email: `admin@hospital.local` and Password: `adminpassword`
3. Click "Add Server" and use the following connection details:
   - Host name: postgres
   - Port: 5432
   - Maintenance database: hospital_db
   - Username: admin
   - Password: adminpassword

## HL7 Integration & Testing

The project simulates the HL7 v2 communication protocol. To test the system:

1. Add a New Patient to the Database:
```bash
curl -X POST "http://localhost:8000/patients/?first_name=Ahmet&last_name=Yilmaz&tc_no=123456789"
```

2. Send an Examination Order (MRI/X-Ray) to PACS:
(This sends a real HL7 ORM^O01 message from HBYS to PACS, and receives an ACK confirmation)
```bash
curl -X POST "http://localhost:8000/order-pacs/1"
```