# Entegrasyon ve Mesajlaşma Kuralları

## Temel ilke: asenkron

Sistemler arası klinik veri akışı HER ZAMAN mesaj kuyruğu üzerinden gider.
Senkron HTTP çağrısı ile bir bounded context'ten diğerine klinik veri
çekilmez.

Gerekçe: Bir sistem yavaşladığında (LIS yoğun, PACS arşivi meşgul) diğerleri
bloke olmamalıdır. Gerçek hastanelerde senkron zincir kaskad çöküşe yol açar.

İstisna: UI'ı besleyen okuma uçları REST'tir. Bu, context içi bir çağrıdır,
context'ler arası değil.

## Mesaj sözleşmeleri

Tüm entegrasyon mesajları `VirtualHospital.Contracts` projesinde tanımlanır.
Bir context, kendi domain event'ini doğrudan yayınlamaz; onu bir
`*IntegrationEvent`'e çevirir.

```csharp
// Domain event (context içi, Domain katmanında)
public sealed record SlideScannedDomainEvent(Guid SlideId, Guid DigitalSlideId) : IDomainEvent;

// Integration event (context dışı, Contracts projesinde)
public sealed record PathologySlideScannedIntegrationEvent(
    Guid CaseId,
    string AccessionCode,
    string MedicalRecordNumber,
    DateTimeOffset ScannedAtUtc);
```

Kural: Integration event, domain entity'sini TAŞIMAZ. Sadece primitive ve
basit tipler. Alıcı context, göndericinin domain modelini bilmemelidir.

## HL7 v2.5

Klinik mesajlar HL7 v2.5 formatındadır. Desteklenen tipler:

| Mesaj | Yön | Amaç |
|---|---|---|
| ADT^A04 | HBYS -> LIS, PMS, PACS | Hasta kaydı / kabul |
| ADT^A08 | HBYS -> hepsi | Hasta demografik güncelleme |
| ORM^O01 | HBYS -> LIS | Lab tetkik istemi |
| ORM^O01 | HBYS -> PMS | Patoloji tetkik istemi |
| ORU^R01 | LIS -> HBYS | Lab sonucu |
| ORU^R01 | PMS -> HBYS | Patoloji raporu |
| ORU^R01 | PACS -> HBYS | Radyoloji raporu |

Ham HL7 mesajı, işlendikten sonra PostgreSQL'de JSONB kolonunda SAKLANIR
(`raw_hl7` alanı). Gerekçe: bir mesaj hatalı işlenirse orijinaline dönebilmek;
denetimde "bize tam olarak ne geldi" sorusunu cevaplayabilmek.

## Mesaj işleme

- Idempotent olmalıdır. Aynı mesaj iki kez gelirse ikinci kez yan etki
  üretmemelidir. Her mesajda `MessageControlId` (MSH-10) vardır; işlenmiş
  mesaj kimlikleri saklanır ve tekrar gelen mesaj yok sayılır.
- Sıra garantisi varsayılmaz. ORU, ORM'den önce gelebilir. Handler bu duruma
  dayanıklı olmalıdır (örneğin ilgili sipariş henüz yoksa mesajı bekleme
  kuyruğuna al, hemen hata verme).
- Hata durumunda: MassTransit retry (exponential backoff, en fazla 3 deneme),
  sonra hata kuyruğuna (`_error`) taşı. Sessizce yutma YASAK.
- Hata kuyruğundaki mesajlar için manuel inceleme ekranı olmalıdır. Kaybolan
  bir lab sonucu hasta güvenliği sorunudur.

## Ayrıştırma (parsing)

- HL7 ayrıştırma tek bir yerde yapılır (`Infrastructure/Messaging/Hl7`).
  Her handler kendi ayrıştırıcısını yazmaz.
- Ayrıştırma hatası, mesajı düşürmez; hatalı mesaj `raw_hl7` ile birlikte
  hata kuyruğuna gider ve manuel incelemeye açılır.
- Segment sırası ve zorunlu alanlar doğrulanır. Zorunlu alan eksikse NAK
  (olumsuz onay) üretilir.

## MassTransit yapılandırması

- Her consumer için ayrı kuyruk.
- `ConsumerDefinition` içinde retry ve concurrency limiti açıkça belirtilir.
- Mesaj sözleşmeleri versiyonlanır. Kırıcı değişiklik (breaking change)
  yapılacaksa yeni bir sözleşme (`...V2`) eklenir, eskisi bir süre desteklenir.

## DICOMweb

PACS ve Patoloji, VNA ile DICOMweb üzerinden konuşur:

- STOW-RS: görüntü yükleme (`POST /dicomweb/studies`)
- WADO-RS: görüntü çekme (`GET /dicomweb/studies/{uid}/series/{uid}/instances/{uid}`)
- QIDO-RS: sorgulama (`GET /dicomweb/studies?PatientID=...`)

Patoloji WSI, DICOM VL Whole Slide Microscopy olarak saklanır
(SOP Class UID: 1.2.840.10008.5.1.4.1.1.77.1.6).

Kural: Viewer, radyoloji ve patoloji için AYNI protokolü kullanır. İki ayrı
erişim yolu yazılmaz.

## SSO ve viewer

Viewer, HBYS içinden Keycloak token'ı ile açılır. Token, viewer'ın VNA'dan
görüntü çekme yetkisini taşır. Görüntü URL'lerinde hasta kimliği veya kalıcı
paylaşım linki BULUNMAZ; erişim her zaman token doğrulamasından geçer.
