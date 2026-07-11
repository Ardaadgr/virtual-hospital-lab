---
name: hbys-domain
description: >
  HBYS (Hastane Bilgi Yönetim Sistemi) domain modeli: hasta kabul, MRN
  üretimi, Encounter (ziyaret) yönetimi, poliklinik yapısı ve faturalama
  iskeleti. Hasta kimliği, ziyaret veya poliklinik ile ilgili her işte kullan.
---

# HBYS Domain

## Kritik ayrım: MRN ve Encounter

Bu ikisi FARKLI kavramlardır ve karıştırılmaları en yaygın modelleme
hatasıdır.

- `MedicalRecordNumber` (MRN): Hastanın hastane genelindeki ÖMÜR BOYU SABİT
  kimliği. Hasta ilk kez kaydolduğunda üretilir. Hasta beş yıl sonra tekrar
  gelse AYNI MRN kullanılır. Bir value object'tir, `string` değildir.
- `Encounter` (Ziyaret / Başvuru): Her başvuru için ayrı bir kayıt. Poliklinik,
  tarih, sorumlu hekim bilgisi taşır.

İlişki: `Patient (1 MRN) 1 --< N Encounter`

Bağlayıcı kural: Klinik iş akışları (lab siparişi, patoloji vakası, radyoloji
tetkiki) her zaman bir `EncounterId`'ye bağlanır, doğrudan MRN'e değil.
Ancak hastanın tüm geçmişi MRN üzerinden sorgulanabilir.

## Hasta kabul akışı

1. Hasta başvurur. Kimlik bilgileriyle mevcut kayıt aranır.
2. Kayıt VARSA: mevcut MRN kullanılır. YENİ MRN ÜRETİLMEZ.
3. Kayıt YOKSA: yeni `Patient` oluşturulur, yeni MRN üretilir.
4. Her durumda yeni bir `Encounter` açılır.
5. `ADT^A04` mesajı yayınlanır (LIS, PMS, PACS bunu dinler ve kendi
   read-model'lerini besler).

Mükerrer kayıt (duplicate patient) hastanelerde ciddi bir sorundur. Aynı
hastanın iki MRN'i olursa klinik geçmişi bölünür. Kimlik eşleştirme mantığı
(TC kimlik numarası + doğum tarihi gibi) dikkatli tasarlanmalıdır.

## Poliklinikler

Tanımlanan poliklinikler: Dahiliye, Cerrahi, Radyoloji, Laboratuvar,
Patoloji, Acil (triyaj dahil).

MEVCUT KAPSAM: Poliklinik yapısı ve Encounter'a bağlanması kurulur, ancak
poliklinik-özel klinik iş mantığı (muayene formu, tanı girişi, reçete) BU
FAZDA GELİŞTİRİLMEZ. Şimdilik yalnızca hasta kabul + MRN üretimi + Encounter
açma çalışır durumdadır. Bkz. ARCHITECTURE.md AD-015.

## Faturalama

İskelet kurulur (`Invoice`, `InvoiceLine`). Gerçek Medula provizyon
entegrasyonu sonraki faza bırakılmıştır.

## Yayınlanan mesajlar

- `ADT^A04`: hasta kaydı / kabul
- `ADT^A08`: demografik güncelleme
- `ORM^O01`: lab veya patoloji tetkik istemi

## Dinlenen mesajlar

- `ORU^R01`: LIS, PMS veya PACS'ten gelen sonuç/rapor
