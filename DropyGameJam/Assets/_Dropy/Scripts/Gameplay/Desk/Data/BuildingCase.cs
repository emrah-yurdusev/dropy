using System.Collections.Generic;
using UnityEngine;

namespace Dropy.Gameplay
{
    /// <summary>Masaya gelen bir müteahhit ve projesi.</summary>
    [CreateAssetMenu(menuName = "Dropy/Masa/Vaka", fileName = "Case")]
    public class BuildingCase : ScriptableObject
    {
        public string contractorName = "Müteahhit";
        public string buildingName = "Bina";
        [Tooltip("Boşsa DeskManager'daki varsayılan karakterlerden biri gelir")]
        public GameObject contractorPrefab;

        [Header("Belgeler")]
        [Tooltip("İlk belge damgalanacak ana belgedir (Başvuru Formu)")]
        public List<DocumentData> documents = new List<DocumentData>();

        [Header("Konuşma")]
        public DialogueData dialogue;
        [Tooltip("Rüşvet kabul edilip onaylanırsa kazanılan para")]
        public int bribeAmount;

        [Header("Karar sonrası tepkiler")]
        [TextArea] public string approvedLine = "Teşekkürler hocam, hayırlı işler.";
        [TextArea] public string revisedLine = "Revizyon mu? Peki... düzelttiririz.";
        [TextArea] public string rejectedLine = "Bu iş burada bitmez!";

        public DocumentData MainDocument => documents.Count > 0 ? documents[0] : null;

        public bool HasFatalFlaw
        {
            get
            {
                foreach (var doc in documents)
                    foreach (var field in doc.fields)
                        if (field.isFatalFlaw)
                            return true;
                return false;
            }
        }
    }
}
