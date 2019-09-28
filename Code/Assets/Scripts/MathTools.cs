using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class MathTools
{ 
    public static Vector3 absVector(Vector3 vector)
    {
        return new Vector3(Mathf.Abs(vector.x), Mathf.Abs(vector.y), Mathf.Abs(vector.z));
    }
}
