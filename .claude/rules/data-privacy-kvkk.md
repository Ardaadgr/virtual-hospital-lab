# Hasta Verisi Gizliliği (KVKK)

Sağlık verisi, KVKK m.6 kapsamında ÖZEL NİTELİKLİ kişisel veridir. Bu
kurallar istisnasızdır.

## Hassas veri kapsamı

Aşağıdakilerin tamamı hassas veridir:
- Kimlik: ad, soyad, TC kimlik numarası, MRN, doğum tarihi, iletişim bilgisi
- Klinik: teşhis, lab sonucu, radyoloji raporu, patoloji raporu
- Görüntü: DICOM serileri, patoloji dijital slide'ları (WSI)
- Görüntü üstü anotasyonlar ve patolog notları

UNUTMA: Bir DICOM dosyasının kendisi hasta kimliğini header'ında taşır.
Görüntüyü "anonim veri" saymak yanlıştır.

## Asla yapılmayacaklar

- Hasta kimliği, MRN, teşhis veya klinik sonucu LOGA yazmak.
  Loglarda yalnızca teknik korelasyon kimliği (`CaseId`, `CorrelationId`)
  bulunur. Hasta adı asla.
- Gerçek hasta verisini repoya commit etmek (`data/`, `seed/`, test fixture).
- Hasta kimliğini dosya adına veya URL query string'ine koymak.
  Yanlış: `/api/slides?patientName=Ahmet%20Yilmaz`
  Doğru: `/api/slides/{slideId}` (kimlik token'dan ve yetkiden gelir)
- Hasta verisini üçüncü parti servise (hata izleme, analitik, LLM API)
  göndermek.
- Örnek veri üretirken gerçek kişi bilgisi kullanmak. Test verisi tamamen
  sentetik olmalıdır.

## Denetim izi (audit trail)

Hasta verisine yapılan HER erişim kaydedilir. İstisnası yoktur.

Kaydedilecekler:
- Kim (kullanıcı kimliği, rol)
- Neye (kaynak tipi ve kimliği: PathologyCase, DicomStudy, LabResult)
- Ne zaman (UTC zaman damgası)
- Hangi işlem (Görüntüle, Değiştir, Dışa Aktar, Sil)
- Sonuç (başarılı / yetkisiz / hata)

Saklama süresi: en az 6 yıl. Denetim kayıtları DEĞİŞTİRİLEMEZ (append-only).

Uygulama: Her API çağrısında çalışan bir middleware veya MediatR pipeline
behavior. Geliştirici bunu her handler'da elle yazmak zorunda kalmamalıdır.

## Yetkilendirme

Rol tabanlı (RBAC) yeterli DEĞİLDİR. Öznitelik tabanlı (ABAC) katman zorunludur.

Örnek: "Patolog" rolüne sahip olmak, TÜM patoloji vakalarını görme yetkisi
vermez. Bir patolog yalnızca:
- kendisine ATANMIŞ vakaları,
- konsültasyon için kendisine YÖNLENDİRİLMİŞ vakaları
görebilir.

Rol matrisi:

| Rol | Erişim |
|---|---|
| SystemAdmin | Sistem yapılandırması. Klinik veriye erişim AYRICA loglanır ve kısıtlıdır. |
| Physician | Kendi hastalarının kaydı, tetkik isteme, sonuç görme |
| Pathologist | Atanmış/konsülte edilen patoloji vakaları, slide görüntüleme, rapor |
| PathologyTechnician | Numune/blok/lam takibi, aşama güncelleme. Rapor YAZAMAZ. |
| LabTechnician | LIS siparişleri, sonuç girişi, oto-onay kuralları |
| RadiologyTechnician | PACS tetkikleri, DICOM yükleme |
| Nurse | Hasta kabul, vital bulgular |
| BillingClerk | Faturalama. Klinik teşhis detayına erişim KISITLI. |

Yetki kontrolü iki katmanda yapılır:
1. API seviyesinde: `[Authorize(Roles = ...)]`
2. Sorgu seviyesinde: repository/handler, kullanıcının erişebileceği kayıtları
   filtreler. API'yi geçen bir istek, veri katmanında da doğrulanmalıdır.

## Şifreleme

Beklemede (at rest):
- PostgreSQL: disk seviyesinde şifreleme; hassas kolonlar için ek kolon
  şifrelemesi değerlendirilir.
- MinIO / VNA: sunucu tarafı şifreleme (AES-256).

Aktarımda (in transit):
- Tüm API çağrıları HTTPS / TLS 1.2+.
- DICOMweb çağrıları HTTPS.
- PostgreSQL bağlantısı `sslmode=require`.
- RabbitMQ bağlantısı TLS.

Sırlar (secrets): kod içine YAZILMAZ. Geliştirmede .NET User Secrets,
üretimde ortam değişkeni veya secret manager.

## Görüntü ve anonimleştirme

- Dışa aktarım (araştırma, eğitim, sunum) için DICOM anonimleştirme ZORUNLUDUR:
  PatientName, PatientID, PatientBirthDate, AccessionNumber, kurum bilgisi
  temizlenir; UID'ler yeniden üretilir.
- Patoloji WSI dosyaları da aynı kurala tabidir. Slide etiketinde (label
  image) hasta adı basılı olabilir; dışa aktarımda label image ÇIKARILIR.
- Anonimleştirme yapılmadan hiçbir görüntü sistem dışına çıkmaz.

## Silme ve saklama

- Klinik veri, yasal saklama süresi dolmadan silinmez.
- Yeniden taranan slide görüntüsü SİLİNMEZ, `Superseded` olur (bkz.
  ARCHITECTURE.md AD-010). Silme, denetim izini yok eder.
- Silme talebi (KVKK unutulma hakkı) hukuki değerlendirme gerektirir; kod
  seviyesinde otomatik hard-delete yoktur. Soft-delete + hukuki onay akışı.

## Yeni özellik yazarken kontrol listesi

1. Bu özellik hangi hassas veriye dokunuyor? Listele.
2. Yetki kontrolü hem API hem sorgu seviyesinde var mı?
3. Denetim kaydı üretiliyor mu?
4. Loglara hasta kimliği sızıyor mu? (log satırlarını gözden geçir)
5. Dışa aktarım varsa anonimleştirme uygulanıyor mu?
6. Test verisi tamamen sentetik mi?

## Uyum notu

Bu kurallar mühendislik güvencesidir. Hukuki uyum (aydınlatma metni, açık
rıza, VERBİS kaydı, veri sorumlusu yükümlülükleri) ayrıca sağlanmalıdır. Kod
bu yükümlülüğün yerine geçmez.
