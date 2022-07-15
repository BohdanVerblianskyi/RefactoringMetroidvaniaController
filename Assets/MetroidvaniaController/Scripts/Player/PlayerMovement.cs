using System;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    private const string AxisHorizontal = "Horizontal";
    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int IsJumping = Animator.StringToHash("IsJumping");

    [SerializeField] private CharacterController2D _controller;
    [SerializeField] private Animator _animator;
    [SerializeField] private float _runSpeed = 40f;

    private float _horizontalMove = 0f;
    private bool _jump = false;
    private bool _dash = false;

    //bool dashAxis = false;

    // Update is called once per frame

    private void OnEnable()
    {
        _controller.OnFallEvent += OnFall;
        _controller.OnLandEvent += OnLanding;
    }
    private void OnDisable()
    {
        _controller.OnFallEvent -= OnFall;
        _controller.OnLandEvent -= OnLanding;
    }

    
    
    private void Update()
    {
        _horizontalMove = Input.GetAxisRaw(AxisHorizontal) * _runSpeed;

        _animator.SetFloat(Speed, Mathf.Abs(_horizontalMove));

        if (Input.GetKeyDown(KeyCode.Z))
        {
           // _controller.Jump();
            _jump = true;
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            //_controller.Dash();
            _dash = true;
        }
    }

    private void FixedUpdate()
    {
        // Move our character
        _controller.Move(_horizontalMove * Time.fixedDeltaTime, _jump, _dash);
        _jump = false;
        _dash = false;
    }

    private void OnLanding()
    {
        _animator.SetBool(IsJumping, true);
    }

    private void OnFall()
    {
        _animator.SetBool(IsJumping, true);
    }
}