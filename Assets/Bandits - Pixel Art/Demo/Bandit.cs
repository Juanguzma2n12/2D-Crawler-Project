using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using Cainos.InteractivePixelWater;

public class Bandit : MonoBehaviour {

    [SerializeField] float      m_speed = 4.0f;
    [SerializeField] float      m_jumpForce = 6.0f;

    [Header("Swimming")]
    [SerializeField] float      m_swimSpeed = 3.0f;
    [SerializeField] float      m_swimAcceleration = 12.0f;
    [Tooltip("How far below the water surface the body floats when idle (keeps the head above water).")]
    [SerializeField] float      m_swimFloatDepth = 0.6f;

    private Animator            m_animator;
    private Rigidbody2D         m_body2d;
    private Collider2D          m_bodyCollider;
    private Sensor_Bandit       m_groundSensor;
    private bool                m_grounded = false;
    private bool                m_combatIdle = false;
    private bool                m_isDead = false;

    // Actions from Assets/InputSystem_Actions (Project Settings > Input System Package)
    private InputAction         m_moveAction;
    private InputAction         m_jumpAction;
    private InputAction         m_attackAction;
    private bool                m_upHeld;

    // Water
    private PixelWater          m_water;
    private float               m_defaultGravityScale;
    private ContactFilter2D     m_waterFilter;
    private readonly List<Collider2D> m_waterHits = new List<Collider2D>();

    public bool IsSwimming { get { return m_water != null; } }

    // True for the one frame Up (W / Up arrow / stick up) is pressed. Used by doors and teleporters.
    public bool UpPressedThisFrame { get; private set; }

    // Where the bandit was placed in the scene. Used for respawning when no checkpoint is active.
    public Vector3 SpawnPosition { get; private set; }

    // Use this for initialization
    void Start () {
        m_animator = GetComponent<Animator>();
        m_body2d = GetComponent<Rigidbody2D>();
        m_bodyCollider = GetComponent<Collider2D>();
        m_groundSensor = transform.Find("GroundSensor").GetComponent<Sensor_Bandit>();
        m_defaultGravityScale = m_body2d.gravityScale;
        SpawnPosition = transform.position;

        m_moveAction = InputSystem.actions.FindAction("Player/Move", true);
        m_jumpAction = InputSystem.actions.FindAction("Player/Jump", true);
        m_attackAction = InputSystem.actions.FindAction("Player/Attack", true);

        // The water's buoyancy effector is tuned for crates and barrels and would launch the bandit out
        // like a cork, so the bandit ignores water physics and swims with its own movement instead.
        int waterMask = LayerMask.GetMask("Water");
        m_body2d.excludeLayers |= waterMask;
        m_waterFilter.SetLayerMask(waterMask);
        m_waterFilter.useTriggers = true;
    }

	// Update is called once per frame
	void Update () {
        Vector2 input = m_moveAction.ReadValue<Vector2>();
        float inputX = input.x;
        Keyboard keyboard = Keyboard.current;

        bool upHeld = input.y > 0.5f;
        UpPressedThisFrame = upHeld && !m_upHeld;
        m_upHeld = upHeld;

        // Swap direction of sprite depending on walk direction
        if (inputX > 0)
            transform.localScale = new Vector3(-1.0f, 1.0f, 1.0f);
        else if (inputX < 0)
            transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);

        if (IsSwimming) {
            UpdateSwimming(inputX);
            return;
        }

        //Check if character just landed on the ground
        if (!m_grounded && m_groundSensor.State()) {
            m_grounded = true;
            m_animator.SetBool("Grounded", m_grounded);
        }

        //Check if character just started falling
        if(m_grounded && !m_groundSensor.State()) {
            m_grounded = false;
            m_animator.SetBool("Grounded", m_grounded);
        }

        // -- Handle input and movement --

        // Move
        m_body2d.linearVelocity = new Vector2(inputX * m_speed, m_body2d.linearVelocity.y);

        //Set AirSpeed in animator
        m_animator.SetFloat("AirSpeed", m_body2d.linearVelocity.y);

        // -- Handle Animations --
        //Death
        if (keyboard != null && keyboard.eKey.wasPressedThisFrame) {
            if(!m_isDead)
                m_animator.SetTrigger("Death");
            else
                m_animator.SetTrigger("Recover");

            m_isDead = !m_isDead;
        }

        //Hurt
        else if (keyboard != null && keyboard.qKey.wasPressedThisFrame)
            m_animator.SetTrigger("Hurt");

        //Attack
        else if(m_attackAction.WasPressedThisFrame()) {
            m_animator.SetTrigger("Attack");
        }

        //Change between idle and combat idle
        else if (keyboard != null && keyboard.fKey.wasPressedThisFrame)
            m_combatIdle = !m_combatIdle;

        //Jump
        else if (m_jumpAction.WasPressedThisFrame() && m_grounded) {
            Jump();
        }

        //Run
        else if (Mathf.Abs(inputX) > Mathf.Epsilon)
            m_animator.SetInteger("AnimState", 2);

        //Combat Idle
        else if (m_combatIdle)
            m_animator.SetInteger("AnimState", 1);

        //Idle
        else
            m_animator.SetInteger("AnimState", 0);
    }

    void FixedUpdate () {
        UpdateWaterState();

        if (IsSwimming)
            Swim();
    }

    // Instantly moves the bandit, e.g. for respawning or teleporting
    public void TeleportTo (Vector3 position) {
        if (IsSwimming)
            ExitWater(false);

        m_body2d.position = position;
        transform.position = position;
        m_body2d.linearVelocity = Vector2.zero;
        m_groundSensor.Disable(0.1f);
    }

    void Jump () {
        m_animator.SetTrigger("Jump");
        m_grounded = false;
        m_animator.SetBool("Grounded", m_grounded);
        m_body2d.linearVelocity = new Vector2(m_body2d.linearVelocity.x, m_jumpForce);
        m_groundSensor.Disable(0.2f);
    }

    // -- Swimming --

    void UpdateSwimming (float inputX) {
        m_animator.SetFloat("AirSpeed", 0.0f);

        if (m_attackAction.WasPressedThisFrame())
            m_animator.SetTrigger("Attack");

        if (m_jumpAction.WasPressedThisFrame()) {
            // Near the surface: leap out of the water. Deeper down: a quick upward stroke.
            if (m_bodyCollider.bounds.center.y > m_water.TopPos - m_swimFloatDepth - 0.4f)
                Jump();
            else
                m_body2d.linearVelocity = new Vector2(m_body2d.linearVelocity.x, m_swimSpeed * 1.5f);
        }

        // There is no swim animation, so use run while moving and combat idle for treading water
        bool moving = m_moveAction.ReadValue<Vector2>().sqrMagnitude > 0.01f;
        m_animator.SetInteger("AnimState", moving ? 2 : 1);
    }

    void Swim () {
        Vector2 input = m_moveAction.ReadValue<Vector2>();
        float centerY = m_bodyCollider.bounds.center.y;
        float floatY = m_water.TopPos - m_swimFloatDepth;
        float toSurface = Mathf.Clamp((floatY - centerY) * 3.0f, -m_swimSpeed, m_swimSpeed);

        float targetY;
        if (input.y > 0.1f)
            targetY = Mathf.Min(input.y * m_swimSpeed, toSurface);     // swim up, but stop at the surface
        else if (input.y < -0.1f)
            targetY = input.y * m_swimSpeed;                            // dive
        else
            targetY = Mathf.Min(toSurface, m_swimSpeed * 0.4f);         // slowly float back up

        Vector2 target = new Vector2(input.x * m_swimSpeed, targetY);
        m_body2d.linearVelocity = Vector2.MoveTowards(m_body2d.linearVelocity, target, m_swimAcceleration * Time.fixedDeltaTime);

        // Ripple the surface while swimming along it
        Bounds bounds = m_bodyCollider.bounds;
        if (bounds.min.y < m_water.TopPos && bounds.max.y > m_water.TopPos) {
            float vx = m_body2d.linearVelocity.x * m_water.waveInfluenceMul.x * Time.fixedDeltaTime;
            m_water.AddWave(bounds.center - new Vector3(bounds.extents.x, 0.0f), 0.2f, -vx);
            m_water.AddWave(bounds.center + new Vector3(bounds.extents.x, 0.0f), 0.2f, vx);
        }
    }

    void UpdateWaterState () {
        // The bandit counts as in the water once its body center is below the surface
        PixelWater water = null;
        if (Physics2D.OverlapPoint(m_bodyCollider.bounds.center, m_waterFilter, m_waterHits) > 0)
            water = m_waterHits[0].GetComponent<PixelWater>();

        if (water == m_water)
            return;

        if (m_water != null)
            ExitWater(true);
        if (water != null)
            EnterWater(water);
    }

    void EnterWater (PixelWater water) {
        m_water = water;
        m_body2d.gravityScale = 0.0f;
        m_grounded = false;
        m_animator.SetBool("Grounded", true);   // use the ground animations while swimming

        Bounds bounds = m_bodyCollider.bounds;
        float speedY = Mathf.Abs(m_body2d.linearVelocity.y);
        water.AddSplash(new Vector3(bounds.center.x, water.TopPos), bounds.size.x, speedY);
        water.AddWave(bounds.center, bounds.extents.x, m_body2d.linearVelocity.y * water.waveInfluenceMul.y);
        water.AddBubble(m_bodyCollider);
    }

    void ExitWater (bool withSplash) {
        if (withSplash) {
            Bounds bounds = m_bodyCollider.bounds;
            m_water.AddSplash(new Vector3(bounds.center.x, m_water.TopPos), bounds.size.x, Mathf.Abs(m_body2d.linearVelocity.y));
            m_water.AddWave(bounds.center, bounds.extents.x, m_body2d.linearVelocity.y * m_water.waveInfluenceMul.y);
        }

        m_water = null;
        m_body2d.gravityScale = m_defaultGravityScale;
        m_grounded = m_groundSensor.State();
        m_animator.SetBool("Grounded", m_grounded);
    }
}
