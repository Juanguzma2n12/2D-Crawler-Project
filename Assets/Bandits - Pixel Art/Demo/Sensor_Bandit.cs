using UnityEngine;
using System.Collections;

public class Sensor_Bandit : MonoBehaviour {

    private int m_ColCount = 0;

    private float m_DisableTimer;

    private void OnEnable()
    {
        m_ColCount = 0;
    }

    public bool State()
    {
        if (m_DisableTimer > 0)
            return false;
        return m_ColCount > 0;
    }

    // Only solid colliders count as ground; trigger zones (checkpoints, platform sensors, hazards) do not
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.isTrigger) return;
        m_ColCount++;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.isTrigger) return;
        m_ColCount--;
    }

    void Update()
    {
        m_DisableTimer -= Time.deltaTime;
    }

    public void Disable(float duration)
    {
        m_DisableTimer = duration;
    }
}
