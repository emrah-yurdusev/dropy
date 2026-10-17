# Müzik sentezleyici

Oyundaki müzikler (`Assets/_Dropy/Audio/Music`) bu betiklerle koddan üretildi; ses dosyası kaynağı yok.

- `synth.py`: enstrümanlar (FM elektrik piyano, keçe piyano, pad, marimba, bas, davullar, drone) ve yankı.
- `tracks.py`: 7 parçanın bestesi (Menu, Ofis_Sakin, Ofis_Gergin, Aksam, Final, Deprem, Hapishane).
- `analyze.py`: seviye, kırpılma ve döngü dikişi kontrolü.

Üretmek için numpy'li bir Python yeter (ör. Blender'ın kendi Python'u):

    python tracks.py out            # hepsi
    python tracks.py out Menu Aksam # seçilenler
    python analyze.py out/*.wav

WAV'ları OGG Vorbis'e çevirip `Assets/_Dropy/Audio/Music`'e koyun. Hangi anda hangi parçanın çaldığı
`Assets/_Dropy/Resources/MusicLibrary.asset` ve `MusicDirector` / `DeskManager` içinde.
Deprem ve Hapishane parçaları ara sahnelerin zamanlamasına göre yazıldı (çöküş ~4.1 sn, kapı ~3.0 sn).
