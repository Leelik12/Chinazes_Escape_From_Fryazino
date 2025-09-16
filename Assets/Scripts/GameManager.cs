using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public bool BotOver = false;
    public bool PlayerOver = false;
    public float timer;
    public float start;
    void Start()
    {
        StaticHolder.PlayerWin = false;
        StaticHolder.PlayerLose = false;
        StaticHolder.Record = false;
        start = Time.time;
    }

    void Update()
    {
        //timer = Time.time - start;
        if (PlayerOver)
        {
            StaticHolder.PlayerWin = true;
            timer = Time.time - start;
            StaticHolder.LastTime = FormatTimeMMSS(timer);
            StaticHolder.LastTimeFloat = timer;
            if (StaticHolder.BestTimeFloat > timer || StaticHolder.BestTimeFloat == 0)
            {
                StaticHolder.Record = true;
                StaticHolder.BestTimeFloat = timer;
                StaticHolder.BestTime = FormatTimeMMSS(timer);
            }
            SceneManager.LoadScene(1);
        }
        else if (BotOver)
        {
            StaticHolder.PlayerLose = true;
        }
    }

    private void FixedUpdate()
    {
        Debug.Log(FormatTimeMMSS(timer));
    }

    string FormatTimeMMSS(float timeInSeconds)
    {
        int minutes = Mathf.FloorToInt(timeInSeconds / 60);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60);
        return string.Format("{0:00}:{1:00}", minutes, seconds);
    }
}
