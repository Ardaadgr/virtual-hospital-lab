---
name: lis-domain
description: >
  LIS (Laboratuvar Bilgi Sistemi) domain modeli: biyokimya ve mikrobiyoloji
  test siparişleri, sonuç girişi, oto-onay (autoverification) kuralları ve
  HL7 sonuç mesajlaşması. Lab siparişi, sonuç veya oto-onay ile ilgili her
  işte kullan.
---

# LIS Domain

## Kapsam

- Biyokimya: sayısal sonuçlar (glukoz, üre, kreatinin, elektrolitler).
  Referans aralığı ve birim taşır.
- Mikrobiyoloji: kültür ve metin bazlı sonuçlar (üreyen mikroorganizma,
  antibiyogram). Sayısal değil, yapılandırılmış metin.

Bu ikisi FARKLI sonuç tipleridir; tek bir `Result.Value` string alanına
sıkıştırılmaz.

## Akış

1. HBYS'den `ORM^O01` gelir, `LabOrder` oluşur.
2. Numune alınır, cihaza (mock) gider.
3. Cihazdan sonuç gelir, `LabResult` oluşur.
4. Oto-onay kuralları çalışır.
5. Onaylanan sonuç `ORU^R01` ile HBYS'ye gider.

## Oto-onay (autoverification)

Belirli koşullar sağlanırsa sonuç, teknisyen onayı beklemeden doğrudan
HBYS'ye gönderilir.

Örnek kural: "Glukoz 70-100 mg/dL aralığındaysa ve delta-check geçtiyse
teknisyen onayına düşmeden ORU^R01 gönder."

KRİTİK GÜVENLİK SINIRI: Oto-onay kuralları KRİTİK DEĞERLERİ (panic values)
ASLA otomatik geçirmez. Kritik değer, hastanın hayatını tehdit eden bir
sonuçtur (örneğin potasyum 7.0 mEq/L) ve mutlaka insan onayından ve acil
bildirimden geçer.

Oto-onay kuralı tasarlanırken `health-governance` ajanının denetimine
sunulmalıdır. Fazla geniş bir oto-onay sınırı doğrudan hasta güvenliği riski
yaratır.

Kural yapısı:
- Test kodu
- Sayısal aralık (alt/üst sınır)
- Delta-check (önceki sonuçla fark eşiği)
- Kritik değer sınırları (bu sınırlar aşılırsa oto-onay DEVRE DIŞI)
- Aktif/pasif durumu, kimin ne zaman değiştirdiği (denetim izi)

## Kaybolan sonuç riski

Hata kuyruğuna düşen bir lab sonucu, kimsenin görmediği bir yerde kalırsa bu
bir hasta güvenliği olayıdır. Manuel inceleme kuyruğu ve uyarı mekanizması
ZORUNLUDUR.
