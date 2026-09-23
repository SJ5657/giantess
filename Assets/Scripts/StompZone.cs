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
                return;
            }

            TinySoldierAI soldier = other.GetComponent<TinySoldierAI>();
            if (soldier != null)
            {
                soldier.Squash();
                return;
            }
        }

        // Police cars and tanks aren't tagged "Tiny" -- their AI script sits on the root
        // vehicle GameObject while the actual collider that triggers this is one of its
        // child body/turret pieces (same layout GiantController.ApplyAttackDamage already
        // looks up this same way), so GetComponentInParent is needed to find them. A stomp
        // should still be able to crush them: one clean stomp destroys a police car outright
        // (however much health punches already chipped off it), and the tougher armored
        // tank takes two full-health stomps.
        PoliceCarAI police = other.GetComponentInParent<PoliceCarAI>();
        if (police != null)
        {
            police.TakeDamage(police.maxHealth);
            return;
        }

        TankAI tank = other.GetComponentInParent<TankAI>();
        if (tank != null)
        {
            tank.TakeDamage(tank.maxHealth / 2f);
            return;
        }

        TrafficCarAI trafficCar = other.GetComponentInParent<TrafficCarAI>();
        if (trafficCar != null)
        {
            trafficCar.Squash();
        }
    }
}
