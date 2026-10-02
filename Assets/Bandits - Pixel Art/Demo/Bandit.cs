using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class Bandit : MonoBehaviour {

    [SerializeField] float      m_speed = 4.0f;
    [SerializeField] float      m_jumpForce = 7.5f;

    private Animator            m_animator;
    private Rigidbody2D         m_body2d;
    private Sensor_Bandit       m_groundSensor;
    private bool                m_grounded = false;
    private bool                m_combatIdle = false;
    private bool                m_isDead = false;

    // Actions from Assets/InputSystem_Actions (Project Settings > Input System Package)
    private InputAction         m_moveAction;
    private InputAction         m_jumpAction;
    private InputAction         m_attackAction;

    // Use this for initialization
    void Start () {
        m_animator = GetComponent<Animator>();
        m_body2d = GetComponent<Rigidbody2D>();
        m_groundSensor = transform.Find("GroundSensor").GetComponent<Sensor_Bandit>();

        m_moveAction = InputSystem.actions.FindAction("Player/Move", true);
        m_jumpAction = InputSystem.actions.FindAction("Player/Jump", true);
        m_attackAction = InputSystem.actions.FindAction("Player/Attack", true);
    }
	
	// Update is called once per frame
	void Update () {
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
        float inputX = m_moveAction.ReadValue<Vector2>().x;
        Keyboard keyboard = Keyboard.current;

        // Swap direction of sprite depending on walk direction
        if (inputX > 0)
            transform.localScale = new Vector3(-1.0f, 1.0f, 1.0f);
        else if (inputX < 0)
            transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);

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
            m_animator.SetTrigger("Jump");
            m_grounded = false;
            m_animator.SetBool("Grounded", m_grounded);
            m_body2d.linearVelocity = new Vector2(m_body2d.linearVelocity.x, m_jumpForce);
            m_groundSensor.Disable(0.2f);
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
}
