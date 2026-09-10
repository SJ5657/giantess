using UnityEngine;

public class StompZone : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Tiny"))
        {
            TinyNPC npc = other.GetComponent<TinyNPC>();
            if (npc != null)
            {
                npc.Squash();
            }
        }
    }
}
