# Test Stratejisi

## Test piramidi

- Birim test (yaklaşık %70): Domain mantığı. Veritabanı yok, ağ yok, dosya yok.
- Entegrasyon testi (yaklaşık %20): Repository, DbContext, mesaj consumer'ları,
  API uçları. Testcontainers ile gerçek PostgreSQL ve RabbitMQ.
- Uçtan uca test (yaklaşık %10): Docker Compose ile tam ortam, kritik iş akışları.

## Birim testleri

Domain katmanı, veritabanı olmadan tam test edilebilir olmalıdır. Bu, Clean
Architecture'ın asıl kazanımıdır; bunu kaybedecek bir tasarım yanlıştır.

İsimlendirme: `MetotAdi_Kosul_BeklenenSonuc`

```csharp
[Fact]
public void CutAdditionalSlide_WithoutRequestReason_ThrowsDomainException()
{
    var block = Block.Create(/* ... */);
    block.CutSlide(/* ilk lam, normal akış */);

    var act = () => block.CutAdditionalSlide(reason: null, requestedByPhysicianId: Guid.NewGuid());

    act.Should().Throw<DomainException>();
}
```

Zorunlu birim testleri (patoloji):
- Aşama geçiş makinesi: geçerli geçişler kabul, geçersiz geçişler REDDEDİLİR.
  Örnek: `Accessioned -> Reported` doğrudan geçişi hata vermelidir.
- Blok -> lam varsayılan 1:1: ikinci lam gerekçesiz oluşturulamaz.
- Yeniden tarama: yeni tarama aktif olur, eskisi `Superseded` olur, SİLİNMEZ.
- Accession kodu üretimi: önek doğru (M/B/S), format tutarlı, çakışma yok.
- Konsültasyon: konsültan atandığında vaka `InConsultation` olur; nihai rapor
  yetkisi asıl patologda kalır.

Zorunlu birim testleri (HBYS):
- MRN üretimi: aynı hasta ikinci kez kaydedilmeye çalışılırsa YENİ MRN
  ÜRETİLMEZ, mevcut MRN döner.
- Encounter: bir MRN'e birden çok Encounter bağlanabilir.

## Entegrasyon testleri

Testcontainers kullanılır. In-memory provider (`UseInMemoryDatabase`)
KULLANILMAZ; PostgreSQL'e özgü davranışları (JSONB, kısıtlar, transaction
izolasyonu) yakalayamaz ve yanlış güven verir.

```csharp
public sealed class PathologyDbFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .Build();
    // ...
}
```

Kapsanacaklar:
- Repository CRUD ve sorgu doğruluğu
- EF Core mapping (Fluent API konfigürasyonu gerçekten çalışıyor mu)
- Migration'lar temiz veritabanında sorunsuz uygulanıyor mu
- Mesaj consumer'ları: HL7 mesajı geldiğinde doğru komut tetikleniyor mu
- Idempotency: aynı mesaj iki kez işlendiğinde ikinci kez yan etki yok

## Uçtan uca test

Kritik akış (patoloji), Docker Compose ortamında:

1. Hasta HBYS'ye kabul edilir, MRN üretilir.
2. Hekim patoloji tetkiki ister (ORM^O01 mesajı PMS'e gider).
3. PMS numuneyi kabul eder, M kodu üretilir.
4. Teknisyen grossing yapar, bloklar (B kodu) oluşur.
5. Kesit alınır, lam (S kodu) oluşur.
6. Boyama yapılır.
7. Lam taranır, WSI VNA'ya yüklenir.
8. Patolog vakayı açar, slide'ı viewer'da görüntüler, anotasyon ekler.
9. Patolog konsültasyon ister; ikinci patolog görüş yazar.
10. Asıl patolog raporu tamamlar (ORU^R01 mesajı HBYS'ye gider).
11. Rapor HBYS'de hasta kaydında görünür.

Her adımda doğrulanacak: aşama geçişi kaydedildi mi, denetim izi üretildi mi.

## Test verisi

Test verisi TAMAMEN SENTETİKTİR. Gerçek hasta verisi, gerçek isim, gerçek TC
kimlik numarası kullanılmaz. Bkz. `data-privacy-kvkk.md`.

DICOM/WSI test dosyaları: her modalite için 5-10 adet sentetik veya kamuya açık
anonim örnek yeterlidir. Büyük görüntü dosyaları git'e COMMIT EDİLMEZ; ayrı
bir indirme script'i ile temin edilir.

## Kapsam hedefleri

- Domain katmanı: %90+ satır kapsamı. İş kuralı içeren her metot test edilir.
- Application katmanı: kritik handler'lar test edilir.
- Infrastructure: entegrasyon testleriyle kapsanır, birim testi aranmaz.
- Api: controller ince olduğu için ayrı test gerekmez; entegrasyon testi yeter.

Kapsam yüzdesi bir hedef değil, bir göstergedir. %100 kapsamlı ama iş kuralını
doğrulamayan test değersizdir.

## Araçlar

xUnit, Moq, FluentAssertions, Testcontainers, Respawn (veritabanı sıfırlama).

## Commit öncesi

```bash
dotnet build
dotnet test
dotnet format --verify-no-changes
```

Üçü de temiz olmadan commit atılmaz.
