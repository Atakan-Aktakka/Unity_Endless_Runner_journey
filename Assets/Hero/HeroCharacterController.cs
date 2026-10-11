using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HeroCharacterController : MonoBehaviour
{
    [SerializeField] private LayerMask groundLayers;
    [SerializeField] private float runSpeed = 8f;
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private Transform[] groundChecks;
    [SerializeField] private Transform[] wallChecks;
    [SerializeField] private AudioClip jumpSoundEffect;

    private float gravity = -50f;
    private CharacterController characterController;
    private Animator animator;
    private Vector3 velocity;
    private bool isGrounded;
    private float horizontalInput;
    private bool jumpPressed;
    private float jumpTimer;
    private float jumpGracePeriod = 0.2f;
    public Touch finger;

    // Exposed for BlockManager: difficulty ramps the run speed, gap math reads the jump physics
    public float RunSpeed
    {
        get { return runSpeed; }
        set { runSpeed = Mathf.Max(0f, value); }
    }
    public float JumpHeight { get { return jumpHeight; } }
    public float Gravity { get { return gravity; } }
    public LayerMask GroundLayers { get { return groundLayers; } }

    // HeroLaneController turns this off and decides tap (jump) vs. swipe (lane change) itself
    public bool TouchJumpEnabled { get; set; } = true;

    // TutorialManager stops the forward run while it waits for the player (gravity / jumping still work)
    public bool MovementLocked { get; set; }

    public bool IsGrounded { get { return isGrounded; } }

    // Raised the moment a jump actually starts (tutorial, sounds, effects)
    public event System.Action Jumped;

    // Buffered jump request (same grace period as the keyboard/touch input), e.g. for UI buttons
    public void RequestJump()
    {
        jumpTimer = Time.time;
    }

    // Start is called before the first frame update
    void Start()
    {
        characterController = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
        // Paused / game over: no input, otherwise a tap on a UI button queues a jump for the resume
        if (Time.timeScale == 0f)
        {
            return;
        }

        horizontalInput = 1;

        // Face Forward
        transform.forward = new Vector3(horizontalInput, 0, Mathf.Abs(horizontalInput) - 1);

        // IsGrounded
        isGrounded = false;
        
        foreach (var groundCheck in groundChecks)
        {
            if (Physics.CheckSphere(groundCheck.position, 0.1f, groundLayers, QueryTriggerInteraction.Ignore))
            {
                isGrounded = true;
                break;
            }
        }        

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = 0;
        }
        else
        {
            // Add gravity
            velocity.y += gravity * Time.deltaTime;
        }

        // Wallcheck - prevents player from getting stuck when running into a block and jumping
        var blocked = false;
        foreach (var wallCheck in wallChecks)
        {
            if (Physics.CheckSphere(wallCheck.position, 0.01f, groundLayers, QueryTriggerInteraction.Ignore))
            {
                blocked = true;
                break;
            }
        }

        if (!blocked && !MovementLocked)
        {
            characterController.Move(new Vector3(horizontalInput * runSpeed, 0, 0) * Time.deltaTime);
        }

        // Jumping
        jumpPressed = Input.GetButtonDown("Jump");

        if (Input.touchCount < 0)
        {
            finger=Input.GetTouch(0);       
        }
        if (TouchJumpEnabled && Input.touchCount==1)  //>0
        {
            jumpPressed=true;
           

        }
        

        if (jumpPressed)
        {
            jumpTimer = Time.time;
        }

        if (isGrounded && (jumpPressed || (jumpTimer > 0 && Time.time < jumpTimer + jumpGracePeriod)))
        {
            velocity.y += Mathf.Sqrt(jumpHeight * -2 * gravity);
            SoundManager.PlayJump(jumpSoundEffect, transform.position);
            jumpTimer = -1;
            if (Jumped != null) Jumped();
            
        }
        jumpPressed=false;

        // Vertical Velocity
        characterController.Move(velocity * Time.deltaTime);

        // Run Animation
        animator.SetFloat("Speed", horizontalInput);

        // Set Animator IsGrounded
        animator.SetBool("IsGrounded", isGrounded);

        // Set parameter for JumpFall Blend Tree Animation
        animator.SetFloat("VerticalSpeed", velocity.y);
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if(hit.transform.tag == "Obstacle")
        {
            HeroManager.gameOver = true;
        }
    }
}
