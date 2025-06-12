using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class GlobalGazeInfo
{
    public static Vector3 GazeOrigin = Vector3.zero;
    public static Vector3 GazeDirection = Vector3.zero;

    public static Vector3 GazeOriginRight = Vector3.zero;
    public static Vector3 GazeOriginUp = Vector3.zero;
    public static Vector3 GazeOriginForward = Vector3.zero;

    public static Vector3 LeftGazeOrigin = Vector3.zero;
    public static Vector3 LeftGazeDirection = Vector3.zero;

    public static Vector3 RightGazeOrigin = Vector3.zero;
    public static Vector3 RightGazeDirection = Vector3.zero;

    public static bool GazeRayCast(out RaycastHit hit, int layer, float maxDistance = Mathf.Infinity)
    {
        return Physics.Raycast(GazeOrigin, GazeDirection, out hit, maxDistance, layer);
    }
    
    public static bool GazeRayCast(out RaycastHit hit, float maxDistance = Mathf.Infinity)
    {
        return Physics.Raycast(GazeOrigin, GazeDirection, out hit, maxDistance);
    }
}
