using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    private float moveSpeed;
    public Transform orientation;
    float horizontalInput;
    float verticalInput;
    Vector3 moveDirection;
    Rigidbody rb;
    [SerializeField] float walkSpeed;
    [SerializeField] float sprintspeed;
    [SerializeField] float slideSpeed;
    public float wallRunSpeed;
    public float dashSpeed;
    public float dashSpeedChangeFactor;

    public float maxYSpeed;
    private float desireMoveSpeed;
    private float lastDesiredMoveSpeed;
    [SerializeField] float speedIncreaseMultiplier;
    [SerializeField] float slopeIncreaseMultiplier;


    [Header("Ground Check")]
    public float playerHeight;
    public LayerMask Ground;
    bool isGrounded;
    public float groundDrag;

    [Header("JumpPlayer")]
    public float jumpForce;
    public float jumpCooldown;
    public float airMultiplier;
    [SerializeField] bool readyToJump;

    [Header("Crounching")]
    [SerializeField] float crouchSpeed;
    [SerializeField] float crouchYScale;
    private float startYScale;

    [Header("Slope Handling")]
    [SerializeField] float maxSlopeAngle;
    private RaycastHit slopeHit;
    private bool exitingSlope;

    [Header("FOV")]

    [SerializeField] Camera Camera;
    [SerializeField] private float dynamicFOVTime;
    [SerializeField] private float dynamicFOVLimit;

    [Header("KenyBinds")]
    [SerializeField] KeyCode jumpKey = KeyCode.Space;
    [SerializeField] KeyCode sprintKey = KeyCode.LeftShift;
    [SerializeField] KeyCode crouchKey = KeyCode.LeftControl;

    public MoveState state;
    public enum MoveState
    {
        walking,
        sprinting,
        wallrunning,
        crouching,
        sliding,
        dashing,
        air
    }

    public bool sliding;
    public bool wallrunning;
    public bool dashing;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        readyToJump = true;

        startYScale = transform.localScale.y;
    }

    private void Update()
    {
        //Ground Check
        isGrounded = Physics.Raycast(transform.position, Vector3.down, playerHeight * 0.5f + 0.2f, Ground);

        //Handle Drag
        if (isGrounded)
        {
            rb.drag = groundDrag;
        }
        else
        {
            rb.drag = 0;
        }

        MyInput();
        SpeedControl();
        StateHandle();
        DynamicFOV();
    }

    private void FixedUpdate()
    {
        MovePlayer();
    }

    private void MyInput()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");

        //Podemos saltar si...
        if(Input.GetKey(jumpKey) && readyToJump && isGrounded)
        {
            readyToJump = false;
            Jump();
            Invoke(nameof(ResetJump), jumpCooldown);
        }

        //Start crouch
        if(Input.GetKeyDown(crouchKey))
        {
            transform.localScale = new Vector3(transform.localScale.x, crouchYScale, transform.localScale.z);
            rb.AddForce(Vector3.down * 5f, ForceMode.Impulse);
        }

        //Stop crouch
        if(Input.GetKeyUp(crouchKey))
        {
            transform.localScale = new Vector3(transform.localScale.x, startYScale, transform.localScale.z);
        }
    }

    private void StateHandle()
    {
        // Mode - Dashing
        if (dashing)
        {
            state = MoveState.dashing;
            desireMoveSpeed = dashSpeed;
            speedChangeFactor = dashSpeedChangeFactor;
        }

        //Mode - WallRunning
        if (wallrunning)
        {
            state = MoveState.wallrunning;
            desireMoveSpeed = wallRunSpeed;
        }


        //Mode - Sliding
        if(sliding)
        {
            state = MoveState.sliding;

            if (OnSlope() && rb.velocity.y < 0.1f)
                desireMoveSpeed = slideSpeed;

            else
                desireMoveSpeed = sprintspeed;
        }


        //Mode - Crouching
        else if(Input.GetKey(crouchKey))
        {
            state = MoveState.crouching;
            desireMoveSpeed = crouchSpeed;
        }

        //Mode - Sprinting
        if(isGrounded && Input.GetKey(sprintKey))
        {
            state = MoveState.sprinting;
            desireMoveSpeed = sprintspeed;
        }
        
        //Mode - walking
        else if(isGrounded)
        {
            state = MoveState.walking;
            desireMoveSpeed = walkSpeed;
        }

        //Mode - air 
        else
        {
            state = MoveState.air;
        }

        //check if desiredMoveSpeed has changed drastically
        if(Mathf.Abs(desireMoveSpeed - lastDesiredMoveSpeed) > 4f && moveSpeed != 0)
        {
            StopAllCoroutines();
            StartCoroutine(SmoothlyLerpMoveSpeed());
        }
        else
        {
            moveSpeed = desireMoveSpeed;
        }

        lastDesiredMoveSpeed = desireMoveSpeed;
    }

    private float speedChangeFactor;
    private IEnumerator SmoothlyLerpMoveSpeed()
    {
        //smoothly lerp movementSpeed to desired value
        float time = 0;
        float difference = Mathf.Abs(desireMoveSpeed - moveSpeed);
        float startValue = moveSpeed;

        while(time < difference)
        {
            moveSpeed = Mathf.Lerp(startValue, desireMoveSpeed, time / difference);

            if(OnSlope())
            {
                float slopeAngle = Vector3.Angle(Vector3.up, slopeHit.normal);
                float slopeAngleIncrease = 1 + (slopeAngle / 90f);
                time += Time.deltaTime * speedIncreaseMultiplier * slopeIncreaseMultiplier * slopeAngleIncrease;
            }
            else
                time += Time.deltaTime * speedIncreaseMultiplier;


            yield return null;
        }

        moveSpeed = desireMoveSpeed;
        speedChangeFactor = 1f;
    }






    private void MovePlayer()
    {
        //Calculamos nuestra direccion
        moveDirection = orientation.forward * verticalInput + orientation.right * horizontalInput;

        //On slope
        if(OnSlope() && !exitingSlope)
        {
            rb.AddForce(GetSlopeMoveDirection(moveDirection) * moveSpeed * 20f, ForceMode.Force);

            if (rb.velocity.y > 0)
                rb.AddForce(Vector3.down * 80f, ForceMode.Force);
        }

        //En el suelo
        if(isGrounded)
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f, ForceMode.Force);
        
        //En el aire
        else if(!isGrounded)
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f * airMultiplier, ForceMode.Force);

        //turn gravity off while on slope
        rb.useGravity = !OnSlope();
    }

    private void SpeedControl()
    {
        //Limitin speed on slope
        if(OnSlope() && !exitingSlope)
        {
            if (rb.velocity.magnitude > moveSpeed)
                rb.velocity = rb.velocity.normalized * moveSpeed;
        }

        //limiting speed on ground or in air
        else
        {
            Vector3 flatVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);

            //Limitar la velocidad que pongamos
            if (flatVel.magnitude > moveSpeed)
            {
                Vector3 limitedVel = flatVel.normalized * moveSpeed;
                rb.velocity = new Vector3(limitedVel.x, rb.velocity.y, limitedVel.z);
            }
        }
    }

    private void Jump()
    {
        exitingSlope = true;

        //Reset Y velocity
        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);

        rb.AddForce(transform.up * jumpForce, ForceMode.Impulse);

    }
    private void ResetJump()
    {
        readyToJump = true;
        exitingSlope = false;
    }


    public bool OnSlope()
    {
        if(Physics.Raycast(transform.position,Vector3.down, out slopeHit, playerHeight * 0.5f + 0.3f))
        {
            float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
            return angle < maxSlopeAngle && angle != 0;
        }
        
        return false;
    }

    public Vector3 GetSlopeMoveDirection(Vector3 direction)
    {
        return Vector3.ProjectOnPlane(direction, slopeHit.normal).normalized;
    }



    void DynamicFOV()
    {
        if (Input.GetKey(sprintKey))
        {
            Camera.fieldOfView = Mathf.Lerp(Camera.fieldOfView, 100, dynamicFOVTime * Time.deltaTime);
        }
        else
        {
            Camera.fieldOfView = Mathf.Lerp(Camera.fieldOfView, 80, dynamicFOVTime * Time.deltaTime);
        }
    }


}
