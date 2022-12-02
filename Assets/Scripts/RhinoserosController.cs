using System.Collections;
using System.Collections.Generic;
using Mirror;
using TMPro;
using UnityEngine;

public class RhinoserosController : NetworkBehaviour
{
    [SyncVar]
    public Color RhinoserosColor = Color.white; //������������� ����� ������

    [SyncVar(hook = nameof(SetConstraintsRotation))] //������������� ���������� �������� ������
    public RigidbodyConstraints RBC;

    [SyncVar(hook = nameof(Scoring))] //������������� �����
    public int SyncCountPuncture = 0;

    [SyncVar(hook = nameof(GetMagnitude))] //������������� ���� �����
    public float MagnitudeJerk;

    [SyncVar(hook = nameof(Invulnerability))] //������������� ������������
    public bool RhinoserosInvulnerability;

    [SyncVar(hook = nameof(SetRhinoserosNumber))] //������������� ������� �������
    public int RhinoserosNumber = 0;

    //�������� ������������
    [Header("Movement")]
    public float walkSpeed;
    public float sprintSpeed;
    public float jerkSpeed;

    private float moveSpeed;

    [Header("Keybinds")]
    public KeyCode sprintKey = KeyCode.LeftShift;
    public KeyCode jerkKey = KeyCode.Mouse0;
    public KeyCode ShowCursorKey = KeyCode.Escape; //���������� ������

    //���������� � ����������� ��� �����������
    [Header("Ground Check")]
    public float playerHeight;
    public LayerMask WhatIsGround;
    public bool grounded;

    //��������� �����
    [Header("Jerk Distance")]
    public float jerkDistance;

    //����������������� ������������
    [Header("Invulnerability Time")]
    public float invulnerabilityTime = 3f;

    //��� ����������� ������������
    public Transform orientation;

    float horizontalInput; //������ ��� ������������
    float verticalInput;

    public Vector3 moveDirection; //����������� ��������

    Rigidbody rb;

    bool isReadyJerk = false; //��� ��������� �����
    bool isShowCursor = false; //�������� ������
    
    public float HookMagnitude = 0f; //�������� ����� ����, ����������� � �����
    public bool isLockKey = false; //���������� ������, ����� ������� ������
    public bool isWait5Sec = false; //�������� �����������

    Camera PlayerView; 

    public TextMeshPro RhinoserosName; //��� ������ ��� �������
    public TextMeshProUGUI RhinoserosNameUI; //��� ������ �� ����� ������
    public TextMeshProUGUI CountPuncture; //���������� ��������� �� ������ �������
    public TextMeshProUGUI MainRhinoseros; //����������� ����������

    public GameObject TriggerCollider; //��������� ��� �������� ��������� �� ������.

    public MovementState state; //��������� ������������

    //��������� ����
    [Header("Mouse Settings")]
    public float sensitivityHor = 9.0f;
    public float sensitivityVert = 9.0f;
    public float minimumVert = -45.0f;
    public float maximumVert = 45.0f;

    //�������� ���� �� ��� �
    private float _rotationX = 0;

    IEnumerator coroutine;

    //��������� ������������
    public enum MovementState
    {
        walking,
        sprinting,
        jerk
    }

    //������� ��� �������� ������ �� �������� ������ � ��������� �����������
    [Command]
    public void CmdSetOffInvulnerability()
    {
        StartCoroutine(SetOffInvulnerability());
    }

    //������� ��� �������� ����� ������
    [Command]
    public void CmdScoreAPoint()
    {
        SyncCountPuncture++;
    }

    //������� ��� ���������� ���� ������� � ������������� ����
    [Command]
    public void CmdGetRhinoserosNumber()
    {
        RhinoserosNumber = NetworkServer.connections.Count;
    }

    //������� ��� ������������� ���������� ��������
    [Command]
    public void CmdSetConstraintsRotation()
    {
        RBC = RigidbodyConstraints.FreezeRotation;
    }

    //������� ��� ������������� ���� �����
    [Command]
    public void CmdGetRhinoserosMagJerk(Vector3 DirJerk)
    {
        MagnitudeJerk = DirJerk.magnitude;
    }

    //������� ��� ������� RPC - ���������� ����, ����� ��������� ������� � �������.
    [Command] 
    public void CmdResetPlayer()
    {
        List<Vector3> startPositions = new List<Vector3>();
        Vector3 startPosition;
        foreach (var Conn in NetworkServer.connections)
        {
            startPosition = NetworkManager.singleton.GetStartPosition().position;
            startPositions.Add(NetworkManager.singleton.GetStartPosition().position);
            if (!startPositions.Contains(startPosition))
            {
                startPositions.Add(startPosition);
                Conn.Value.identity.gameObject.GetComponent<RhinoserosController>().RpcResetPlayer(RhinoserosNameUI.text + " MAIN!", startPosition);
            }
        }
    }

    //RPC - ���������� ����, ����� ��������� ������� � �������.
    [ClientRpc]
    public void RpcResetPlayer(string Main, Vector3 startPosition)
    {
        StartCoroutine(Wait5Sec(Main, startPosition));
    }

    //��� ��� ������������� ���� �����
    void GetMagnitude(float OldDirMag, float NewDirMag)
    {
        HookMagnitude = NewDirMag;
    }

    //��� ��� ������������� ���������� ��������
    void SetConstraintsRotation(RigidbodyConstraints OldRBC, RigidbodyConstraints NewRBC)
    {
        gameObject.GetComponent<Rigidbody>().constraints = NewRBC;
    }

    //��� ��� �������� ����� ������
    void Scoring(int OldScore, int NewScore)
    {
        CountPuncture.text = "COUNT PUNCTURE: " + NewScore.ToString();
        if (NewScore == 3)
            if (!isWait5Sec)
                CmdResetPlayer();
    }

    //��� ��� ���������� ���� ������� � ������������� ����
    void SetRhinoserosNumber(int OldNumber, int NewNumber)
    {
        gameObject.name = "RHINOSEROS " + NewNumber.ToString();
        RhinoserosName.text = gameObject.name;
        RhinoserosNameUI.text = gameObject.name;
        RhinoserosNameUI.enabled = true;
    }

    //��� ��� ������������� ������������
    void Invulnerability(bool OldInvulnerability, bool NewInvulnerability)
    {
        gameObject.GetComponent<MeshRenderer>().material.color = RhinoserosColor;
        if (RhinoserosInvulnerability)
        {
            gameObject.layer = LayerMask.NameToLayer("Invulnerability");
            gameObject.transform.Find("TriggerCollider").gameObject.layer = LayerMask.NameToLayer("Invulnerability");
        }
        else
        {
            gameObject.layer = LayerMask.NameToLayer("Default");
            gameObject.transform.Find("TriggerCollider").gameObject.layer = LayerMask.NameToLayer("Default");
        }
    }

    //������������ ��� ����� � ��������� ������������ � ������ �� ����
    private IEnumerator SetOffInvulnerability()
    {
        RhinoserosColor = Color.green;
        RhinoserosInvulnerability = true;
        yield return new WaitForSeconds(invulnerabilityTime);
        RhinoserosColor = Color.white;
        RhinoserosInvulnerability = false;
    }

    //������������ ��� ����������� ��������� �������
    IEnumerator Wait5Sec(string Main, Vector3 startPosition)
    {
        isLockKey = true;
        MainRhinoseros.enabled = true;
        MainRhinoseros.text = Main;
        isWait5Sec = true;
        yield return new WaitForSeconds(5f);
        isWait5Sec = false;
        gameObject.transform.position = startPosition;
        isLockKey = false;
        MainRhinoseros.enabled = false;
        MainRhinoseros.text = "";
        SyncCountPuncture = 0;
        CountPuncture.text = "COUNT PUNCTURE: 0";
    }


    public override void OnStartLocalPlayer()
    {
        rb = GetComponent<Rigidbody>();
        rb.inertiaTensorRotation = Quaternion.identity;
        CmdGetRhinoserosNumber();
        CmdSetConstraintsRotation();
        gameObject.layer = LayerMask.NameToLayer("Default");
        gameObject.transform.Find("TriggerCollider").gameObject.layer = LayerMask.NameToLayer("Default");

        Camera.main.transform.SetParent(gameObject.transform);
        PlayerView = Camera.main;
        PlayerView.name = "PlayerView";
        orientation = PlayerView.transform;
        CountPuncture.enabled = true;
        Cursor.lockState = CursorLockMode.Locked;
        ViewPosition();
    }

    void ViewPosition() //������� ������
    {
        Vector3 CameraPosition = gameObject.transform.position;
        CameraPosition.y += 0.5f;
        PlayerView.transform.position = CameraPosition;
        PlayerView.transform.position -= PlayerView.transform.forward * 4;
        Vector3 originRay = gameObject.transform.position;
        Vector3 directionRay = PlayerView.transform.position - originRay;
        RaycastHit hit;
        if (Physics.Raycast(originRay, directionRay, out hit)) //��������� ������������ �� ������ � ������� ���������
        {
            if (hit.distance < Vector3.Magnitude(directionRay)) //���� ������ ������ ������ ������� ������ �� ����� ���� �� ������� ���������
                PlayerView.transform.position = hit.point * 0.95f;
        }
    }

    //���� ������ ESC ������ ���������� � ������. ������ �� ������� �� ������� ������
    void ShowCursor()
    {
        if (!isLockKey)
        {
            if (Input.GetKeyDown(ShowCursorKey))
            {
                isShowCursor = !isShowCursor;
            }
            if (isShowCursor)
                Cursor.lockState = CursorLockMode.Confined;
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
            }
        }
    }

    // Update is called once per frame
    private void Update()
    {
        if (!isLocalPlayer)
        {
            RhinoserosName.gameObject.GetComponent<MeshRenderer>().enabled = true;
            RhinoserosName.gameObject.transform.LookAt(Camera.main.transform);
            RhinoserosName.gameObject.transform.Rotate(new Vector3(0f, 180f, 0f));
            RhinoserosNameUI.enabled = false;
            CountPuncture.enabled = false;
            return;
        }

        // ground check
        grounded = Physics.Raycast(gameObject.transform.position, Vector3.down, playerHeight * 0.5f + 0.2f, WhatIsGround);

        MyInput();
        SpeedControl();
        StateHandler();

        if (isLocalPlayer)
        {
            ShowCursor();
        }
        //�������������� ������
        if (!isShowCursor && !isLockKey)
        {
            _rotationX -= Input.GetAxis("Mouse Y") * sensitivityVert;
            _rotationX = Mathf.Clamp(_rotationX, minimumVert, maximumVert);
            float delta = Input.GetAxis("Mouse X") * sensitivityHor;
            float rotationY = PlayerView.transform.localEulerAngles.y + delta;
            PlayerView.transform.localEulerAngles = new Vector3(_rotationX, rotationY, 0);
            ViewPosition();
        }

        ///////////////////////

        coroutine = JerkOrMovePlayer();
        StartCoroutine(coroutine);
        if (gameObject.transform.Find("TriggerCollider").GetComponent<SetInvulnerability>().isHit) 
        {
            gameObject.transform.Find("TriggerCollider").GetComponent<SetInvulnerability>().isHit = false;
            rb.inertiaTensorRotation = Quaternion.identity;
            CmdSetOffInvulnerability();
        }
        if (gameObject.transform.Find("TriggerCollider").GetComponent<SetInvulnerability>().isPoint)
        {
            gameObject.transform.Find("TriggerCollider").GetComponent<SetInvulnerability>().isPoint = false;
            CmdScoreAPoint();
        }
    }

    IEnumerator JerkOrMovePlayer() //����� ����� ���� ���� � ��������� ����� ���� ������������, ���� �� �� �������� ����� �� �� ������ ������� �����������.
    {
        if (Input.GetKeyDown(jerkKey))
        {
            Vector3 RhinoserosStartPosition = gameObject.transform.position;
            
            if (!moveDirection.Equals(Vector3.zero))
            {
                isReadyJerk = true;
                Vector3 DirectionJerk = Vector3.zero;
                
                float conclusiveJerkTime = jerkDistance / (jerkSpeed * 10);
                float startTime = Time.time;
                float currentTime = 0f;
                float currentDistance;
                while (DirectionJerk.magnitude < jerkDistance)
                {
                    currentDistance = DirectionJerk.magnitude;
                    rb.AddForce(moveDirection.normalized * jerkSpeed * 10, ForceMode.VelocityChange);
                    yield return new WaitForFixedUpdate();
                    DirectionJerk = RhinoserosStartPosition - gameObject.transform.position; //��������� ���������� ���������� � ������� ������ ����� � �������� ��� �������
                    CmdGetRhinoserosMagJerk(DirectionJerk);
                    currentTime = Time.time;
                    if (currentTime - startTime > conclusiveJerkTime || currentDistance == DirectionJerk.magnitude) //���� ������ ������ �������, ��� ������ ����, � ����� ��� �� �������� �����,
                                                                                                                    //���, ���� ����� �� ����������� � ���������� ����������� ����,
                                                                                                                    //������ �� �������� - ������� �� �����
                        break;
                }
                DirectionJerk = Vector3.zero;
                CmdGetRhinoserosMagJerk(DirectionJerk);
                isReadyJerk = false;
            }
        }
        else if (!isReadyJerk)
        {
            MovePlayer();
        }
    }

    private void MyInput()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");
    }

    bool GetDirection() //���������� ������� ��������
    {
        moveDirection = orientation.forward * verticalInput + orientation.right * horizontalInput;
        moveDirection.y = 0f;
        if (moveDirection != Vector3.zero)
            return true;
        return false;
    }

    private void StateHandler()
    {
        //Mode - Sprinting
        if(grounded && Input.GetKey(sprintKey))
        {
            state = MovementState.sprinting;
            moveSpeed = sprintSpeed;
        }

        //Mode - walking
        else if (grounded)
        {
            state = MovementState.walking;
            moveSpeed = walkSpeed;
        }
    }

    private void MovePlayer()
    {
        // calculate movement direction
        
        if (!GetDirection())
            rb.velocity = Vector3.zero;

        //on ground
        if(grounded && GetDirection())
        {
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f, ForceMode.Force);
        }
    }

    private void SpeedControl()
    {
        Vector3 flatVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);

        //limit velocity if needed
        if(flatVel.magnitude > moveSpeed)
        {
            Vector3 limitedVel = flatVel.normalized * moveSpeed;
            rb.velocity = new Vector3(limitedVel.x, rb.velocity.y, limitedVel.z);
        }
    }
}