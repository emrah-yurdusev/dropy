using UnityEngine;

namespace Dropy.Gameplay
{
    /// <summary>
    /// Sahne açılınca NPC'yi yürütmeden doğrudan sandalyeye (ör. hücredeki yatağın kenarına) oturtur.
    /// NavMesh gerektirmez; NavMeshAgent kapalı olabilir.
    /// </summary>
    [RequireComponent(typeof(NpcController))]
    public class SnapSeatOnStart : MonoBehaviour
    {
        [SerializeField] Chair chair;

        void Start()
        {
            if (chair == null)
            {
                Debug.LogWarning($"{name}: oturacağı Chair atanmamış.", this);
                return;
            }
            GetComponent<NpcController>().SnapSeat(chair);
        }
    }
}
