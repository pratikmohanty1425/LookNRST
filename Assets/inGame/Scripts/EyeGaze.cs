using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DefaultExecutionOrder(-2)]
public class EyeGaze : MonoBehaviour
{
    [SerializeField] protected Transform LeftEye;
    [SerializeField] protected Transform RightEye;
    [SerializeField] protected Transform CenterEye;

    protected OneEuroFilter<Vector3> _gazeDirFilter;
    [HideInInspector] public Vector3 CombineGazeDirection;
    [HideInInspector] public Vector3 CombineGazeOrigin;
    public bool useMouseAsGaze;


    [Header("1€ Filter On Gaze")]
    public bool use1EuroFilter;
    public float filterFreq;
    public float filterMinCutoff;
    public float filterBeta;
    public float filterDCutoff;

    [Header("Fixation Filtering")]
    public bool useFixationFilter;
    public float fixationAngle;
    public float fixationTime;
    private Queue<Vector3> previousGazeQueue;
    private Vector3? lockedGazeDirection = null;
    private Vector3? lockedGazePosition = null;
    [HideInInspector] public bool snappedGaze;



    protected virtual void Awake()
    {
        _gazeDirFilter = new OneEuroFilter<Vector3>(filterFreq, filterMinCutoff, filterBeta, filterDCutoff);
    }
    
    
    protected virtual void Start()
    {
        previousGazeQueue = new Queue<Vector3>();
    }

    protected virtual void Update()
    {
        _gazeDirFilter.UpdateParams(filterFreq, filterMinCutoff, filterBeta, filterDCutoff);

        CombineGazeOrigin = (LeftEye.transform.position + RightEye.transform.position) / 2.0f;

        if (useMouseAsGaze) // TODO: move to DevelopmentTools.cs
        {
            if (Input.GetMouseButton(0))
            {
                Vector3 mousePos = Input.mousePosition;
                Ray ray = Camera.main.ScreenPointToRay(mousePos);
                CombineGazeDirection = ray.direction;
            }
            else if (Input.GetMouseButton(1))
            {
                CombineGazeDirection = Camera.main.transform.forward;
            }
        }
        else
        {
            CombineGazeDirection = Quaternion.Lerp(LeftEye.transform.rotation, RightEye.transform.rotation, 0.5f) * Vector3.forward;
        }

        snappedGaze = false;
        if (useFixationFilter)
        {
            (snappedGaze, CombineGazeOrigin, CombineGazeDirection) = FixationFilter(CombineGazeOrigin, CombineGazeDirection);

            if (snappedGaze)
                _gazeDirFilter = new OneEuroFilter<Vector3>(filterFreq, filterMinCutoff, filterBeta, filterDCutoff);
        }

        if (use1EuroFilter && !snappedGaze)
            CombineGazeDirection = _gazeDirFilter.Filter(CombineGazeDirection);


        GlobalGazeInfo.GazeOrigin = CombineGazeOrigin;
        GlobalGazeInfo.GazeDirection = CombineGazeDirection;

        GlobalGazeInfo.GazeOriginUp = CenterEye.transform.up;
        GlobalGazeInfo.GazeOriginRight = CenterEye.transform.right;
        GlobalGazeInfo.GazeOriginForward = CenterEye.transform.forward;

        GlobalGazeInfo.LeftGazeOrigin = LeftEye.transform.position;
        GlobalGazeInfo.LeftGazeDirection = LeftEye.transform.forward;

        GlobalGazeInfo.RightGazeOrigin = RightEye.transform.position;
        GlobalGazeInfo.RightGazeDirection = RightEye.transform.forward;
    }


    protected (bool, Vector3, Vector3) FixationFilter(Vector3 gazePosition, Vector3 gazeDirection)
    {
        float frameRate = 1.0f / Time.deltaTime;
        int maxQueueSize = Mathf.CeilToInt(fixationTime * frameRate);

        previousGazeQueue.Enqueue(gazeDirection);

        // Remove from queue if older than fixationTime
        while (maxQueueSize > 0 && previousGazeQueue.Count > maxQueueSize)
            previousGazeQueue.Dequeue();

        bool snappedGaze = false;

        if (previousGazeQueue.Count != 0)
        {
            float angle = Vector3.Angle(previousGazeQueue.Peek(), gazeDirection);
            if (angle >= fixationAngle)
            {
                if (lockedGazeDirection == null)
                {
                    lockedGazeDirection = previousGazeQueue.Last();
                    lockedGazePosition = gazePosition;
                }

                return (false, lockedGazePosition.Value, lockedGazeDirection.Value);

            }
            else
            {
                if(lockedGazeDirection != null)
                {
                    snappedGaze = true;
                }

                lockedGazeDirection = null;
                lockedGazePosition = null;
            }

        }

        return (snappedGaze, gazePosition, gazeDirection);
    }


}
