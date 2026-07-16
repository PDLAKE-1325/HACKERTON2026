using UnityEngine;

public class TestScript : MonoBehaviour
{
    [SerializeField] KeyCode SoundMuk = KeyCode.P;
    [SerializeField] KeyCode TestChat = KeyCode.O;

    [SerializeField] DialogueSO chatData;
    void Update()
    {
        if (Input.GetKeyDown(SoundMuk))
        {
            AudioManager.Instance.ToggleMuffle();
        }
        else if (Input.GetKeyDown(TestChat))
        {
            DialogueManager.Instance.StartDialogue(chatData);

        }
    }
}

// public class NPC : MonoBehaviour
// {
//     [SerializeField] private DialogueSO myDialogue;

//     private void OnInteract() // 상호작용 트리거에서 호출
//     {
//         DialogueManager.Instance.StartDialogue(myDialogue);
//     }
// }