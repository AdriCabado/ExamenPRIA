using Unity.Netcode;
using UnityEngine;

public class PlayerController : NetworkBehaviour
{
    [SerializeField] private float speed = 5f;

    private Rigidbody _rb;
    private Vector3 _lastPosition;      // Para fallback se o equipo está cheo
    private Color _assignedColor = Color.white;

    // 0 = sen equipo; 1 = Equipo 1; 2 = Equipo 2
    public NetworkVariable<int> _currentTeam =
        new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        // Se somos owner, pedimos teleport inicial ao servidor
        if (IsOwner)
        {
            RequestTeleportServerRpc();
        }

        // Suscribimos ao cambio de equipo para pintar cor
        _currentTeam.OnValueChanged += OnTeamChanged;

        // Aplicar a cor inicial (en caso de que o servidor xa a fixese antes de spawn)
        OnTeamChanged(0, _currentTeam.Value);
    }

    private void Update()
    {
        if (!IsOwner) return;

        // Gardar posición anterior en todo momento
        _lastPosition = transform.position;

        HandleMovementInput();
        HandleTeleportInput();
        HandleTeamCheck();
    }

    // Movimento con ASDW ou flechas
    private void HandleMovementInput()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 dir = new Vector3(h, 0f, v);

        if (dir.magnitude > 0.1f)
        {
            Vector3 move = dir.normalized * speed * Time.deltaTime;
            transform.Translate(move, Space.World);
        }
    }

    // Tecla "M" para teletransportarse á zona central
    private void HandleTeleportInput()
    {
        if (Input.GetKeyDown(KeyCode.M))
        {
            RequestTeleportServerRpc();
        }
    }

    // Comproba cada frame en que zona estamos e, se mudou, pídelle ao servidor cambiar de equipo
    private void HandleTeamCheck()
    {
        int newTeam = TeamManager.Instance.DetermineTeamByPosition(transform.position);
        int oldTeam = _currentTeam.Value;

        if (newTeam != oldTeam)
        {
            RequestChangeTeamServerRpc(newTeam);
        }
    }

    // ============================================================
    // == 1) TELEPORT: Cliente → Servidor
    // ============================================================
    [ServerRpc(RequireOwnership = false)]
    public void RequestTeleportServerRpc(ServerRpcParams rpcParams = default)
    {
        // Só o servidor pode reposicionar de verdade: calculamos nova posición no centro
        Vector3 centerPos = TeamManager.Instance.GetRandomCenterPosition();
        transform.position = centerPos;
    }

    // ============================================================
    // == 2) CAMBIO DE EQUIPO: Cliente → Servidor
    // ============================================================
    [ServerRpc(RequireOwnership = false)]
    public void RequestChangeTeamServerRpc(int desiredTeam, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        int oldTeam = _currentTeam.Value;

        // Se cabe no equipo desexado:
        if (TeamManager.Instance.CanJoinTeam(desiredTeam))
        {
            // 1) Se estaba noutro equipo, liberamos a cor dese equipo
            if (oldTeam == 1 || oldTeam == 2)
            {
                TeamManager.Instance.ReleaseColorFromTeam(oldTeam, clientId);
            }

            // 2) Engadimos ao novo equipo (0, 1 ou 2)
            TeamManager.Instance.AddPlayerToTeam(clientId, desiredTeam);
            _currentTeam.Value = desiredTeam;

            // 3) Xestionar cor segundo equipo
            if (desiredTeam == 1 || desiredTeam == 2)
            {
                Color newCol = TeamManager.Instance.GetRandomColorForTeam(desiredTeam);
                _assignedColor = newCol;
                TeamManager.Instance.RecordAssignedColor(clientId, newCol);
            }
            else
            {
                // Se volve ao centro, cor branca
                _assignedColor = Color.white;
            }

            // 4) Aplicar cor localmente en servidor (propagaráse aos clientes vía NetworkVariable)
            ApplyColor(_assignedColor);
        }
        else
        {
            // Equipo cheo: devolvemos ao client á posición anterior e avisámolo
            transform.position = _lastPosition;
            TeamManager.Instance.NotifyClientTeamFull(clientId);
        }
    }

    [ClientRpc]
    public void TeamFullClientRpc(ClientRpcParams rpcParams = default)
    {
        if (!IsOwner) return;
        Debug.Log("Equipo cheo! Non podes unirte a ese equipo de momento.");
        // Podes amosar aquí un aviso en pantalla (GUI, etc.)
    }

    // ============================================================
    // == 3) ONVALUECHANGED para _currentTeam: aplica cor
    // ============================================================
    private void OnTeamChanged(int oldTeam, int newTeam)
    {
        // El _assignedColor xa foi calculado no ServerRpc
        Color col = (newTeam == 1 || newTeam == 2) ? _assignedColor : Color.white;
        ApplyColor(col);
    }

    private void ApplyColor(Color col)
    {
        var renderer = GetComponent<Renderer>();
        if (renderer == null) return;
        renderer.material = new Material(renderer.sharedMaterial);
        renderer.material.color = col;
    }

    /// <summary>
    /// Chamado por GameManager cando se fai spawn inicial para poñer no servidor equipo 0 e cor branca.
    /// </summary>
    public void SetInitialTeamOnServer(int teamId)
    {
        _currentTeam.Value = teamId;
        _assignedColor = Color.white;
        ApplyColor(Color.white);
    }

    
}
