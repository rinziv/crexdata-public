using UnityEngine;

public class RotateUserPointer : MonoBehaviour
{
    
    private float timer = 0;

    // Update is called once per frame
    void Update()
    {
        timer += Time.deltaTime;

        if(timer >= 0.25f){
            
            Vector3 currentLocalRotation = this.transform.localRotation.eulerAngles;
            currentLocalRotation.z = -Camera.main.transform.rotation.eulerAngles.y;
            this.transform.localRotation = Quaternion.Euler(currentLocalRotation);

            timer = 0;
        }
    }
}
