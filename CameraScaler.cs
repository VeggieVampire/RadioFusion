using UnityEngine;

public class CameraScaler : MonoBehaviour
{
    public LineRenderer lineRenderer;

    void Start()
    {
        Camera.main.orthographic = true;
        float length = lineRenderer.positionCount;

        // Adjust the camera size based on line length
        Camera.main.orthographicSize = Mathf.Max(5, length / 40);
        Camera.main.transform.position = new Vector3(0, 0, -10);
    }
}

