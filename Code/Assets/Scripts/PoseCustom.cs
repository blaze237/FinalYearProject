using UnityEngine;
using Valve.VR;

//Custom subclass of steam vr tracked controller in which node position is set relative to the hmd
public class PoseCustom : SteamVR_Behaviour_Pose
{

    override protected void Update()
    {
        if (poseAction == null)
            return;

        CheckDeviceIndex();

        //Get position of hmd and this controller node relative to the ground
        Vector3 hmdPos = UnityEngine.XR.InputTracking.GetLocalPosition(UnityEngine.XR.XRNode.Head);
        Vector3 controllerPos = poseAction.GetLocalPosition(inputSource);
        //Get relative distance vector between the two
        Vector3 diff = hmdPos - controllerPos;

        //Apply distance offset to the position of the hmd within the virtual scene.
        transform.position = origin.transform.position - diff;
        transform.rotation = poseAction.GetLocalRotation(inputSource);
    

        if (poseAction.GetChanged(inputSource))
        {
            if (onTransformChanged != null)
                onTransformChanged.Invoke(poseAction);
        }

        if (onTransformUpdated != null)
            onTransformUpdated.Invoke(poseAction);
    }

}
