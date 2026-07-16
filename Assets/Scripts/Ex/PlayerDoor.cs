using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerDoor : MonoBehaviour
{
    [SerializeField] string nextSceneName;
    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.name == "Door" && nextSceneName != "")
        {
            SceneManager.LoadScene(nextSceneName);
        }
    }
}
