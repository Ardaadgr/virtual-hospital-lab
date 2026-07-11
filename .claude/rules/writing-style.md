# Yazım ve Üslup Kuralları

## Emoji yasağı

Bu projede emoji KULLANILMAZ. İstisnası yoktur.

Kapsam:
- Kaynak kodu ve kod yorumları
- Commit mesajları ve PR açıklamaları
- Markdown dokümanları (CLAUDE.md, README, ADR, skill dosyaları)
- Log mesajları ve hata metinleri
- Kullanıcıya verilen sohbet yanıtları
- Test isimleri ve test çıktıları

Gerekçe: Bu kurumsal bir sağlık bilişimi projesidir. Çıktılar denetime,
klinik incelemeye ve resmi dokümantasyona konu olabilir. Emoji bu bağlamda
ciddiyetsiz durur ve bazı terminal/log toplayıcı ortamlarda bozuk karakter
üretir.

Vurgu gerekiyorsa emoji yerine şunlar kullanılır:
- Metinsel etiketler: `UYARI:`, `KRİTİK:`, `YAPILMAZ:`, `ÖNERİ:`
- Markdown vurgusu: kalın, italik, kod bloğu
- Yapılandırılmış listeler ve tablolar

Yanlış:
```
## Kurulum 🚀
- ✅ Docker kurulu olmalı
- ⚠️ Port 5432 boş olmalı
```

Doğru:
```
## Kurulum
- Docker kurulu olmalı.
- UYARI: 5432 portu boş olmalı.
```

## Dil

- Dokümantasyon ve kullanıcıya yanıtlar: Türkçe.
- Kod, sınıf/metot/değişken isimleri, XML doc yorumları: İngilizce.
  Gerekçe: .NET ekosistemi ve olası ekip genişlemesi için standart.
- Commit mesajları: Türkçe, emirsel kip. Örnek:
  `Patoloji vaka aşama geçişi doğrulaması eklendi`

## Ton

- Doğrudan ve ölçülü. Abartı, pazarlama dili ve gereksiz süsleme yok.
- Belirsizlik varsa belirt. "Bu çalışır" yerine "Bunu derleyip test etmedim,
  doğrulanması gerekiyor."
- Bir şey ölçülmediyse ölçülmüş gibi sunulmaz.
