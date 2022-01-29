using UnityEngine;
using System;
using System.Runtime.InteropServices;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class AutoDriveAdvanced : MonoBehaviour
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
    private const int RIGHTSEGMENT = SIZEVIEW - 17;
    private const int MIDDLESEGMENT = SIZEVIEW / 2;
    // Size of the lane for the observed line
    private const int LANEWIDTH = 102 - 17;

    // Variables for mapping
    private int turn_left_nb = 0;
    private int turn_right_nb = 0;
    private int straight = 0;
    private List<string> Actions = new List<string>();
    private List<int> StraigtLine = new List<int>();
    private const int TURN_ACTIONS = 25;

    // Try to get new motion
    private int[] old_pix = new int[SIZEVIEW];
    private int got_old = 1;

    static int[] get_closest_side(int LINE, int[] pixels)
    {
        /* Check where are the closest black pixel*/
        int j;
        int closest_left = SIZEVIEW / 2;
        int closest_right = SIZEVIEW / 2;
        int[] ret = new int[2];
        int coeff = 1;
        if (LINE != 0) coeff = LINE1 / LINE;
        for (j = 1; j < SIZEVIEW / 2; j++)
        { // Double loop, not pretty. Need optimize if possible
            if (SIZEVIEW / 2 == closest_right && pixels[LINE + SIZEVIEW / 2 + j] < 100)
            {
                closest_right = j * coeff; //Math.Min(SIZEVIEW/2 + j*LINE/LINE1, SIZEVIEW);
            }
            if (SIZEVIEW / 2 == closest_left && pixels[LINE + SIZEVIEW / 2 - j] < 100)
            {
                closest_left = j * coeff; //Math.Max(SIZEVIEW/2 - j*LINE/LINE1,0);   
            }
            if (SIZEVIEW / 2 != closest_left && SIZEVIEW / 2 != closest_right && closest_left != closest_right)
            {
                ret[0] = closest_left;
                ret[1] = closest_right;
                return ret;
            }
        }
        ret[0] = closest_left;
        ret[1] = closest_right;
        return ret;

    }

    static int[,] get_image_shift(int[] new_pic, int[] old_pic)
    {
        /* Try to determine how new_pic have shifted from old_pic */
        int i, j, i2, j2;
        int closest_i = -1;
        int closest_j = -1;
        int closest_value = 0;
        int[,] shift = new int[2, SIZEVIEW * SIZEVIEW];
        print("fine");
        for (i = 0; i < SIZEVIEW; i++)
        {
            for (j = 0; j < SIZEVIEW; j++)
            {
                if (old_pic[i * SIZEVIEW + j] < 100)
                {
                    closest_i = -1;
                    for (i2 = Math.Max(i - 5, 0); i2 < Math.Min(i + 5, SIZEVIEW) - 1; i2++)
                    {
                        for (j2 = Math.Max(j - 5, 0); j2 < Math.Min(j + 5, SIZEVIEW) - 1; j2++)
                        {
                            print(j2);
                            if (closest_i == -1)
                            {
                                closest_i = i2;
                                closest_j = j2;
                                closest_value = Math.Abs(old_pic[i * SIZEVIEW + j] - new_pic[i2 * SIZEVIEW + j2]);
                            }
                            else
                            {
                                if (Math.Abs(old_pic[i * SIZEVIEW + j] - new_pic[i2 * SIZEVIEW + j2]) < closest_value)
                                {
                                    closest_i = i2;
                                    closest_j = j2;
                                    closest_value = Math.Abs(old_pic[i * SIZEVIEW + j] - new_pic[i2 * SIZEVIEW + j2]);
                                }
                            }
                        }
                    }
                    shift[0, i * SIZEVIEW + j] = closest_i - i;
                    shift[1, i * SIZEVIEW + j] = closest_j - j;
                }
            }
        }
        return shift;
    }


    // Called once, at initialization of the scene
    void Start()
    {
        // Find the rigidbody of the car
        rb = GetComponent<Rigidbody>();
        // Find all the WheelColliders down in the hierarchy.
        m_Wheels = GetComponentsInChildren<WheelCollider>();
        for (int i = 0; i < m_Wheels.Length; ++i)
        {
            var wheel = m_Wheels[i];
            // Create wheel shapes only when needed.
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
    public int target;
    public int gear = 0;// gear= 1: slow speed, gear = 2: high speed
    public const float SPEED1 = 3f, SPEED2 = 7f;
    public int[] array_closest = new int[] { SIZEVIEW / 2, SIZEVIEW / 2 };
    // Called once each frame

    void Update()
    {
        // Dynamic parameters for driving the car
        float angle = 0; // Direction of the front wheels. 0 = straight
        float torque = 0; // Angular acceleration of the wheels 
        float correction = 0; // Angle correction in order to drive in the middle of the lane
        float handBrake = 0; // handbrake > 0 is braking

        int i, index;
        int[] old_array_closest = new int[] { SIZEVIEW / 2, SIZEVIEW / 2 };
        // Transform the image of the car camera into an array of pixels
        RenderTexture.active = carcamera;
        tex.ReadPixels(new Rect(0, 0, SIZEVIEW, SIZEVIEW), 0, 0);
        tex.Apply();
        Color32[] pix = tex.GetPixels32();
        //
        int[,] shift;

        for (i = 0; i < SIZEVIEW * SIZEVIEW; i++)
            pixels[i] = pix[i].r; // Only keep the red component

        /***** CODE FOR AUTOPILOT *****/
        // Find the leftmost black pixel at the line y=LINE1 in the image
        print("update");
        // if(got_old == 0){ 
        //     print("got new pix");
        //     shift = get_image_shift(pixels,old_pix); 
        // }
        // old_pix = pixels;
        // got_old = 0;  
        for (i = 2; i < SIZEVIEW / 2; i++)
        {
            if (pixels[SIZEVIEW / 2 + i * SIZEVIEW] < 100)
            { // Line in the middle of image, following y-axis
                break;
            }
        }

        if (i < SIZEVIEW / 2)
        {
            array_closest = get_closest_side((i - 1) * SIZEVIEW, pixels);
        }
        else if (array_closest[0] != SIZEVIEW / 2 || array_closest[1] != SIZEVIEW / 2)
        {
            /* Something can be added to improve the pilot's behavior */
            // old_array_closest[0] = array_closest[0]; 
            // old_array_closest[1] = array_closest[1]; 
            // for(index = 0; i<SIZEVIEW/2;i++){ 
            //     array_closest = get_closest_side(index*SIZEVIEW, pixels);
            //     if(array_closest[0] != SIZEVIEW/2 && array_closest[1] != SIZEVIEW/2){
            //         if(Math.Abs(array_closest[0] - old_array_closest[0]) < 1 && Math.Abs(array_closest[1] - old_array_closest[1]) < 1){ 
            //             old_array_closest[0] = SIZEVIEW/2; 
            //             old_array_closest[1] =  SIZEVIEW/2; 
            //             break; 
            //         }else{ 
            //             old_array_closest[0] = array_closest[0]; 
            //             old_array_closest[1] = array_closest[1]; 
            //         }
            //     }
            // }
        }


        /* The following if, tries to understand what the car is doing (don't work) */
        if (Math.Abs(array_closest[0] - array_closest[1]) < 10)
        {
            target = SIZEVIEW / 2;
            array_closest[1] = SIZEVIEW / 2;
            array_closest[0] = SIZEVIEW / 2;
            straight += 1;
            turn_left_nb = 0;
            turn_right_nb = 0;
        }
        else if (array_closest[0] < array_closest[1])
        { //Turn right
            target = array_closest[1] + SIZEVIEW / 2;
            if (i >= SIZEVIEW / 2) array_closest[1] = Math.Max(array_closest[1] - 1, 0);
            turn_right_nb += 1;
            turn_left_nb = 0;
            straight += 1;
        }
        else
        {
            straight += 1;
            turn_left_nb += 1;
            turn_right_nb = 0;
            target = SIZEVIEW / 2 - array_closest[0]; //turn left
            if (i >= SIZEVIEW / 2) array_closest[0] = Math.Max(array_closest[0] - 1, 0);
        }

        // Find the correction to apply in order to be in the middle of the lane
        if (target < MIDDLESEGMENT) { correction -= maxAngle * (MIDDLESEGMENT - target); }
        else if (target > MIDDLESEGMENT) { correction += maxAngle * (target - MIDDLESEGMENT); }
        // Scaling factor from pixels to angle (hand tuned)
        angle = Math.Min((correction / 2) * 0.01f, maxAngle);
        // Find the gear for the car 
        if (Mathf.Floor(correction) > 10) gear = 1;
        else gear = 2;
        // According to the gear and the speed, accelerate or brake
        if (gear == 1 && rb.velocity.magnitude < SPEED1) torque = maxTorque;

        else if (gear == 1 && rb.velocity.magnitude > 1.2 * SPEED1) handBrake = brakeTorque * 0.1f;
        if (gear == 2 && rb.velocity.magnitude < SPEED2) torque = maxTorque;
        else if (gear == 2 && rb.velocity.magnitude > 1.2 * SPEED2) handBrake = brakeTorque * 0.1f;
        /* The commented code below tries to determine the movements of the car, but it don't works. Various tries have been made, including finding formula with trigonometric function or saving previous actions */
        // if(turn_right_nb != 1 && turn_right_nb % TURN_ACTIONS == 1 ){ 
        //     if(turn_right_nb < TURN_ACTIONS + 2 ){ 
        //         Actions.Add("Straight");
        //         StraigtLine.Add(straight); 
        //     }
        //     straight = 0;
        //     Actions.Add("Right"); 
        // }else if(turn_left_nb != 1 && turn_left_nb % TURN_ACTIONS == 1 ){ 
        //     if(turn_left_nb < TURN_ACTIONS + 2 ){ 
        //         Actions.Add("Straight");
        //         StraigtLine.Add(straight); 
        //     }
        //     StraigtLine.Add(straight); 
        //     straight = 0; 
        //     Actions.Add("Left"); 
        // }

        // foreach( var x in Actions) {
        //     Debug.Log( x.ToString());
        // }
        // foreach( var x in StraigtLine) {
        //     Debug.Log( x.ToString());
        // }

        /***** END OF AUTOPILOT *******/

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

    }

}
