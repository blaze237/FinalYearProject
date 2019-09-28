using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StickToCam : MonoBehaviour {

    public Camera cam;



    void FixedUpdate()
    {
      

        transform.position = cam.transform.position;

    }
}
