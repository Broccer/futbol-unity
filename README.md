# Mahalle Futbolu (Unity)

Tarayıcıda oyna: https://broccer.github.io/futbol-unity/

Her `main` push'unda GitHub Actions oyunu WebGL olarak derler ve GitHub Pages'e koyar
(`.github/workflows/webgl.yml`, secret'lar: `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`).

## Modlar
- **Maç (2 oyuncu)** — aynı klavyede Mavi vs Kırmızı, 2/3/5 dakika, skor ve süre. Berabere biterse penaltılar.
- **Penaltı atışları (2 oyuncu)** — 5'er atış, eşitlikte ani ölüm. Sırayla atıcı / kaleci.
- **Antrenman (tek oyuncu)** — top sürme, şut, iki takım arkadaşıyla pas.

## Kontroller
| | Mavi | Kırmızı | Gamepad |
|---|---|---|---|
| Koş | WASD | Oklar | Sol çubuk |
| Sprint | Sol Shift | Sağ Shift | RT / RB |
| Şut (basılı tut → bırak) | Space | Enter | B |
| Kayma / plonjon | F | Sağ Ctrl | X |
| Pas (antrenman) | E | – | A |
| Menü | Esc | Esc | Start |

- **Şut gücü:** tuşu ne kadar uzun tutarsan o kadar sert ve yüksek. Başın üstünde güç çubuğu çıkar.
- **Şut yönü:** basılı tutarken yön tuşuyla nişan alınır; yerde ok çıkar. Kaleye yakın açılar hafif düzeltilir.
- **Penaltıda:** bırakırken basılı tuttuğun yön köşeyi seçer (yukarı = üst köşe, aşağı = yerden). %80'in üstü güç isabeti bozar.
- **Kalecilik:** kendi ceza sahanda plonjon tuşu yana atlatır; 22 m/s'den yavaş topu elle tutarsın, sertini gövdeyle çelersin.
- **Kayarak müdahale:** ceza sahası dışında aynı tuş; rakibin ayağındaki topu dürtüp alır. Rakibe yakın girmek de topu çalar.

## Dosyalar
- `Assets/Scripts/PlayerController.cs` — koşu, top sürme, şut/penaltı, plonjon, top çalma
- `Assets/Scripts/MatchManager.cs` — menü, maç süresi, skor, penaltı serisi, ekran yazıları
- `Assets/Scripts/PitchBuilder.cs` — mahalle sahasını, kaleleri, oyuncuları koddan kurar
- `Assets/Scripts/Ball.cs`, `GoalTrigger.cs`, `CameraFollow.cs`, `Teammate.cs`, `Core/InputReader.cs`
