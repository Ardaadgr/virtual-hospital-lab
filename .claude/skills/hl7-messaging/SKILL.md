---
name: hl7-messaging
description: >
  HL7 v2.5 mesajlaşma: ADT (hasta kaydı), ORM (tetkik istemi), ORU (sonuç)
  mesajlarının yapısı, ayrıştırılması, RabbitMQ/MassTransit üzerinden
  taşınması, idempotency ve hata yönetimi. Sistemler arası klinik mesajlaşma
  ile ilgili her işte kullan.
---

# HL7 v2.5 Mesajlaşma

## Mesaj tipleri

| Mesaj | Yön | Amaç |
|---|---|---|
| ADT^A04 | HBYS -> LIS, PMS, PACS | Hasta kaydı / kabul |
| ADT^A08 | HBYS -> hepsi | Demografik güncelleme |
| ORM^O01 | HBYS -> LIS | Lab tetkik istemi |
| ORM^O01 | HBYS -> PMS | Patoloji tetkik istemi |
| ORU^R01 | LIS -> HBYS | Lab sonucu |
| ORU^R01 | PMS -> HBYS | Patoloji raporu |
| ORU^R01 | PACS -> HBYS | Radyoloji raporu |

## Yapı

Mesaj, satırlarla ayrılmış segmentlerden oluşur. Alanlar `|` ile, bileşenler
`^` ile ayrılır.

Örnek ORU^R01 (lab sonucu):

```
MSH|^~\&|LIS|HASTANE|HBYS|HASTANE|20260711103000||ORU^R01|MSG00001|P|2.5
PID|1||MRN000123||YILMAZ^AYSE||19850515|K
OBR|1|ORD001||GLU^Glukoz|||20260711100000
OBX|1|NM|GLU^Glukoz||92|mg/dL|70-100|N|||F
```

Segmentler:
- `MSH`: mesaj başlığı. MSH-10 = `MessageControlId` (idempotency anahtarı).
- `PID`: hasta kimliği.
- `ORC` / `OBR`: sipariş bilgisi.
- `OBX`: gözlem/sonuç satırı. Bir siparişte birden çok OBX olabilir.

## Bağlayıcı kurallar

1. **Idempotency.** Aynı `MessageControlId` (MSH-10) ikinci kez gelirse yan
   etki ÜRETİLMEZ. İşlenmiş mesaj kimlikleri saklanır.

2. **Sıra garanti değildir.** ORU, ORM'den önce gelebilir. Handler buna
   dayanıklı olmalıdır: ilgili sipariş henüz yoksa mesajı bekleme kuyruğuna
   al, hemen hata verme.

3. **Ham mesaj saklanır.** Ayrıştırıldıktan sonra orijinal HL7 metni
   PostgreSQL `jsonb` kolonunda tutulur. Bir mesaj hatalı işlenirse
   orijinaline dönebilmek; denetimde "bize tam olarak ne geldi" sorusunu
   cevaplayabilmek gerekir.

4. **Ayrıştırma tek yerde.** Her handler kendi parser'ını yazmaz.
   `Infrastructure/Messaging/Hl7` altında tek ayrıştırıcı.

5. **Sessiz yutma YASAK.** Zorunlu segment eksikse NAK üret. Ayrıştırma
   hatası mesajı düşürmez; hatalı mesaj ham haliyle hata kuyruğuna gider ve
   MANUEL İNCELEME EKRANINA düşer.

   Kaybolan bir lab sonucu veya patoloji raporu, hasta güvenliği olayıdır.
   Kimsenin görmediği bir hata kuyruğu kabul edilemez.

6. **CorrelationId taşınır.** Bir isteğin HBYS -> LIS -> HBYS yolculuğu tek
   kimlikle izlenebilmelidir.

## MassTransit

- Her consumer için ayrı kuyruk.
- Retry: exponential backoff, en fazla 3 deneme. Sonra hata kuyruğu.
- Mesaj sözleşmeleri versiyonlanır. Kırıcı değişiklik yapılacaksa yeni
  sözleşme (`...V2`) eklenir, eskisi bir süre desteklenir.

## Integration event vs domain event

- Domain event: context İÇİNDE kalır, domain entity referansı taşıyabilir.
- Integration event: context DIŞINA çıkar, `VirtualHospital.Contracts`
  projesindedir, YALNIZCA primitive tip taşır. Alıcı, göndericinin domain
  modelini bilmemelidir.
