using UnityEngine;

public class RotorSpin : MonoBehaviour
{
    public float spinSpeed = 900f;

    void Update()
    {
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.Self);
    }
}
