using System.Collections;
using System.Collections.Generic;
using UnityEngine;

struct Command
{
    public float forward;
    public float rightward;
    public float upwards;
}

[RequireComponent(typeOf(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public Transform camera;
    private CharacterController _controller;
    public float cameraYOffset = 0.6f;
    public float xMouseSensitivity = 30.0f;
    public float yMouseSensitivity = 30.0f;

    public float gravity = 20.0f;

    public float friction = 6;

    public float moveSpeed = 7.0f;
    public float groundAcceleration = 14.0f;
    public float groundDeacceleration = 10.0f;
    public float airAcceleration = 2.0f;
    public float airDecceleration = 2.0f;
    public float airControl = 0.3f;
    public float strafeAcceleration = 50.0f;
    public float strafeSpeed = 1.0f;
    public float jumpSpeed = 8.0f;
    public bool holdJumpToBhop = false;

    private int frameCount = 0;
    private float dt = 0.0f;
    private float fps = 0.0f;

    private float rotX = 0.0f;
    private float rotY = 0.0f;

    private Vector3 moveDirectionNorm = Vector3.zero;
    private Vector3 playerVelocity = Vector3.zero;
    private float playerTopVelocity = 0.0f;

    private bool wishJump = false;

    private float characterFriction = 0.0f;

    private Command _cmd;

    private void Start()
    {
        // Hide the cursor
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        if (camera == null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
                camera = mainCamera.gameObject.transform;
        }

        // Put it in ( ͡° ͜ʖ ͡°)
        camera.position = new Vector3(
            transform.position.x,
            transform.position.y + cameraYOffset,
            transform.position.z);

        _controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        if (Cursor.lockState != CursorLockMode.Locked) {
            if (Input.GetButtonDown("Fire1"))
                Cursor.lockState = CursorLockMode.Locked;
        }

        rotX -= Input.GetAxisRaw("Mouse Y") * xMouseSensitivity * 0.02f;
        rotY += Input.GetAxisRaw("Mouse X") * yMouseSensitivity * 0.02f;

        if(rotX < -90)
            rotX = -90;
        else if(rotX > 90)
            rotX = 90;

        transform.rotation = Quaternion.Euler(0, rotY, 0);
        camera.rotation = Quaternion.Euler(rotX, rotY, 0);


        QueueJump();
        if(_controller.isGrounded)
            GroundMove();
        else if(!_controller.isGrounded)
            AirMove();

        _controller.Move(playerVelocity * Time.deltaTime);

        Vector3 udp = playerVelocity;
        udp.y = 0.0f;
        if(udp.magnitude > playerTopVelocity)
            playerTopVelocity = udp.magnitude;

        camera.position = new Vector3(
            transform.position.x,
            transform.position.y + cameraYOffset,
            transform.position.z);
    }

    private void SetMovementDir()
    {
        _cmd.forward = Input.GetAxisRaw("Vertical");
        _cmd.rightward   = Input.GetAxisRaw("Horizontal");
    }

    private void QueueJump()
    {
        if(holdJumpToBhop)
        {
            wishJump = Input.GetButton("Jump");
            return;
        }

        if(Input.GetButtonDown("Jump") && !wishJump)
            wishJump = true;
        if(Input.GetButtonUp("Jump"))
            wishJump = false;
    }

    private void AirMove()
    {
        Vector3 wishdir;
        float wishvel = airAcceleration;
        float accel;

        SetMovementDir();

        wishdir =  new Vector3(_cmd.rightward, 0, _cmd.forward);
        wishdir = transform.TransformDirection(wishdir);

        float wishspeed = wishdir.magnitude;
        wishspeed *= moveSpeed;

        wishdir.Normalize();
        moveDirectionNorm = wishdir;

        float wishspeed2 = wishspeed;
        accel = Vector3.Dot(playerVelocity, wishdir) < 0 ? airDecceleration : airAcceleration;

        if(_cmd.forward == 0 && _cmd.rightward != 0)
        {
            if(wishspeed > strafeSpeed)
                wishspeed = strafeSpeed;
            accel = strafeAcceleration;
        }

        Accelerate(wishdir, wishspeed, accel);
        if(airControl > 0)
            AirControl(wishdir, wishspeed2);
        playerVelocity.y -= gravity * Time.deltaTime;
    }

    private void AirControl(Vector3 wishdir, float wishspeed)
    {
        float zspeed;
        float speed;
        float dot;
        float k;

        if(Mathf.Abs(_cmd.forward) < 0.001 || Mathf.Abs(wishspeed) < 0.001)
            return;
        zspeed = playerVelocity.y;
        playerVelocity.y = 0;
        speed = playerVelocity.magnitude;
        playerVelocity.Normalize();

        dot = Vector3.Dot(playerVelocity, wishdir);
        k = 32;
        k *= airControl * dot * dot * Time.deltaTime;

        if (dot > 0)
        {
            playerVelocity.x = playerVelocity.x * speed + wishdir.x * k;
            playerVelocity.y = playerVelocity.y * speed + wishdir.y * k;
            playerVelocity.z = playerVelocity.z * speed + wishdir.z * k;

            playerVelocity.Normalize();
            moveDirectionNorm = playerVelocity;
        }

        playerVelocity.x *= speed;
        playerVelocity.y = zspeed;
        playerVelocity.z *= speed;
    }

    private void GroundMove()
    {
        Vector3 wishdir;

        if (!wishJump)
            ApplyFriction(1.0f);
        else
            ApplyFriction(0);

        SetMovementDir();

        wishdir = new Vector3(_cmd.rightward, 0, _cmd.forward);
        wishdir = transform.TransformDirection(wishdir);
        wishdir.Normalize();
        moveDirectionNorm = wishdir;

        var wishspeed = wishdir.magnitude;
        wishspeed *= moveSpeed;

        Accelerate(wishdir, wishspeed, groundAcceleration);

        playerVelocity.y = -gravity * Time.deltaTime;

        if(wishJump)
        {
            playerVelocity.y = jumpSpeed;
            wishJump = false;
        }
    }

    private void ApplyFriction(float t)
    {
        Vector3 vec = playerVelocity;
        float speed;
        float newspeed;
        float control;
        float drop;

        vec.y = 0.0f;
        speed = vec.magnitude;
        drop = 0.0f;

        if(_controller.isGrounded)
        {
            control = speed < groundDeacceleration ? groundDeacceleration : speed;
            drop = control * friction * Time.deltaTime * t;
        }

        newspeed = speed - drop;
        characterFriction = newspeed;
        if(newspeed < 0)
            newspeed = 0;
        if(speed > 0)
            newspeed /= speed;

        playerVelocity.x *= newspeed;
        playerVelocity.z *= newspeed;
    }

    private void Accelerate(Vector3 wishdir, float wishspeed, float accel)
    {
        float addspeed;
        float accelspeed;
        float currentspeed;

        currentspeed = Vector3.Dot(playerVelocity, wishdir);
        addspeed = wishspeed - currentspeed;
        if(addspeed <= 0)
            return;
        accelspeed = accel * Time.deltaTime * wishspeed;
        if(accelspeed > addspeed)
            accelspeed = addspeed;

        playerVelocity.x += accelspeed * wishdir.x;
        playerVelocity.z += accelspeed * wishdir.z;
    }
}