using UnityEngine;

public class FrequencyObject : MonoBehaviour
{
    public float frequency;
    private float minDistance = 6.0f;  // Minimum spacing
    private int maxAttempts = 50;
    private float baseOffset = 8f;  // Start with a larger offset
    private bool isResolvingOverlap = false;
    private SpriteRenderer spriteRenderer;
    private FrequencyScanner scanner;  // Reference to FrequencyScanner for audio control

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        ApplyFrequencyColor();
        AvoidOverlap();
    }

    // Set reference to FrequencyScanner (from scanner when spawned)
    public void SetScanner(FrequencyScanner scannerRef)
    {
        scanner = scannerRef;
    }

    void FixedUpdate()
    {
        if (IsOverlapping() && !isResolvingOverlap)
        {
            AvoidOverlap();
        }
    }

    // Trigger when the player enters the frequency object
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && scanner != null)
        {
            Debug.Log($"Player entered frequency object at {frequency / 1e6f} MHz.");
            scanner.PauseScanning(true);  // Enable audio
        }

        if (other.CompareTag("FrequencyObject") && other.gameObject != gameObject)
        {
            Debug.Log($"Overlap detected with {other.gameObject.name}, repositioning...");
            AvoidOverlap();
        }
    }

    // Trigger when the player exits the frequency object
    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player") && scanner != null)
        {
            Debug.Log($"Player exited frequency object at {frequency / 1e6f} MHz.");
            scanner.PauseScanning(false);  // Disable audio
        }
    }

    // Reposition the object until no overlap is detected
    void AvoidOverlap()
    {
        int attempts = 0;
        float currentOffset = baseOffset;
        isResolvingOverlap = true;

        while (IsOverlapping() && attempts < maxAttempts)
        {
            Vector3 offset = new Vector3(
                Random.Range(-currentOffset, currentOffset),
                Random.Range(-currentOffset, currentOffset),
                0f
            );

            transform.position += offset;
            attempts++;

            // Increase offset gradually to avoid persistent overlap
            currentOffset *= 1.3f;

            Debug.Log($"Reposition attempt {attempts}: {transform.position}");

            // Slight rotation to avoid alignment issues
            transform.Rotate(0, 0, Random.Range(15f, 60f));
        }

        if (attempts >= maxAttempts)
        {
            Debug.LogWarning($"Failed to reposition after {maxAttempts} attempts. Destroying object.");
            Destroy(gameObject);  // Destroy if overlap cannot be resolved
        }
        else
        {
            Debug.Log($"Object repositioned to {transform.position}");
        }

        isResolvingOverlap = false;
    }

    // Check for overlap with other FrequencyObjects
    bool IsOverlapping()
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, minDistance);
        foreach (var collider in colliders)
        {
            if (collider.CompareTag("FrequencyObject") && collider.gameObject != gameObject)
            {
                Debug.Log($"Still overlapping with {collider.gameObject.name}");
                return true;
            }
        }
        return false;
    }

    // Map frequency to color for visualization
    void ApplyFrequencyColor()
    {
        if (spriteRenderer == null) return;

        // Normalize frequency to a value between 0 and 1
        float normalizedFrequency = Mathf.InverseLerp(88000000f, 108000000f, frequency);
        Color color = Color.Lerp(Color.blue, Color.red, normalizedFrequency);
        spriteRenderer.color = color;
    }

    // Visualize overlap detection area in the Scene view
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, minDistance);
    }
}
