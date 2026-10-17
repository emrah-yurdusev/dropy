using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Dropy.Gameplay
{
    /// <summary>
    /// Vize bürosu masa akışı: 5 gün, günde 3 dosya. Müteahhit kapıdan girer, karşıya oturur, konuşur;
    /// oyuncu yönetmeliğe, künyeye ve ek rapora bakıp künyeye mühür basar. Akşam kira düşer, telefondan
    /// alışveriş yapılır. 5. günün üçüncü dosyasından sonra deprem olur.
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
        }

        /// <summary>Müteahhitler biraz hızlı yürür (1. gün 1.2, 2. günden itibaren 1.3 kat); fazlası koşar adım görünür.</summary>
        const int FastWalkDay = 2;
        const float FirstDayWalkMultiplier = 1.2f;
        const float FastWalkMultiplier = 1.3f;
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

        [Header("Sahne")]
        [Tooltip("Oyuncunun gözü: karşıdaki sandalyeye bakan sabit kamera")]
        [SerializeField] Camera deskCamera;
        [SerializeField] Chair visitorChair;
        [SerializeField] Transform spawnPoint;
        [SerializeField] Transform exitPoint;
        [Tooltip("Varsa müteahhit bu kapıyı açıp girer/çıkar")]
        [SerializeField] Door door;

        [Header("İçerik")]
        [SerializeField] RegulationData regulations;
        [Tooltip("Açılış telefonu, sabah radyosu, akşam mesajları, sarsıntılar")]
        [SerializeField] StoryData story;
        [Tooltip("Sırayla 15 dosya: her gün 3 tane")]
        [SerializeField] BuildingCase[] cases;
        [Tooltip("Vakada karakter seçilmemişse sırayla bunlar kullanılır")]
        [SerializeField] GameObject[] defaultContractors;

        DialogueUI dialogue;
        InspectionPanel inspection;
        DeskHud hud;
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
        readonly HashSet<FamilyNeed> paidNeeds = new HashSet<FamilyNeed>();
        /// <summary>Aile bağı. Sıfıra düşerse kalıcıdır: aile küser, aileden mesaj gelmez.</summary>
        int bond;
        /// <summary>Akşam defterinde aile bağı için gösterilecek not (dün ödenmeyen istek, depremde devrilen dolap...).</summary>
        string bondNote;
        /// <summary>Son depremden sonra hapse girildiyse oyun biter.</summary>
        bool jailed;
        int hammersUsedToday;
        bool paused;
        /// <summary>Deprem sahnesi oynarken büronun arayüzü gizli; Esc/Tab çalışmasın.</summary>
        bool cinematicPlaying;
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

        void OnDestroy() => Time.timeScale = 1f;

        void Start() => StartCoroutine(RunWeek());

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

            dialogue = canvasGo.AddComponent<DialogueUI>();
            dialogue.Build(root, deskCamera);

            hud = canvasGo.AddComponent<DeskHud>();
            hud.Build(root);
            hud.InspectClicked += ToggleInspection;
            hud.ResumeClicked += () => SetPaused(false);
            hud.RestartClicked += Restart;

            evening = canvasGo.AddComponent<EveningPanel>();
            evening.Build(root);
            evening.BuyClicked += Buy;
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

        IEnumerator RunWeek()
        {
            int expected = Economy.Days * Economy.FilesPerDay;
            if (cases.Length != expected)
                Debug.LogWarning($"DeskManager: {expected} dosya bekleniyordu, {cases.Length} var. Dropy > Dosyaları Üret ile kur.");

            yield return hud.Title();

            for (day = 1; day <= Economy.Days; day++)
            {
                shearWallUsedToday = false;
                hammersUsedToday = 0;
                inspection.SetDay(day);
                Sfx.Ambience(0.03f + 0.035f * day);
                yield return hud.DayBanner(day, Morning(day));
                yield return MorningCalls();

                int income = 0;
                for (int k = 0; k < Economy.FilesPerDay; k++)
                {
                    int index = (day - 1) * Economy.FilesPerDay + k;
                    if (index >= cases.Length)
                        break;
                    current = cases[index];
                    hud.SetCounter(day, k + 1, Economy.FilesPerDay, current.contractorName);
                    yield return RunCase(current, index);
                    income += results[results.Count - 1].money;
                }

                if (Economy.IsQuakeDay(day))
                {
                    yield return Earthquake(day);
                    if (jailed)
                        yield break;
                }
                if (day < Economy.Days)
                    yield return Evening(income);
            }
            day = Economy.Days;

            Sfx.Ambience(0f);
            var family = story != null
                ? story.needs.Select(n => (n, paidNeeds.Contains(n)))
                : Enumerable.Empty<(FamilyNeed, bool)>();
            hud.ShowFinal(results, purchased, family, Estranged, money, DeskHud.ReportCard(results));
        }

        string Morning(int forDay)
        {
            string rules = "Yönetmelikte yeni madde yok.";
            if (regulations != null)
            {
                var titles = regulations.pages.Where(p => p.day == forDay).Select(p => p.title).ToList();
                if (titles.Count > 0)
                    rules = "Yönetmeliğe yeni madde: " + string.Join(", ", titles);
            }
            if (forDay > Economy.LastPaidDay)
                rules += "\n<b>Bugün harç yok: mühürler para getirmez, binalar yine yapılır.</b>";
            string radio = story != null ? story.Radio(forDay) : null;
            string cracks = CrackNews(forDay);
            if (!string.IsNullOrEmpty(cracks))
                radio = string.IsNullOrEmpty(radio) ? cracks : radio + "\n" + cracks;
            return string.IsNullOrEmpty(radio) ? rules : $"<i>{radio}</i>\n\n{rules}";
        }

        /// <summary>
        /// Dünkü sarsıntıda çatlayan binalar: yalnız oyuncunun hatalı onayladığı (depremde çökecek) binalar.
        /// Oyuncu hatasını depremden önce sezsin diye en fazla iki haber.
        /// </summary>
        string CrackNews(int forDay)
        {
            if (story == null || story.Tremor(forDay - 1) <= 0f)
                return null;
            var news = results
                .Where(r => r.fate == BuildingFate.Collapsed && r.day < forDay && !crackReported.Contains(r.buildingCase))
                .OrderByDescending(r => r.day)
                .Take(2)
                .ToList();
            foreach (var r in news)
                crackReported.Add(r.buildingCase);
            return news.Count == 0 ? null
                : string.Join("\n", news.Select(r => $"Radyo: Dünkü sarsıntıda {r.buildingCase.buildingName} binasında derin çatlaklar görüldü."));
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
            foreach (var call in story.calls.Where(c => c.day == day))
            {
                Sfx.Buzz();
                yield return new WaitForSeconds(0.8f);
                foreach (var line in call.lines)
                    yield return dialogue.Say(call.caller, line, null, waitForClick: true);
                dialogue.HideBubble();
            }
        }

        IEnumerator RunCase(BuildingCase buildingCase, int index)
        {
            submitted = false;

            var prefab = buildingCase.contractorPrefab != null
                ? buildingCase.contractorPrefab
                : defaultContractors[index % defaultContractors.Length];
            var go = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
            visitor = go.GetComponent<NpcController>();
            visitorHead = FindDeep(go.transform, "Head") ?? HumanBone(go, HumanBodyBones.Head) ?? go.transform;
            visitor.SetWalkSpeedMultiplier(day >= FastWalkDay ? FastWalkMultiplier : FirstDayWalkMultiplier);
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

            yield return React(buildingCase, result.seal);

            yield return Skippable(Leave(), skippable, () =>
            {
                if (door != null)
                    door.SnapClosed();
            });
            visitorChair.Release(visitor);
            Destroy(visitor.gameObject);
            visitor = null;
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
            foreach (var line in buildingCase.introLines)
            {
                var replies = replied ? null : ContractorTalk.RepliesTo(line);
                // Cevap kartı gelecekse balon tıklama beklemez: kartlar hemen altında açılır.
                yield return dialogue.Say(buildingCase.contractorName, line, visitorHead, waitForClick: replies == null);
                if (replies == null)
                    continue;
                replied = true;
                int picked = 0;
                yield return dialogue.Choose(replies.Select(r => r.option).ToList(), i => picked = i);
                var reply = replies[picked];
                visitor.Face?.SetExpression(honest ? FaceExpression.Happy : FaceExpression.Suspicious);
                yield return dialogue.Say(buildingCase.contractorName, honest ? reply.honestReaction : reply.dishonestReaction,
                    visitorHead, waitForClick: true);
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
                yield return dialogue.Say(buildingCase.contractorName, asked.answer, visitorHead, waitForClick: true);
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
                default: return true;
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
            tools.SetSeal(Seal.Approve, FeeCaption(BuildingRules.Fee(buildingCase, Seal.Approve, day)), true);
            tools.SetSeal(Seal.Reject, FeeCaption(0), true);
            tools.SetSeal(Seal.ShearWall, FeeCaption(BuildingRules.Fee(buildingCase, Seal.ShearWall, day)) + perDay, !shearWallUsedToday);
        }

        /// <summary>5. gün harç yok; mühürlerin altında "0 TL" yerine bu yazar ki bozuk sanılmasın.</summary>
        string FeeCaption(int fee)
        {
            if (day > Economy.LastPaidDay)
                return "harç yok";
            return fee > 0 ? $"+{Ui.Money(fee)}" : "0 TL";
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

        CaseResult Decide(BuildingCase buildingCase)
        {
            var seal = inspection.MainCard != null ? inspection.MainCard.Stamp : Seal.Reject;
            if (seal == Seal.ShearWall)
                shearWallUsedToday = true;
            var result = new CaseResult
            {
                buildingCase = buildingCase,
                day = day,
                seal = seal,
                money = BuildingRules.Fee(buildingCase, seal, day),
                fate = BuildingRules.Fate(buildingCase, seal),
                warning = BuildingRules.KnownViolation(buildingCase, seal, day)
            };
            // Para cezası yok: yanlış onayın bedeli depremde ödenir (bina çökerse hapis).
            return result;
        }

        IEnumerator React(BuildingCase buildingCase, Seal seal)
        {
            string line;
            FaceExpression expression;
            switch (seal)
            {
                case Seal.Reject:
                    line = buildingCase.rejectedLine;
                    expression = FaceExpression.Angry;
                    break;
                case Seal.ShearWall:
                    line = buildingCase.shearWallLine;
                    expression = FaceExpression.Sad;
                    break;
                default:
                    line = buildingCase.approvedLine;
                    expression = buildingCase.approveFeeOverride > 0 ? FaceExpression.Suspicious : FaceExpression.Happy;
                    break;
            }
            visitor.Face?.SetExpression(expression);
            yield return dialogue.Say(buildingCase.contractorName, line, visitorHead, waitForClick: true);
            // Haksız red: dürüst müteahhit dosyasını savunur; oyuncu neyi kaçırdığını buradan da öğrenir.
            if (seal == Seal.Reject && BuildingRules.Flaws(buildingCase).Count == 0)
                yield return dialogue.Say(buildingCase.contractorName,
                    $"Beton {BuildingCase.Concrete(buildingCase.TrueConcrete)}, kolon tam, temel yerinde. Yönetmelikte reddettirecek ne var?",
                    visitorHead, waitForClick: true);
            dialogue.HideBubble();
        }

        IEnumerator Evening(int income)
        {
            int rent = Mathf.Min(money, Economy.Rent);
            money -= rent;
            hud.SetMoney(money);

            // Sıra: dünkü isteğe cevap, o akşamın mesajları, sonda bugünkü istek (Öde düğmeli).
            // Aile küstüyse aileden hiçbir şey gelmez; ev sahibi ve bilinmeyen numara yine yazar.
            var messages = new List<EveningMessage>();
            var requests = new List<FamilyNeed>();
            if (story != null)
            {
                if (!Estranged)
                    foreach (var need in story.needs.Where(n => n.day == day - 1))
                    {
                        string reply = paidNeeds.Contains(need) ? need.paidReply : need.unpaidReply;
                        if (!string.IsNullOrEmpty(reply))
                            messages.Add(new EveningMessage { sender = need.sender, text = reply });
                    }
                foreach (var m in story.messages.Where(m => m.day == day && Holds(m) && !(m.family && Estranged)))
                    messages.Add(new EveningMessage { sender = m.sender, text = m.text });
                if (!Estranged)
                    requests.AddRange(story.needs.Where(n => n.day == day));
                foreach (var need in requests)
                    messages.Add(new EveningMessage { sender = need.sender, text = need.request, request = need });
            }
            if (messages.Count > 0)
                Sfx.Buzz();

            yield return evening.Run(day, income, rent, money, purchased, messages, paidNeeds,
                bond, BondMax, bondNote, shopOpen: day <= Economy.LastPaidDay);
            bondNote = null;

            // "Sabah oldu": karşılanmayan her istek aile bağından bir dilim götürür.
            int before = bond;
            foreach (var need in requests)
                if (!paidNeeds.Contains(need))
                    bond = Mathf.Max(0, bond - 1);
            if (bond < before)
                bondNote = $"Dün karşılanmayan istek: aile bağı -{before - bond}";
        }

        int BondMax => story != null ? story.bondMax : 5;

        /// <summary>
        /// Müteahhidin RED'e itirazı. Satırlar oyuncunun gerçek durumuna göre seçilir:
        /// kira sonrası kasa güçlendirmeye yetmiyorsa para, aile bağı düşükse aile; hiçbiri değilse tehdit.
        /// </summary>
        IEnumerator Pressure(BuildingCase buildingCase, System.Action<bool> onDecided)
        {
            int rentTonight = day <= Economy.LastPaidDay ? Economy.Rent : 0;
            int afterRent = Mathf.Max(0, money - rentTonight);
            int retrofit = Shop.Price(ShopItem.Retrofit);
            bool retrofitShort = !purchased.Contains(ShopItem.Retrofit) && afterRent < retrofit;
            bool lowBond = bond <= LowBond;

            var lines = buildingCase.rejectPressure.Where(p => p.condition == PressureCondition.Always).ToList();
            var specific = buildingCase.rejectPressure.Where(p =>
                (p.condition == PressureCondition.RetrofitShort && retrofitShort) ||
                (p.condition == PressureCondition.BondLow && lowBond)).ToList();
            lines.AddRange(specific.Count > 0 ? specific : buildingCase.rejectPressure.Where(p => p.condition == PressureCondition.Otherwise));

            visitor.Face?.SetExpression(FaceExpression.Suspicious);
            foreach (var line in lines)
            {
                string text = line.text
                    .Replace("{kasa}", Ui.Money(afterRent))
                    .Replace("{eksik}", Ui.Money(Mathf.Max(0, retrofit - afterRent)));
                yield return dialogue.Say(buildingCase.contractorName, text, visitorHead, waitForClick: true);
            }
            int picked = 0;
            yield return dialogue.Choose(new List<string> { KeepReject, Reconsider }, i => picked = i);
            dialogue.HideBubble();
            onDecided(picked == 0);
        }

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
        /// 1. depremde (3. gün) aile evi her durumda hasar alır; sabitleme yoksa aile bağı iki dilim düşer.
        /// 2. depremde (6. gün) evi ayakta tutan onarımdır.
        /// </summary>
        IEnumerator Earthquake(int quakeDay)
        {
            hud.Quake("DEPREM");
            Sfx.Rumble(4.5f, 1f);
            yield return Shake(4f, 0.06f);
            yield return new WaitForSeconds(0.8f);

            int previousQuake = Economy.QuakeDays.Where(d => d < quakeDay).DefaultIfEmpty(0).Max();
            var fallen = results.Where(r => r.fate == BuildingFate.Collapsed && r.day > previousQuake && r.day <= quakeDay).ToList();
            bool firstQuake = quakeDay == Economy.QuakeDays[0];

            var captions = fallen.Take(MaxQuakeHeadlines).Select(DeskHud.Headline).ToList();
            if (fallen.Count > MaxQuakeHeadlines)
                captions.Add($"ve {fallen.Count - MaxQuakeHeadlines} bina daha");
            if (firstQuake)
                captions.Add(purchased.Contains(ShopItem.Fixing)
                    ? "Ailenin evi hasar aldı; dolaplar sabitti, kimse yaralanmadı"
                    : "Ailenin evi hasar aldı; sabitlenmemiş dolap Elif'in üstüne devrildi");
            else if (!purchased.Contains(ShopItem.Retrofit))
                captions.Add("Onarılmayan aile evi ikinci depremde yıkıldı");

            cinematicPlaying = true;
            yield return EarthquakeCinematic.Play(captions);
            cinematicPlaying = false;

            if (fallen.Count > 0)
            {
                jailed = true;
                Sfx.Ambience(0f);
                hud.ShowJail(results, fallen, quakeDay);
                yield break;
            }

            if (firstQuake && !purchased.Contains(ShopItem.Fixing))
            {
                int before = bond;
                bond = Mathf.Max(0, bond - FixingQuakeBondLoss);
                bondNote = $"Depremde dolap Elif'in üstüne devrildi: aile bağı -{before - bond}";
            }
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
