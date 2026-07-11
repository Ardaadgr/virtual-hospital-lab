---
name: vna-architecture
description: >
  VNA (Vendor Neutral Archive) mimarisi: radyoloji DICOM görüntüleri ve
  patoloji dijital slide'larının (WSI) tek birleşik arşivde saklanması,
  DICOMweb protokolü (STOW-RS/WADO-RS/QIDO-RS), MinIO nesne depolama ve
  birleşik viewer entegrasyonu. Görüntü arşivleme, sorgulama veya viewer
  ile ilgili her işte kullan.
---

# VNA (Vendor Neutral Archive)

## Temel fikir

"Vendor Neutral" ifadesi tam olarak şunu anlatır: modaliteden ve üreticiden
BAĞIMSIZ tek arşiv. Radyoloji görüntüsü de patoloji slide'ı da AYNI arşivde,
AYNI protokolle saklanır ve çekilir.

İki ayrı erişim yolu (biri radyoloji için, biri patoloji için) YAZILMAZ. Bu,
VNA fikrinin kendisini bozar.

## Bileşenler

```
Uygulama (.NET)
     |  DICOMweb (HTTP)
Orthanc (DICOM sunucusu, DICOMweb eklentisi)
     |  nesne depolama
MinIO (S3 uyumlu)
```

## Protokol: DICOMweb

- STOW-RS: yükleme. `POST /dicomweb/studies`
- WADO-RS: çekme. `GET /dicomweb/studies/{uid}/series/{uid}/instances/{uid}`
- QIDO-RS: sorgulama. `GET /dicomweb/studies?PatientID=...`

## Patoloji WSI

Patoloji dijital slide'ları DICOM VL Whole Slide Microscopy olarak saklanır.
SOP Class UID: `1.2.840.10008.5.1.4.1.1.77.1.6`

Bu, patoloji slide'ının DICOM ekosistemine girmesini sağlar. Böylece aynı
arşiv, aynı sorgu protokolü ve aynı viewer kullanılabilir.

## WSI'nin özel zorlukları

- Dosyalar ÇOK BÜYÜKTÜR (gigabaytlar). Tüm dosyayı belleğe alma.
- Piramidal/kademeli (tiled) yapıdadır. Viewer, yalnızca görünen bölgeyi ve
  yalnızca gereken zoom seviyesini çeker.
- Yüklemede multipart kullan.
- Slide'ın ETİKET GÖRÜNTÜSÜ (label image) üzerinde hasta adı BASILI olabilir.
  Dışa aktarımda label image ÇIKARILIR. Bu sık kaçırılan bir sızıntı yoludur.

## Birleşik viewer

React tabanlı tek viewer:
- Radyoloji (MR/CT/DX): Cornerstone3D
- Patoloji (WSI): OpenSeadragon veya Slim
- Ortak: hasta paneli, seri/vaka seçici, anotasyon katmanı

HBYS içinden Keycloak SSO token'ı ile açılır. Görüntü URL'lerinde hasta
kimliği veya kalıcı paylaşım linki BULUNMAZ; erişim her zaman token
doğrulamasından geçer.

## Görüntü silinmez

Yeniden taranan bir patoloji slide'ının eski görüntüsü SİLİNMEZ; `Superseded`
statüsüyle arşivde kalır. Bkz. ARCHITECTURE.md AD-010.

MinIO tarafında nesne sürümleme (versioning) AÇIK olmalıdır.
