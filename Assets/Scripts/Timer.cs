using UnityEngine;
using TMPro;

public class TimerUI : MonoBehaviour
{
    public TMP_Text text;
    float t;

    void Update()
    {
        t += Time.deltaTime;
        text.text = "Time: " + t.ToString("F1");
    }
}