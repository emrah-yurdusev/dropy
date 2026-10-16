# 🏗️ PROJE ADI: VİZE BÜROSU (Çalışma Başlığı)

**Etkinlik:** KTMMOB İnşaat Mühendisleri Odası Game Jam  
**Tema:** Afete Hazırlık ve Farkındalık (*"Hazırlıklı Ol, Farkında Ol, Hayat Kurtar"*)  
**Tür:** 2D Simülasyon, Kriz Yönetimi, Ahlaki İkilem, Bulmaca  
**Referans Oyunlar:** *Papers, Please* (Temel Döngü), *Poly Bridge* (Yapısal Test/Müdahale)

---

## 🎯 1. Yüksek Konsept (Elevator Pitch)
Oyuncu, bir Yapı Denetim / Proje Vize Bürosu'nda çalışan inşaat mühendisidir. Gündüzleri masasına gelen bina projelerindeki ölümcül mühendislik hatalarını tespit edip düzelterek şehri depreme hazırlamaya çalışır; akşamları ise kazandığı kısıtlı parayla **kendi ailesinin yaşadığı eski apartmanı** yaklaşan büyük depreme karşı güçlendirir. 

**Temel İkilem:** Çürük bir projeye rüşvetle onay verip kendi aileni ve evini mi kurtaracaksın, yoksa mühendislik etiğinden ödün vermeyip hem kendi evini hem de bütün şehri riske mi atacaksın?

---

## 🧠 2. Temaya Uygunluk ("Hazırlıklı Ol, Farkında Ol, Hayat Kurtar")
* **FARKINDA OL:** Projedeki ölümcül hataları (yumuşak kat, sıvılaşan zemin, korozyon, kesilmiş kolon) önemsiz kusurlardan (balkon boyutu, boya rengi) ayırt et.
* **HAZIRLIKLI OL:** Kırmızı kalemle projelere müdahale ederek binaları güçlendir. Kazandığın parayla kendi evine deprem çantası al, eşyaları sabitle ve binanı güçlendir.
* **HAYAT KURTAR:** 5. günün sonunda gerçekleşen büyük depremde, doğru denetlediğin binaların ayakta kalmasıyla yüzlerce insanın (ve kendi ailenin) hayatını kurtar.

---

## ⚙️ 3. Temel Oynanış Döngüsü (Core Loop)

Oyun **5 Gün** sürer ve her gün **Gündüz (Ofis)** ve **Akşam (Ev & Market)** olmak üzere iki aşamadan oluşur:

### A. Gündüz Aşaması: Vize Bürosu (Masa Başı)
Müteahhitler sırayla masaya bina projelerini getirir. Oyuncunun önünde 3 temel araç vardır:
1. **Proje Paftası:** Binanın 2D kesiti, kat sayısı, beton sınıfı ve zemin bilgisi.
2. **Sarsıntı Simülatörü (Monitör):** Projeyi simülatöre sürükleyip "Test Et"e basınca bina sallanır; zayıf kolonlar ve kirişler kırmızıya dönerek çatlar.
3. **Mühendislik Ekipmanları:** Zamanla satın alınan *Zemin Radarı* (fay/sıvılaşma gösterir) ve *Beton Test Çekici* (malzeme kalitesini gösterir).

#### Karar Mekanizması (4 Seçenek):
| Karar | Kazanç / Maliyet | Sonuç |
| :--- | :--- | :--- |
| **1. DOĞRUDAN ONAY** | +1.000 TL (Vize Harcı) | Bina sağlamsa sorun yok. Hatalıysa depremde yıkılır. |
| **2. RÜŞVETLE ONAY** | +5.000 TL (Kara Para) | Müteahhit hatalı binayı bilerek geçirmen için teklif eder. Cebin dolar ama şehirde yıkım riski artar. |
| **3. KIRMIZI KALEM (Revizyon)** | +500 TL / **-Zaman Harcar** | **Özgün Mekanik:** Oyuncu hatalı kolona tıklayıp *"Perde Beton Ekle"*, *"Temeli Derinleştir"* veya *"Sismik İzolatör Koy"* der. Bina kurtulur ama gün içinde bakabileceğin müşteri sayısı azalır. |
| **4. RED** | 0 TL | Çok ağır kusurlu projeler reddedilir. Para kazanamazsın, çok reddedersen büro iflas eder. |

---

### B. Akşam Aşaması: Ev ve Harcamalar (Para Neden Lazım?)
Mesai bittiğinde oyuncu kazandığı parayı 3 kritik yere harcamak zorundadır:
1. **Büro Giderleri & Ekipman:** Büro kirası (zorunlu) ve ileriki günlerdeki gizli bina hatalarını görmeyi sağlayan yeni mühendislik cihazları.
2. **Kendi Evini Hazırlama (Bireysel Farkındalık):**
   * *Deprem Çantası Hazırlama (500 TL)*
   * *Evdeki Dolapları ve Beyaz Eşyaları Sabitleme (1.000 TL)*
   * *Oturduğun Apartmana Karbon Fiber Güçlendirme Yaptırma (10.000 TL)*

---

## 🏛️ 4. Jüriyi (İMO) Vuracak Mühendislik Detayları
Oyunda kullanılacak gerçek inşaat mühendisliği terimleri (Kıbrıs ve Türkiye gerçeklerine uygun):
* **Yumuşak Kat (Soft Story):** Zemin katı yüksek tavanlı market/galeri olan, üst katları normal daire olan binalar (Depremde ilk yıkılanlar).
* **Zemin Sıvılaşması:** Alüvyonlu/kumlu zemine kazık temelsiz yüksek bina yapılması.
* **Korozyon & Deniz Kumu:** Maliyeti düşürmek için yıkanmamış deniz kumu ve düşük sınıf beton (Örn: C30 yerine C16) kullanılması.
* **Kısa Kolon Etkisi & Kesilen Kolonlar:** Alan açmak için zemin katta kolon kesilmesi.
* **Gri Alanlar (Tuzak Hatalar):** Deprem güvenliğini etkilemeyen ufak mimari kusurlar (Örn: "Balkon 15 cm geniş olmuş"). Oyuncu para kazanmak için bunlara inisiyatif alıp onay vermelidir.

---

## 🎬 5. Oyunun Finali: "7.2 Büyüklüğünde Gerçek"
5. günün sonunda deprem gerçekleşir. Ekrana iki aşamalı karne gelir:
1. **Evim:** Evde eşyaları sabitledin mi? Çantan var mıydı? Binanı güçlendirdin mi? (Ailenin hayatta kalma durumu).
2. **Şehir Panoraması:** 5 gün boyunca masandan geçen binalar sırayla ekrana gelir ve sallanır. Kırmızı kalemle düzelttiğin *Öğrenci Yurdu* ayakta kalırken, rüşvetle geçirdiğin *Lüks Rezidans* yıkılır.
3. **Gerçek Bilgi Ekranı:** Oyun sonunda AFAD ve İMO'nun gerçek hayattaki altın kuralları ekranda beliriş yapar.

---