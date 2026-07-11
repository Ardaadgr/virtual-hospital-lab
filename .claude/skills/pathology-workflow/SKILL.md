---
name: pathology-workflow
description: >
  Patoloji laboratuvarının fiziksel iş akışının dijitalleştirilmesi: numune
  (M) - blok (B) - lam (S) takip zinciri, aşama makinesi, barkod üretimi,
  dijital slide taraması ve konsültasyon. Patoloji domain modeli, aşama
  geçişi, takip kodu veya slide yaşam döngüsü ile ilgili her işte kullan.
---

# Patoloji İş Akışı (PMS)

Bu sistemin özü, fiziksel bir patoloji laboratuvarındaki numune yaşam
döngüsünü dijital olarak takip etmektir. Yazılım, laboratuvarın gerçekliğine
uyar; laboratuvar yazılıma uymaz.

## Takip zinciri

```
Numune (Specimen)      kod: M...
  |  1 --< N           grossing (makroskopik örnekleme)
Blok (Block)           kod: B...
  |  1 --< N           mikrotom kesiti; VARSAYILAN 1:1
Lam (Slide)            kod: S...
  |  1 --< N           tarama; yalnızca EN GÜNCEL aktif
Dijital Slide (WSI)    VNA'da DICOM WSI
```

Her halka bir öncekine doğrulanabilir şekilde bağlıdır. Bir blok, hangi
numuneden geldiği belirsiz olacak şekilde var olamaz. Yanlış numune eşleşmesi
patolojide en ciddi hasta güvenliği hatasıdır ve barkod zinciri bunu önlemek
içindir.

## Aşama makinesi

```
Accessioned      Kabul. Numune patolojiye teslim alındı, M kodu üretildi.
     v
Grossing         Makroskopik örnekleme. Numune incelenip parçalara ayrılır.
     v
Processing       Doku takibi (dehidratasyon, berraklaştırma, parafin emdirme).
     v
Embedding        Bloklama. Parafin bloklar oluşur, B kodları üretilir.
     v
Sectioning       Mikrotom ile kesit alma. Lamlar oluşur, S kodları üretilir.
     v
Staining         Boyama (H&E, IHC, özel boyalar).
     v
Scanning         Dijital tarama. WSI üretilir, VNA'ya yüklenir.
     v
UnderReview      Patolog inceliyor.
     |
     +--> InConsultation   (opsiyonel dal: ikinci görüş)
     |         |
     v         v
Reported         Rapor tamamlandı, HBYS'ye ORU^R01 ile gönderildi.
```

Geçersiz geçişler domain seviyesinde REDDEDİLİR. Örneğin `Accessioned`
durumundan doğrudan `Reported` durumuna geçilemez.

Her geçiş `StageTransition` olarak kaydedilir:
- Önceki aşama, yeni aşama
- Zaman damgası (UTC)
- İşlemi yapan personel kimliği

Vakanın "şu an hangi aşamada" olduğu TEK kaynaktan okunur: bu zincirin son
halkası. Aşama bilgisi birden fazla yerde tutulup senkronize edilmeye
çalışılmaz.

## Boyama

Boyama türü LAM seviyesinde bir alandır (`StainType`), blok seviyesinde değil.
Aynı bloktan farklı boyalarla lam çıkarılabilir (H&E, IHC, PAS, Masson vb.).

## Blok -> Lam varsayılan 1:1

Normal akışta bir bloktan bir lam çıkar. İkinci bir lam ancak EK KESİT TALEBİ
ile üretilir:

- Talep eden hekim kimliği zorunludur.
- Gerekçe zorunludur (`AdditionalSectionReason`: ek boyama, yetersiz kesit,
  konsültasyon talebi, derinlik kesiti vb.).
- Domain bunu zorlar: `Block.CutAdditionalSlide(...)` gerekçesiz çağrılırsa
  `DomainException` atar.

Gerekçe: Ek kesit, bloktaki dokuyu tüketen geri alınamaz bir işlemdir.
Gelişigüzel yapılmamalıdır ve kim istedi sorusu cevaplanabilmelidir.

## Yeniden tarama (rescan)

Bir lam yeniden taranabilir (bulanık görüntü, odak hatası, kirli lam,
eksik tarama).

Davranış:
- Yeni `DigitalSlide` oluşur ve `Slide.CurrentDigitalSlideId` ona işaret eder.
- Eski `DigitalSlide` SİLİNMEZ. `DigitalSlideStatus.Superseded` olur ve
  VNA'da kalır.
- Yeniden tarama gerekçesi (`RescanReason`), zamanı ve tarayan personel
  kaydedilir.
- Patolog arayüzde YALNIZCA aktif olanı görür. Eski görüntüler ekranı
  kirletmez ama denetimde erişilebilirdir.

Gerekçe: Tıbbi görüntü, tanıya esas alınan kanıttır. Bir patolog v1 görüntüye
bakıp rapor yazdıysa, o raporun hangi görüntüye dayandığı geriye dönük
kanıtlanabilmelidir. Fiziksel silme, KVKK denetim izi yükümlülüğü ve olası
tıbbi sorumluluk incelemesi açısından kabul edilemez.

## Konsültasyon

Bir patolog vakayı ikinci görüş için başka bir patologa yönlendirebilir.

- Vaka `InConsultation` aşamasına geçer.
- Konsültan patolog, vakayı ve slide'ları görüntüleme yetkisi kazanır (ABAC:
  yalnızca kendisine yönlendirilen vaka).
- Konsültan görüşünü yazar (`ConsultationOpinion`).
- NİHAİ RAPORU YAZMA SORUMLULUĞU ASIL PATOLOGDA KALIR. Konsültasyon, sorumluluk
  devri değildir.

## Roller

- `PathologyTechnician`: numune kabul, grossing kaydı, bloklama, kesit,
  boyama, tarama. Aşama günceller. TANI KOYAMAZ, RAPOR YAZAMAZ.
- `Pathologist`: slide inceleme, anotasyon, konsültasyon talebi, rapor yazma.
  Yalnızca kendisine atanmış veya konsülte edilen vakalara erişir.

## Takip kodları

Numune, blok ve lam için ayrı önekli kodlar, patoloji sistemine KABUL anında
otomatik üretilir. Hastanın tetkik/vaka kimliğinden türetilir.

- Numune: `M` öneki
- Blok: `B` öneki
- Lam: `S` öneki

Üretim `IAccessionCodeGenerator` arkasında soyutlanmıştır ve
yapılandırılabilir.

AÇIK KONU: Kodun tam kompozisyonu (yıl bileşeni, hane sayısı, hasta tetkik
kimliğinin kaç hanesi) henüz kesinleşmemiştir. Varsayılan implementasyon
değiştirilebilir. Bkz. ARCHITECTURE.md AD-011.

## HBYS ile entegrasyon

- Gelen: `ORM^O01` (patoloji tetkik istemi). Hekim HBYS'den patoloji ister.
- Giden: `ORU^R01` (patoloji raporu). Rapor tamamlanınca HBYS'ye gider.
- Patoloji, HBYS'nin veritabanına ERİŞMEZ. Hasta demografisi `ADT^A04/A08`
  mesajıyla gelir ve patoloji şemasında bir read-model (`PatientSnapshot`)
  olarak tutulur.

## İlgili dosyalar

- `src/Pathology/VirtualHospital.Pathology.Domain/Entities/`
- `src/Pathology/VirtualHospital.Pathology.Domain/ValueObjects/AccessionCode.cs`
- `src/Pathology/VirtualHospital.Pathology.Domain/Enums/PathologyStage.cs`
- `src/Pathology/VirtualHospital.Pathology.Domain/Services/StageTransitionPolicy.cs`

## Doğrulama

Değişiklik sonrası `pathology-specialist` ajanını, kritik tasarım kararlarında
`health-governance` ajanını çalıştır. `tests/` altındaki patoloji domain
testlerini koştur.
