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
    public enum DeskDecision
    {
        DirectApprove,
        BribedApprove,
        RevisedApprove,
        Reject
    }

    /// <summary>
    /// Vize bürosu masa akışı: müteahhit kapıdan girer, karşıya oturur, sohbet edilir,
    /// belgeler incelenip damgalanır, karar verilir, müteahhit tepki verip çıkar. Sıradaki gelir.
    /// Arayüzü (Canvas, EventSystem) kendisi kurar; sahnede yalnızca bu bileşen ve referanslar gerekir.
    /// </summary>
    public class DeskManager : MonoBehaviour
    {
        public class CaseResult
        {
            public BuildingCase buildingCase;
            public DeskDecision decision;
            public int money;
            public bool buildingSafe;
        }

        public const int DirectFee = 1000;
        public const int RevisionFee = 500;

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
        [SerializeField] BuildingCase[] cases;
        [Tooltip("Vakada karakter seçilmemişse sırayla bunlar kullanılır")]
        [SerializeField] GameObject[] defaultContractors;
        [SerializeField] int startMoney;

        DialogueUI dialogue;
        InspectionPanel inspection;
        DeskHud hud;
        NpcController visitor;
        Transform visitorHead;
        BuildingCase current;
        bool bribeAccepted;
        bool submitted;
        bool inspectAllowed;
        int money;
        readonly List<CaseResult> results = new List<CaseResult>();

        public IReadOnlyList<CaseResult> Results => results;
        public int Money => money;

        void Awake()
        {
            BuildUi();
            money = startMoney;
            hud.SetMoney(money);
        }

        void Start() => StartCoroutine(RunDay());

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

            dialogue = canvasGo.AddComponent<DialogueUI>();
            dialogue.Build(root, deskCamera);

            hud = canvasGo.AddComponent<DeskHud>();
            hud.Build(root);
            hud.InspectClicked += ToggleInspection;
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

        IEnumerator RunDay()
        {
            for (int i = 0; i < cases.Length; i++)
            {
                current = cases[i];
                hud.SetCounter(i + 1, cases.Length, current.contractorName);
                yield return RunCase(current, i);
            }
            hud.ShowSummary(results, money);
        }

        IEnumerator RunCase(BuildingCase buildingCase, int index)
        {
            bribeAccepted = false;
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

            inspection.LoadCase(buildingCase);
            bool openDocs = false;
            yield return RunDialogue(buildingCase.dialogue, () => openDocs = true);

            inspectAllowed = true;
            hud.SetInspectAvailable(true);
            if (openDocs && !inspection.IsOpen)
                ToggleInspection();

            while (!submitted)
                yield return null;

            var result = Evaluate(buildingCase);
            results.Add(result);
            money += result.money;
            hud.SetMoney(money);
            hud.Toast($"{DecisionName(result.decision)}   +{result.money:N0} TL",
                result.decision == DeskDecision.BribedApprove ? new Color(1f, 0.55f, 0.45f) : new Color(0.98f, 0.82f, 0.4f));

            if (inspection.IsOpen)
                ToggleInspection();
            inspectAllowed = false;
            hud.SetInspectAvailable(false);
            inspection.ClearCase();

            yield return React(buildingCase, result.decision);

            yield return visitor.StandUp();
            if (door != null)
                yield return visitor.WalkThrough(door);
            if (exitPoint != null)
                yield return visitor.MoveTo(exitPoint.position);
            Destroy(visitor.gameObject);
            visitor = null;
        }

        IEnumerator RunDialogue(DialogueData data, System.Action onOpenDocuments)
        {
            if (data == null || data.nodes.Count == 0)
                yield break;

            var node = data.nodes[0];
            while (node != null)
            {
                visitor.Face?.SetExpression(node.expression);
                bool hasChoices = node.choices.Count > 0;
                yield return dialogue.Say(current.contractorName, node.npcText, visitorHead, waitForClick: !hasChoices);

                string next = node.next;
                var evt = node.evt;
                if (hasChoices)
                {
                    int picked = 0;
                    yield return dialogue.Choose(node.choices.Select(c => c.text).ToList(), i => picked = i);
                    next = node.choices[picked].next;
                    evt = node.choices[picked].evt;
                }

                switch (evt)
                {
                    case DialogueEvent.AcceptBribe: bribeAccepted = true; break;
                    case DialogueEvent.RefuseBribe: bribeAccepted = false; break;
                    case DialogueEvent.OpenDocuments: onOpenDocuments?.Invoke(); break;
                }
                node = data.Find(next);
            }
            dialogue.HideBubble();
        }

        CaseResult Evaluate(BuildingCase buildingCase)
        {
            var marked = new HashSet<DocumentField>(inspection.MarkedFields);
            var fatal = buildingCase.documents.SelectMany(d => d.fields).Where(f => f.isFatalFlaw).ToList();
            bool approved = inspection.MainCard != null && inspection.MainCard.Stamp == StampKind.Approve;
            var result = new CaseResult { buildingCase = buildingCase };

            if (!approved)
            {
                result.decision = DeskDecision.Reject;
                result.buildingSafe = true; // bina yapılmıyor
            }
            else if (bribeAccepted)
            {
                result.decision = DeskDecision.BribedApprove;
                result.money = buildingCase.bribeAmount;
                result.buildingSafe = fatal.Count == 0;
            }
            else if (marked.Count > 0)
            {
                result.decision = DeskDecision.RevisedApprove;
                result.money = RevisionFee;
                result.buildingSafe = fatal.All(marked.Contains);
            }
            else
            {
                result.decision = DeskDecision.DirectApprove;
                result.money = DirectFee;
                result.buildingSafe = fatal.Count == 0;
            }
            return result;
        }

        IEnumerator React(BuildingCase buildingCase, DeskDecision decision)
        {
            string line;
            FaceExpression expression;
            switch (decision)
            {
                case DeskDecision.Reject:
                    line = buildingCase.rejectedLine;
                    expression = FaceExpression.Angry;
                    break;
                case DeskDecision.RevisedApprove:
                    line = buildingCase.revisedLine;
                    expression = FaceExpression.Sad;
                    break;
                case DeskDecision.BribedApprove:
                    line = buildingCase.approvedLine;
                    expression = FaceExpression.Suspicious;
                    break;
                default:
                    line = buildingCase.approvedLine;
                    expression = FaceExpression.Happy;
                    break;
            }
            visitor.Face?.SetExpression(expression);
            yield return dialogue.Say(buildingCase.contractorName, line, visitorHead, waitForClick: true);
            dialogue.HideBubble();
        }

        public static string DecisionName(DeskDecision decision)
        {
            switch (decision)
            {
                case DeskDecision.BribedApprove: return "Rüşvetle Onay";
                case DeskDecision.RevisedApprove: return "Revizyonla Onay";
                case DeskDecision.Reject: return "Red";
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
