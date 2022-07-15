using System;
using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using UnityEngine.SceneManagement;

public class CharacterController2D : MonoBehaviour
{
    private const float GroundedRadius = 0.2f; // Radius of the overlap circle to determine if grounded
    private const float StunDuration = 0.25f;
    private const float InvincibleDuration = 1f;
    public event Action OnFallEvent;
    public event Action OnLandEvent;

    [SerializeField] private float m_JumpForce = 400f; // Amount of force added when the player jumps.
    [Range(0, .3f)] [SerializeField] private float m_MovementSmoothing = .05f; // How much to smooth out the movement
    [SerializeField] private LayerMask m_WhatIsGround; // A mask determining what is ground to the character
    [SerializeField] private Transform m_GroundCheck; // A position marking where to check if the player is grounded.
    [SerializeField] private Transform m_WallCheck; //Posicion que controla si el personaje toca una pared

    private bool _grounded; // Whether or not the player is grounded.
    private Rigidbody2D _rigidbody2D;
    private bool _facingRight = true; // For determining which way the player is currently facing.
    private Vector3 _velocity = Vector3.zero;
    private float _limitFallSpeed = 25f; // Limit fall speed

    [SerializeField] private float m_DashForce = 25f;
    private bool _canDoubleJump = true; //If player can double jump
    private bool _canDash = true;
    private bool _isDashing = false; //If player is dashing
    private bool _isWallSliding = false; //If player is sliding in a wall
    private bool _oldWallSlidding = false; //If player is sliding in a wall in the previous frame
    private bool _canCheck = false; //For check if player is wallsliding

    public float life = 10f; //Life of the player
    public bool invincible = false; //If player can die
    private bool _canMove = true; //If player can move

    private Animator _animator;
    public ParticleSystem particleJumpUp; //Trail particles
    public ParticleSystem particleJumpDown; //Explosion particles

    private float _jumpWallStartX = 0;
    private float _jumpWallDistX = 0; //Distance between player and wall
    private bool _limitVelOnWallJump = false; //For limit wall jump distance with low fps

    private static readonly int IsDoubleJumping = Animator.StringToHash("IsDoubleJumping");
    private static readonly int IsWallSliding = Animator.StringToHash("IsWallSliding");
    private static readonly int IsDashing = Animator.StringToHash("IsDashing");
    private static readonly int IsJumping = Animator.StringToHash("IsJumping");
    private static readonly int IsDead = Animator.StringToHash("IsDead");
    private static readonly int JumpUp = Animator.StringToHash("JumpUp");
    private static readonly int Hit = Animator.StringToHash("Hit");

    private void Awake()
    {
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();
    }

    private void FixedUpdate()
    {
        GroundCheck();

        if (!_grounded)
        {
            _animator.SetBool(IsJumping, true);
        }

        if (IsWall())
        {
            _isDashing = false;
        }

        if (_limitVelOnWallJump)
        {
            if (_rigidbody2D.velocity.y < -0.5f)
            {
                _limitVelOnWallJump = false;
            }

            _jumpWallDistX = (_jumpWallStartX - transform.position.x) * transform.localScale.x;
            if (_jumpWallDistX < -0.5f && _jumpWallDistX > -1f)
            {
                _canMove = true;
            }
            else if (_jumpWallDistX < -1f && _jumpWallDistX >= -2f)
            {
                _canMove = true;
                _rigidbody2D.velocity = new Vector2(10f * transform.localScale.x, _rigidbody2D.velocity.y);
            }
            else if (_jumpWallDistX < -2f)
            {
                _limitVelOnWallJump = false;
                _rigidbody2D.velocity = new Vector2(0, _rigidbody2D.velocity.y);
            }
            else if (_jumpWallDistX > 0)
            {
                _limitVelOnWallJump = false;
                _rigidbody2D.velocity = new Vector2(0, _rigidbody2D.velocity.y);
            }
        }
    }

    public void Move(float directionX, bool jump, bool dash)
    {
        if (!_canMove)
            return;

        if (dash && _canDash && !_isWallSliding)
        {
            StartCoroutine(DashCooldown());
        }

        if (!_isDashing)
        {
            if (_rigidbody2D.velocity.y < -_limitFallSpeed)
                _rigidbody2D.velocity = new Vector2(_rigidbody2D.velocity.x, -_limitFallSpeed);
            Vector3 targetVelocity = new Vector2(directionX * 10f, _rigidbody2D.velocity.y);
            _rigidbody2D.velocity =
                Vector3.SmoothDamp(_rigidbody2D.velocity, targetVelocity, ref _velocity, m_MovementSmoothing);

            if (directionX > 0 && !_facingRight && !_isWallSliding)
            {
                Flip();
            }
            else if (directionX < 0 && _facingRight && !_isWallSliding)
            {
                Flip();
            }
        }

        if (_grounded && jump)
        {
            DoJump();
        }
        else if (!_grounded && jump && _canDoubleJump && !_isWallSliding)
        {
            DoDoubleJump();
        }
        else if (IsWall() && !_grounded)
        {
            _isDashing = false;
            if (!_oldWallSlidding && _rigidbody2D.velocity.y < 0 || _isDashing)
            {
                OnWallSliding();
            }

            if (_isWallSliding)
            {
                if (directionX * transform.localScale.x > 0.1f)
                {
                    StartCoroutine(WaitToEndSliding());
                }
                else
                {
                    _oldWallSlidding = true;
                    _rigidbody2D.velocity = new Vector2(-transform.localScale.x * 2, -5);
                }
            }

            if (jump && _isWallSliding)
            {
                JumpFromWall();
            }
            else if (dash && _canDash)
            {
                OffWallSliding();
                StartCoroutine(DashCooldown());
            }
        }
        else if (_isWallSliding && !IsWall() && _canCheck)
        {
            OffWallSliding();
        }
    }

    private void OffWallSliding()
    {
        _animator.SetBool(IsWallSliding, false);
        _isWallSliding = false;
        _oldWallSlidding = false;
        _canDoubleJump = true;
        WallCheckCorrectPosition();
    }

    private void JumpFromWall()
    {
        _animator.SetBool(IsJumping, true);
        _animator.SetBool(JumpUp, true);
        _rigidbody2D.velocity = new Vector2(0f, 0f);
        _rigidbody2D.AddForce(new Vector2(transform.localScale.x * m_JumpForce * 1.2f, m_JumpForce));
        _jumpWallStartX = transform.position.x;
        _limitVelOnWallJump = true;
        _canMove = false;
        OffWallSliding();
    }

    private void OnWallSliding()
    {
        _isWallSliding = true;
        m_WallCheck.localPosition = new Vector3(-m_WallCheck.localPosition.x, m_WallCheck.localPosition.y, 0);
        Flip();
        StartCoroutine(WaitToCheck(0.1f));
        _canDoubleJump = true;
        _animator.SetBool(IsWallSliding, true);
    }

    private void DoDoubleJump()
    {
        _canDoubleJump = false;
        _rigidbody2D.velocity = new Vector2(_rigidbody2D.velocity.x, 0);
        _rigidbody2D.AddForce(new Vector2(0f, m_JumpForce / 1.2f));
        _animator.SetBool(IsDoubleJumping, true);
    }

    private void DoJump()
    {
        _animator.SetBool(IsJumping, true);
        _animator.SetBool(JumpUp, true);
        _rigidbody2D.AddForce(new Vector2(0f, m_JumpForce));
        _canDoubleJump = true;
        particleJumpDown.Play();
        particleJumpUp.Play();
    }

    private void WallCheckCorrectPosition()
    {
        m_WallCheck.localPosition = new Vector3(Mathf.Abs(m_WallCheck.localPosition.x), m_WallCheck.localPosition.y, 0);
    }

    private void GroundCheck()
    {
        bool flies = !_grounded;
        if (IsGround())
        {
            _grounded = true;
            if (flies)
            {
                _animator.SetBool(IsJumping, false);
                _canDoubleJump = true;
                if (!IsWall() && !_isDashing)
                    particleJumpDown.Play();
                if (_rigidbody2D.velocity.y < 0f)
                    _limitVelOnWallJump = false;
            }
        }
        else
        {
            _grounded = false;
        }
    }

    private void Flip()
    {
        _facingRight = !_facingRight;
        Vector3 theScale = transform.localScale;
        theScale.x *= -1;
        transform.localScale = theScale;
    }

    public void ApplyDamage(float damage, Vector3 position)
    {
        if (!invincible)
        {
            _animator.SetBool(Hit, true);
            life -= damage;
            Vector2 damageDir = Vector3.Normalize(transform.position - position) * 40f;
            _rigidbody2D.velocity = Vector2.zero;
            _rigidbody2D.AddForce(damageDir * 10);
            if (life <= 0)
            {
                StartCoroutine(WaitToDead());
            }
            else
            {
                StartCoroutine(Stun());
                StartCoroutine(MakeInvincible());
            }
        }
    }

    private bool IsGround() =>
        Physics2D.OverlapCircle(m_GroundCheck.position, GroundedRadius, m_WhatIsGround) != null;

    private bool IsWall() =>
        !IsGround() && Physics2D.OverlapCircle(m_WallCheck.position, GroundedRadius, m_WhatIsGround) != null;

    IEnumerator DashCooldown()
    {
        _rigidbody2D.velocity = new Vector2(transform.localScale.x * m_DashForce, 0);
        _animator.SetBool(IsDashing, true);
        _isDashing = true;
        _canDash = false;
        yield return new WaitForSeconds(0.1f);
        _rigidbody2D.velocity = new Vector2(0, _rigidbody2D.velocity.y);
        _isDashing = false;
        yield return new WaitForSeconds(0.5f);
        _canDash = true;
    }

    IEnumerator Stun()
    {
        _canMove = false;
        yield return new WaitForSeconds(StunDuration);
        _canMove = true;
    }

    IEnumerator MakeInvincible()
    {
        invincible = true;
        yield return new WaitForSeconds(InvincibleDuration);
        invincible = false;
    }

    IEnumerator WaitToMove(float time)
    {
        _canMove = false;
        yield return new WaitForSeconds(time);
        _canMove = true;
    }

    IEnumerator WaitToCheck(float time)
    {
        _canCheck = false;
        yield return new WaitForSeconds(time);
        _canCheck = true;
    }

    IEnumerator WaitToEndSliding()
    {
        yield return new WaitForSeconds(0.1f);
        _canDoubleJump = true;
        _isWallSliding = false;
        _animator.SetBool(IsWallSliding, false);
        _oldWallSlidding = false;
        m_WallCheck.localPosition = new Vector3(Mathf.Abs(m_WallCheck.localPosition.x), m_WallCheck.localPosition.y, 0);
    }

    IEnumerator WaitToDead()
    {
        _animator.SetBool(IsDead, true);
        _canMove = false;
        invincible = true;
        GetComponent<Attack>().enabled = false;
        yield return new WaitForSeconds(0.4f);
        _rigidbody2D.velocity = new Vector2(0, _rigidbody2D.velocity.y);
        yield return new WaitForSeconds(1.1f);
        SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().buildIndex);
    }
}