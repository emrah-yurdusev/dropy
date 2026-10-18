using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Dropy.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Dropy.Gameplay
{
    /// <summary>
    /// İskân bürosu masa akışı: 6 gün, günde 3 dosya. Müteahhit kapıdan girer, karşıya oturur, konuşur;
    /// oyuncu yönetmeliğe, künyeye ve ek rapora bakıp künyeye mühür basar (onay: aileler taşınır, red: bina boş kalır).
    /// Akşam kira düşer, telefondan alışveriş yapılır. 4. günün son dosyasından sonra deprem olur.
    /// Arayüzü (Canvas, EventSystem) kendisi kurar; sahnede yalnızca bu bileşen ve referanslar gerekir.
    /// </summary>
    public class DeskManager : MonoBehaviour
    {
        public class CaseResult
        {
            public BuildingCase buildingCase;
            public int day;
            public Seal seal;
            public int money;
            public BuildingFate fate;
            public string warning;
            /// <summary>Mühürden sonra müteahhidin söylediği cümle (gazete onu alıntılar).</summary>
            public string spokenLine;
            /// <summary>Onayın karşılığında harç dışında para alındı (gri rüşvet ya da Ozan Bey'in teklifi).</summary>
            public bool bribed;
        }

        /// <summary>Müteahhitler hızlı yürür (1. gün 1.8, 2. günden itibaren 1.95 kat); animasyon hıza kendisi uyar.</summary>
        const int FastWalkDay = 2;
        const float FirstDayWalkMultiplier = 1.8f;
        const float FastWalkMultiplier = 1.95f;
        /// <summary>Oyunun ilk müteahhidi (atlanamayan giriş) bunun katı daha hızlı yürür: ofis görünsün ama beklenmesin.</summary>
        const float FirstVisitorWalkBoost = 1.5f;
        /// <summary>Kapıya yürürken söylenen tepki balonu en az/en çok bu kadar durur (okuma hızı kelime/saniye).</summary>
        const float LeavingWordsPerSecond = 3.5f, MinLeavingHold = 1.5f, MaxLeavingHold = 4f;
        /// <summary>Gelişi/gidişi geçerken kararma süreleri (saniye).</summary>
        const float SkipFadeOut = 0.15f;
        const float SkipFadeIn = 0.25f;
        const string EndQuestions = "Tamam, dosyaya bakayım.";
        const string KeepReject = "Kararım belli, red.";
        const string Reconsider = "Bir daha bakayım.";
        /// <summary>Aile bağı bu değerin altındaysa Ozan Bey aileyi koz olarak kullanır.</summary>
        const int LowBond = 2;
        /// <summary>Deprem sahnesinde en fazla bu kadar bina manşeti gösterilir; fazlası "ve N bina daha" olur.</summary>
        const int MaxQuakeHeadlines = 3;
        /// <summary>İlk depremde eşya sabitlenmemişse aile bağından giden dilim.</summary>
        const int FixingQuakeBondLoss = 2;
        /// <summary>İlk depremde deprem çantası yoksa (karanlıkta fener bile yok) aile bağından giden dilim.</summary>
        const int BagQuakeBondLoss = 1;
        const string TakeBribe = "Onaylarsam konuşuruz.";
        const string RefuseBribe = "Ben harç dışında para almam.";
        const string Landlord = "Ev sahibi";

        [Header("Sahne")]
        [Tooltip("Oyuncunun gözü: karşıdaki sandalyeye bakan sabit kamera")]
        [SerializeField] Camera deskCamera;
        [SerializeField] Chair visitorChair;
        [SerializeField] Transform spawnPoint;
        [SerializeField] Transform exitPoint;
        [Tooltip("Varsa müteahhit bu kapıyı açıp girer/çıkar")]
        [SerializeField] Door door;
        [Tooltip("Müdür arayınca konuşma balonu bunun üstünde çıkar. Boşsa sahnede \"Telefon\" adlı nesne aranır")]
        [SerializeField] Transform deskPhone;

        [Header("İçerik")]
        [SerializeField] RegulationData regulations;
        [Tooltip("Açılış telefonu, sabah haberleri, akşam mesajları, sarsıntılar")]
        [SerializeField] StoryData story;
        [Tooltip("Sabah televizyonu ve final gazetesinin görünüşü ve sabit yazıları. Boşsa varsayılanlar kullanılır")]
        [SerializeField] NewsStyle newsStyle;
        [Tooltip("Mühür eldeyken imleç (Texture Type: Cursor). Boş olan yerine koddan çizilen damga gelir")]
        [SerializeField] StampCursorSet stampCursors = new StampCursorSet();
        [Tooltip("Kırmızı kalemle öğretici: hangi gün, hangi dosyada neyin etrafına çizilir. Boşsa öğretici yok")]
        [SerializeField] TutorialData tutorial;
        [Tooltip("Sırayla 18 dosya: her gün 3 tane")]
        [SerializeField] BuildingCase[] cases;
        [Tooltip("Vakada karakter seçilmemişse sırayla bunlar kullanılır")]
        [SerializeField] GameObject[] defaultContractors;
        [Tooltip("Test için: doluysa ilk gelen müteahhit vakadan bağımsız olarak bu karakter olur")]
        [SerializeField] GameObject testFirstContractor;

        DialogueUI dialogue;
        InspectionPanel inspection;
        DeskHud hud;
        PenTutorial pen;
        EveningPanel evening;
        NpcController visitor;
        Transform visitorHead;
        BuildingCase current;
        bool submitted;
        bool inspectAllowed;
        bool shearWallUsedToday;
        int day;
        int money;
        readonly List<CaseResult> results = new List<CaseResult>();
        readonly HashSet<ShopItem> purchased = new HashSet<ShopItem>();
        /// <summary>Hangi eşya hangi akşam alındı: gazete "ilk depremde vardı mı" diye buna bakar.</summary>
        readonly Dictionary<ShopItem, int> boughtOn = new Dictionary<ShopItem, int>();
        /// <summary>Masadaki müteahhidin gri rüşvet teklifi kabul edildi (onay basılırsa harca eklenir).</summary>
        bool bribeAccepted;
        readonly HashSet<FamilyNeed> paidNeeds = new HashSet<FamilyNeed>();
        /// <summary>Aile bağı. Sıfıra düşerse kalıcıdır: aile küser, aileden mesaj gelmez.</summary>
        int bond;
        /// <summary>Akşam defterinde aile bağı için gösterilecek not (dün ödenmeyen istek, depremde devrilen dolap...).</summary>
        string bondNote;
        /// <summary>Son depremden sonra hapse girildiyse oyun biter.</summary>
        bool jailed;
        /// <summary>Kusursuz kayıt primi son akşam kasaya girdi (banka ve müdür mesajları bunu bekler).</summary>
        bool bonusPaid;
        int hammersUsedToday;
        bool paused;
        /// <summary>Deprem sahnesi oynarken büronun arayüzü gizli; Esc/Tab çalışmasın.</summary>
        bool cinematicPlaying;
        /// <summary>Haftayı yürüten ana akış; geliştirici paneli atlarken durdurup başka noktadan yeniden başlatır.</summary>
        Coroutine week;
        /// <summary>Akşam paneli açık; bu akşamın aile istekleri sabah karşılanmamışsa bağdan düşer.</summary>
        bool inEvening;
        readonly List<FamilyNeed> eveningRequests = new List<FamilyNeed>();
        /// <summary>İnceleme sırasında oyuncunun sorduğu satıra müteahhidin cevabı (bir sonraki soru öncekini keser).</summary>
        Coroutine fieldTalk;
        /// <summary>Sarsıntıdan sonra radyoda adı geçmiş çürük binalar (aynı haber iki kez okunmasın).</summary>
        readonly HashSet<BuildingCase> crackReported = new HashSet<BuildingCase>();

        bool Estranged => bond <= 0;

        public IReadOnlyList<CaseResult> Results => results;
        public int Money => money;
        public int Day => day;

        void Awake()
        {
            // Editörde Play'den duraklatılmış çıkılırsa zaman ölçeği 0'da kalabiliyor; her açılışta sıfırla.
            Time.timeScale = 1f;
            BuildUi();
            money = Economy.StartMoney;
            hud.SetMoney(money);
            bond = story != null ? story.bondStart : 3;
        }

        void OnDestroy()
        {
            Time.timeScale = 1f;
            stampCursors.Apply(Seal.None);
        }

        void Start() => week = StartCoroutine(RunWeek(0, eveningFirst: false));

        void BuildUi()
        {
            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
                es.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }

            var canvasGo = new GameObject("DeskCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var root = (RectTransform)canvasGo.transform;

            inspection = canvasGo.AddComponent<InspectionPanel>();
            inspection.Build(root, regulations, deskCamera);
            inspection.SubmitRequested += () => submitted = true;
            inspection.StampFilter = CanStamp;
            inspection.HammerRequested = UseHammer;
            inspection.FieldAsked += OnFieldAsked;
            inspection.Tools.ActiveChanged += seal => stampCursors.Apply(paused ? Seal.None : seal);

            dialogue = canvasGo.AddComponent<DialogueUI>();
            dialogue.Build(root, deskCamera);

            hud = canvasGo.AddComponent<DeskHud>();
            hud.Build(root, newsStyle != null ? newsStyle : NewsStyle.Default);
            hud.BuildTracker(root,
                d => story != null && story.News(d) != null ? story.News(d).forecast : null,
                Economy.IsQuakeDay,
                d => story != null && story.Tremor(d) > 0f);
            hud.InspectClicked += ToggleInspection;
            hud.ResumeClicked += () => SetPaused(false);
            hud.RestartClicked += Restart;

            evening = canvasGo.AddComponent<EveningPanel>();
            evening.Build(root, (newsStyle != null ? newsStyle : NewsStyle.Default).familyPhoto);
            evening.BuyClicked += Buy;

            pen = canvasGo.AddComponent<PenTutorial>();
            pen.Build(root, tutorial, ResolveTarget, TutorialObstacles);
            evening.PayClicked += Pay;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || cinematicPlaying)
                return;
            // Esc: önce eldeki mührü bırakır, elde mühür yoksa duraklatma menüsünü açar/kapar.
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (!paused && inspection.Tools.Active != Seal.None)
                    inspection.Tools.Select(Seal.None);
                else if (paused || !hud.BlocksInput)
                    SetPaused(!paused);
            }
            if (keyboard.tabKey.wasPressedThisFrame && !paused)
                ToggleInspection();
        }

        void SetPaused(bool value)
        {
            paused = value;
            stampCursors.Apply(value ? Seal.None : inspection.Tools.Active);
            Time.timeScale = value ? 0f : 1f;
            AudioListener.pause = value;
            hud.SetPaused(value);
        }

        void Restart()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Sfx.Ambience(0f);
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        void ToggleInspection()
        {
            if (!inspectAllowed || paused)
                return;
            inspection.Toggle();
            hud.SetInspectOpen(inspection.IsOpen);
        }

        /// <param name="startIndex">Kaçıncı dosyadan başlanacağı (0 = oyunun başı, başlık ekranıyla).</param>
        /// <param name="eveningFirst">Önceki günün akşamı henüz yaşanmadıysa önce o akşam açılır.</param>
        IEnumerator RunWeek(int startIndex, bool eveningFirst)
        {
            int expected = Economy.Days * Economy.FilesPerDay;
            if (cases.Length != expected)
                Debug.LogWarning($"DeskManager: {expected} dosya bekleniyordu, {cases.Length} var. Dropy > Dosyaları Üret ile kur.");

            if (startIndex == 0)
            {
                MusicDirector.Play(MusicCue.OfficeCalm);
                yield return hud.Briefing();
            }

            int startDay = Economy.Day(startIndex);
            if (eveningFirst && startDay > 1)
            {
                day = startDay - 1;
                if (Economy.IsQuakeDay(day))
                {
                    yield return Earthquake(day);
                    if (jailed)
                        yield break;
                }
                yield return Evening(IncomeOf(day));
            }

            for (day = startDay; day <= Economy.Days; day++)
            {
                int firstFile = day == startDay ? startIndex % Economy.FilesPerDay : 0;
                inspection.SetDay(day);
                Sfx.Ambience(0.03f + 0.035f * day);
                MusicDirector.Play(OfficeMusic(day));
                if (firstFile == 0)
                {
                    shearWallUsedToday = false;
                    hammersUsedToday = 0;
                    yield return hud.Morning(Broadcast(day));
                    yield return MorningCalls();
                }

                for (int k = firstFile; k < Economy.FilesPerDay; k++)
                {
                    int index = (day - 1) * Economy.FilesPerDay + k;
                    if (index >= cases.Length)
                        break;
                    current = cases[index];
                    hud.SetCounter(day, k + 1, Economy.FilesPerDay, current.contractorName);
                    yield return RunCase(current, index);
                }

                if (Economy.IsQuakeDay(day))
                {
                    yield return Earthquake(day);
                    if (jailed)
                        yield break;
                }
                if (day < Economy.Days)
                    yield return Evening(IncomeOf(day));
            }
            yield return Finale();
        }

        int IncomeOf(int forDay) => results.Where(r => r.day == forDay).Sum(r => r.money);

        /// <summary>Deprem günleri ve sondan bir önceki gün gergin parça, diğer günler sakin parça.</summary>
        static MusicCue OfficeMusic(int forDay) =>
            Economy.IsQuakeDay(forDay) || forDay == Economy.Days - 1 ? MusicCue.OfficeTense : MusicCue.OfficeCalm;

        /// <summary>
        /// 6. günün sonu: gazete ve karne. Deprem normalde haftanın akışında oynar; geliştirici paneli doğrudan
        /// sona atlarsa <paramref name="quakeFirst"/> ile önce 6. gün depremi (ve gerekirse hapis) oynatılır.
        /// </summary>
        IEnumerator Finale(bool quakeFirst = false)
        {
            day = Economy.Days;
            if (quakeFirst)
            {
                yield return Earthquake(day);
                if (jailed)
                    yield break;
            }
            Sfx.Ambience(0f);
            MusicDirector.Play(MusicCue.Final, 2f);
            var family = story != null
                ? story.needs.Select(n => (n, paidNeeds.Contains(n)))
                : Enumerable.Empty<(FamilyNeed, bool)>();
            hud.ShowFinal(results, boughtOn, family, Estranged, money, DeskHud.ReportCard(results), HomeOutcome,
                bonusPaid ? PerfectBonus : 0);
        }

        /// <summary>
        /// Sabah haberleri: günün manşeti ve kayan yazısı hikâyeden gelir. Dünkü sarsıntıda çatlayan bina varsa
        /// SON DAKİKA o olur, günün manşeti kayan yazıya iner. Yönetmeliğe giren madde ve harç notu televizyonun
        /// altında sabit durur (kayan yazıda kaçırılmasın).
        /// </summary>
        TvBroadcast Broadcast(int forDay)
        {
            var broadcast = new TvBroadcast { day = forDay };
            var news = story != null ? story.News(forDay) : null;
            string headline = news != null ? news.headline : story != null ? story.Radio(forDay) : null;
            var cracks = CrackNews(forDay);
            if (cracks.Count > 0)
            {
                broadcast.headline = cracks[0];
                broadcast.ticker.AddRange(cracks.Skip(1));
                if (!string.IsNullOrEmpty(headline))
                    broadcast.ticker.Add(headline);
            }
            else
                broadcast.headline = headline;
            if (news != null)
            {
                broadcast.ticker.AddRange(news.ticker.Where(t => !string.IsNullOrEmpty(t)));
                broadcast.graphic = news.graphic;
                broadcast.graphicLabel = news.graphicLabel;
                broadcast.clip = news.clip;
            }

            string rules = "Yönetmelikte yeni madde yok.";
            if (regulations != null)
            {
                var titles = regulations.pages.Where(p => p.day == forDay).Select(p => p.title).ToList();
                if (titles.Count > 0)
                    rules = "Yönetmeliğe yeni madde: <b>" + string.Join(", ", titles) + "</b>";
            }
            broadcast.notes.Add(rules);
            broadcast.ticker.Add(rules.Replace("<b>", "").Replace("</b>", ""));
            if (forDay > Economy.LastPaidDay)
                broadcast.notes.Add("<b>Bugün harç yok: mühürler para getirmez, ama aileler yine taşınır.</b>");
            return broadcast;
        }

        /// <summary>
        /// Dünkü sarsıntıda çatlayan binalar: yalnız oyuncunun hatalı onayladığı (depremde çökecek) binalar.
        /// Oyuncu hatasını depremden önce sezsin diye en fazla iki haber.
        /// </summary>
        List<string> CrackNews(int forDay)
        {
            if (story == null || story.Tremor(forDay - 1) <= 0f)
                return new List<string>();
            var news = results
                .Where(r => r.fate == BuildingFate.Collapsed && r.day < forDay && !crackReported.Contains(r.buildingCase))
                .OrderByDescending(r => r.day)
                .Take(2)
                .ToList();
            foreach (var r in news)
                crackReported.Add(r.buildingCase);
            return news.Select(r => $"Dünkü sarsıntıda {r.buildingCase.buildingName} binasında derin çatlaklar görüldü").ToList();
        }

        /// <summary>Çekiç günde sınırlı: hak varsa birini düşer.</summary>
        bool UseHammer()
        {
            if (hammersUsedToday >= Economy.HammerPerDay)
                return false;
            hammersUsedToday++;
            return true;
        }

        /// <summary>Günün ilk müteahhidinden önce gelen telefonlar (1. gün müdürün tanıtımı, 5. gün harç yok).</summary>
        IEnumerator MorningCalls()
        {
            if (story == null)
                yield break;
            if (deskPhone == null)
                deskPhone = GameObject.Find("Telefon")?.transform;
            foreach (var call in story.calls.Where(c => c.day == day))
            {
                Sfx.Buzz();
                yield return new WaitForSeconds(0.8f);
                foreach (var line in call.lines)
                    yield return dialogue.Say(call.caller, line, deskPhone, waitForClick: true);
                dialogue.HideBubble();
            }
        }

        /// <summary>
        /// Vakada karakter seçilmemişse, müteahhidin cinsiyetini tutan varsayılanlardan sırayla biri gelir.
        /// Aynı cinsten önceki vakalar sayılır ki art arda gelenler farklı kişiler olsun.
        /// </summary>
        GameObject PickContractor(BuildingCase buildingCase, int index)
        {
            var gender = buildingCase.ContractorGender;
            var matching = defaultContractors
                .Where(p => p != null && p.TryGetComponent(out NpcController npc) && npc.Gender == gender)
                .ToArray();
            if (matching.Length == 0)
                return defaultContractors[index % defaultContractors.Length];
            int order = 0;
            for (int i = 0; i < index; i++)
                if (cases[i].contractorPrefab == null && cases[i].ContractorGender == gender)
                    order++;
            return matching[order % matching.Length];
        }

        IEnumerator RunCase(BuildingCase buildingCase, int index)
        {
            submitted = false;
            bribeAccepted = false;

            var profile = buildingCase.contractor;
            var prefab = index == 0 && testFirstContractor != null ? testFirstContractor
                : buildingCase.contractorPrefab != null ? buildingCase.contractorPrefab
                : profile != null && profile.prefab != null ? profile.prefab
                : PickContractor(buildingCase, index);
            dialogue.SpeedMultiplier = Voice.TypingSpeed(buildingCase);
            var go = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
            visitor = go.GetComponent<NpcController>();
            visitorHead = FindDeep(go.transform, "Head") ?? HumanBone(go, HumanBodyBones.Head) ?? go.transform;
            visitor.SetWalkSpeedMultiplier((day >= FastWalkDay ? FastWalkMultiplier : FirstDayWalkMultiplier) * (index == 0 ? FirstVisitorWalkBoost : 1f));
            // Oyunun ilk müteahhidinde (Veysel Bey) geçiş yok: oyuncu ofisi, kapıyı ve oturuşu bir kez görsün.
            bool skippable = index > 0;
            if (!go.TryGetComponent<SeatedLife>(out var life))
                life = go.AddComponent<SeatedLife>();
            life.lookTarget = deskCamera != null ? deskCamera.transform : null;

            yield return Skippable(Enter(), skippable, () =>
            {
                if (door != null)
                    door.SnapClosed();
                visitor.SnapSeat(visitorChair);
            });

            float tremor = story != null ? story.Tremor(day) : 0f;
            if (tremor > 0f && index % Economy.FilesPerDay == 1)
                yield return Tremor(tremor);

            RefreshSeals(buildingCase);
            inspection.LoadCase(buildingCase, day >= Economy.HammerDay ? Economy.HammerPerDay - hammersUsedToday : -1);
            yield return Talk(buildingCase);

            inspectAllowed = true;
            hud.SetInspectAvailable(true);
            if (!inspection.IsOpen)
                ToggleInspection();
            ShowDayMessages(index % Economy.FilesPerDay);
            pen.Play(day, index % Economy.FilesPerDay);

            // Rüşvet dosyasında RED teslim edilince müteahhit bir kez araya girer; oyuncu dönebilir.
            bool pressured = false;
            while (true)
            {
                while (!submitted)
                    yield return null;
                StopFieldTalk();
                if (pressured || buildingCase.rejectPressure.Count == 0 || inspection.MainCard.Stamp != Seal.Reject)
                    break;

                pressured = true;
                if (inspection.IsOpen)
                    ToggleInspection();
                inspectAllowed = false;
                hud.SetInspectAvailable(false);
                bool keep = true;
                yield return Pressure(buildingCase, k => keep = k);
                if (keep)
                    break;

                inspection.MainCard.ClearStamp();
                submitted = false;
                inspectAllowed = true;
                hud.SetInspectAvailable(true);
                ToggleInspection();
            }

            pen.Clear();
            var result = Decide(buildingCase);
            results.Add(result);
            money += result.money;
            hud.SetMoney(money);
            string paid = day > Economy.LastPaidDay ? "harç yok" : $"+{Ui.Money(result.money)}";
            hud.Toast($"{SealName(result.seal)}   {paid}", new Color(0.98f, 0.82f, 0.4f));
            if (result.warning != null)
                hud.Warn("Yönetmelik: " + result.warning);

            if (inspection.IsOpen)
                ToggleInspection();
            inspectAllowed = false;
            hud.SetInspectAvailable(false);
            inspection.ClearCase();

            // Müteahhit mühürü görünce hemen kalkar, tepkisini kapıya yürürken söyler. Rüşvet dosyasında (Ozan)
            // eskisi gibi: önce oturup konuşur, sonra çıkar.
            bool talkWhileLeaving = buildingCase.approveFeeOverride <= 0;
            Coroutine talk = null;
            if (talkWhileLeaving)
                talk = StartCoroutine(ReactWhileLeaving(buildingCase, result.seal));
            else
                yield return React(buildingCase, result.seal);

            yield return Skippable(Leave(), skippable, () =>
            {
                if (door != null)
                    door.SnapClosed();
            });
            if (talk != null)
            {
                StopCoroutine(talk);
                dialogue.HideBubble();
                dialogue.KeepClearOfTop = false;
            }
            visitorChair.Release(visitor);
            Destroy(visitor.gameObject);
            visitor = null;
            dialogue.SpeedMultiplier = 1f;

            // Rüşvet bir seçenek değil, suçtur: müteahhit gidince (gün bitmeden) sistem kayda geçirir ve paraya el koyar.
            if (result.bribed)
                yield return hud.SystemAlert("KAYIT ALTINA ALINDI", BribeSeizedText(buildingCase, result));
        }

        IEnumerator Enter()
        {
            if (door != null)
                yield return visitor.WalkThrough(door);
            yield return visitor.SitOn(visitorChair);
        }

        IEnumerator Leave()
        {
            yield return visitor.StandUp();
            if (door != null)
                yield return visitor.WalkThrough(door);
            if (exitPoint != null)
                yield return visitor.MoveTo(exitPoint.position);
        }

        /// <summary>
        /// Yürüyüşü oynatır; oyuncu sol tıklarsa (ya da Space) ekran kısa süre kararır, yürüyüş kesilir ve
        /// <paramref name="snap"/> sonucu anında kurar. Geçişe sebep olan tık bir sonraki konuşmayı ilerletmez.
        /// </summary>
        IEnumerator Skippable(IEnumerator walk, bool allowed, System.Action snap)
        {
            bool done = false;
            var routine = StartCoroutine(Track(walk, () => done = true));
            hud.SetSkipHint(allowed);
            // Önceki konuşmayı bitiren tık bu karede hâlâ "basıldı" görünür; onu geçiş saymamak için bir kare bekle.
            yield return null;
            while (!done)
            {
                if (allowed && SkipPressed())
                {
                    hud.SetSkipHint(false);
                    yield return hud.FadeTo(1f, SkipFadeOut);
                    StopCoroutine(routine);
                    snap();
                    yield return null;
                    yield return hud.FadeTo(0f, SkipFadeIn);
                    yield break;
                }
                yield return null;
            }
            hud.SetSkipHint(false);
        }

        static IEnumerator Track(IEnumerator routine, System.Action onDone)
        {
            yield return routine;
            onDone();
        }

        bool SkipPressed()
        {
            if (paused || hud.BlocksInput)
                return false;
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            return (mouse != null && mouse.leftButton.wasPressedThisFrame)
                || (keyboard != null && keyboard.spaceKey.wasPressedThisFrame);
        }

        IEnumerator Talk(BuildingCase buildingCase)
        {
            if (buildingCase.dialogue != null && buildingCase.dialogue.nodes.Count > 0)
            {
                yield return RunDialogue(buildingCase.dialogue);
                yield break;
            }
            visitor.Face?.SetExpression(FaceExpression.Happy);
            foreach (var callback in buildingCase.callbacks)
                if (callback.other != null && SealOf(callback.other) == callback.seal)
                    yield return dialogue.Say(buildingCase.contractorName, callback.line, visitorHead, waitForClick: true);
            // Müteahhit oyuncuya bir şey sorarsa (ya da baskı yaparsa) oyuncu iki karttan biriyle cevap verir.
            // Konuşma uzamasın diye dosya başına bir kez; geri dönüş cümleleri teşekkür olduğu için hariç.
            bool replied = false;
            bool honest = ContractorTalk.IsHonest(buildingCase);
            bool afterQuake = day > Economy.QuakeDays[0];
            // Kişiliğin selamı ilk cümlenin başına eklenir: ayrı bir tıklama istemez.
            var intro = buildingCase.introLines.Select(l => Voice.Fill(l, buildingCase)).ToList();
            string greeting = Voice.Fill(Voice.Greeting(buildingCase), buildingCase);
            if (!string.IsNullOrEmpty(greeting))
            {
                if (intro.Count > 0)
                    intro[0] = greeting + " " + intro[0];
                else
                    intro.Add(greeting);
            }
            foreach (var line in intro)
            {
                var replies = replied ? null : ContractorTalk.RepliesTo(line, afterQuake);
                // Cevap kartı gelecekse balon tıklama beklemez: kartlar hemen altında açılır.
                yield return dialogue.Say(buildingCase.contractorName, line, visitorHead, waitForClick: replies == null);
                if (replies == null)
                    continue;
                replied = true;
                int picked = 0;
                yield return dialogue.Choose(replies.Select(r => r.option).ToList(), i => picked = i);
                var reply = replies[picked];
                visitor.Face?.SetExpression(honest ? FaceExpression.Happy : FaceExpression.Suspicious);
                // İlk kart kural/kitap tavrı, ikincisi yumuşak; kişilik ikisine de kendi tarzında tepki verir.
                bool firm = picked == 0;
                string topical = honest ? reply.honestReaction : reply.dishonestReaction;
                string reaction = Voice.Line(buildingCase,
                    v => firm ? (honest ? v.firmHonest : v.firmDishonest) : (honest ? v.softHonest : v.softDishonest),
                    reply.specific ? topical : null, topical);
                yield return dialogue.Say(buildingCase.contractorName, reaction, visitorHead, waitForClick: true);
            }

            // Gri rüşvet: bina sağlam olsa da harç dışında para teklif edilir. Kabul edilirse onayda kasaya girer.
            if (buildingCase.bribe > 0 && !string.IsNullOrEmpty(buildingCase.bribeOffer))
            {
                visitor.Face?.SetExpression(FaceExpression.Suspicious);
                yield return dialogue.Say(buildingCase.contractorName, buildingCase.bribeOffer, visitorHead, waitForClick: false);
                int picked = 1;
                yield return dialogue.Choose(new List<string> { TakeBribe, RefuseBribe }, i => picked = i);
                bribeAccepted = picked == 0;
                RefreshSeals(buildingCase);
                yield return dialogue.Say(buildingCase.contractorName,
                    bribeAccepted ? buildingCase.bribeAcceptedLine : buildingCase.bribeRefusedLine, visitorHead, waitForClick: true);
            }

            // Oyuncu soru sorabilir; cevaplar karakteri açar, kuralı değiştirmez.
            var remaining = new List<CaseQuestion>(buildingCase.questions);
            while (remaining.Count > 0)
            {
                var options = remaining.Select(q => q.question).ToList();
                options.Add(EndQuestions);
                int picked = options.Count - 1;
                yield return dialogue.Choose(options, i => picked = i);
                if (picked >= remaining.Count)
                    break;
                var asked = remaining[picked];
                remaining.RemoveAt(picked);
                yield return dialogue.Say(buildingCase.contractorName, Voice.Fill(asked.answer, buildingCase), visitorHead, waitForClick: true);
            }
            dialogue.HideBubble();
        }

        Seal SealOf(BuildingCase other)
        {
            foreach (var r in results)
                if (r.buildingCase == other)
                    return r.seal;
            return Seal.None;
        }

        void OnFieldAsked(DocumentField field)
        {
            if (!inspectAllowed || paused || visitor == null || current == null)
                return;
            StopFieldTalk();
            fieldTalk = StartCoroutine(AnswerField(field));
        }

        /// <summary>Satır sorusunun cevabı: yazılır, okunacak kadar bekler, kendiliğinden kapanır (tıklama belgelere gider).</summary>
        IEnumerator AnswerField(DocumentField field)
        {
            var answer = ContractorTalk.AboutField(current, field);
            visitor.Face?.SetExpression(answer.defensive ? FaceExpression.Suspicious : FaceExpression.Happy);
            yield return dialogue.Say(current.contractorName, answer.text, visitorHead, waitForClick: false);
            yield return new WaitForSeconds(2f + answer.text.Length * 0.03f);
            dialogue.HideBubble();
            fieldTalk = null;
        }

        void StopFieldTalk()
        {
            if (fieldTalk == null)
                return;
            StopCoroutine(fieldTalk);
            fieldTalk = null;
            dialogue.HideBubble();
        }

        bool Holds(PhoneMessage message)
        {
            switch (message.condition)
            {
                case MessageCondition.Bought: return purchased.Contains(message.item);
                case MessageCondition.NotBought: return !purchased.Contains(message.item);
                case MessageCondition.SealWas: return message.other != null && SealOf(message.other) == message.seal;
                case MessageCondition.MoneyBelow: return money < message.threshold;
                case MessageCondition.BondBelow: return bond < message.threshold;
                case MessageCondition.PerfectRecord: return bonusPaid;
                default: return true;
            }
        }

        /// <summary>Bu mesaj şu an gönderilebilir mi: gün ve zaman tutuyor, koşul sağlanıyor, aile küsse aileden gelmez.</summary>
        bool Deliverable(PhoneMessage message, MessageTiming timing) =>
            message.day == day && message.timing == timing && Holds(message) && !(message.family && Estranged);

        /// <summary>Mesaj metnindeki {kasa} {eksik} {hazirlik} yer tutucuları.</summary>
        string MessageText(PhoneMessage message) => Voice.Fill(message.text, current, MoneyTokens(money, MissingPrepCost()));

        /// <summary>Gün içi mesajlar: bu dosya masadayken telefon titrer, köşede mesaj belirir.</summary>
        void ShowDayMessages(int fileOfDay)
        {
            if (story == null)
                return;
            foreach (var m in story.messages.Where(m => Deliverable(m, MessageTiming.DuringFile) && m.fileOfDay == fileOfDay))
            {
                Sfx.Buzz();
                hud.PhoneToast(m.sender, MessageText(m));
            }
        }

        IEnumerator RunDialogue(DialogueData data)
        {
            var node = data.nodes[0];
            while (node != null)
            {
                visitor.Face?.SetExpression(node.expression);
                bool hasChoices = node.choices.Count > 0;
                yield return dialogue.Say(current.contractorName, node.npcText, visitorHead, waitForClick: !hasChoices);

                string next = node.next;
                if (hasChoices)
                {
                    int picked = 0;
                    yield return dialogue.Choose(node.choices.Select(c => c.text).ToList(), i => picked = i);
                    next = node.choices[picked].next;
                }
                node = data.Find(next);
            }
            dialogue.HideBubble();
        }

        void RefreshSeals(BuildingCase buildingCase)
        {
            var tools = inspection.Tools;
            string perDay = shearWallUsedToday ? " · bugün kullanıldı" : " · günde 1";
            tools.SetSeal(Seal.Approve, FeeCaption(ApproveIncome(buildingCase)), true);
            tools.SetSeal(Seal.Reject, FeeCaption(0), true);
            tools.SetSeal(Seal.ShearWall, FeeCaption(BuildingRules.Fee(buildingCase, Seal.ShearWall, day)) + perDay, !shearWallUsedToday);
        }

        /// <summary>6. gün harç yok; mühürlerin altında "0 TL" yerine bu yazar ki bozuk sanılmasın.</summary>
        string FeeCaption(int fee)
        {
            if (day > Economy.LastPaidDay && fee == 0)
                return "harç yok";
            return fee > 0 ? $"+{Ui.Money(fee)}" : "0 TL";
        }

        /// <summary>Rüşvetli onaydan sonra Teftiş Kurulu kartının metni: teklif edilen para kasaya girmez.</summary>
        string BribeSeizedText(BuildingCase buildingCase, CaseResult result)
        {
            int offered = ApproveIncome(buildingCase);
            return "<b>Rüşvet teklif etmek de kabul etmek de suçtur.</b>\n" +
                   $"{buildingCase.contractorName} Bey'in teklif ettiği {Ui.Money(offered)} kasana girmedi; paraya el konuldu. " +
                   $"Yalnız olağan harç ({Ui.Money(result.money)}) yazıldı.\n" +
                   "Bu dosya ve imzan incelemeye alındı.";
        }

        /// <summary>Onay mührünün altında yazan tutar: harç, rüşvetli dosyada müteahhidin teklifi (kasaya girmez).</summary>
        int ApproveIncome(BuildingCase buildingCase)
        {
            int fee = BuildingRules.Fee(buildingCase, Seal.Approve, day);
            return bribeAccepted && buildingCase == current ? fee + buildingCase.bribe : fee;
        }

        /// <summary>Perde mührü yalnız yumuşak katı kurtarır; işe yaramıyorsa basılmaz ve hak harcanmaz.</summary>
        bool CanStamp(Seal seal)
        {
            if (seal != Seal.ShearWall)
                return true;
            if (shearWallUsedToday)
            {
                hud.Warn("Bugünkü perde hakkı kullanıldı");
                return false;
            }
            if (!BuildingRules.ShearWallSaves(current))
            {
                hud.Warn(BuildingRules.Flaws(current).Count == 0 ? "Bu binada perde gerekmiyor" : "Perde bunu kurtarmaz");
                return false;
            }
            return true;
        }

        CaseResult Decide(BuildingCase buildingCase) =>
            Decide(buildingCase, inspection.MainCard != null ? inspection.MainCard.Stamp : Seal.Reject);

        CaseResult Decide(BuildingCase buildingCase, Seal seal)
        {
            if (seal == Seal.ShearWall)
                shearWallUsedToday = true;
            bool approved = seal == Seal.Approve;
            var result = new CaseResult
            {
                buildingCase = buildingCase,
                day = day,
                seal = seal,
                // Rüşvetli onayda yalnız olağan harç kasaya girer; teklif edilen paraya el konulur.
                money = BuildingRules.Income(buildingCase, seal, day),
                fate = BuildingRules.Fate(buildingCase, seal),
                warning = BuildingRules.KnownViolation(buildingCase, seal, day),
                bribed = approved && (buildingCase.approveFeeOverride > 0 || (bribeAccepted && buildingCase == current))
            };
            // Para cezası yok: yanlış onayın bedeli depremde ödenir (bina çökerse soruşturma, oyun biter).
            return result;
        }

        /// <summary>Oturduğu yerde tepki: her cümle tıklama bekler (rüşvet dosyası).</summary>
        IEnumerator React(BuildingCase buildingCase, Seal seal)
        {
            foreach (var line in Reaction(buildingCase, seal))
                yield return dialogue.Say(buildingCase.contractorName, line, visitorHead, waitForClick: true);
            dialogue.HideBubble();
        }

        /// <summary>Kapıya yürürken tepki: balon müteahhidin başının üstünde kalır, okunacak kadar durup kendisi geçer.</summary>
        IEnumerator ReactWhileLeaving(BuildingCase buildingCase, Seal seal)
        {
            dialogue.KeepClearOfTop = true;
            foreach (var line in Reaction(buildingCase, seal))
            {
                yield return dialogue.Say(buildingCase.contractorName, line, visitorHead, waitForClick: false);
                float hold = Mathf.Clamp(line.Split(' ').Length / LeavingWordsPerSecond, MinLeavingHold, MaxLeavingHold);
                for (float t = 0f; t < hold; t += Time.deltaTime)
                    yield return null;
            }
            dialogue.HideBubble();
        }

        /// <summary>Mühürden sonraki cümle(ler); yüz ifadesini ayarlar ve gazetenin alıntılayacağı sözü kaydeder.</summary>
        List<string> Reaction(BuildingCase buildingCase, Seal seal)
        {
            string line = SealLine(buildingCase, seal);
            FaceExpression expression = seal == Seal.Reject ? FaceExpression.Angry
                : seal == Seal.ShearWall ? FaceExpression.Sad
                : buildingCase.approveFeeOverride > 0 ? FaceExpression.Suspicious : FaceExpression.Happy;
            if (results.Count > 0 && results[results.Count - 1].buildingCase == buildingCase)
                results[results.Count - 1].spokenLine = line;
            visitor.Face?.SetExpression(expression);
            var lines = new List<string> { line };
            // Haksız red: dürüst müteahhit dosyasını savunur; oyuncu neyi kaçırdığını buradan da öğrenir.
            if (seal == Seal.Reject && BuildingRules.Flaws(buildingCase).Count == 0)
                lines.Add(Voice.Line(buildingCase, v => v.unjustReject, null,
                    "Beton {rapor}, kolon tam, temel yerinde. Yönetmelikte reddettirecek ne var?"));
            return lines;
        }

        /// <summary>Mühürden sonraki cümle: dosyanın kendi cümlesi, yoksa müteahhidin/kişiliğin, yoksa genel bir cümle.</summary>
        public static string SealLine(BuildingCase buildingCase, Seal seal)
        {
            switch (seal)
            {
                case Seal.Reject:
                    return Voice.Line(buildingCase, v => v.rejected, buildingCase.rejectedLine, "Bu iş burada bitmez!");
                case Seal.ShearWall:
                    return Voice.Line(buildingCase, v => v.shearWall, buildingCase.shearWallLine, "Perde mi? Masraf ama... peki, ekletiriz.");
                default:
                    return Voice.Line(buildingCase, v => v.approved, buildingCase.approvedLine, "Sağ olun, hayırlı işler.");
            }
        }

        IEnumerator Evening(int income)
        {
            MusicDirector.Play(MusicCue.Evening);
            // Kira her akşam tam düşer; kasa yetmezse eksiye iner, ev sahibi borcu hatırlatır.
            int rent = Economy.Rent;
            money -= rent;
            int bonus = PayBonusIfPerfect();
            hud.SetMoney(money);

            // Sıra: borç, dünkü isteğe cevap, o akşamın mesajları, sonda bugünkü istek (Öde düğmeli).
            // Aile küstüyse aileden hiçbir şey gelmez; ev sahibi ve bilinmeyen numara yine yazar.
            var messages = new List<EveningMessage>();
            var requests = new List<FamilyNeed>();
            if (money < 0)
                messages.Add(new EveningMessage { sender = Landlord, text = DebtMessage() });
            if (story != null)
            {
                if (!Estranged)
                    foreach (var need in story.needs.Where(n => n.day == day - 1))
                    {
                        string reply = paidNeeds.Contains(need) ? need.paidReply : need.unpaidReply;
                        if (!string.IsNullOrEmpty(reply))
                            messages.Add(new EveningMessage { sender = need.sender, text = reply });
                    }
                foreach (var m in story.messages.Where(m => Deliverable(m, MessageTiming.Evening)))
                    messages.Add(new EveningMessage { sender = m.sender, text = MessageText(m) });
                if (!Estranged)
                    requests.AddRange(story.needs.Where(n => n.day == day));
                foreach (var need in requests)
                    messages.Add(new EveningMessage { sender = need.sender, text = need.request, request = need });
            }
            if (messages.Count > 0)
                Sfx.Buzz();

            inEvening = true;
            eveningRequests.Clear();
            eveningRequests.AddRange(requests);
            yield return evening.Run(day, income, rent, money, purchased, messages, paidNeeds,
                bond, BondMax, bondNote, shopOpen: day <= Economy.LastEvening, offered: OfferedTonight(), bonus: bonus);
            bondNote = null;
            inEvening = false;
            ApplyMorning(eveningRequests);
        }

        /// <summary>"Sabah oldu": karşılanmayan her istek aile bağından bir dilim götürür.</summary>
        void ApplyMorning(IEnumerable<FamilyNeed> requests)
        {
            int before = bond;
            foreach (var need in requests)
                if (!paidNeeds.Contains(need))
                    bond = Mathf.Max(0, bond - 1);
            if (bond < before)
                bondNote = $"Dün karşılanmayan istek: aile bağı -{before - bond}";
        }

        int BondMax => story != null ? story.bondMax : 5;

        int PerfectBonus => story != null ? story.perfectBonus : 0;

        /// <summary>
        /// Son akşam, o güne kadarki bütün dosyalar doğru ve hiç rüşvet alınmadıysa müdürlük prim yatırır
        /// (banka ve müdür mesajı gelir). Dürüst ve kusursuz oyuncu böylece evi de onartabilir. Yatan tutarı döner.
        /// </summary>
        int PayBonusIfPerfect()
        {
            if (bonusPaid || day != Economy.LastEvening || PerfectBonus <= 0)
                return 0;
            bool perfect = results.Count >= day * Economy.FilesPerDay
                && DeskHud.CountCorrect(results) == results.Count
                && !results.Any(r => r.bribed);
            if (!perfect)
                return 0;
            money += PerfectBonus;
            bonusPaid = true;
            return PerfectBonus;
        }

        /// <summary>Aile evi sarsıntıda hasar aldı: sarsıntının olduğu günün ikinci dosyasına gelindi.</summary>
        bool HomeDamaged => story != null && results.GroupBy(r => r.day).Any(g => story.Tremor(g.Key) > 0f && g.Count() >= Economy.FilesPerDay);

        /// <summary>Evin depremdeki sonu: hasarlıysa ve onarılmadıysa yıkılır; aileyi çanta ve sabitleme kurtarır.</summary>
        FamilyOutcome HomeOutcome => FamilyHome.Outcome(HomeDamaged, purchased.Contains(ShopItem.Repair),
            purchased.Contains(ShopItem.Bag), purchased.Contains(ShopItem.Fixing));

        /// <summary>
        /// Bu akşam dükkânda ne satılır. Ev güçlendirme 2. akşamdan vitrinde: dürüst kasa ona ancak kusursuz kayıt
        /// primiyle yeter, yoksa yolu rüşvetten geçer (BuildCaseFiles.Simulate aynı kuralı kullanır).
        /// </summary>
        HashSet<ShopItem> OfferedTonight() => new HashSet<ShopItem>(Shop.All.Where(i => Shop.Offered(i, day)));

        string DebtMessage()
        {
            int unpaid = Mathf.Min(Economy.Rent, -money);
            string total = unpaid < -money ? $" Toplam borcunuz {Ui.Money(-money)}." : "";
            return $"Bu akşamki kiranın {Ui.Money(unpaid)}'si ödenmedi.{total} Ödemezseniz icraya vermek zorunda kalırım.";
        }

        /// <summary>
        /// Müteahhidin RED'e itirazı. Satırlar oyuncunun gerçek durumuna göre seçilir:
        /// kira sonrası kasa eksik hazırlığa (çanta, sabitleme) yetmiyorsa para, aile bağı düşükse aile; hiçbiri değilse tehdit.
        /// </summary>
        IEnumerator Pressure(BuildingCase buildingCase, System.Action<bool> onDecided)
        {
            int rentTonight = day < Economy.Days ? Economy.Rent : 0;
            int afterRent = Mathf.Max(0, money - rentTonight);
            int prepCost = MissingPrepCost();
            bool prepShort = prepCost > 0 && afterRent < prepCost;
            bool lowBond = bond <= LowBond;
            bool tookBribe = results.Any(r => r.bribed);

            var lines = buildingCase.rejectPressure.Where(p => p.condition == PressureCondition.Always).ToList();
            var specific = buildingCase.rejectPressure.Where(p =>
                (p.condition == PressureCondition.PrepShort && prepShort) ||
                (p.condition == PressureCondition.BondLow && lowBond) ||
                (p.condition == PressureCondition.TookBribe && tookBribe)).ToList();
            lines.AddRange(specific.Count > 0 ? specific : buildingCase.rejectPressure.Where(p => p.condition == PressureCondition.Otherwise));

            visitor.Face?.SetExpression(FaceExpression.Suspicious);
            foreach (var line in lines)
            {
                string text = Voice.Fill(line.text, buildingCase, MoneyTokens(afterRent, prepCost));
                yield return dialogue.Say(buildingCase.contractorName, text, visitorHead, waitForClick: true);
            }
            int picked = 0;
            yield return dialogue.Choose(new List<string> { KeepReject, Reconsider }, i => picked = i);
            dialogue.HideBubble();
            onDecided(picked == 0);
        }

        /// <summary>Öğreticinin hedefini ekranda bulur: belge satırı, yönetmelik sekmesi, mühür ya da çekiç.</summary>
        RectTransform ResolveTarget(TutorialTarget target)
        {
            switch (target.kind)
            {
                case TutorialTargetKind.Field:
                    var row = inspection.GetComponentsInChildren<FieldRow>().FirstOrDefault(r => r.Key == target.field);
                    return row != null ? (RectTransform)row.transform : null;
                case TutorialTargetKind.RuleTab:
                    return inspection.RuleTab(target.topic);
                case TutorialTargetKind.Seal:
                    return inspection.Tools.SlotRect(target.seal);
                case TutorialTargetKind.Hammer:
                    var hammer = inspection.GetComponentsInChildren<Button>().FirstOrDefault(b => b.name == "Hammer");
                    return hammer != null ? (RectTransform)hammer.transform : null;
                case TutorialTargetKind.Building:
                    var viewer = inspection.GetComponentInChildren<BuildingViewer>();
                    return viewer != null ? (RectTransform)viewer.transform : null;
                default:
                    return null;
            }
        }

        /// <summary>Öğretici notunun mümkünse üstüne yazılmayacağı paneller: açık belgeler (bina inceleme kartı dahil).</summary>
        IEnumerable<RectTransform> TutorialObstacles() =>
            inspection.GetComponentsInChildren<DocumentCard>().Select(card => card.RectTransform);

        /// <summary>Henüz alınmamış ve hâlâ satılan hazırlıkların (çanta, sabitleme) toplam fiyatı.</summary>
        int MissingPrepCost() =>
            Shop.Items.Where(i => !purchased.Contains(i) && Shop.Offered(i, day)).Sum(Shop.Price);

        /// <summary>Metinlerdeki para yer tutucuları: {kasa}, {eksik} (hazırlığa kalan açık), {hazirlik} (eksik eşyalar).</summary>
        Dictionary<string, string> MoneyTokens(int cash, int prepCost) => new Dictionary<string, string>
        {
            ["kasa"] = Ui.Money(cash),
            ["eksik"] = Ui.Money(Mathf.Max(0, prepCost - cash)),
            ["hazirlik"] = string.Join(" ve ", Shop.Items.Where(i => !purchased.Contains(i)).Select(i => Shop.Name(i).ToLower(new System.Globalization.CultureInfo("tr-TR")))),
            ["prim"] = Ui.Money(PerfectBonus),
        };

        /// <summary>İsteğe bağlı aile masrafı. Para güvenlik alışverişiyle aynı kasadan çıkar.</summary>
        void Pay(FamilyNeed need)
        {
            if (paidNeeds.Contains(need) || money < need.price)
                return;
            money -= need.price;
            paidNeeds.Add(need);
            bond = Mathf.Min(BondMax, bond + 1);
            hud.SetMoney(money);
            evening.Refresh(money, purchased, paidNeeds, bond, BondMax);
        }

        void Buy(ShopItem item)
        {
            int price = Shop.Price(item);
            if (purchased.Contains(item) || money < price)
                return;
            money -= price;
            purchased.Add(item);
            boughtOn[item] = day;
            hud.SetMoney(money);
            evening.Refresh(money, purchased, paidNeeds, bond, BondMax);
        }

        /// <summary>Depremden önceki küçük sarsıntı: kamera titrer, uğultu gelir.</summary>
        IEnumerator Tremor(float strength)
        {
            Sfx.Rumble(1.5f, Mathf.Clamp01(strength * 12f));
            yield return Shake(1.2f, strength);
            yield return new WaitForSeconds(0.4f);
        }

        /// <summary>
        /// Deprem: bu bölümde onaylanıp çöken bina varsa oyuncu hapse girer, oyun biter.
        /// Reddedilen çürük binalar da çöker ama boştur: iskân verilmediği için içinde kimse yoktur.
        /// 1. depremde (3. gün) aile evi her durumda hasar alır; sabitleme yoksa aile bağı iki, çanta yoksa bir dilim düşer.
        /// 2. depremde (6. gün) evi ayakta tutan onarımdır; ev yıkılırsa ailene ulaşılmasını çanta hızlandırır.
        /// </summary>
        IEnumerator Earthquake(int quakeDay)
        {
            // Müzik çekilir, uğultu tek başına kalır; ara sahne kendi müziğini başlatır.
            MusicDirector.Stop(0.8f);
            hud.Quake("DEPREM");
            Sfx.Rumble(4.5f, 1f);
            yield return Shake(4f, 0.06f);
            yield return new WaitForSeconds(0.8f);

            var fallen = Fallen(quakeDay);
            cinematicPlaying = true;
            yield return EarthquakeCinematic.Play(QuakeCaptions(quakeDay, fallen));
            // Onayladığı bir bina çöktüyse memur gözaltına alınır: nezarethane sahnesi, ardından soruşturma haberi ve karne.
            if (fallen.Count > 0)
                yield return PrisonCinematic.Play(PrisonCaptions(fallen));
            cinematicPlaying = false;

            if (fallen.Count > 0)
            {
                jailed = true;
                Sfx.Ambience(0f);
                MusicDirector.Play(MusicCue.Final, 2f);
                hud.ShowJail(results, fallen, quakeDay, boughtOn, HomeOutcome);
                yield break;
            }
            ApplyQuakeToFamily(quakeDay);
        }

        /// <summary>Bu depremin bölümünde (önceki depremden bu yana) onaylanıp çöken binalar.</summary>
        List<CaseResult> Fallen(int quakeDay) =>
            results.Where(r => r.fate == BuildingFate.Collapsed && InPart(r, quakeDay)).ToList();

        /// <summary>Bu depremin bölümünde reddedilen çürük binalar: depremde çöktüler ama boştular.</summary>
        List<CaseResult> FallenEmpty(int quakeDay) =>
            results.Where(r => DeskHud.FellEmpty(r) && InPart(r, quakeDay)).ToList();

        static bool InPart(CaseResult r, int quakeDay)
        {
            int previousQuake = Economy.QuakeDays.Where(d => d < quakeDay).DefaultIfEmpty(0).Max();
            return r.day > previousQuake && r.day <= quakeDay;
        }

        /// <summary>Deprem sahnesinin manşetleri: çöken binalar ve aile evinin durumu.</summary>
        List<string> QuakeCaptions(int quakeDay, IList<CaseResult> fallen)
        {
            var captions = fallen.Take(MaxQuakeHeadlines).Select(DeskHud.Headline).ToList();
            if (fallen.Count > MaxQuakeHeadlines)
                captions.Add($"ve {fallen.Count - MaxQuakeHeadlines} bina daha");
            // Oyun çocuklara da oynatılıyor: çöken binada can kaybı yok, bedel yuvasını kaybeden ailelerin.
            if (fallen.Count > 0)
                captions.Add("Tatbikat sayesinde herkes son anda dışarı çıktı; ama aileler yuvasız kaldı");
            var empty = FallenEmpty(quakeDay);
            if (empty.Count == 1)
                captions.Add($"{empty[0].buildingCase.buildingName} çöktü; iskân verilmediği için içi boştu");
            else if (empty.Count > 1)
                captions.Add($"İskân verilmeyen {empty.Count} bina çöktü; hiçbirinde kimse yoktu");

            bool bag = purchased.Contains(ShopItem.Bag);
            switch (HomeOutcome)
            {
                case FamilyOutcome.Survived:
                    captions.Add("Ailenin sarsıntıda çatlayan evi yıkıldı");
                    captions.Add("Çanta hazırdı, dolaplar sabitti: aile hemen toplanma alanına çıktı, herkes güvende");
                    return captions;
                case FamilyOutcome.Injured:
                    captions.Add("Ailenin sarsıntıda çatlayan evi yıkıldı");
                    captions.Add(bag ? "Aile çantayı kapıp çıktı; devrilen dolap Elif'i çok korkuttu"
                        : "Dolaplar sabitti, aile dışarı çıktı; ama fener yoktu, gece karanlıkta geçti");
                    return captions;
                case FamilyOutcome.Lost:
                    captions.Add("Ailenin sarsıntıda çatlayan evi yıkıldı");
                    captions.Add("Allahtan aile o gece teyzedeydi; ama hiçbir hazırlık yoktu, bütün eşyalar yıkıntıda kaldı");
                    return captions;
            }
            if (purchased.Contains(ShopItem.Repair))
                captions.Add("Güçlendirilen aile evi depremi ayakta karşıladı");
            captions.Add(purchased.Contains(ShopItem.Fixing)
                ? "Ailenin evi sarsıldı; dolaplar sabitti, kimsenin burnu bile kanamadı"
                : "Ailenin evi sarsıldı; sabitlenmemiş dolap devrildi, Elif son anda kenara kaçtı");
            captions.Add(bag ? "Elektrikler kesildi; çantadaki fener ve su işe yaradı" : "Elektrikler kesildi; evde ne fener vardı ne su");
            return captions;
        }

        /// <summary>Hapishane sahnesinin yazıları; çöken bina yoksa boş liste.</summary>
        static List<string> PrisonCaptions(IList<CaseResult> fallen)
        {
            if (fallen.Count == 0)
                return new List<string>();
            string what = fallen.Count == 1
                ? $"Çöken {fallen[0].buildingCase.buildingName} binasının onayında senin imzan vardı"
                : $"Çöken {fallen.Count} binanın onayında senin imzan vardı";
            return new List<string> { what, "İskân memuru gözaltına alındı; hakkında soruşturma başlatıldı" };
        }

        /// <summary>1. depremde eşya sabitlenmemişse dolap Elif'in üstüne devrilir, çanta yoksa karanlıkta kalınır: aile bağı düşer.</summary>
        void ApplyQuakeToFamily(int quakeDay)
        {
            if (quakeDay != Economy.QuakeDays[0])
                return;
            int before = bond;
            var what = new List<string>();
            if (!purchased.Contains(ShopItem.Fixing))
            {
                bond -= FixingQuakeBondLoss;
                what.Add("dolap devrilip Elif'i korkuttu");
            }
            if (!purchased.Contains(ShopItem.Bag))
            {
                bond -= BagQuakeBondLoss;
                what.Add("evde fener bile yoktu");
            }
            bond = Mathf.Max(0, bond);
            if (bond < before)
                bondNote = $"Depremde {string.Join(", ", what)}: aile bağı -{before - bond}";
        }

        IEnumerator Shake(float seconds, float strength)
        {
            var cam = deskCamera != null ? deskCamera.transform : null;
            if (cam == null)
                yield break;
            var origin = cam.localPosition;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                cam.localPosition = origin + Random.insideUnitSphere * (Mathf.Sin(Mathf.Clamp01(t / seconds) * Mathf.PI) * strength);
                yield return null;
            }
            cam.localPosition = origin;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Geliştirici paneli (F1) kısayolları. Yayın derlemesine girmez.

        public enum DebugSeal
        {
            Correct,
            ApproveAll,
            RejectAll
        }

        /// <summary>Üzerinde çalışılan (ya da sıradaki) dosyanın sırası; bitmiş dosya sayısına eşittir.</summary>
        public int DebugFileIndex => results.Count;
        public int DebugFileCount => cases.Length;
        public bool DebugCanJump => !cinematicPlaying && !AdditiveCinematic.IsPlaying && results.Count < cases.Length;
        public bool DebugHasFixing => purchased.Contains(ShopItem.Fixing);
        public int DebugBond => bond;
        public int DebugCollapsedCount => results.Count(r => r.fate == BuildingFate.Collapsed);
        /// <summary>Sıradaki deprem günü (paneldeki "deprem sahnesini oynat" önizlemesi bunu gösterir).</summary>
        int DebugQuakeDay
        {
            get
            {
                foreach (var d in Economy.QuakeDays)
                    if (d >= Mathf.Max(day, 1))
                        return d;
                return Economy.Days;
            }
        }
        public IList<string> DebugQuakeCaptions => QuakeCaptions(DebugQuakeDay, Fallen(DebugQuakeDay));
        public IList<string> DebugPrisonCaptions =>
            PrisonCaptions(results.Where(r => r.fate == BuildingFate.Collapsed).ToList());

        public bool DebugCinematicPlaying
        {
            get => cinematicPlaying;
            set => cinematicPlaying = value;
        }

        /// <summary>
        /// Akışı keser ve <paramref name="targetIndex"/>. dosyadan devam eder. Aradaki dosyalara <paramref name="mode"/>'a göre
        /// mühür basılır; aradaki akşamların kirası ve aile bağı sessizce işlenir. Atlanan 3. gün depremi sessizce
        /// geçer (aile evine etkisi işlenir, hapis aranmaz: panel test aracıdır). Hedef bir günün ilk dosyasıysa önce
        /// önceki günün (deprem günüyse önce depremi, sonra) akşamı açılır. Hedef dosya sayısıysa 6. gün depremine gidilir.
        /// </summary>
        public void DebugJumpTo(int targetIndex, DebugSeal mode)
        {
            if (!DebugCanJump)
                return;
            targetIndex = Mathf.Clamp(targetIndex, results.Count, cases.Length);
            InterruptWeek();

            bool eveningPending = false;
            while (results.Count < targetIndex)
            {
                int index = results.Count;
                if (index % Economy.FilesPerDay == 0)
                {
                    shearWallUsedToday = false;
                    hammersUsedToday = 0;
                }
                day = Economy.Day(index);
                var result = Decide(cases[index], AutoSeal(cases[index], mode));
                results.Add(result);
                money += result.money;

                bool dayEnds = (index + 1) % Economy.FilesPerDay == 0;
                if (dayEnds && day < Economy.Days)
                {
                    if (index + 1 < targetIndex)
                    {
                        ApplyQuakeToFamily(day);
                        SimulateEvening();
                    }
                    else
                        eveningPending = true;
                }
            }
            hud.SetMoney(money);
            week = StartCoroutine(targetIndex >= cases.Length ? Finale(quakeFirst: true) : RunWeek(targetIndex, eveningPending));
        }

        public void DebugAddMoney(int amount)
        {
            money = Mathf.Max(0, money + amount);
            hud.SetMoney(money);
        }

        public void DebugSetFixing(bool on)
        {
            if (on)
            {
                purchased.Add(ShopItem.Fixing);
                boughtOn[ShopItem.Fixing] = Mathf.Max(day, 1);
            }
            else
            {
                purchased.Remove(ShopItem.Fixing);
                boughtOn.Remove(ShopItem.Fixing);
            }
        }

        public void DebugFillBond() => bond = BondMax;

        Seal AutoSeal(BuildingCase buildingCase, DebugSeal mode)
        {
            switch (mode)
            {
                case DebugSeal.ApproveAll:
                    return Seal.Approve;
                case DebugSeal.RejectAll:
                    return Seal.Reject;
                default:
                    var seal = BuildingRules.CorrectSeal(buildingCase);
                    // Günde tek perde hakkı: ikincisi gerekirse red, yine kimse ölmez.
                    return seal == Seal.ShearWall && shearWallUsedToday ? Seal.Reject : seal;
            }
        }

        /// <summary>Atlanan akşam: kira düşer, karşılanmayan aile istekleri sabah bağdan gider.</summary>
        void SimulateEvening()
        {
            money -= Economy.Rent;
            PayBonusIfPerfect();
            var requests = story != null && !Estranged ? story.needs.Where(n => n.day == day).ToList() : new List<FamilyNeed>();
            ApplyMorning(requests);
        }

        /// <summary>Haftanın akışını o anki yerinde keser ve ekranda yarım kalan her şeyi toplar.</summary>
        void InterruptWeek()
        {
            StopAllCoroutines();
            week = null;
            fieldTalk = null;
            if (visitor != null)
            {
                visitorChair.Release(visitor);
                Destroy(visitor.gameObject);
                visitor = null;
            }
            if (door != null)
                door.SnapClosed();
            dialogue.Clear();
            pen.Clear();
            inspection.SetOpen(false);
            hud.SetInspectOpen(false);
            inspection.ClearCase();
            inspectAllowed = false;
            hud.SetInspectAvailable(false);
            hud.ResetOverlays();
            evening.Hide();
            if (inEvening)
            {
                inEvening = false;
                ApplyMorning(eveningRequests);
            }
            submitted = false;
            bribeAccepted = false;
            current = null;
            if (paused)
                SetPaused(false);
        }
#endif

        public static string SealName(Seal seal)
        {
            switch (seal)
            {
                case Seal.ShearWall: return "Perde";
                case Seal.Reject: return "Red";
                default: return "Onay";
            }
        }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>())
                if (t.name == name)
                    return t;
            return null;
        }

        // Kemik isimleri farklı (ör. Mixamo "mixamorig:Head") Humanoid karakterler için.
        static Transform HumanBone(GameObject go, HumanBodyBones bone)
        {
            var animator = go.GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman)
                return null;
            var t = animator.GetBoneTransform(bone);
            return t != null ? t : null;
        }
    }
}
