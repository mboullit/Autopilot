using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System;

public class MLAgentDrive : Agent
{
    [Tooltip("Maximum steering angle of the wheels")]
    public float maxAngle = 30f;
    [Tooltip("Maximum torque applied to the driving wheels")]
    public float maxTorque = 300f;
    [Tooltip("Maximum brake torque applied to the driving wheels")]
    public float brakeTorque = 30000f;
    [Tooltip("If you need the visual wheels to be attached automatically, drag the wheel shape here.")]
    public GameObject wheelShape;

    [Tooltip("The vehicle's speed when the physics engine can use different amount of sub-steps (in m/s).")]
    public float criticalSpeed = 5f;
    [Tooltip("Simulation sub-steps when the speed is above critical.")]
    public int stepsBelow = 5;
    [Tooltip("Simulation sub-steps when the speed is below critical.")]
    public int stepsAbove = 1;

    [Tooltip("The vehicle's drive type: rear-wheels drive, front-wheels drive or all-wheels drive.")]
    public DriveType driveType;

    private WheelCollider[] m_Wheels;
    // The rendertexture of the car camera
    public RenderTexture carcamera;
    // The rigidbody of the car. Used to access to its speed 
    private Rigidbody rb;

    private Transform tr;

    // Width and height of the image captured by the car camera
    private const int SIZEVIEW = 256;
    // The pixel array for the image captured
    private int[] pixels;
    // The texture captured by the car camera
    private Texture2D tex;

    // The y of the line of pixel observed, in the array of pixels. Its equation is y=LINE1
    private const int LINE1 = SIZEVIEW * SIZEVIEW / 2;
    // x of leftmost pixel in the line when the car is in the middle of the lane
    private const int LEFTSEGMENT = 17;
    // Size of the lane for the observed line
    private const int LANEWIDTH = 102 - 17;

    // Var used to initiate a strategy of slam
    Vector3 initial_pos;
    DateTime startEpTime;
    Vector3 start_pos;
    List<float> rotations = new List<float>();
    List<float> distances = new List<float>();
    float last_rot = 0;

    // Called once, at initialization of the scene
    void Start()
    {
        m_Wheels = GetComponentsInChildren<WheelCollider>();
        // Create wheel shapes only when needed.
        initial_pos = this.transform.localPosition;
        for (int i = 0; i < m_Wheels.Length; ++i)
        {
            var wheel = m_Wheels[i];
            if (wheelShape != null)
            {
                var ws = Instantiate(wheelShape);
                ws.transform.parent = wheel.transform;
            }
        }

        // Initialize texture and pixel array
        tex = new Texture2D(SIZEVIEW, SIZEVIEW);
        pixels = new int[SIZEVIEW * SIZEVIEW];
    }
    public int left = -1; // where is the left border of the lane
    public int gear = 0;// gear= 1: slow speed, gear = 2: high speed
    public const float SPEED1 = 3f, SPEED2 = 7f;


    public override void OnEpisodeBegin()
    {
        // analyze_rotations();
        // Find the rigidbody of the car
        rb = GetComponent<Rigidbody>();
        tr = GetComponent<Transform>();
        // Find all the WheelColliders down in the hierarchy.
        this.transform.localPosition = initial_pos;
        start_pos = tr.localPosition;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.Sleep();
        tr.rotation = Quaternion.identity;
        startEpTime = DateTime.Now;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Observation = Speed + Rotation
        sensor.AddObservation(rb.velocity.normalized);
        sensor.AddObservation(tr.rotation);
    }

    // Function not used, for SLAM.
    public void analyze_rotations()
    {
        int i = 0;
        float err = 15.0f;
        float dist = 0;
        List<int> angles = new List<int> { 0, 45, 90, 135, 180, 225, 270, 315, 360};
        List<string> map = new List<string>();
        while (i < rotations.Count)
        {
            int ang = 0;
            dist = 0;
            Boolean found = false;
            foreach (int angle in angles)
            {
                while (i < rotations.Count && rotations[i] < ((angle + err) % 360) && rotations[i] > ((angle - err) % 360))
                {
                    found = true;
                    dist += distances[i];
                    i += 1;
                    ang = angle;
                }
            }
            if (!found)
                i += 1;
            else
            {
                Debug.Log(String.Format("Angle {0}, segments: {1}\n", ang, dist / 7));
                Debug.Log(rotations[i]);
                Debug.Log((ang - err) % 360);
            }
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // For driving car with keyboard arrows 
        int forwardAction = 0;
        if (Input.GetKey(KeyCode.UpArrow)) forwardAction = 1;
        if (Input.GetKey(KeyCode.DownArrow)) forwardAction = 2;

        int turnAction = 0;
        if (Input.GetKey(KeyCode.RightArrow)) turnAction = 2;
        if (Input.GetKey(KeyCode.LeftArrow)) turnAction = 1;

        ActionSegment<int> discreteActions = actionsOut.DiscreteActions;
        discreteActions[0] = forwardAction;
        discreteActions[1] = turnAction;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Function that reward the agent when hitting a transparent wall (checkpoint)
        if (other.gameObject.name.StartsWith("CheckPoint"))
        {
            int reward_checkPoint = 25;
            int checkPoint_n = Int16.Parse(other.gameObject.name.Substring(10));
            if (checkPoint_n == 18) // Point depart
            {
                TimeSpan elapsedTime = DateTime.Now - startEpTime;
                int elapsedTimems = elapsedTime.Milliseconds + elapsedTime.Seconds * 1000 + elapsedTime.Minutes * 60 * 1000;
                int timeDiff = 30 * 1000 - elapsedTimems;
                AddReward(+1000);
                AddReward(timeDiff*0.01f);
                EndEpisode();
            }
            else
            {
                AddReward(reward_checkPoint * checkPoint_n);
            }
        }
    }

    public float mean_rot(List<float> rotations)
    {
        float mean = 0;
        foreach (float val in rotations)
        {
            mean += val;
        }
        return mean / rotations.Count;
    }

    public void get_path_info_test()
    {
        var tr = GetComponent<Transform>();
        float dist = Vector3.Distance(start_pos, tr.localPosition);
        if (dist >= 2)
        {
            start_pos = tr.localPosition;
            rotations.Add(tr.localEulerAngles.y);
            distances.Add(dist);
            //Debug.Log(tr.localEulerAngles.y);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        int move = Mathf.FloorToInt(actions.DiscreteActions[0]);
        int direction = Mathf.FloorToInt(actions.DiscreteActions[1]);
        float handBrake = 0;
        float torque = 0;
        float correction = 0;
        //get_path_info_test();
        // Dynamic parameters for driving the car
        float angle = 0; // Direction of the front wheels. 0 = straight
                         // Look up the index in the movement action list
        switch (move)
        {
            case 0: break;
            case 1: torque = maxTorque; break;
            case 2: handBrake = brakeTorque * 0.1f; break;
        }
        switch (direction)
        {
            case 0: correction += 0; break;
            case 1: correction -= maxAngle * 80; break;
            case 2: correction += maxAngle * 80; break;
        }
        // Scaling factor from pixels to angle (hand tuned)
        angle = correction * 0.01f;


        // Display of the car and its turning wheels
        m_Wheels[0].ConfigureVehicleSubsteps(criticalSpeed, stepsBelow, stepsAbove);
        foreach (WheelCollider wheel in m_Wheels)
        {
            // A simple car where front wheels steer while rear ones drive.
            if (wheel.transform.localPosition.z > 0)
                wheel.steerAngle = angle;

            wheel.motorTorque = torque;
            wheel.brakeTorque = handBrake;
            // Update visual wheels if any.
            if (wheelShape)
            {
                Quaternion q;
                Vector3 p;
                wheel.GetWorldPose(out p, out q);

                // Assume that the only child of the wheelcollider is the wheel shape.
                Transform shapeTransform = wheel.transform.GetChild(0);
                shapeTransform.position = p;
                shapeTransform.rotation = q;
            }
        }

        RaycastHit hit;
        if (!(Physics.Raycast(tr.position, tr.TransformDirection(Vector3.down), out hit) && hit.collider.CompareTag("Road")))
        {
            // negative reward for going out of the track
            AddReward(-200f);
            EndEpisode();
        }
    }
}
