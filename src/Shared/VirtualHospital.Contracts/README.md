# VirtualHospital.Contracts

Bounded context'ler arasındaki TEK iletişim yolu.

Kurallar:
- Buradaki mesajlar YALNIZCA primitive ve basit tip taşır.
- Domain entity'si, value object'i veya enum'ı BURAYA SIZMAZ. Alıcı context,
  göndericinin domain modelini bilmemelidir.
- Bir mesaj yayımlandıktan sonra kırıcı değişiklik yapılmaz; gerekiyorsa yeni
  bir sürüm (`...V2`) eklenir ve eskisi bir süre desteklenir.
- Her mesaj `CorrelationId` taşır; bir isteğin sistemler arası yolculuğu tek
  kimlikle izlenebilmelidir.

Bkz. `.claude/rules/integration-messaging.md`.
