using UnityEngine;

// Dreht die Flügel eines Windrads mit einem gleichmäßigen Drehmoment.
public class Windrad : MonoBehaviour
{
    [SerializeField] Rigidbody fluegel;
    [SerializeField] float staerke = 2f;

    void FixedUpdate()
    {
        fluegel.AddTorque(transform.forward * staerke);
    }
}
