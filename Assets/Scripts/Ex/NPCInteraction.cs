using UnityEngine;

public class NPCInteraction : MonoBehaviour
{
    [SerializeField] DialogueSO chatData;
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private GameObject interactionUI;

    [Header("Settings")]
    [SerializeField] private float interactionRange = 2f;

    private bool _isPlayerInRange;

    private void Update()
    {
        if (player == null)
            return;

        float distance = Vector2.Distance(transform.position, player.position);

        _isPlayerInRange = distance <= interactionRange;

        if (interactionUI != null)
            interactionUI.SetActive(_isPlayerInRange);

        if (_isPlayerInRange && Input.GetKeyDown(KeyCode.F))
        {
            DialogueManager.Instance.StartDialogue(chatData);
        }
    }


#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
#endif
}