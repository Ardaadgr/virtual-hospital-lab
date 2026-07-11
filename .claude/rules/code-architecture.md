# Kod Mimarisi Kuralları

## Katman bağımlılıkları (ihlal edilemez)

```
Api  ->  Application  ->  Domain
              ^
              |
       Infrastructure
```

- `Domain`: Hiçbir projeye bağımlı DEĞİLDİR. NuGet paketi bile almaz (MediatR
  hariç, yalnızca `INotification` için domain event tanımlamak amacıyla).
  EF Core attribute'ları (`[Key]`, `[Table]`) domain sınıflarında KULLANILMAZ;
  mapping `Infrastructure` içinde Fluent API ile yapılır.
- `Application`: Yalnızca `Domain`'e bağımlıdır. Altyapı detayını (EF Core,
  RabbitMQ, S3) bilmez; `Abstractions` altında interface tanımlar.
- `Infrastructure`: `Application`'daki interface'leri implement eder.
- `Api`: Yalnızca `Application`'a bağımlıdır. Controller içinde iş mantığı
  YAZILMAZ; sadece request alır, MediatR'a gönderir, response döner.

## Bounded context izolasyonu

Bir context başka bir context'in şu unsurlarına ERİŞEMEZ:
- `DbContext` veya veritabanı şeması (cross-schema JOIN yasak)
- Domain entity'si veya value object'i
- Repository veya Application servisi

Tek iletişim yolu: `VirtualHospital.Contracts` altındaki mesaj sözleşmeleri,
RabbitMQ üzerinden.

Başka bir context'in verisine sık ihtiyaç duyuluyorsa, o veriyi mesajla alıp
KENDİ şemanda bir read-model (projection) olarak tut. Örnek: Patoloji
sisteminin `PatientSnapshot` tablosu — HBYS'den gelen ADT mesajıyla beslenir,
hasta adı/MRN gibi minimum bilgiyi tutar.

## SOLID

### Tek Sorumluluk (S)
Her sınıf tek bir değişim nedenine sahiptir.

Doğru ayrım (patoloji):
- `AccessionCodeGenerator`: kod üretir
- `SpecimenIntakeService`: numune kabulü yapar
- `SlideScanService`: tarama sonucunu VNA'ya yazar
- `StageTransitionService`: aşama geçişini doğrular ve kaydeder

Yanlış: `PathologyService` içinde kod üretme + kabul + tarama + rapor.

### Açık/Kapalı (O)
Yeni bir depolama arka ucu (Azure Blob) eklemek, mevcut kodu DEĞİŞTİRMEDEN
`IArchiveStorage` interface'ini implement etmekle mümkün olmalıdır.

### Liskov (L)
`IArchiveStorage` implementasyonları (MinIO, AzureBlob, LocalFile) birbirinin
yerine geçebilmelidir. Bir implementasyon "bu metot bende çalışmaz" diye
`NotSupportedException` atıyorsa, interface yanlış tasarlanmıştır.

### Arayüz Ayrımı (I)
`IBarcodeReader` ve `IBarcodeGenerator` ayrı interface'lerdir. Tarayıcı
cihazı yalnızca okur; kod üretmek zorunda kalmamalıdır.

### Bağımlılığın Tersine Çevrilmesi (D)
Constructor injection. Somut sınıfa değil, interface'e bağımlı ol.
Service locator anti-pattern'i (`IServiceProvider.GetService` çağrısı iş
kodunun içinde) YASAKTIR.

## CQRS (MediatR)

Her kullanım senaryosu bir Command veya Query'dir.

```csharp
// Command: durum değiştirir, void veya minimal sonuç döner
public sealed record AccessionSpecimenCommand(
    Guid PathologyCaseId,
    SpecimenType SpecimenType,
    string CollectedFrom,
    Guid AccessionedByStaffId) : IRequest<Result<SpecimenAccessionedDto>>;

// Query: durum değiştirmez, veri okur
public sealed record GetCaseStageQuery(Guid PathologyCaseId)
    : IRequest<Result<CaseStageDto>>;
```

Kurallar:
- Handler tek bir iş yapar, 100 satırı geçiyorsa domain servisine bölünmelidir.
- Doğrulama `FluentValidation` ile, MediatR pipeline behavior olarak yapılır.
  Handler içinde manuel `if (x == null) throw` doğrulaması YAZILMAZ.
- Query handler'ları `AsNoTracking()` kullanır.
- Command handler'ları domain metodunu çağırır; iş kuralını handler içinde
  yeniden yazmaz.

## Domain modelleme

- Anemik model YASAK. `Slide` sınıfı sadece property torbası olamaz; kendi
  değişmezliklerini (invariant) korumalıdır.
- Durum değişimi public setter ile DEĞİL, anlamlı bir metotla yapılır.

Yanlış:
```csharp
slide.Status = SlideStatus.Scanned;  // hiçbir kural kontrol edilmedi
```

Doğru:
```csharp
slide.AttachScan(digitalSlideId, scannedByStaffId, clock.UtcNow);
// metot içinde: aşama uygun mu, önceki tarama superseded edildi mi, event yayıldı mı
```

- Değişmezlik gerektiren kavramlar value object olur: `MedicalRecordNumber`,
  `AccessionCode`, `DicomUid`.
- Aggregate root dışından alt entity'ye doğrudan erişilmez. `Block`'a
  `PathologyCase` üzerinden ulaşılır.

## İsimlendirme

- Servis: `*Service` (`SpecimenIntakeService`)
- Repository: `I*Repository` / `*Repository`
- Command: `*Command`, Query: `*Query`, Handler: `*CommandHandler`
- DTO: `*Dto`
- Domain event: `*DomainEvent` (`SlideScannedDomainEvent`)
- Entegrasyon mesajı: `*IntegrationEvent` (`PathologyReportCompletedIntegrationEvent`)

Namespace: `VirtualHospital.{Context}.{Layer}.{Klasör}`
Örnek: `VirtualHospital.Pathology.Domain.Entities`

Veritabanı: tablo ve kolon isimleri `snake_case` (PostgreSQL konvansiyonu),
EF Core mapping'de açıkça belirtilir.

## Async

- I/O yapan her metot `async` ve `Task<T>` döner, `CancellationToken` alır.
- `.Result` ve `.Wait()` KULLANILMAZ (deadlock riski).
- Kütüphane kodunda `ConfigureAwait(false)`; ASP.NET Core kodunda gerekmez.

## Yasaklar

- `Domain` katmanında EF Core, HTTP, dosya sistemi veya RabbitMQ referansı.
- Controller içinde `DbContext` kullanımı.
- Magic string / magic number. Sabitler enum veya config'e taşınır.
- Kod içine gömülü bağlantı dizesi, parola, API anahtarı.
- `catch (Exception) { }` — sessiz yutma.
