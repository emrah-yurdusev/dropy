using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
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

        const int HammerDay = 3;

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

        public IReadOnlyList<CaseResult> Results => results;
        public int Money => money;
        public int Day => day;

        void Awake()
        {
            BuildUi();
            money = Economy.StartMoney;
            hud.SetMoney(money);
        }

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

            dialogue = canvasGo.AddComponent<DialogueUI>();
            dialogue.Build(root, deskCamera);

            hud = canvasGo.AddComponent<DeskHud>();
            hud.Build(root);
            hud.InspectClicked += ToggleInspection;

            evening = canvasGo.AddComponent<EveningPanel>();
            evening.Build(root);
            evening.BuyClicked += Buy;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame)
                ToggleInspection();
        }

        void ToggleInspection()
        {
            if (!inspectAllowed)
                return;
            inspection.Toggle();
            hud.SetInspectOpen(inspection.IsOpen);
        }

        IEnumerator RunWeek()
        {
            int expected = Economy.Days * Economy.FilesPerDay;
            if (cases.Length != expected)
                Debug.LogWarning($"DeskManager: {expected} dosya bekleniyordu, {cases.Length} var. Dropy > Dosyaları Üret ile kur.");

            for (day = 1; day <= Economy.Days; day++)
            {
                shearWallUsedToday = false;
                inspection.SetDay(day);
                yield return hud.DayBanner(day, NewRules(day));

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

                if (day < Economy.Days)
                    yield return Evening(income);
            }
            day = Economy.Days;

            yield return Earthquake();
            hud.ShowFinal(results, purchased, money);
        }

        string NewRules(int forDay)
        {
            if (regulations == null)
                return null;
            var titles = regulations.pages.Where(p => p.day == forDay).Select(p => p.title).ToList();
            return titles.Count == 0 ? "Yönetmelikte yeni madde yok." : "Yönetmeliğe yeni madde: " + string.Join(", ", titles);
        }

        IEnumerator RunCase(BuildingCase buildingCase, int index)
        {
            submitted = false;

            var prefab = buildingCase.contractorPrefab != null
                ? buildingCase.contractorPrefab
                : defaultContractors[index % defaultContractors.Length];
            var go = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
            visitor = go.GetComponent<NpcController>();
            visitorHead = FindDeep(go.transform, "Head") ?? go.transform;

            if (door != null)
                yield return visitor.WalkThrough(door);
            yield return visitor.SitOn(visitorChair);

            RefreshSeals(buildingCase);
            inspection.LoadCase(buildingCase, day >= HammerDay);
            yield return Talk(buildingCase);

            inspectAllowed = true;
            hud.SetInspectAvailable(true);
            if (!inspection.IsOpen)
                ToggleInspection();

            while (!submitted)
                yield return null;

            var result = Decide(buildingCase);
            results.Add(result);
            money += result.money;
            hud.SetMoney(money);
            hud.Toast($"{SealName(result.seal)}   +{result.money:N0} TL", new Color(0.98f, 0.82f, 0.4f));
            if (result.warning != null)
                hud.Warn("Yönetmelik: " + result.warning);

            if (inspection.IsOpen)
                ToggleInspection();
            inspectAllowed = false;
            hud.SetInspectAvailable(false);
            inspection.ClearCase();

            yield return React(buildingCase, result.seal);

            yield return visitor.StandUp();
            if (door != null)
                yield return visitor.WalkThrough(door);
            if (exitPoint != null)
                yield return visitor.MoveTo(exitPoint.position);
            Destroy(visitor.gameObject);
            visitor = null;
        }

        IEnumerator Talk(BuildingCase buildingCase)
        {
            if (buildingCase.dialogue != null && buildingCase.dialogue.nodes.Count > 0)
            {
                yield return RunDialogue(buildingCase.dialogue);
                yield break;
            }
            visitor.Face?.SetExpression(FaceExpression.Happy);
            foreach (var line in buildingCase.introLines)
                yield return dialogue.Say(buildingCase.contractorName, line, visitorHead, waitForClick: true);
            dialogue.HideBubble();
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
            tools.SetSeal(Seal.Approve, BuildingRules.Fee(buildingCase, Seal.Approve, day), true);
            tools.SetSeal(Seal.Reject, 0, true);
            tools.SetSeal(Seal.ShearWall, BuildingRules.Fee(buildingCase, Seal.ShearWall, day), !shearWallUsedToday,
                shearWallUsedToday ? " · bugün kullanıldı" : " · günde 1");
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
                hud.Warn("Perde bunu kurtarmaz");
                return false;
            }
            return true;
        }

        CaseResult Decide(BuildingCase buildingCase)
        {
            var seal = inspection.MainCard != null ? inspection.MainCard.Stamp : Seal.Reject;
            if (seal == Seal.ShearWall)
                shearWallUsedToday = true;
            return new CaseResult
            {
                buildingCase = buildingCase,
                day = day,
                seal = seal,
                money = BuildingRules.Fee(buildingCase, seal, day),
                fate = BuildingRules.Fate(buildingCase, seal),
                warning = BuildingRules.KnownViolation(buildingCase, seal, day)
            };
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
            dialogue.HideBubble();
        }

        IEnumerator Evening(int income)
        {
            int rent = Mathf.Min(money, Economy.Rent);
            money -= rent;
            hud.SetMoney(money);
            yield return evening.Run(day, income, rent, money, purchased, shopOpen: day <= Economy.LastPaidDay);
        }

        void Buy(ShopItem item)
        {
            int price = Shop.Price(item);
            if (purchased.Contains(item) || money < price)
                return;
            money -= price;
            purchased.Add(item);
            hud.SetMoney(money);
            evening.Refresh(money, purchased);
        }

        IEnumerator Earthquake()
        {
            hud.Quake("DEPREM");
            var cam = deskCamera != null ? deskCamera.transform : null;
            var origin = cam != null ? cam.localPosition : Vector3.zero;
            for (float t = 0f; t < 4f; t += Time.deltaTime)
            {
                float strength = Mathf.Sin(Mathf.Clamp01(t / 4f) * Mathf.PI) * 0.06f;
                if (cam != null)
                    cam.localPosition = origin + Random.insideUnitSphere * strength;
                yield return null;
            }
            if (cam != null)
                cam.localPosition = origin;
            yield return new WaitForSeconds(0.8f);
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
    }
}
