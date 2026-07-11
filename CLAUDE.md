# CLAUDE.md — Sanal Hastane (Virtual Hospital)

Bu dosya her Claude Code oturumunun başında otomatik okunur. Buradaki kurallar
oturum boyunca bağlayıcıdır.

## Proje amacı

Kurumsal düzeyde, gerçekçi bir sanal hastane ortamı. Dört bilgi sistemi ve
bunları birbirine bağlayan bir arşiv katmanı:

- HBYS: Hastane Bilgi Yönetim Sistemi (hasta kabul, ziyaret, poliklinik, faturalama)
- LIS: Laboratuvar Bilgi Sistemi (biyokimya, mikrobiyoloji, oto-onay)
- PACS: Radyoloji görüntüleme (MR, CT, DX)
- PMS: Patoloji Yönetim Sistemi (numune/blok/lam takibi, dijital slide)
- VNA: Vendor Neutral Archive (radyoloji ve patoloji görüntüleri tek arşivde)

Sistemler asenkron mesajlaşma (HL7 v2.5 + RabbitMQ) ile konuşur.

## Önce ARCHITECTURE.md oku

Tüm mimari kararlar `ARCHITECTURE.md` içinde kilitlenmiştir. Bir tasarım
kararı vermeden önce oraya bak. Kod, o dosyayla çelişemez. Bir kararı
değiştirmek gerekiyorsa önce ARCHITECTURE.md güncellenir, sonra kod.

## Teknoloji yığını (kilitli)

- .NET 8, C#, ASP.NET Core Web API
- PostgreSQL + EF Core (Code-First, Fluent API mapping)
- RabbitMQ + MassTransit (asenkron mesajlaşma)
- HL7 v2.5 (sistemler arası klinik mesajlar)
- Keycloak (OAuth2 + OIDC, SSO)
- MinIO (S3 uyumlu depolama) + Orthanc (DICOMweb)
- fo-dicom (DICOM parse/oluşturma)
- MediatR (CQRS), FluentValidation, AutoMapper
- Docker Compose (tüm bağımlılıklar tek komutla)
- Frontend: React + TailwindCSS, Cornerstone3D (radyoloji), OpenSeadragon/Slim (patoloji WSI)
- Test: xUnit, Moq, FluentAssertions, Testcontainers

## Mimari harita

```
src/
  Shared/
    VirtualHospital.SharedKernel   -> Entity, AggregateRoot, ValueObject, DomainEvent
    VirtualHospital.Contracts      -> Bounded context'ler arası mesaj sözleşmeleri
  Hbys/       {Domain, Application, Infrastructure, Api}
  Lis/        {Domain, Application, Infrastructure, Api}
  Pacs/       {Domain, Application, Infrastructure, Api}
  Pathology/  {Domain, Application, Infrastructure, Api}
  Vna/        {Domain, Application, Infrastructure, Api}
```

Bağımlılık yönü İÇE doğrudur:
`Api -> Application -> Domain`, `Infrastructure -> Application`.
Domain katmanı HİÇBİR ŞEYE bağımlı değildir (EF Core dahil).

## Kesin kurallar

1. **Bounded context sınırları geçilmez.** Bir context başka bir context'in
   `DbContext`'ine, entity'sine veya repository'sine ERİŞEMEZ. Sadece
   `VirtualHospital.Contracts` altındaki mesajlarla konuşurlar.
   Cross-schema SQL JOIN yasaktır.

2. **Patoloji bağımsız bir sistemdir.** HBYS'ye gömülü bir modül değildir.
   HBYS ile yalnızca HL7 mesajı üzerinden konuşur.

3. **MRN ve Encounter karıştırılmaz.** MRN hastanın ömür boyu sabit
   kimliğidir. Encounter her başvuruda yeniden oluşur. Klinik iş akışları
   Encounter'a bağlanır (bkz. ARCHITECTURE.md AD-003).

4. **Görüntü silinmez.** Yeniden taranan bir lamın eski görüntüsü
   `Superseded` olur, VNA'da kalır. Fiziksel silme yasaktır (AD-010).

5. **Blok → Lam varsayılan 1:1'dir.** İkinci bir lam ancak açık ek kesit
   talebiyle (gerekçe + talep eden hekim) üretilir. Domain bunu zorlar.

6. **Hasta verisine her erişim loglanır.** İstisnasız. Bkz.
   `.claude/rules/data-privacy-kvkk.md`.

7. **Senkron klinik entegrasyon yasaktır.** Sistemler arası klinik veri akışı
   her zaman mesaj kuyruğu üzerinden gider.

8. **Ölçmeden iddia etme.** "Bu daha hızlı", "bu güvenli" gibi iddialar
   ölçüme veya kaynağa dayanmalıdır.

## Standart komutlar

```bash
docker compose up -d              # PostgreSQL, RabbitMQ, Keycloak, MinIO, Orthanc
dotnet restore
dotnet build
dotnet ef migrations add <Ad> --project src/Pathology/VirtualHospital.Pathology.Infrastructure
dotnet ef database update --project src/Pathology/VirtualHospital.Pathology.Infrastructure
dotnet test
dotnet format --verify-no-changes
```

## Alt ajanlar (subagent)

`health-governance` ajanı bir ORKESTRATÖRDÜR. PubMed MCP'sine bağlıdır ve
diğer ajanların çıktısını tıbbi/kurumsal standartlara karşı denetler.
Kritik tasarım kararlarında (domain modeli, HL7 mesaj sözleşmesi, DICOM/WSI
kullanımı, hasta verisi güvenliği) bu ajan devreye alınır.

Diğer ajanlar: `domain-modeler`, `api-designer`, `database-architect`,
`integration-specialist`, `pathology-specialist`, `dicom-vna-specialist`,
`security-auditor`, `test-strategist`.

## Yazım kuralı

Emoji KULLANILMAZ. Ne kodda, ne yorumda, ne commit mesajında, ne dokümanda,
ne de kullanıcıya verilen yanıtlarda. Bkz. `.claude/rules/writing-style.md`.

## Kapsam dışı (şimdilik)

Yapay zeka modülleri, FHIR, Kubernetes, gerçek Medula entegrasyonu, HBYS
poliklinik iş mantığı (yalnızca hasta kabul + MRN üretimi aktiftir).
