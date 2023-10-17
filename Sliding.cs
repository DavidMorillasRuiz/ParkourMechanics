using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Sliding : MonoBehaviour
{
    [Header("References")]
    public Transform orientation;
    public Transform playerObj;
    private Rigidbody rb;
    private PlayerController playerController;

    [Header("Sliding")]
    [SerializeField] float maxSlideTime;
    [SerializeField] float slideForce;
    private float slideTimer;

    [SerializeField] float slideYScale;
    private float startYScale;

    [Header("Input")]
    public KeyCode slideKey = KeyCode.LeftControl;
    private float horizontalInput;
    private float verticalInput;
    

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        playerController = GetComponent<PlayerController>();

        startYScale = playerObj.localScale.y;
    }

    private void Update()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");

        if(Input.GetKeyDown(slideKey) && (horizontalInput != 0 || verticalInput !=0))
            StartSlide();

        if (Input.GetKeyUp(slideKey) && playerController.sliding)
            StopSlide();
    }


    private void FixedUpdate()
    {
        if (playerController.sliding)
            SlidingMovement();
    }


    private void StartSlide()
    {
        playerController.sliding = true;

        playerObj.localScale = new Vector3(playerObj.localScale.x, slideYScale, playerObj.localScale.z);
        rb.AddForce(Vector3.down * 5f, ForceMode.Impulse);

        slideTimer = maxSlideTime;
    }


    private void SlidingMovement()
    {
        Vector3 inputDirection = orientation.forward * verticalInput + orientation.right * horizontalInput;

        //sliding normal
        if(!playerController.OnSlope() || rb.velocity.y > -0.1f)
        {
            rb.AddForce(inputDirection.normalized * slideForce, ForceMode.Force);

            slideTimer -= Time.deltaTime;
        }

        //Sliding down a slope
        else
        {
            rb.AddForce(playerController.GetSlopeMoveDirection(inputDirection) * slideForce, ForceMode.Force);
        }

        if (slideTimer <= 0)
            StopSlide();
    }

    private void StopSlide()
    {
        playerController.sliding = false;
        playerObj.localScale = new Vector3(playerObj.localScale.x, startYScale, playerObj.localScale.z);
    }
}
