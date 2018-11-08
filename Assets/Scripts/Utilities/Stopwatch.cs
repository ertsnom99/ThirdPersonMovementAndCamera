using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Text))]
public class Stopwatch : MonoBehaviour
{
    public Text text;
    private string defaultText;

    public float CurrentTime { get; private set; }

    // Use this for initialization
    void Start()
    {
        CurrentTime = 0f;
        defaultText = text.text;
    }

    // Update is called once per frame
    void Update()
    {
        CurrentTime += Time.deltaTime;
        DisplayTime();
    }

    private void DisplayTime()
    {
        string timeString = "";
        int minutes = Mathf.FloorToInt(CurrentTime / 60.0f);

        if (minutes < 10)
        {
            timeString += "0";
        }
        timeString += minutes.ToString();
        timeString += ":";

        int timeInSeconds = Mathf.RoundToInt(CurrentTime);

        int remainingSeconds = timeInSeconds % 60;

        if (remainingSeconds < 10)
        {
            timeString += "0";
        }
        timeString += remainingSeconds.ToString();

        text.text = defaultText + timeString;
    }

    public void Reinitialize()
    {
        CurrentTime = 0.0f;
        DisplayTime();
    }
}
