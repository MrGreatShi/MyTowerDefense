using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    float minX = 4f;
    float maxX = 13f;
    float minZ = -2f;
    float maxZ = 1f;
    float posY = 7f;
    float rataX = 0.3f;
    float rateZ = 0.05f;
    public Camera cam;

    void Start()
    {
        this.transform.position = new Vector3(minX, posY, minZ);
        if (cam == null)
        {
            cam = Camera.main;
        }    
        cam.transform.position = new Vector3(minX, posY, minZ);
    }
    // Update is called once per frame
    void FixedUpdate()
    {
        if (Input.GetKey(KeyCode.A))  //摄像机向左移动
        {
            Vector3 newPos = cam.transform.position;
            newPos.x = Mathf.Max(newPos.x - rataX, minX);
            cam.transform.position = newPos;
        }
        else if (Input.GetKey(KeyCode.D))
        { 
            Vector3 newPos = cam.transform.position;
            newPos.x = Mathf.Min(newPos.x + rataX, maxX);
            cam.transform.position = newPos;
        }
        else if (Input.GetKey(KeyCode.W))
        {
            Vector3 newPos = cam.transform.position;
            newPos.z = Mathf.Min(newPos.z + rateZ, maxZ);
            cam.transform.position = newPos;
        }
        else if (Input.GetKey(KeyCode.S))
        {
            Vector3 newPos = cam.transform.position;
            newPos.z = Mathf.Max(newPos.z - rateZ, minZ);
            cam.transform.position = newPos;
        }
    }
}
