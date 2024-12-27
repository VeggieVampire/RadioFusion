using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    private Collider2D currentFrequencyObject;  // Track the frequency object

    void Update()
    {
        // Get input from keyboard or controller (left stick)
        float moveX = Input.GetAxis("Horizontal");
        float moveY = Input.GetAxis("Vertical");

        // Apply movement
        Vector3 movement = new Vector3(moveX, moveY, 0f) * moveSpeed * Time.deltaTime;
        transform.position += movement;

        // Delete FrequencyObject on Fire1 (A button on Xbox controller)
        if (Input.GetButtonDown("Fire1") && currentFrequencyObject != null)
        {
            Debug.Log($"Deleting {currentFrequencyObject.gameObject.name}");
            Destroy(currentFrequencyObject.gameObject);
            currentFrequencyObject = null;  // Clear reference after deletion
        }
    }

    // Detect player entering a frequency object
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("FrequencyObject"))
        {
            Debug.Log("Player collided with frequency object!");

            currentFrequencyObject = other;  // Store reference to the object

            FrequencyScanner scanner = FindObjectOfType<FrequencyScanner>();
            if (scanner != null)
            {
                scanner.PauseScanning(true);  // Enable audio
            }
        }
    }

    // Detect player leaving a frequency object
    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("FrequencyObject"))
        {
            Debug.Log("Player exited frequency object!");

            if (other == currentFrequencyObject)
            {
                currentFrequencyObject = null;  // Clear reference when leaving
            }

            FrequencyScanner scanner = FindObjectOfType<FrequencyScanner>();
            if (scanner != null)
            {
                scanner.PauseScanning(false);  // Disable audio
            }
        }
    }
}
