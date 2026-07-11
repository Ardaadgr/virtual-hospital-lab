---
name: pacs-dicom
description: >
  PACS radyoloji görüntüleme domain modeli: DICOM Study/Series/Instance
  hiyerarşisi, MR/CT/DX modaliteleri, fo-dicom ile ayrıştırma ve VNA'ya
  arşivleme. Radyoloji görüntüsü, DICOM metadata veya modalite ile ilgili
  her işte kullan.
---

# PACS / DICOM Domain

## DICOM hiyerarşisi

```
Patient
  Study        (bir tetkik; StudyInstanceUID)
    Series     (bir seri; SeriesInstanceUID)
      Instance (tek bir görüntü; SOPInstanceUID)
```

Bu hiyerarşi DICOM standardının kendisidir; kendi hiyerarşini uydurma.

## Modaliteler

MR (manyetik rezonans), CT (bilgisayarlı tomografi), DX (dijital röntgen).

## Kritik uyarı: DICOM anonim değildir

Bir DICOM dosyası, header'ında hasta adını, kimlik numarasını ve doğum
tarihini TAŞIR. "Görüntü dosyası anonim veridir" varsayımı YANLIŞTIR ve
yaygın bir gizlilik ihlali kaynağıdır.

Dışa aktarımda anonimleştirme zorunludur: PatientName, PatientID,
PatientBirthDate, AccessionNumber, InstitutionName temizlenir; UID'ler
yeniden üretilir.

## UID üretimi

DICOM UID'leri rastgele GUID DEĞİLDİR. Yapı: kurum kök UID'i + benzersiz
sonek. fo-dicom bunun için yardımcı sunar; elle string birleştirme yapma.

## VNA ile ilişki

PACS, görüntüleri kendi disk alanında tutmaz. VNA'ya (Orthanc + MinIO)
DICOMweb STOW-RS ile yükler. PACS şemasında yalnızca METADATA (Study, Series,
Instance kimlikleri, modalite, tarih, hangi Encounter'a ait) tutulur.

Görüntünün kendisi VNA'dadır. Bkz. `vna-architecture` skill.

## Test verisi

Modalite başına 5-10 sentetik veya kamuya açık anonim örnek yeterlidir. Büyük
DICOM dosyaları git'e COMMIT EDİLMEZ; ayrı bir indirme script'i ile temin
edilir.
