using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance { get; private set; }

    [SerializeField] private CinemachineCamera initialCamera;

    private CinemachineCamera activeCamera;
    private Coroutine shakeCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        activeCamera = initialCamera;
        if (activeCamera != null)
            activeCamera.gameObject.SetActive(true);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ActivateCamera(CinemachineCamera nextCamera)
    {
        if (nextCamera == null || nextCamera == activeCamera)
            return;

        if (activeCamera != null)
            activeCamera.gameObject.SetActive(false);

        nextCamera.gameObject.SetActive(true);
        activeCamera = nextCamera;
    }

    public void Shake(float intensity, float duration)
    {
        if (activeCamera == null || intensity <= 0f || duration <= 0f)
            return;

        CinemachineBasicMultiChannelPerlin noise =
            activeCamera.GetComponent<CinemachineBasicMultiChannelPerlin>();
        if (noise == null)
            return;

        if (shakeCoroutine != null)
            StopCoroutine(shakeCoroutine);

        shakeCoroutine = StartCoroutine(ShakeRoutine(noise, intensity, duration));
    }

    private IEnumerator ShakeRoutine(
        CinemachineBasicMultiChannelPerlin noise,
        float intensity,
        float duration)
    {
        noise.AmplitudeGain = intensity;
        yield return new WaitForSecondsRealtime(duration);

        if (noise != null)
            noise.AmplitudeGain = 0f;

        shakeCoroutine = null;
    }
}
