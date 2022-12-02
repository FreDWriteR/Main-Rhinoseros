using System.Collections;
using System.Collections.Generic;
using Mirror;
using TMPro;
using UnityEngine;

public class RhinoserosController : NetworkBehaviour
{
    [SyncVar]
    public Color RhinoserosColor = Color.white; //Синхронизация цвета игрока

    [SyncVar(hook = nameof(SetConstraintsRotation))] //Синхронизация блокировки вращения игрока
    public RigidbodyConstraints RBC;

    [SyncVar(hook = nameof(Scoring))] //Синхронизация очков
    public int SyncCountPuncture = 0;

    [SyncVar(hook = nameof(GetMagnitude))] //Синхронизация длин рывка
    public float MagnitudeJerk;

    [SyncVar(hook = nameof(Invulnerability))] //Синхронизация неуязвимости
    public bool RhinoserosInvulnerability;

    [SyncVar(hook = nameof(SetRhinoserosNumber))] //Синхронизация номеров игроков
    public int RhinoserosNumber = 0;

    //Скорости передвижения
    [Header("Movement")]
    public float walkSpeed;
    public float sprintSpeed;
    public float jerkSpeed;

    private float moveSpeed;

    [Header("Keybinds")]
    public KeyCode sprintKey = KeyCode.LeftShift;
    public KeyCode jerkKey = KeyCode.Mouse0;
    public KeyCode ShowCursorKey = KeyCode.Escape; //Отобразить курсор

    //Информация о поверхности для перевижения
    [Header("Ground Check")]
    public float playerHeight;
    public LayerMask WhatIsGround;
    public bool grounded;

    //Дистанция рывка
    [Header("Jerk Distance")]
    public float jerkDistance;

    //Продолжительность неуязвимости
    [Header("Invulnerability Time")]
    public float invulnerabilityTime = 3f;

    //Для направления передвижения
    public Transform orientation;

    float horizontalInput; //Инпуты для передвижения
    float verticalInput;

    public Vector3 moveDirection; //Направление движения

    Rigidbody rb;

    bool isReadyJerk = false; //Для удержания рывка
    bool isShowCursor = false; //Показать курсор
    
    public float HookMagnitude = 0f; //Перехват длины пути, пройденного в рывке
    public bool isLockKey = false; //Блокировка камеры, когда показан курсор
    public bool isWait5Sec = false; //Ожидание перезапуска

    Camera PlayerView; 

    public TextMeshPro RhinoserosName; //Имя игрока над головой
    public TextMeshProUGUI RhinoserosNameUI; //Имя игрока на канве экрана
    public TextMeshProUGUI CountPuncture; //Количество попаданий по другим игрокам
    public TextMeshProUGUI MainRhinoseros; //Отображение победителя

    public GameObject TriggerCollider; //Коллайдер для коллизии попадания по игроку.

    public MovementState state; //Состояние передвижения

    //Настройки мыши
    [Header("Mouse Settings")]
    public float sensitivityHor = 9.0f;
    public float sensitivityVert = 9.0f;
    public float minimumVert = -45.0f;
    public float maximumVert = 45.0f;

    //Вращение мыши по оси Х
    private float _rotationX = 0;

    IEnumerator coroutine;

    //Состояния передвижения
    public enum MovementState
    {
        walking,
        sprinting,
        jerk
    }

    //Команда для перевода игрока по которому попали в состояние невидимости
    [Command]
    public void CmdSetOffInvulnerability()
    {
        StartCoroutine(SetOffInvulnerability());
    }

    //Команда для подсчета очков игрока
    [Command]
    public void CmdScoreAPoint()
    {
        SyncCountPuncture++;
    }

    //Команда для присвоения имен игрокам и синхронизации имен
    [Command]
    public void CmdGetRhinoserosNumber()
    {
        RhinoserosNumber = NetworkServer.connections.Count;
    }

    //Команда для синхронизации блокировки вращения
    [Command]
    public void CmdSetConstraintsRotation()
    {
        RBC = RigidbodyConstraints.FreezeRotation;
    }

    //Команда для синхронизации длин рывка
    [Command]
    public void CmdGetRhinoserosMagJerk(Vector3 DirJerk)
    {
        MagnitudeJerk = DirJerk.magnitude;
    }

    //Команда для запуска RPC - перезапуск игры, сброс попаданий игроков и респаун.
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

    //RPC - перезапуск игры, сброс попаданий игроков и респаун.
    [ClientRpc]
    public void RpcResetPlayer(string Main, Vector3 startPosition)
    {
        StartCoroutine(Wait5Sec(Main, startPosition));
    }

    //Хук для синхронизации длин рывка
    void GetMagnitude(float OldDirMag, float NewDirMag)
    {
        HookMagnitude = NewDirMag;
    }

    //Хук для синхронизации блокировки вращения
    void SetConstraintsRotation(RigidbodyConstraints OldRBC, RigidbodyConstraints NewRBC)
    {
        gameObject.GetComponent<Rigidbody>().constraints = NewRBC;
    }

    //Хук для подсчета очков игрока
    void Scoring(int OldScore, int NewScore)
    {
        CountPuncture.text = "COUNT PUNCTURE: " + NewScore.ToString();
        if (NewScore == 3)
            if (!isWait5Sec)
                CmdResetPlayer();
    }

    //Хук для присвоения имен игрокам и синхронизации имен
    void SetRhinoserosNumber(int OldNumber, int NewNumber)
    {
        gameObject.name = "RHINOSEROS " + NewNumber.ToString();
        RhinoserosName.text = gameObject.name;
        RhinoserosNameUI.text = gameObject.name;
        RhinoserosNameUI.enabled = true;
    }

    //Хук для синхронизации неуязвимости
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

    //Подпрограмма для ввода в состояния неуязвимости и вывода из него
    private IEnumerator SetOffInvulnerability()
    {
        RhinoserosColor = Color.green;
        RhinoserosInvulnerability = true;
        yield return new WaitForSeconds(invulnerabilityTime);
        RhinoserosColor = Color.white;
        RhinoserosInvulnerability = false;
    }

    //Подпрограмма для перезапуска состояний игроков
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

    void ViewPosition() //Позиция камеры
    {
        Vector3 CameraPosition = gameObject.transform.position;
        CameraPosition.y += 0.5f;
        PlayerView.transform.position = CameraPosition;
        PlayerView.transform.position -= PlayerView.transform.forward * 4;
        Vector3 originRay = gameObject.transform.position;
        Vector3 directionRay = PlayerView.transform.position - originRay;
        RaycastHit hit;
        if (Physics.Raycast(originRay, directionRay, out hit)) //Проверяем приблизилась ли камера к объекту окружения
        {
            if (hit.distance < Vector3.Magnitude(directionRay)) //если камера близко меняем позицию камеры на точку хита на объекте окружения
                PlayerView.transform.position = hit.point * 0.95f;
        }
    }

    //Если нажать ESC курсор отобрацися в центре. Курсор не заходит за граници экрана
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
        //Преобразование камеры
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

    IEnumerator JerkOrMovePlayer() //Игрок может быть либо в состоянии рывка либо передвижения, пока он не закончит рывок он не сможет сменить направление.
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
                    DirectionJerk = RhinoserosStartPosition - gameObject.transform.position; //Вычисляем расстояние пройденное с момента начала рывка и сообщаем его серверу
                    CmdGetRhinoserosMagJerk(DirectionJerk);
                    currentTime = Time.time;
                    if (currentTime - startTime > conclusiveJerkTime || currentDistance == DirectionJerk.magnitude) //Если прошло больше времени, чем должно было, а игрок еще не закончил рывок,
                                                                                                                    //или, если игрок не продвинулся в результате воздействия силы,
                                                                                                                    //значит он врезался - выходим из цикла
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

    bool GetDirection() //Вичисление вектора движения
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