using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class CameraController : MonoBehaviour
{

    public GameObject topWall;

    Vector3 topLeftBoundary = new Vector3(-45.0f+0.8f, 48.5f+0.8f, 0);
    Vector3 bottomRightBoundary = new Vector3(64.0f, -29.5f, 0);

    // Start is called before the first frame update
    void Start()
    {
        CalculateCameraSize();
    }

    /**
    * OrthographicSize defines the viewing volume of an orthographic camera. 
    * The orthographicSize is half the size of the vertical(!) viewing 
    * volume. The horizontal size of the viewing volume depends on the aspect ratio.
    **/
    void CalculateCameraSize()
    {
        // Calculate the distance between top-left and bottom-right boundaries
        float width = Mathf.Abs(topLeftBoundary.x - bottomRightBoundary.x);
        float height = Mathf.Abs(topLeftBoundary.y - bottomRightBoundary.y);

        float aspectRatio = Camera.main.aspect;

        Debug.Log("Aspect Ratio: " + aspectRatio);
        Debug.Log("boundary horizontal distance: " + width);
        Debug.Log("boundary vertical distance: " + height);

        float orthographicSize = 0f;
        if (Camera.main.pixelWidth > Camera.main.pixelHeight) {
            // Screen is wider than it's height
            orthographicSize = height / 2.0f;
        } else {
            orthographicSize = width / 2.0f / aspectRatio;
        }
        // Calculate the orthographic size based on the larger dimension (width or height)
        //float orthographicSize = Mathf.Max(width / 2.0f, height / 2.0f) / aspectRatio;
        
        // Set the orthographic size of the camera
        Debug.Log("orthographicSize: " + orthographicSize);
        Camera.main.orthographicSize = orthographicSize;

        float horizontalCameraDisplayVolume = orthographicSize * 2 * aspectRatio;
        Debug.Log("horizontalCameraDisplayVolume: " + horizontalCameraDisplayVolume);

        SpriteRenderer topwallSpriteRenderer = topWall.GetComponent<SpriteRenderer>();
        float topWallWidth = topwallSpriteRenderer.bounds.size.x;
        float horizontalOffset = horizontalCameraDisplayVolume - topWallWidth;

        // Calculate the position to align the top-left corner of the camera view with the top-left boundary
        Vector3 cameraPosition = new Vector3( 
            topWall.transform.position.x - horizontalOffset/2,
            topWall.transform.position.y - (height / 2),
            Camera.main.transform.position.z
        );

        Camera.main.transform.position = cameraPosition;
    }
}
