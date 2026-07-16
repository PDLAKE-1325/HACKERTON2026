using DG.Tweening;
using UnityEngine;

[RequireComponent(typeof(PlayerMovement))]
public class PlayerDashAfterimage : MonoBehaviour
{
    [Header("Afterimage")]
    [SerializeField] private Color afterimageColor = new Color(0.15f, 0.8f, 1f, 0.55f);
    [SerializeField] private float spawnInterval = 0.035f;
    [SerializeField] private float lifetime = 0.22f;
    [SerializeField] private int spriteSortingOrderOffset = -1;

    private PlayerMovement movement;
    private float nextSpawnTime;
    private bool wasDashing;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
    }

    private void Update()
    {
        if (movement == null || !movement.IsDashing)
        {
            wasDashing = false;
            return;
        }

        if (!wasDashing)
        {
            wasDashing = true;
            nextSpawnTime = Time.time;
        }

        if (Time.time < nextSpawnTime)
            return;

        nextSpawnTime = Time.time + Mathf.Max(0.01f, spawnInterval);
        SpawnAfterimage();
    }

    private void SpawnAfterimage()
    {
        foreach (SpriteRenderer source in GetComponentsInChildren<SpriteRenderer>())
        {
            if (source.enabled && source.gameObject.activeInHierarchy)
                SpawnSpriteGhost(source);
        }

        foreach (MeshRenderer source in GetComponentsInChildren<MeshRenderer>())
        {
            if (source.enabled && source.gameObject.activeInHierarchy)
                SpawnMeshGhost(source);
        }
    }

    private void SpawnSpriteGhost(SpriteRenderer source)
    {
        GameObject ghostObject = new GameObject("DashAfterimage");
        ghostObject.layer = source.gameObject.layer;
        CopyWorldTransform(source.transform, ghostObject.transform);

        SpriteRenderer ghost = ghostObject.AddComponent<SpriteRenderer>();
        ghost.sprite = source.sprite;
        ghost.sharedMaterial = source.sharedMaterial;
        ghost.drawMode = source.drawMode;
        ghost.size = source.size;
        ghost.flipX = source.flipX;
        ghost.flipY = source.flipY;
        ghost.maskInteraction = source.maskInteraction;
        ghost.sortingLayerID = source.sortingLayerID;
        ghost.sortingOrder = source.sortingOrder + spriteSortingOrderOffset;

        Color color = afterimageColor;
        color.a *= source.color.a;
        ghost.color = color;
        ghost.DOFade(0f, Mathf.Max(0.01f, lifetime))
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                if (ghostObject != null)
                    Destroy(ghostObject);
            });
    }

    private void SpawnMeshGhost(MeshRenderer source)
    {
        MeshFilter sourceFilter = source.GetComponent<MeshFilter>();
        if (sourceFilter == null || sourceFilter.sharedMesh == null || source.sharedMaterial == null)
            return;

        GameObject ghostObject = new GameObject("DashAfterimage");
        ghostObject.layer = source.gameObject.layer;
        CopyWorldTransform(source.transform, ghostObject.transform);

        MeshFilter ghostFilter = ghostObject.AddComponent<MeshFilter>();
        ghostFilter.sharedMesh = sourceFilter.sharedMesh;

        MeshRenderer ghostRenderer = ghostObject.AddComponent<MeshRenderer>();
        Material ghostMaterial = new Material(source.sharedMaterial);
        ConfigureTransparentMaterial(ghostMaterial, afterimageColor);
        ghostRenderer.sharedMaterial = ghostMaterial;

        float fadeDuration = Mathf.Max(0.01f, lifetime);
        Color color = afterimageColor;
        DOTween.To(
                () => color.a,
                alpha =>
                {
                    color.a = alpha;
                    SetMaterialColor(ghostMaterial, color);
                },
                0f,
                fadeDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                if (ghostMaterial != null)
                    Destroy(ghostMaterial);
                if (ghostObject != null)
                    Destroy(ghostObject);
            });
    }

    private static void CopyWorldTransform(Transform source, Transform target)
    {
        target.position = source.position;
        target.rotation = source.rotation;
        target.localScale = source.lossyScale;
    }

    private static void ConfigureTransparentMaterial(Material material, Color color)
    {
        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", 5f);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", 10f);

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = 3000;
        SetMaterialColor(material, color);
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }
}
