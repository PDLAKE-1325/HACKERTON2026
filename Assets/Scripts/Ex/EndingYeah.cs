using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class EndingYeah : MonoBehaviour
{
    public static EndingYeah Instance;
    void Awake()
    {
        Instance = this;
    }
    void Update()
    {

    }
    public virtual void kk()
    {
        print("KKK");
        InputManager.Instance.SetInputAllowed(false);
        AudioManager.Instance.SetMuffled(true);
        StartCoroutine(End());
    }

    [SerializeField] string nextSceneName;
    IEnumerator End()
    {
        yield return new WaitForSeconds(1.5f);
        SceneManager.LoadScene(nextSceneName);
    }
}
