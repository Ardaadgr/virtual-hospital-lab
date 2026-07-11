# Dağıtım ve Operasyon

## Geliştirme ortamı

Tek komutla ayağa kalkar:

```bash
docker compose up -d
```

Compose ile gelen servisler:

| Servis | Amaç | Port |
|---|---|---|
| postgres | Tüm bounded context şemaları | 5432 |
| rabbitmq | Mesaj kuyruğu (management UI dahil) | 5672 / 15672 |
| keycloak | OAuth2 / OIDC kimlik sağlayıcı | 8080 |
| minio | S3 uyumlu nesne depolama (VNA arka ucu) | 9000 / 9001 |
| orthanc | DICOM sunucusu, DICOMweb uçları | 8042 |

.NET API'leri geliştirme sırasında `dotnet run` ile lokal çalışır; yalnızca
bağımlılıklar konteynerdedir. Bu, hata ayıklamayı kolaylaştırır.

## Yapılandırma

- Sırlar kod içine YAZILMAZ. Geliştirmede .NET User Secrets, üretimde ortam
  değişkeni.
- `appsettings.json`: yapısal varsayılanlar.
- `appsettings.Development.json`: yerel geliştirme (git'e girer, sır içermez).
- Bağlantı dizeleri, Keycloak client secret, MinIO anahtarları: ortam değişkeni.

## Veritabanı migration

Her bounded context'in kendi migration geçmişi vardır.

```bash
dotnet ef migrations add <Ad> \
  --project src/Pathology/VirtualHospital.Pathology.Infrastructure \
  --startup-project src/Pathology/VirtualHospital.Pathology.Api

dotnet ef database update \
  --project src/Pathology/VirtualHospital.Pathology.Infrastructure \
  --startup-project src/Pathology/VirtualHospital.Pathology.Api
```

Kurallar:
- Üretilen SQL, uygulanmadan önce GÖZDEN GEÇİRİLİR. EF Core'un ürettiği her
  migration doğru değildir; veri kaybına yol açan kolon silme/tip değiştirme
  işlemleri elle düzeltilir.
- Migration'lar geriye dönük uyumlu olmalıdır (önce kolon ekle, sonra kod
  yaz, sonra eski kolonu kaldır). Tek adımda kırıcı değişiklik yapılmaz.
- Üretim veritabanında `EnsureCreated()` KULLANILMAZ; yalnızca migration.

## CI (GitHub Actions veya Azure Pipelines)

Pull request'te çalışır:

1. Checkout
2. .NET 8 SDK kurulumu
3. `dotnet restore`
4. `dotnet build --no-restore -warnaserror`
5. `dotnet format --verify-no-changes`
6. `dotnet test` (Testcontainers için Docker gerekir)
7. Statik analiz (Roslyn analyzers, opsiyonel SonarQube)

Herhangi bir adım başarısızsa PR birleştirilemez.

## Loglama ve izleme

- Yapılandırılmış loglama (Serilog), JSON formatında.
- Her istek bir `CorrelationId` taşır; bu kimlik mesaj kuyruğu üzerinden
  sistemler arasında TAŞINIR. Bir lab sonucunun HBYS'den LIS'e ve geri
  dönüşünü tek bir kimlikle izleyebilmek gerekir.
- Loglara hasta kimliği YAZILMAZ (bkz. data-privacy-kvkk.md).
- Denetim izi (audit) ile uygulama logu AYRI kanallardır. Audit kaydı
  silinemez ve 6 yıl saklanır.

## Sağlık kontrolleri

Her API `/health` ucu sunar; bağımlılıklarını (PostgreSQL, RabbitMQ, MinIO,
Orthanc) kontrol eder. Docker Compose healthcheck bunları kullanır.

## Yedekleme

- PostgreSQL: günlük tam yedek, saatlik WAL arşivi.
- MinIO (görüntü arşivi): sürümleme açık, çapraz bölge replikasyonu
  (üretimde).
- Görüntü kaybı geri alınamaz bir klinik veri kaybıdır. Yedek stratejisi
  test EDİLİR; "yedek alıyoruz" demek yetmez, geri yükleme provası yapılır.

## Kapsam dışı

Kubernetes, service mesh, çoklu bölge dağıtımı. Docker Compose bu fazda
yeterlidir (bkz. ARCHITECTURE.md AD-015).
