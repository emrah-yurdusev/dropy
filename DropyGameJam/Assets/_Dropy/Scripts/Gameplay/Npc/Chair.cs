using UnityEngine;

namespace Dropy.Gameplay
{
    /// <summary>
    /// NPC'nin oturabileceği sandalye.
    /// Sit Point: NPC'nin oturmadan önce ayakta duracağı nokta; mavi oku (+Z) oturunca bakacağı yön,
    /// yani sandalyeden dışarı doğru. Oturma animasyonu kalçayı ~0.35 m geri ve ~0.45 m yüksekliğe
    /// götürür; gizmo'daki yeşil kutu oturağın denk gelmesi gereken yeri gösterir.
    /// Sandalye modeline NavMeshModifier (Override Area = Not Walkable) ekleyin; yoksa oturak
    /// NavMesh'e yürünebilir alan olarak girer ve NPC kalkınca oturağın üstüne ışınlanabilir.
    /// </summary>
    public class Chair : MonoBehaviour
    {
        static readonly Vector3 SeatOffset = new Vector3(0f, 0.45f, -0.35f);

        [Tooltip("Boşsa bu objenin kendisi kullanılır")]
        [SerializeField] Transform sitPoint;

        public Transform SitPoint => sitPoint != null ? sitPoint : transform;
        public NpcController Occupant { get; private set; }
        public bool IsFree => Occupant == null;

        public bool TryOccupy(NpcController npc)
        {
            if (Occupant != null && Occupant != npc)
                return false;
            Occupant = npc;
            return true;
        }

        public void Release(NpcController npc)
        {
            if (Occupant == npc)
                Occupant = null;
        }

        void OnDrawGizmos()
        {
            var point = SitPoint;
            Gizmos.color = IsFree ? Color.green : Color.red;
            Gizmos.DrawWireSphere(point.position + Vector3.up * 0.05f, 0.2f);
            Gizmos.DrawLine(point.position + Vector3.up * 0.05f, point.position + Vector3.up * 0.05f + point.forward * 0.5f);
            Gizmos.matrix = Matrix4x4.TRS(point.TransformPoint(SeatOffset), point.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(0.4f, 0.04f, 0.4f));
        }
    }
}
