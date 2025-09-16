using TMPro;
using UnityEditor.SearchService;
using UnityEngine;
using UnityEngine.SceneManagement;


public class MenuManager : MonoBehaviour
{
    public TMP_Text messedge;
    public TMP_Text Best;
    public TMP_Text Last;

    private void Awake()
    {
        if (StaticHolder.PlayerWin)
        {
            messedge.text = "Вы выиграли";
            Best.text = "Лучшее время: " + StaticHolder.BestTime;
            Last.text = "Последнее время: " + StaticHolder.LastTime;
        }
        else if (StaticHolder.PlayerLose)
        {
            messedge.text = "Вы проиграли";
            Best.text = "Лучшее время: " + StaticHolder.BestTime;
            Last.text = "Последнее время: " + StaticHolder.LastTime;
        }
        if (StaticHolder.Record)
        {
            messedge.text = messedge.text + " и поставили рекорд!";
        }
    }

    public void StartGame()
    {
        SceneManager.LoadScene(0);
    }
    public void EndGame()
    {
        Application.Quit();
    }
}
