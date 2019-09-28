using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.XR;

public class TranslationalGain : MonoBehaviour
{
    //The size of the sliding window(in seconds) to use for caching direction data
    private const float DIRECTION_WINDOW_SIZE = 1f;

    public enum GainMode
    {
        Uniform, Directed
    }

    [Tooltip("Use inspector settings and ignore config files")]
    public bool debug = true;

    [Tooltip("Heading indicator mesh displayed in debug mode")]
    public GameObject arrow;

    [Tooltip("The camera of the VR rig.")]
    [SerializeField]
    private Camera cam;

    [Tooltip("The type of gain to apply")]
    [SerializeField]
    private GainMode mode = GainMode.Directed;

    [Tooltip("The maximum amount of gain to apply to movement")]
    [SerializeField]
    private float gainLevel = 2;

    [Tooltip("Gain applied to movement with instantaneous speed in the x-z plane less than cuttoff is determined via exonential curve. Above threshold maxgain is applied")]
    [SerializeField]
    private float rampingCutoffSpeed = 0.5f;

    [Tooltip("If instantaneous xz speed is below this cutoff, then switch over to uniform gain")]
    [SerializeField]
    private float uniformCuttoffSpeed = 0.45f;

    [Tooltip("If instantaneous xz speed is below this cutoff, then consider any direction data to be unreliable and discard it")]
    [SerializeField]
    private float driectionCuttoffSpeed = 0.15f;

    [Tooltip("If calculated heading is within this many degrees of gaze direction then clamp heading to gaze direction.")]
    [SerializeField]
    private float gazeClampAngle = 15;

    [Tooltip("If camera view direction changes by greater than this many degrees between two consectutive frames, then consider directional data to be invalid and discard it.")]
    [SerializeField]
    private float sharpAngleDelta = 2;

    [Tooltip("Should gain be temporarily disabled when sharp angle changes in heading detected.")]
    [SerializeField]
    private bool disableGainOnSharpAngle = true;


    //Previous ticks cam view direction. Used in sharp angle detection
    private Vector3 prevViewDir;
    //Real world position of the user as measured in the previous tick. Used for measuring position deltas and applying gain accordingly.
    private Vector3 prevPosition;
    //Constants used in exponential gain ramping function. These are calculated once in start based on the rampingCuttoffVelocity and gainLevel values. 
    private float RAMPING_CONST_A, RAMPING_CONST_B;

    //Sliding windows used for collection of movement data
    private SlidingWindow<Vector3> directionWindow;

    //Cache what mode was on at start, enables us to turn directional mode on and off accoriding to cuttoff
    private GainMode initialMode;


    /**
     * Load in settings from config files.
     **/
    private void Awake()
    {
        //Dont load settings from config files in debug mode
        if (debug)
        {
            initialMode = mode;
            return;
        }

        //Get gain level from file
        StreamReader sr = new StreamReader(Application.dataPath + "/gainLevel.txt");
        string line;
        if ((line = sr.ReadLine()) != null)
        {
            gainLevel = float.Parse(line);
        }
        else
        {
            Debug.Log("CANT READ");
            gainLevel = 1;
        }

        sr.Close();


        //Get gain mode from file
        sr = new StreamReader(Application.dataPath + "/mode.txt");
        if ((line = sr.ReadLine()) != null)
        {
            switch (line)
            {
                case "d":
                    mode = GainMode.Directed;
                    break;
                case "u":
                    mode = GainMode.Uniform;
                    break;
                default:
                    gainLevel = 1;
                    break;
            }
        }
        else
        {
            Debug.Log("CANT READ");
            gainLevel = 1;
        }

        sr.Close();

        initialMode = mode;
    }


    /**
     * Turn of built in tracking and set up ramping function 
     **/
    void Start()
    {
        //Disable built in position tracking
        InputTracking.disablePositionalTracking = true;
        //Make sure cam is in center of play space
        cam.transform.localPosition = new Vector3(0, 0, 0);
        //Create sliding windows
        directionWindow = new SlidingWindow<Vector3>(Time.fixedDeltaTime, DIRECTION_WINDOW_SIZE);

        //Calculate the constants for the ramping function
        calculateRampingFunction();

        prevViewDir = cam.transform.forward;

        prevPosition = InputTracking.GetLocalPosition(XRNode.Head);
        transform.position = prevPosition;

    }

    public void setOrigin(Vector3 origin)
    {
        transform.position = Vector3.zero;
        prevPosition = origin;
    }

    Vector3 hmdVelocity;
    void Update()
    {
        //Grab the current velocity of the headset
         hmdVelocity = getHMDVel();
        float ySpeed = Mathf.Abs(hmdVelocity.y);
        hmdVelocity.y = 0;
        float hmdSpeed = new Vector2(hmdVelocity.x, hmdVelocity.z).magnitude;

        Vector3 hmdV = new Vector3(hmdVelocity.x, 0, hmdVelocity.z).normalized;

        //Initialise heading to be cam view direction
        Vector3 heading = cam.transform.forward;
        heading.y = 0;

        //When a sharp change in view direction occurs, this indicates two things [1] Circular motion is occuring, [2] The user has likley changed their direction of regard.  In either case, all prior direction data would no longer be valid, and so is discarded
        bool sharpAngleDetected = false;
        if (Mathf.Abs(Vector3.SignedAngle(prevViewDir, hmdV, Vector3.up)) > sharpAngleDelta)
        {
            directionWindow.reset();
            sharpAngleDetected = true;
        }
        //Cache the cam direction of this frame to be compared against next tick
        prevViewDir = heading;

        //Push current user velocity to the sliding window for direction calculation in subsequent frames
        directionWindow.push(hmdVelocity);

        //In directed mode, we enable and disable directional gain according to user speed. 
        //At very low speeds we consider it not possible to accuratley determing heading over noise data, and so switch to uniform mode.
        if (initialMode == GainMode.Directed)
        {
            //At super low speeds, consider any directional data to be noise and delete it
            if (hmdSpeed < driectionCuttoffSpeed)
            {
                directionWindow.reset();
            }
            //Only use calcualted heading if we havent reset the windows, else just stick with cam forward dir set prior
            else
                heading =  getUserHeading();

            //At slow speeds, use only uniform mode
            if (hmdSpeed < uniformCuttoffSpeed)
                mode = GainMode.Uniform;
            else
                mode = GainMode.Directed;
        }

        //Amount of gain to apply is some multiple of base gain level as determined by the ramping function
        float curScale = gainLevel;
        //If speed is below ramping cuttoff, then the gain is a function of user speed. For no gain (level=1) skip this, as ramping function will give NAN results
        if (hmdSpeed < rampingCutoffSpeed && gainLevel != 1)
            curScale = RAMPING_CONST_A * Mathf.Exp(RAMPING_CONST_B * hmdSpeed) + 1;


        //Check to see if y component of headset velocity exceeds a cuttoff, if it does, then motion is determined to be a result of user crouching, and as such no gain should be applied to this motion. 
        if (ySpeed > 0.5f)
            curScale = 1;

        //If sharp angle was detected and the relevant flag is set to true then disable gain for this tick
        if (sharpAngleDetected && disableGainOnSharpAngle)
            curScale = 1;

        //Scaling is only applied in the xz plane to avoid exagerrated head bob.
        Vector3 scaling = new Vector3(curScale, 0, curScale);

        //If using directed gain then need to apply direction multipliers to scaling vector
        if (mode != GainMode.Uniform)
        {
            //Get directional multiplier to apply to scaling vector. This is the Abs direction projected into the xz plane. 
            Vector3 directionMult = new Vector3(Mathf.Abs(heading.x), 1, Mathf.Abs(heading.z));

            //Multiplier the scaling vector by the directional multiplier vector
            scaling = Vector3.Scale(scaling, directionMult);

            //Only ever want scaling to be additive, dont want to reduce movement along non direction axis
            scaling.x = Mathf.Max(1, scaling.x);
            scaling.z = Mathf.Max(1, scaling.z);
        }

        //Calculate the base amount of movement in the real world
        Vector3 curPos = InputTracking.GetLocalPosition(XRNode.Head);
        Vector3 delta = curPos - prevPosition;
        prevPosition = curPos;

        //Apply scaling to this movement delta and apply result of this to our position in the VE.
        Vector3 newPos = transform.position + Vector3.Scale(scaling, delta);
        //No scaling in y dir
        newPos.y = curPos.y;
        transform.position = newPos;
    }



    //(Try) to get velocity of head node. If cannot get velocity for the node then it is set to Vector3.positiveInfinity
    public Vector3 getHMDVel()
    {
        //Grab all XR nodes
        List<XRNodeState> nodeStates = new List<XRNodeState>();
        InputTracking.GetNodeStates(nodeStates);

        //Temp vars used to store velocities for each node
        Vector3 hmdVel = Vector3.positiveInfinity;

        //Find the head node and get its velocity
        foreach (XRNodeState ns in nodeStates)
        {
            if (ns.nodeType == XRNode.Head)
                ns.TryGetVelocity(out hmdVel);
        }

        return hmdVel;
    }


    //Solve function of the form y = A(e^(Bx)) + 1 to determine ramping constants
    private void calculateRampingFunction()
    {
        //Simulataneous equations are solved using this point and the cuttof point (RAMPING_CUTTOFF, scaling)
        Vector2 REFERENCE_POINT = new Vector2(rampingCutoffSpeed * 0.5f, 1.0016f);

        //Eqn 1 : scale-1 = A*e^(CUTTOFF*B)
        float LHS_eqn1 = gainLevel - 1;
        //Eqn 2: REFERENCE_POINT.y -1 = A*e^(REFERENCE_POINT.x*B)
        float LHS_eqn2 = REFERENCE_POINT.y - 1;

        //Divide eqn1 by eqn2
        float LHS_sim1 = LHS_eqn1 / LHS_eqn2;

        //After some simplification and re-aranging, B can then be calculated as ln(LHS_sim1) / (CUTTOFF - REFERENCE_POINT.x)
        RAMPING_CONST_B = Mathf.Log(LHS_sim1) / (rampingCutoffSpeed - REFERENCE_POINT.x);

        //Now we have B, can simply sub in for A 
        RAMPING_CONST_A = (gainLevel - 1) / Mathf.Exp(rampingCutoffSpeed * RAMPING_CONST_B);
    }


    //Get user heading as a unit vector.
    public Vector3 getUserHeading()
    {
        //Get the average heading based on velocity
        Vector3 avgDir = calcAvgDir();
        Vector3 hmdV = new Vector3(hmdVelocity.x, 0, hmdVelocity.z).normalized;

        //Get the angle between heading and gaze direction.
        float angle = Vector3.Angle(MathTools.absVector(avgDir), MathTools.absVector(cam.transform.forward));
        //If angle is small, then clamp user heading to be equal to gaze direction to give more stable heading
        if (angle <= gazeClampAngle)
            return cam.transform.forward;

        return avgDir;
    }



    private Vector3 calcAvgDir()
    {
        //Grab the current velocity values stored in the window
        Queue<Vector3> values = directionWindow.getValues();

        //Get the sum of all velocities in the window
        Vector3 sum = new Vector3(0, 0, 0);
        foreach (Vector3 v in values)
            sum += v;


        //Get the normalised mean
        sum = (sum / values.Count).normalized;

        return sum;
    }

    /****** Getters and Setters. ******/

    public GainMode getMode()
    {
        return mode;
    }
    public void setGainMode(GainMode m)
    {
        mode = m;
    }

    public float getGainLevel()
    {
        return gainLevel;
    }
    public void setGainLevel(float l)
    {
        float tmp = gainLevel;
        gainLevel = l;

        //If gainlevel has been altered then need to recalculate the constants for the ramping function
        if (tmp != gainLevel)
            calculateRampingFunction();
    }

    public float getRampingCutoff()
    {
        return rampingCutoffSpeed;
    }
    public void setRampingCutoff(float f)
    {
        float tmp = rampingCutoffSpeed;
        rampingCutoffSpeed = f;

        //If rampingCutoffSpeed has been altered then need to recalculate the constants for the ramping function
        if (rampingCutoffSpeed != tmp)
            calculateRampingFunction();
    }

    private float getGazeClampAngle()
    {
        return gazeClampAngle;
    }
    private void setGazeClampAngle(float f)
    {
        gazeClampAngle = f;
    }

}