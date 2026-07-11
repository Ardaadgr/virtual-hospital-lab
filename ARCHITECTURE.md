# Mimari Kararlar (Architecture Decision Record)

Bu dosya, projede alınmış ve kilitlenmiş mimari kararları kaydeder. Bir karar
değişecekse bu dosya güncellenir ve nedeni yazılır. Kod, bu dosyayla çelişemez.

## AD-001: Sistem Mimarisi — Modular Monolith

Karar: HBYS, LIS, PACS, PMS (Patoloji) ve VNA tek bir .NET solution içinde,
ayrı projeler (bounded context) olarak geliştirilir.

Gerekçe: Mikroservisler başlangıçta ciddi DevOps yükü ve dağıtık veri
tutarlılığı sorunu getirir. Modular Monolith, sınırları net tutarak ileride
ayrıştırma imkânı bırakır.

Bağlayıcı kural: Bir bounded context, başka bir context'in veritabanına veya
domain sınıflarına DOĞRUDAN erişemez. Sadece `VirtualHospital.Contracts`
altındaki entegrasyon mesajları üzerinden konuşurlar.

## AD-002: Patoloji Bağımsız Bir Sistemdir

Karar: PMS (Patoloji Yönetim Sistemi), HBYS'ye gömülü bir modül DEĞİLDİR.
Kendi bounded context'i, kendi veritabanı şeması ve kendi API'si vardır.

Gerekçe: Gerçek hastanelerde patoloji ayrı bir bilgi sistemidir. HBYS ile
sadece asenkron mesajlaşma (HL7 ORM/ORU, RabbitMQ) üzerinden konuşur.

## AD-003: Hasta Kimliği — MRN ve Encounter Ayrımı

Karar: İki ayrı kimlik kavramı vardır ve karıştırılmaz.

- `MedicalRecordNumber` (MRN): Hastanın hastane genelindeki ÖMÜR BOYU SABİT
  kimliği. Hasta ilk kez kaydolduğunda üretilir. Hasta yıllar sonra tekrar
  gelse aynı MRN kullanılır. Value object.
- `Encounter` (Ziyaret / Başvuru): Her başvuru için ayrı bir kayıt. Bir MRN'e
  bağlı N adet Encounter olabilir.

İlişki: `Patient (1 MRN) 1 ──< N Encounter`

Bağlayıcı kural: Klinik iş akışları (lab order, patoloji vakası, radyoloji
tetkiki) her zaman bir `EncounterId`'ye bağlanır, doğrudan MRN'e değil.
Ama hastanın tüm geçmişi MRN üzerinden sorgulanabilir.

## AD-004: Entegrasyon — Asenkron, RabbitMQ + MassTransit, HL7 v2.5

Karar: Sistemler arası tüm klinik iletişim asenkrondur.

- Taşıma: RabbitMQ, .NET tarafında MassTransit soyutlaması.
- Mesaj formatı: HL7 v2.5 (ORM^O01 sipariş, ORU^R01 sonuç, ADT^A01/A04 hasta).
- UI'ı besleyen uçlar: REST API (gerekirse SignalR ile canlı bildirim).

Gerekçe: Senkron HTTP zinciri, bir sistem yavaşladığında tüm hastaneyi
kilitler. LIS yoğunken HBYS'deki doktor beklememelidir.

## AD-005: Veritabanı — PostgreSQL, Şema Bazlı Mantıksal Ayrım

Karar: Tek PostgreSQL sunucusu, her bounded context için AYRI ŞEMA ve AYRI
`DbContext`.

- `hbys` şeması: Patient, Encounter, Clinic, Staff, Billing
- `lis` şeması: LabOrder, LabResult, AutoValidationRule
- `pacs` şeması: DicomStudy, DicomSeries, DicomInstance (metadata)
- `pathology` şeması: PathologyCase, Specimen, Block, Slide, DigitalSlide,
  Consultation, StageTransition
- `vna` şeması: ArchiveObject (birleşik indeks)

Gerekçe: JSONB (ham HL7 mesajını olduğu gibi saklamak için), partitioning ve
açık kaynak lisansı. Şema ayrımı, ileride ayrı veritabanına taşımayı
kolaylaştırır.

Bağlayıcı kural: Cross-schema JOIN YASAK. Bir context başka context'in
verisine ihtiyaç duyuyorsa, ya mesajla alır ya da kendi tarafında bir
read-model (projection) tutar.

## AD-006: Kimlik Doğrulama — Keycloak (OAuth2 + OIDC)

Karar: Keycloak, Docker Compose içinde ayağa kalkar. Tüm API'ler JWT bearer
token doğrular. SSO, PACS/Patoloji viewer'ının HBYS içinden token ile
açılmasını sağlar.

Yetkilendirme modeli: Rol tabanlı (RBAC) + öznitelik tabanlı (ABAC).
Roller: SystemAdmin, Physician, Pathologist, PathologyTechnician,
LabTechnician, RadiologyTechnician, Nurse, BillingClerk.

ABAC boyutu: Bir patolog yalnızca KENDİSİNE ATANMIŞ veya konsültasyon için
kendisine YÖNLENDİRİLMİŞ vakaları görebilir.

## AD-007: VNA — Birleşik Arşiv (DICOM + Patoloji WSI)

Karar: Radyoloji görüntüleri (MR/CT/DX) ve patoloji dijital slide'ları (WSI)
AYNI arşivde saklanır.

- Depolama: MinIO (S3 uyumlu), Docker Compose içinde.
- Protokol: DICOMweb (STOW-RS / WADO-RS / QIDO-RS).
- Patoloji slide'ları DICOM WSI olarak saklanır
  (SOP Class UID: 1.2.840.10008.5.1.4.1.1.77.1.6 — VL Whole Slide Microscopy).
- DICOM sunucusu: Orthanc (dcm4chee alternatifi; Orthanc daha hafif ve
  DICOMweb eklentisi hazır gelir).

Gerekçe: "Vendor Neutral Archive" tam olarak bunu ifade eder — modaliteden
bağımsız tek arşiv. Viewer, ikisini de aynı protokolle çeker.

## AD-008: Viewer — Tek Birleşik Web Viewer

Karar: React tabanlı tek viewer.

- Radyoloji (MR/CT/DX): Cornerstone3D
- Patoloji (WSI): OpenSeadragon veya Slim (DICOMweb WSI desteği)
- Ortak katman: Hasta paneli, seri/vaka seçici, anotasyon katmanı

HBYS içinden SSO token ile yeni sekmede veya iframe içinde açılır.

## AD-009: Patoloji Fiziksel Takip Zinciri

Karar: Fiziksel numune yaşam döngüsü dijital olarak takip edilir.

```
Specimen (Numune, M kodu)
   │ 1 ──< N   (grossing / makroskopik örnekleme)
Block (Blok, B kodu)
   │ 1 ──< N   (varsayılan 1:1; ek kesit talebi istisnadır)
Slide (Lam, S kodu)
   │ 1 ──< N   (yalnızca en güncel olan aktif; eskiler superseded)
DigitalSlide (WSI, VNA'da)
```

Kritik kurallar:
- Blok → Lam varsayılan 1:1'dir. İkinci bir lam ancak açık bir EK KESİT
  TALEBİ (gerekçe + talep eden hekim) ile oluşturulabilir. Bu, domain
  seviyesinde zorlanır (bkz. `Block.CutAdditionalSlide`).
- Bir lam yeniden taranabilir. Yeni tarama aktif olur, ESKİSİ SİLİNMEZ:
  `Superseded` statüsüne düşer ve VNA'da kalır (bkz. AD-010).

## AD-010: Yeniden Tarama — Mantıksal Üzerine Yazma, Fiziksel Saklama

Karar: `Slide.CurrentDigitalSlideId` her zaman en güncel taramayı gösterir.
Patolog arayüzde SADECE onu görür. Ancak eski tarama fiziksel olarak silinmez;
`DigitalSlideStatus.Superseded` statüsüyle VNA'da kalır.

Her yeniden tarama kaydedilir: kim, ne zaman, hangi gerekçeyle (`RescanReason`:
BlurredImage, FocusError, DirtySlide, IncompleteScan, Other).

Gerekçe: Tıbbi görüntü, tanıya esas alınan kanıttır. Bir patolog v1 görüntüye
bakıp rapor yazdıysa, o raporun hangi görüntüye dayandığı geriye dönük
kanıtlanabilmelidir. Fiziksel silme, KVKK denetim izi yükümlülüğü (6 yıl) ve
olası tıbbi sorumluluk incelemesi açısından kabul edilemez bir boşluk yaratır.

## AD-011: Takip Kodları (Accession Numbers)

Karar: Numune, blok ve lam için ayrı önekli kodlar, patoloji sistemine KABUL
anında otomatik üretilir.

- Numune (Materyal): `M` öneki
- Blok: `B` öneki
- Lam (Slide): `S` öneki

Kod, hastanın tetkik/vaka kimliğinden türetilir. Üretim mantığı
`IAccessionCodeGenerator` arkasında soyutlanmıştır ve
`appsettings.json` üzerinden yapılandırılır (önek, hane sayısı, yıl bileşeni).

AÇIK KONU: Kodun tam kompozisyonu (hasta tetkik ID'sinin kaça hanesi girecek,
yıl bileşeni olacak mı) henüz kesinleşmedi. Varsayılan implementasyon
`{Önek}{Yıl:2}{VakaSıra:6}{AltSıra:2}` şeklindedir ve DEĞİŞTİRİLEBİLİR.
Bu karar netleşince bu bölüm güncellenecek.

## AD-012: Patoloji İş Akışı — Durum Makinesi

Vaka aşamaları (`PathologyStage`):

```
Accessioned (Kabul)
  -> Grossing (Makroskopik örnekleme)
  -> Processing (Doku takibi)
  -> Embedding (Bloklama)
  -> Sectioning (Kesit alma / mikrotom)
  -> Staining (Boyama)
  -> Scanning (Dijital tarama)
  -> UnderReview (Patolog incelemesi)
  -> [InConsultation (Konsültasyon)]  <-- opsiyonel dal
  -> Reported (Raporlandı)
```

Her geçiş `StageTransition` olarak kaydedilir: önceki aşama, yeni aşama,
zaman damgası, işlemi yapan personel. Vakanın "şu an hangi aşamada" olduğu
bu zincirin son halkasından okunur.

Geçersiz geçişler domain seviyesinde reddedilir (örn. Accessioned'dan
doğrudan Reported'a geçilemez).

## AD-013: Konsültasyon

Karar: Bir patolog, vakayı ikinci görüş için başka bir patologa
yönlendirebilir (`Consultation`).

- Vaka `InConsultation` aşamasına geçer.
- Konsültan patolog vakayı ve slide'ları görüntüleme yetkisi kazanır (ABAC).
- Konsültan görüşünü yazar; asıl patolog nihai raporu yazma sorumluluğunu
  korur.

## AD-014: Kod Yapısı — Clean Architecture + CQRS

Her bounded context dört katmandan oluşur:

```
{Context}.Domain          -> Entity, ValueObject, DomainEvent, domain servis
{Context}.Application     -> Command/Query (MediatR), Handler, DTO, Validator
{Context}.Infrastructure  -> EF Core DbContext, Repository, MassTransit
{Context}.Api             -> Controller, middleware
```

Bağımlılık yönü İÇE doğrudur: Api -> Application -> Domain.
Infrastructure -> Application (interface implement eder). Domain hiçbir şeye
bağımlı DEĞİLDİR (EF Core attribute'ları dahil; mapping Fluent API ile
Infrastructure'da yapılır).

## AD-015: Kapsam Dışı (Şimdilik)

- Yapay zeka / karar destek modülleri (sonraki faz)
- FHIR (HL7 v2 şimdilik yeterli)
- Kubernetes (Docker Compose yeterli)
- Medula provizyon entegrasyonu (faturalama iskeleti kurulur, gerçek
  entegrasyon sonraki faz)
- HBYS poliklinik iş mantığı: Poliklinik yapısı (Dahiliye, Cerrahi,
  Radyoloji, Acil/Triyaj, Laboratuvar, Patoloji) tanımlanır ancak şimdilik
  yalnızca HASTA KABUL ve MRN ÜRETİMİ çalışır durumdadır.
