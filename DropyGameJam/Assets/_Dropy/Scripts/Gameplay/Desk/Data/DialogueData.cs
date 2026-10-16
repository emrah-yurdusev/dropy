using System.Collections.Generic;
using UnityEngine;

namespace Dropy.Gameplay
{
    public enum DialogueEvent
    {
        None,
        AcceptBribe,
        RefuseBribe,
        OpenDocuments
    }

    [System.Serializable]
    public class DialogueChoice
    {
        [Tooltip("Oyuncunun cevap balonu")]
        public string text;
        [Tooltip("Sonraki düğümün id'si. Boşsa konuşma biter")]
        public string next;
        public DialogueEvent evt;
    }

    [System.Serializable]
    public class DialogueNode
    {
        public string id;
        [TextArea(2, 5)] public string npcText;
        public FaceExpression expression;
        [Tooltip("Boşsa tıklayınca 'next' düğümüne geçer")]
        public List<DialogueChoice> choices = new List<DialogueChoice>();
        public string next;
        [Tooltip("Bu düğüm bitince tetiklenecek olay (seçeneksiz düğümler için)")]
        public DialogueEvent evt;
    }

    /// <summary>NPC ile oyuncu arasındaki dallanan konuşma. İlk düğümden başlar.</summary>
    [CreateAssetMenu(menuName = "Dropy/Masa/Diyalog", fileName = "Dialogue")]
    public class DialogueData : ScriptableObject
    {
        public List<DialogueNode> nodes = new List<DialogueNode>();

        public DialogueNode Find(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return nodes.Find(n => n.id == id);
        }
    }
}
