using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PulseGlow : MonoBehaviour {

    public Color glowColor;
    public float pulseDuration = 1;
    public bool startOn = true;

    private bool pulseEnabled = true;

    private float t;
    private bool increasing = true;


    private void Awake()
    {
        if (startOn)
        {
            //Make sure emission is enabled
            GetComponent<Renderer>().material.EnableKeyword("_EMISSION");
        }

        pulseEnabled = startOn;
    }

    public void enablePulse()
    {
        pulseEnabled = true;

        //Make sure emission is enabled
        GetComponent<Renderer>().material.EnableKeyword("_EMISSION");
    }

    public void disablePulse()
    {

        pulseEnabled = false;
        GetComponent<Renderer>().material.DisableKeyword("_EMISSION");
    }




    // Update is called once per frame
    void Update ()
    {
        if (!pulseEnabled)  
            return;
        
        //Lerp our emission colour to some intermediary value
        Color col = Color.Lerp(Color.black, glowColor, t);
        GetComponent<Renderer>().material.SetColor("_EmissionColor", col); 

        //Update our timer
        if (increasing)
        {
            if (t < 1)
                t += Time.deltaTime / pulseDuration;
            else
            {
                increasing = false;
                t = 1;
            }
        }
        else
        {
            if (t > 0)
                t -= Time.deltaTime / pulseDuration;
            else
            {
                increasing = true;
                t = 0;
            }
        }
    }
}
