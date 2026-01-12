using UnityEngine;

public class AreaScript : MonoBehaviour
{
    private MeshRenderer AreaRenderer;
    public WebSocketConnection webSocketConnection;

    private void Start()
    {
        // Check if the MeshRenderer component is missing
        if (!TryGetComponent<MeshRenderer>(out AreaRenderer))
        {
            Debug.LogError("MeshRenderer component is missing on " + gameObject.name);
        }
        webSocketConnection = GameObject.Find("ObjectSpawner").GetComponent<WebSocketConnection>();
    }
    void OnDrawGizmos()
    {
        if (GetComponent<MeshCollider>())
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireMesh(GetComponent<MeshCollider>().sharedMesh, transform.position, transform.rotation, transform.localScale);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // Check if the other collider is null
        if (other == null)
        {
            return;
        }
        // Check if the other collider is the player
        if (other.gameObject.tag == "Player")
        {
            // Check if the MeshRenderer component is missing
            if (AreaRenderer == null)
            {
                return;
            }
            // Change the color of the material
            AreaRenderer.material.color = Color.red;
            // Send a message to the server and the message will be 'flood1, and the current time without seconds like 19:58'
            //webSocketConnection.SendMessageToServer("flood2," + System.DateTime.Now.ToString("HH:mm"));

            webSocketConnection.SendMessageToServer(name + ",17:13");
        }
    }

    void OnTriggerExit(Collider other)
    {
        // Check if the other collider is null
        if (other == null)
        {
            Debug.LogError("Other collider is null");
            return;
        }

        // Check if the other collider is the player
        if (other.gameObject.tag == "Player")
        {
            // Check if the MeshRenderer component is missing
            if (AreaRenderer == null)
            {
                return;
            }

            // Change the color of the material back to its original state
            AreaRenderer.material.color = Color.white;

            // Print a message to the console
            Debug.Log("Player exited the cube area");
        }
    }
}
