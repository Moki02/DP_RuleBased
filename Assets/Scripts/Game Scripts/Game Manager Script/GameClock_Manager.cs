
using UnityEngine;
using TMPro;

public class GameClock_Manager : MonoBehaviour
{
    [Header("Game Clock")]
    public float dayLength = 240f;
    public int currentDay = 1;
    public float dayTimer = 0f;

    [Header("Clock UI")]
    [SerializeField] TMP_Text clockText;

    private void Start()
    {
        UpdateClockUI();
    }

    private void Update()
    {
        if (Time.timeScale == 0f)
            return;

        dayTimer += Time.deltaTime;

        if (dayLength <= 0f)
            return;

        if (dayTimer >= dayLength)
        {
            dayTimer -= dayLength;
            currentDay++;
        }

        UpdateClockUI();
    }


    // Dev Note: This is only a day and time indicator and doesn't have the day effects yet
    private void UpdateClockUI()
    {
        if (clockText == null)
            return;

        int minutes = Mathf.FloorToInt(dayTimer / 60f);
        int seconds = Mathf.FloorToInt(dayTimer % 60f);

        clockText.text = "Day " + currentDay +
                         " - " +
                         minutes.ToString("00") + ":" +
                         seconds.ToString("00");
    }

    public void PauseGame()
    {
        Time.timeScale = 0f;
    }

    public void ResumeGame()
    {
        Time.timeScale = 1f;
    }

    // Dev Note: Will implement the drought stages in the future
}
