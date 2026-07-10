using UnityEngine;

[ExecuteAlways]
public class FitSpriteToCamera : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private float extraScale = 1.02f;

    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void LateUpdate()
    {
        FitToCamera();
    }

    private void FitToCamera()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        Camera cam = targetCamera != null ? targetCamera : Camera.main;

        if (cam == null || spriteRenderer == null || spriteRenderer.sprite == null)
            return;

        if (!cam.orthographic)
            return;

        float cameraHeight = cam.orthographicSize * 2f;
        float cameraWidth = cameraHeight * cam.aspect;

        Vector2 spriteSize = spriteRenderer.sprite.bounds.size;

        float scaleX = cameraWidth / spriteSize.x;
        float scaleY = cameraHeight / spriteSize.y;

        transform.localScale = new Vector3(
            scaleX * extraScale,
            scaleY * extraScale,
            1f
        );
    }
}