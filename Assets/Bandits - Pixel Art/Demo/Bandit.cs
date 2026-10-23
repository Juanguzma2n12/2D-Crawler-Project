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
    [SerializeField] float      m_swimFloatDepth = 0.4f;
    [Tooltip("How far (degrees) the bandit leans into the swim direction. 90 = lying flat.")]
    [SerializeField] float      m_swimLeanAngle = 75.0f;
    [Tooltip("Walking speed multiplier in water that is too shallow to swim in.")]
    [SerializeField] float      m_wadeSpeedMultiplier = 0.7f;

    private Animator            m_animator;
    private SpriteRenderer      m_renderer;
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
    private PixelWater          m_water;        // water the bandit is swimming in
    private PixelWater          m_wadeWater;    // water around the bandit's feet
    private float               m_defaultGravityScale;
    private ContactFilter2D     m_waterFilter;
    private readonly List<Collider2D> m_waterHits = new List<Collider2D>();

    // The animations have no swim pose, so while swimming the sprite is drawn by a copy that can be rotated
    // (rotating the bandit itself would rotate its collider too)
    private Transform           m_swimPivot;
    private SpriteRenderer      m_swimRenderer;
    private float               m_swimAngle;

    public bool IsSwimming { get { return m_water != null; } }

    // True for the one frame Up (W / Up arrow / stick up) is pressed. Used by doors and teleporters.
    public bool UpPressedThisFrame { get; private set; }

    // Where the bandit was placed in the scene. Used for respawning when no checkpoint is active.
    public Vector3 SpawnPosition { get; private set; }

    // Use this for initialization
    void Start () {
        m_animator = GetComponent<Animator>();
        m_renderer = GetComponent<SpriteRenderer>();
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

        CreateSwimPose();
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

        // Move (slower when wading through shallow water)
        float speed = m_wadeWater != null ? m_speed * m_wadeSpeedMultiplier : m_speed;
        m_body2d.linearVelocity = new Vector2(inputX * speed, m_body2d.linearVelocity.y);

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
        else if (m_wadeWater != null)
            RippleSurface(m_wadeWater);
    }

    void LateUpdate () {
        UpdateSwimPose();
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

        RippleSurface(m_water);
    }

    // Ripple the surface where the body crosses it while moving
    void RippleSurface (PixelWater water) {
        Bounds bounds = m_bodyCollider.bounds;
        if (bounds.min.y > water.TopPos || bounds.max.y < water.TopPos)
            return;

        float vx = m_body2d.linearVelocity.x * water.waveInfluenceMul.x * Time.fixedDeltaTime;
        Vector3 side = new Vector3(bounds.extents.x, 0.0f);
        water.AddWave(bounds.center - side, 0.2f, -vx);
        water.AddWave(bounds.center + side, 0.2f, vx);
    }

    void UpdateWaterState () {
        Bounds bounds = m_bodyCollider.bounds;

        // Swimming once the body center is below the surface; wading while only the feet are in
        PixelWater water = FindWaterAt(bounds.center);
        PixelWater wadeWater = water == null ? FindWaterAt(new Vector2(bounds.center.x, bounds.min.y + 0.05f)) : null;

        // Splash when stepping or landing in shallow water
        if (wadeWater != null && m_wadeWater == null && m_water == null)
            wadeWater.AddSplash(new Vector3(bounds.center.x, wadeWater.TopPos), bounds.size.x, Mathf.Abs(m_body2d.linearVelocity.y));
        m_wadeWater = wadeWater;

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

    PixelWater FindWaterAt (Vector2 point) {
        if (Physics2D.OverlapPoint(point, m_waterFilter, m_waterHits) == 0)
            return null;
        return m_waterHits[0].GetComponent<PixelWater>();
    }

    // -- Swim pose --

    void CreateSwimPose () {
        m_swimPivot = new GameObject("SwimPose").transform;
        m_swimPivot.SetParent(transform, false);
        m_swimPivot.localPosition = m_bodyCollider.offset;      // rotate around the middle of the body

        Transform visual = new GameObject("Sprite").transform;
        visual.SetParent(m_swimPivot, false);
        visual.localPosition = -m_bodyCollider.offset;

        m_swimRenderer = visual.gameObject.AddComponent<SpriteRenderer>();
        m_swimRenderer.sharedMaterial = m_renderer.sharedMaterial;
        m_swimRenderer.sortingLayerID = m_renderer.sortingLayerID;
        m_swimRenderer.sortingOrder = m_renderer.sortingOrder;
        m_swimRenderer.enabled = false;
    }

    void UpdateSwimPose () {
        // Lean into the swim direction: nearly flat when swimming sideways, head first when diving,
        // upright when treading water
        float targetAngle = 0.0f;
        if (IsSwimming) {
            Vector2 velocity = m_body2d.linearVelocity;
            if (velocity.sqrMagnitude > 0.5f) {
                // 0 = moving straight up, 90 = sideways, 180 = straight down
                float angle = Mathf.Atan2(Mathf.Abs(velocity.x), velocity.y) * Mathf.Rad2Deg;
                targetAngle = angle <= 90.0f
                    ? angle / 90.0f * m_swimLeanAngle
                    : Mathf.Lerp(m_swimLeanAngle, 160.0f, (angle - 90.0f) / 90.0f);
            }
        }
        m_swimAngle = Mathf.MoveTowards(m_swimAngle, targetAngle, 300.0f * Time.deltaTime);

        bool usePose = IsSwimming || m_swimAngle > 0.5f;
        m_swimRenderer.enabled = usePose;
        m_renderer.enabled = !usePose;
        if (!usePose)
            return;

        // The sprite faces left, so a positive angle leans it forward; the flipped scale mirrors it when facing right
        float bob = IsSwimming ? Mathf.Sin(Time.time * 4.0f) * 0.04f : 0.0f;
        m_swimPivot.localPosition = (Vector3)m_bodyCollider.offset + Vector3.up * bob;
        m_swimPivot.localRotation = Quaternion.Euler(0.0f, 0.0f, m_swimAngle);

        m_swimRenderer.sprite = m_renderer.sprite;
        m_swimRenderer.flipX = m_renderer.flipX;
        m_swimRenderer.color = m_renderer.color;
    }
}
