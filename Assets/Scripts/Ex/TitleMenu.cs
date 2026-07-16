using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleMenu : MonoBehaviour
{
    [SerializeField] string nextSceneName = "";
    [SerializeField] GameObject credit;

    public void ToggleCredit()
    {
        print("a");
        if (credit != null)
            credit.SetActive(!credit.activeSelf);
    }
    public void StartGame()
    {
        print("b");
        if (nextSceneName != "")
            SceneManager.LoadScene(nextSceneName);
    }
}
