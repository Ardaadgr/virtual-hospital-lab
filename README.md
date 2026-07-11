# Sanal Hastane (Virtual Hospital)

Kurumsal düzeyde, gerçekçi bir sanal hastane ortamı. Dört bilgi sistemi ve
bunları birbirine bağlayan bir arşiv katmanı:

| Sistem | Açıklama |
|---|---|
| HBYS | Hastane Bilgi Yönetim Sistemi (hasta kabul, MRN, ziyaret, poliklinik) |
| LIS | Laboratuvar Bilgi Sistemi (biyokimya, mikrobiyoloji, oto-onay) |
| PACS | Radyoloji görüntüleme (MR, CT, DX) |
| PMS | Patoloji Yönetim Sistemi (numune/blok/lam takibi, dijital slide) |
| VNA | Vendor Neutral Archive (radyoloji ve patoloji görüntüleri tek arşivde) |

Sistemler asenkron mesajlaşma (HL7 v2.5 + RabbitMQ) ile konuşur. Mimari
kararların tamamı `ARCHITECTURE.md` içindedir.

## DURUM: Bu bir iskelettir, çalışan bir sistem değildir

Dürüst olmak gerekirse: **bu kod henüz derlenmedi.** Hazırlandığı ortamda .NET
SDK yoktu. Kod elle yazıldı ve gözden geçirildi, ancak `dotnet build`
çalıştırılmadı. Derleme hatası çıkması beklenir; ilk iş bunları temizlemektir.

Ne var:
- Mimari kararlar (`ARCHITECTURE.md`), Claude Code yapılandırması (`.claude/`)
- SharedKernel: `Entity`, `AggregateRoot`, `ValueObject`, `IDomainEvent`, `IClock`
- HBYS Domain: `Patient`, `Encounter`, `MedicalRecordNumber`, `NationalIdentifier`
- Patoloji Domain (tam): `PathologyCase` aggregate, `Specimen`, `Block`, `Slide`,
  `DigitalSlide`, `Consultation`, `StageTransition`, `AccessionCode`,
  `StageTransitionPolicy`
- `VirtualHospital.Contracts`: sistemler arası mesaj sözleşmeleri
- Patoloji domain birim testleri (yazıldı, koşturulmadı)
- Docker Compose: PostgreSQL, RabbitMQ, Keycloak, MinIO, Orthanc

Ne yok (Claude Code ile yapılacak):
- Application katmanı (MediatR command/query/handler/validator)
- Infrastructure (EF Core DbContext, Fluent API mapping, repository, migration)
- Api katmanı (controller, yetkilendirme, middleware)
- LIS, PACS, VNA domain modelleri
- HL7 ayrıştırıcı ve MassTransit consumer'ları
- Frontend (React viewer)

## Kurulum

```bash
docker compose up -d      # PostgreSQL, RabbitMQ, Keycloak, MinIO, Orthanc
dotnet restore
dotnet build              # ILK İŞ BU. Hataları temizle.
dotnet test
```

UYARI: `docker-compose.yml` içindeki parolalar yalnızca yerel geliştirme
içindir. Üretimde ortam değişkeni veya secret manager kullanılır.

## Claude Code ile çalışma

`.claude/` altındaki yapılandırma otomatik yüklenir:

- `CLAUDE.md` — her oturumda okunur, proje kuralları
- `.claude/rules/` — 6 kural dosyası (mimari, KVKK, entegrasyon, test, yazım, devops)
- `.claude/skills/` — 6 alan bilgisi (patoloji, HBYS, LIS, PACS/DICOM, VNA, HL7)
- `.claude/agents/` — 9 alt ajan

### health-governance ajanı

`health-governance` bir orkestratördür, kod yazmaz. PubMed MCP'sine bağlıdır.
Kritik bir tasarım kararında (domain modeli, HL7 sözleşmesi, oto-onay sınırı,
görüntü saklama) çağrılır; ilgili uzman ajanı `Task` ile devreye alır, çıkan
tasarımı PubMed literatürüne ve standartlara (HL7 v2.5, DICOM, KVKK) karşı
denetler, uyum raporu üretir.

PubMed MCP'si `.mcp.json` içinde tanımlıdır. Claude Code ilk açılışta bu
sunucuya bağlanmak için onay ister.

Kullanım:

```
> health-governance ajanını çalıştır: patoloji oto-onay eşiklerini denetle
> pathology-specialist ile ek kesit akışını tasarla, sonra health-governance ile denetlet
```

## Yazım kuralı

Bu projede emoji kullanılmaz. Kodda, yorumda, commit mesajında, dokümanda
veya sohbet yanıtında. Bkz. `.claude/rules/writing-style.md`.

## Kritik domain kuralları

Bunlar hasta güvenliği kurallarıdır ve domain katmanında zorlanır:

1. **Barkod zinciri kopmaz.** Her B kodu bir M koduna, her S kodu bir B koduna
   bağlıdır. Yanlış tipte barkod okutulursa işlem reddedilir.
2. **Blok → lam varsayılan 1:1.** İkinci lam ancak gerekçe + talep eden hekim
   ile üretilir. Ek kesit dokuyu geri alınamaz şekilde tüketir.
3. **Görüntü silinmez.** Yeniden taranan lamın eski görüntüsü `Superseded`
   olur, VNA'da kalır. Bir rapor o görüntüye dayanmış olabilir.
4. **Geçersiz aşama geçişi reddedilir.** `Accessioned` durumundan doğrudan
   `Reported` durumuna geçilemez.
5. **Konsültasyon sorumluluk devretmez.** Nihai raporu yalnızca vakanın atanmış
   patoloğu yazabilir.
6. **MRN ömür boyu sabittir.** Hasta yıllar sonra dönse aynı MRN kullanılır.
   Encounter her ziyarette yenidir.

## Açık konu

Takip kodlarının (M/B/S) tam hane kompozisyonu henüz kesinleşmedi. Kod
`IAccessionCodeGenerator` arkasında soyutlandı; value object yalnızca şekli
(önek + rakam) doğruluyor, sabit uzunluk dayatmıyor. Bkz. ARCHITECTURE.md
AD-011.
