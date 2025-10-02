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
    
    // Start is called before the first frame update
    void Start()
    {
        characterController = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
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

        if (!blocked)
        {
            characterController.Move(new Vector3(horizontalInput * runSpeed, 0, 0) * Time.deltaTime);
        }

        // Jumping
       // jumpPressed = Input.GetButtonDown("Jump");

        if (Input.touchCount < 0)
        {
            finger=Input.GetTouch(0);       
        }
        if (Input.touchCount==1)  //>0
        {
            jumpPressed=true;
           

        }
        

        if (jumpPressed)
        {
            jumpTimer = Time.time;
            Debug.Log("jumpTimer"+jumpTimer);

        }

        if (isGrounded && (jumpPressed || (jumpTimer > 0 && Time.time < jumpTimer + jumpGracePeriod)))
        {
            velocity.y += Mathf.Sqrt(jumpHeight * -2 * gravity);
            if (jumpSoundEffect != null)
            {
                AudioSource.PlayClipAtPoint(jumpSoundEffect, transform.position, 0.5f);
            }
            jumpTimer = -1;
            
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
