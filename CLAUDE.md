# CLAUDE.md - Unity Endless Runner Journey

Bu dosya, yapay zekanın (Claude / Antigravity) projeyi her seferinde sıfırdan taramadan doğrudan hedef dosyalara ve nesnelere erişebilmesi, ayrıca Unity kodlama kurallarına ve proje kapsam sınırlarına tam uyması için hazırlanmıştır.

---

## 🛑 KODLAMA VE PROJE TARAMA SINIRLARI (SCOPING RULES)

> [!IMPORTANT]
> **HER KOMUTTA TÜM PROJEYİ TARAMAYIN!**
> Aşağıda listelenen dosya yolları sabittir. Kullanıcı bir script veya nesne ismi söylediğinde doğrudan tablodaki dosya yoluna gidin.

### 1. Taranmayacak / Değiştirilmeyecek Dizinler (Blacklist / Exclude)
Aşağıdaki klasörler harici kütüphane ve paketlerdir. Özel bir talep olmadıkça **asla aranmamalı, taranmamalı ve değiştirilmemelidir**:
- `Assets/Firebase/` & `Assets/ExternalDependencyManager/` *(Firebase ve bağımlılık motoru)*
- `Assets/Plugins/` *(Android / iOS yerel eklentileri)*
- `Assets/TextMesh Pro/` *(TMP font motoru ve demo scriptleri)*
- `Assets/TutorialInfo/` *(Unity varsayılan şablon dosyaları)*
- `Assets/ToonyTinyPeople/` *(Harici demo karakter varlıkları)*
- `Assets/AurynSky/` *(Sadece ortam modelleri/animasyonları içerir, kod aramasında taranmaz)*
- `Library/`, `Temp/`, `Logs/`, `obj/`, `Build/` *(Unity geçici sistem klasörleri)*
- `*.meta` dosyaları *(Unity tarafından otomatik üretilir, elle düzenlenmez veya silinmez)*

### 2. Aktif Geliştirme ve Çalışma Alanları (Whitelist)
Yeni kod veya değişiklikler yalnızca aşağıdaki klasörlerde yapılır:
- **Kodlar**: `Assets/Scripts/` ve `Assets/Hero/`
- **Prefabler**: `Assets/Prefabs/`
- **Sahneler**: `Assets/Scenes/`
- **Materyal & Doku**: `Assets/Materials/`, `Assets/Meshes/`, `Assets/Textures/`

### 3. Unity C# Kodlama Standartları
1. **Performans**:
   - `Update()` ve `LateUpdate()` içinde kesinlikle `GetComponent<T>()`, `GameObject.Find()`, `FindWithTag()` veya `new` nesne oluşturma yapmayın. Referansları `Awake()` veya `Start()` içinde önbelleğe alın (`caching`).
2. **Kapsülleme & Inspector**:
   - Inspector'dan atanacak değişkenler için `[SerializeField] private` kullanın (gerekmedikçe `public` yapmayın).
3. **Null Güvenliği**:
   - UI metinleri, Animator, CharacterController ve Firebase referanslarını kullanmadan önce `null` kontrollerini yapın.
4. **Zaman Yönetimi**:
   - Oyun duraklatma ve Game Over durumlarında `Time.timeScale` kontrolünü `HeroManager` ile senkronize tutun.
5. **İsimlendirme**:
   - Sınıf ve Metotlar: `PascalCase` (ör. `HeroCharacterController`, `SpawnBlock`)
   - Özel Değişkenler: `camelCase` (ör. `runSpeed`, `isGrounded`)
   - UI / Inspector Alanları: Amacı açık belirten isimler (ör. `scoreText`, `gameOverPanel`)

---

## ⚡ HIZLI BAŞVURU: KOD VE DOSYA YOLLARI KATALOĞU

### 1. C# Kodları (`Scripts`)
| İsim | Dosya Yolu | Açıklama & Görevi |
| :--- | :--- | :--- |
| **HeroCharacterController** | `Assets/Hero/HeroCharacterController.cs` | Karakter koşusu, zıplama mekaniği, yer/duvar algılama, yerçekimi, animator entegrasyonu. |
| **RandomTrailEmit** | `Assets/Hero/RandomTrailEmit.cs` | Kahramanın arkasında rastgele sürelerle iz/efekt (TrailRenderer) yayılımı. |
| **BlockManager** | `Assets/Scripts/BlockManager.cs` | Sonsuz koşu yol bloklarını (`BlockCrate`, `BlockDirt`, `BlockRock`) dinamik oluşturma/yönetme. |
| **HeroManager** | `Assets/Scripts/HeroManager.cs` | Game Over durumu, `Time.timeScale` durdurma ve Game Over panel gösterimi. |
| **CameraFollow** | `Assets/Scripts/CameraFollow.cs` | Kameranın karakteri yumuşak biçimde (`Lerp`) X/Z ekseninde takip etmesi. |
| **Score** | `Assets/Scripts/Score.cs` | Skor sayacı, zamana göre skor artışı ve UI `Text` elemanına yansıtma. |
| **ScoreElement** | `Assets/Scripts/ScoreElement.cs` | Skor tablosu satır bileşeni (Kullanıcı adı, kills, deaths, XP textleri). |
| **UIManager** | `Assets/Scripts/UIManager.cs` | Giriş menüsü ekran yönetimi (`LoginScreen`, `RegisterScreen`, `UserDataScreen`, `ScoreboardScreen`). |
| **AuthManager** | `Assets/Scripts/AuthManager.cs` | Firebase Authentication entegrasyonu (Kullanıcı kaydı, şifre teyidi, giriş, asenkron bildirimler). |
| **Events** | `Assets/Scripts/Events.cs` | Sahne buton olayları (`ReplayGame`, `QuitGame`). |
| **UnityAds** | `Assets/Scripts/UnityAds.cs` | Unity Reklam servisleri (Geçiş reklamı, Ödüllü video, Banner reklamları ve callback'ler). |

---

### 2. Prefabler (`Prefabs`)
| Obje / Prefab İsmi | Dosya Yolu | Açıklama |
| :--- | :--- | :--- |
| **Hero** | `Assets/Prefabs/Hero.prefab` | Ana oyuncu karakter prefabı (Mesh, CharacterController, Animator, Ses bileşenleri yüklü). |
| **BlockCrate** | `Assets/Prefabs/BlockCrate.prefab` | Sandık/kutu engelli yol bloğu prefabı. |
| **BlockDirt** | `Assets/Prefabs/BlockDirt.prefab` | Toprak zeminli yol bloğu prefabı. |
| **BlockRock** | `Assets/Prefabs/BlockRock.prefab` | Kaya engelli yol bloğu prefabı. |

#### UI Prefableri (`Assets/Clean Settings UI/Prefabs/`)
- `Bar.prefab`
- `Description text - Dark.prefab` / `Description text - Light.prefab`
- `Header - Dark.prefab` / `Header - Light.prefab`
- `InputField.prefab`
- `RoundButton.prefab`
- `SquareButton.prefab`
- `Scrollbar.prefab`
- `Slider.prefab`
- `Toggle.prefab`

---

### 3. Animatör Kontrolcüleri (`Animator Controllers` - `.controller`)
| Kontrolcü İsmi | Dosya Yolu |
| :--- | :--- |
| **HeroAnimatorController** | `Assets/Hero/HeroAnimatorController.controller` |
| **InputField** | `Assets/Clean Settings UI/Animations/InputField.controller` |
| **RoundButton** | `Assets/Clean Settings UI/Animations/RoundButton.controller` |
| **SquareButton** | `Assets/Clean Settings UI/Animations/SquareButton.controller` |
| **Slider** | `Assets/Clean Settings UI/Animations/Slider.controller` |
| **Toggle** | `Assets/Clean Settings UI/Animations/Toggle.controller` |
| **DesertBanner** | `Assets/AurynSky/Desert Pack/Animation/DesertBanner.controller` |
| **DesertChest** | `Assets/AurynSky/Desert Pack/Animation/DesertChest.controller` |
| **DesertFlag** | `Assets/AurynSky/Desert Pack/Animation/DesertFlag.controller` |
| **BannerDAnim** | `Assets/AurynSky/Dungeon Pack/Animation/BannerDAnim.controller` |
| **ChestD** | `Assets/AurynSky/Dungeon Pack/Animation/ChestD.controller` |
| **SpikeTrapD** | `Assets/AurynSky/Dungeon Pack/Animation/SpikeTrapD.controller` |
| **SwingTrapD** | `Assets/AurynSky/Dungeon Pack/Animation/SwingTrapD.controller` |
| **TrapDoorD** | `Assets/AurynSky/Dungeon Pack/Animation/TrapDoorD.controller` |
| **Banner** | `Assets/AurynSky/Forest Pack/Animation/Banner.controller` |
| **Flag** | `Assets/AurynSky/Forest Pack/Animation/Flag.controller` |
| **ForestChestTop** | `Assets/AurynSky/Forest Pack/Animation/ForestChestTop.controller` |
| **WinterBannerAnim** | `Assets/AurynSky/WinterArena/Animations/WinterBannerAnim.controller` |
| **WinterChest** | `Assets/AurynSky/WinterArena/Animations/WinterChest.controller` |
| **WinterFlagAnim** | `Assets/AurynSky/WinterArena/Animations/WinterFlagAnim.controller` |

---

### 4. Animasyon Dosyaları (`.anim`)
| Animasyon İsmi | Dosya Yolu |
| :--- | :--- |
| **Chest** | `Assets/AurynSky/Desert Pack/Animation/Chest.anim` |
| **ChestAnimD** | `Assets/AurynSky/Dungeon Pack/Animation/ChestAnimD.anim` |
| **SpikeTrapAnimD** | `Assets/AurynSky/Dungeon Pack/Animation/SpikeTrapAnimD.anim` |
| **SwingTrapAnimD** | `Assets/AurynSky/Dungeon Pack/Animation/SwingTrapAnimD.anim` |
| **TrapDoorAnimD** | `Assets/AurynSky/Dungeon Pack/Animation/TrapDoorAnimD.anim` |
| **ChestOpen** | `Assets/AurynSky/Forest Pack/Animation/ChestOpen.anim` |
| **WinterChestOpen** | `Assets/AurynSky/WinterArena/Animations/WinterChestOpen.anim` |

---

### 5. Sahneler (`Scenes` - `.unity`)
| Sahne İsmi | Dosya Yolu | Açıklama |
| :--- | :--- | :--- |
| **SampleScene** | `Assets/Scenes/SampleScene.unity` | Ana sonsuz koşu oyun sahnesi (`Hero`, `BlockManager`, `CameraFollow`, Oyun döngüsü). |
| **ScoreMenuScene** | `Assets/Scenes/ScoreMenuScene.unity` | Giriş, kayıt ve çevrimiçi skor tablosu menü sahnesi (`AuthManager`, `UIManager`). |
| **Clean Settings UI** | `Assets/Clean Settings UI/Clean Settings UI.unity` | Ayarlar ve ses/grafik seçenekleri demo arayüz sahnesi. |

---

### 6. Görsel & İşitsel Varlıklar (`Meshes`, `Audio`, `Materials`, `Textures`)
| Varlık İsmi | Dosya Yolu | Tür |
| :--- | :--- | :--- |
| **HeroMesh** | `Assets/Hero/HeroMesh.fbx` | 3D Karakter Modeli |
| **HeroJump** | `Assets/Hero/HeroJump.wav` | Karakter Zıplama Ses Efekti |
| **Environment** | `Assets/Meshes/Environment.fbx` | Çevre / Parkur 3D Modelleri |
| **Common** | `Assets/Materials/Common.mat` | Çevre / Bloklar Ana Materyali |
| **Skybox_Mat** | `Assets/Materials/Skybox_Mat.mat` | Gökyüzü / Skybox Materyali |
| **ImphenziaPalette** | `Assets/Textures/ImphenziaPalette01-256-Gradient.png` | Ortak Doku / Renk Paleti Texture'ı |

---

## 💡 İPUÇLARI VE PRATİK ÇALIŞMA YÖNERGESİ
- **"Hero scriptinde değişiklik yap"** denildiğinde -> Doğrudan `Assets/Hero/HeroCharacterController.cs` açılır.
- **"Blok oluşturmayı düzenle"** denildiğinde -> Doğrudan `Assets/Scripts/BlockManager.cs` açılır.
- **"Scoreboard veya Firebase değiştir"** denildiğinde -> Doğrudan `Assets/Scripts/AuthManager.cs` veya `Assets/Scripts/ScoreElement.cs` açılır.
- **"Karakter prefabı nerede"** denildiğinde -> Doğrudan `Assets/Prefabs/Hero.prefab` referans verilir.
- Arama (`grep_search` veya `run_command` Get-ChildItem) çalıştırmadan önce mutlaka bu dosyadaki yollar kontrol edilmelidir.
