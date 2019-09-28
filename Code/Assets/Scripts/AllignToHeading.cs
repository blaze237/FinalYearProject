using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AllignToHeading : MonoBehaviour {
    public Camera cam;
    public float yOffset = 0.16f;
    public TranslationalGain gainSystem;


	void FixedUpdate ()
    {
        Vector3 direction = gainSystem.getUserHeading();
        direction.y = 0;

        transform.position = cam.transform.position + new Vector3(0, yOffset, 0);// + direction*0.2f;

        transform.right = -direction;
        transform.eulerAngles = new Vector3(90, transform.eulerAngles.y, transform.eulerAngles.z);

    }
}
