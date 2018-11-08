using UnityEngine;

public class Pulsating : MonoBehaviour
{
    [Header("Pulsation")]
    [SerializeField]
    private float m_pulsationScale = 1.1f;
    private float m_scaleDiff;
    [SerializeField]
    private float m_pulseDuration = 1.0f;
    [SerializeField]
    private AnimationCurve m_pulsationMovement;
    [SerializeField]
    private bool m_useStartTimeHasReference;
    private float m_startTime;

    private void Awake()
    {
        m_scaleDiff = m_pulsationScale - 1.0f;
    }

    private void Start()
    {
        if (m_useStartTimeHasReference)
        {
            m_startTime = Time.time;
        }
    }

    private void Update()
    {
        UpdateLocalScale();
    }

    private void UpdateLocalScale()
    {
        float scale = 1.0f + m_pulsationMovement.Evaluate(Mathf.PingPong((Time.time - m_startTime) / (m_pulseDuration / 2), 1)) * m_scaleDiff;
        transform.localScale = new Vector3(scale, scale, 1.0f);
    }

    private void OnEnable()
    {
        if (!m_useStartTimeHasReference)
        {
            m_startTime = Time.time;
        }
        else
        {
            UpdateLocalScale();
        }
    }

    private void OnDisable()
    {
        transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
    }
}
