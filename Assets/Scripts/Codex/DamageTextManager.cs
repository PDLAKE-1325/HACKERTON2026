using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class DamageTextManager : MonoBehaviour
{
    public static DamageTextManager Instance { get; private set; }

    [Header("Screen Space Overlay UI")]
    [SerializeField] private Canvas overlayCanvas;
    [SerializeField] private Text damageTextPrefab;
    [SerializeField] private Camera worldCamera;

    [Header("Animation")]
    [SerializeField] private float moveDistance = 70f;
    [SerializeField] private float duration = 0.65f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Show(Vector3 worldPosition, float damage, Vector2 knockbackDirection)
    {
        if (overlayCanvas == null || damageTextPrefab == null)
            return;

        Camera cameraToUse = worldCamera != null ? worldCamera : Camera.main;
        if (cameraToUse == null)
            return;

        Vector3 screenPosition = cameraToUse.WorldToScreenPoint(worldPosition);
        if (screenPosition.z < 0f)
            return;

        RectTransform canvasRect = overlayCanvas.transform as RectTransform;
        Camera uiCamera = overlayCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : overlayCanvas.worldCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPosition, uiCamera, out Vector2 localPosition))
            return;

        Text textInstance = Instantiate(damageTextPrefab, overlayCanvas.transform);
        RectTransform textRect = textInstance.rectTransform;
        textRect.anchoredPosition = localPosition;
        textInstance.text = Mathf.Approximately(damage, Mathf.Round(damage))
            ? Mathf.RoundToInt(damage).ToString()
            : damage.ToString("0.##");

        Color color = textInstance.color;
        color.a = 1f;
        textInstance.color = color;

        Vector3 directionEndScreen = cameraToUse.WorldToScreenPoint(
            worldPosition + (Vector3)knockbackDirection.normalized);
        Vector2 screenDirection = ((Vector2)directionEndScreen - (Vector2)screenPosition).normalized;
        if (screenDirection.sqrMagnitude < 0.01f)
            screenDirection = Vector2.up;

        Sequence sequence = DOTween.Sequence().SetUpdate(true);
        sequence.Join(textRect.DOAnchorPos(localPosition + screenDirection * moveDistance, duration)
            .SetEase(Ease.OutQuad));
        sequence.Join(textInstance.DOFade(0f, duration).SetEase(Ease.InQuad));
        sequence.OnComplete(() =>
        {
            if (textInstance != null)
                Destroy(textInstance.gameObject);
        });
    }
}
