# Futbol prototipi (Unity)

Hedef: bir futbolcu koşsun, topu sürsün, pas versin, kaleye şut çeksin.

## Açma
1. Unity Hub → **Add → Add project from disk** → bu klasör (`futbol_unity`).
2. Unity 6 (6000.0 LTS) önerilir; farklı bir sürüm seçilirse Hub yükseltmeyi sorar, kabul et.
3. İlk açılışta `Assets/Scenes/Saha.unity` kendiliğinden oluşur ve açılır
   (olmazsa menü: **Futbol → Saha sahnesini oluştur**).
4. **Play**. Saha, kaleler, top, futbolcu ve iki takım arkadaşı koddan kurulur.
   Herhangi boş bir sahnede Play'e basmak da yeterli.
5. "Input System etkinleştirilsin mi / yeniden başlat" sorusu gelirse **Yes** de; kod iki sistemle de çalışır.

## Kontroller
| Tuş | Gamepad | İş |
|---|---|---|
| WASD / Oklar | Sol çubuk | Koş, yön değiştir |
| Shift | RT / RB | Sprint (top biraz daha önde) |
| Space basılı tut → bırak | B | Şut (ne kadar uzun, o kadar sert ve yüksek) |
| E | A | Baktığın yöndeki arkadaşa pas |
| R | Y | Pozisyonları sıfırla |

## Dosyalar
- `Assets/Scripts/PlayerController.cs` — koşu, dönüş, top sürme, şut, pas (ayarlar Inspector'da)
- `Assets/Scripts/Ball.cs` — top fiziği, yuvarlanma direnci, kim kontrol ediyor
- `Assets/Scripts/Teammate.cs` — pası alıp geri veren basit takım arkadaşı
- `Assets/Scripts/GoalTrigger.cs`, `MatchManager.cs` — gol, skor, ekran yazıları
- `Assets/Scripts/PitchBuilder.cs` — sahneyi koddan kurar
- `Assets/Scripts/CameraFollow.cs` — takip kamerası
