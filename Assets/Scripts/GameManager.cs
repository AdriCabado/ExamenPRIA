using Unity.Netcode;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    private NetworkManager _netManager;
    private TeamManager _teamManager; // referencia buscada dinámicamente

    // Límite de players por equipo en string para o TextField
    private string _maxPlayersInput = "2";
    private int _maxPlayersPerTeam = 2;

    private void Awake()
    {
        // 1) Obter o NetworkManager do mesmo GameObject
        _netManager = GetComponent<NetworkManager>();
        if (_netManager == null)
        {
            Debug.LogError("GameManager debe ir no mesmo GameObject que NetworkManager.");
            enabled = false;
            return;
        }

        // 2) Buscar o TeamManager en toda a escena (non confiar en Instance nun primeiro momento)
        _teamManager = FindObjectOfType<TeamManager>();
        if (_teamManager == null)
        {
            Debug.LogError("Non se atopou ningunha instancia de TeamManager na escena. Asegúrate de engadir un GameObject con TeamManager.");
        }
        else
        {
            _teamManager.Initialize(_maxPlayersPerTeam);
        }

        // 3) Activar aprobación de conexión
        _netManager.NetworkConfig.ConnectionApproval = true;
        _netManager.ConnectionApprovalCallback += ApproveOrReject;
        _netManager.OnClientConnectedCallback += OnClientConnected;
        _netManager.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 260, 400));
        GUILayout.BeginVertical("box");

        // 1) Se non somos cliente nin servidor, mostramos botons Host/Client/Server
        if (!_netManager.IsClient && !_netManager.IsServer)
        {
            if (GUILayout.Button("Host"))
                _netManager.StartHost();
            if (GUILayout.Button("Client"))
                _netManager.StartClient();
            if (GUILayout.Button("Server"))
                _netManager.StartServer();
        }
        else
        {
            // 2) Mostramos transporte e modo actual
            string mode = _netManager.IsHost   ? "Host"
                         : _netManager.IsServer ? "Server"
                         : _netManager.IsClient ? "Client"
                                                 : "None";
            GUILayout.Label($"Transporte: {_netManager.NetworkConfig.NetworkTransport.GetType().Name}");
            GUILayout.Label($"Modo: {mode}");

            GUILayout.Space(10);

            // 3) Botón "Mover a inicio":
            //    - se somos servidor puro, teleporta a todos;
            //    - se somos cliente/host, só a nós mesmos
            if (GUILayout.Button("Mover a inicio"))
            {
                if (_netManager.IsServer && !_netManager.IsClient)
                {
                    // Servidor puro: teleporta a todos os clients
                    foreach (ulong clientId in _netManager.ConnectedClientsIds)
                    {
                        var netObj = _netManager.SpawnManager.GetPlayerNetworkObject(clientId);
                        if (netObj == null) continue;
                        var pc = netObj.GetComponent<PlayerController>();
                        if (pc != null)
                            pc.RequestTeleportServerRpc();
                    }
                }
                else
                {
                    // Cliente ou Host: só teleporta o local
                    var localObj = _netManager.SpawnManager.GetLocalPlayerObject();
                    if (localObj != null)
                    {
                        var pc = localObj.GetComponent<PlayerController>();
                        if (pc != null)
                            pc.RequestTeleportServerRpc();
                    }
                }
            }

            GUILayout.Space(10);

            // 4) Se somos servidor (ou Host), amosamos campo para configurar límite por equipo
            if (_netManager.IsServer)
            {
                GUILayout.Label("Límite max por equipo:");
                _maxPlayersInput = GUILayout.TextField(_maxPlayersInput, GUILayout.Width(50));
                if (int.TryParse(_maxPlayersInput, out int parsed))
                    _maxPlayersPerTeam = Mathf.Max(1, parsed);

                if (GUILayout.Button("Aplicar límite"))
                {
                    if (_teamManager != null)
                        _teamManager.SetMaxPerTeam(_maxPlayersPerTeam);
                }
            }
        }

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    /// <summary>
    /// Antes de aprobar a conexión, rexeita se os dous equipos xa están cheos.
    /// Cada equipo admite _maxPlayersPerTeam. Os sen equipo non teñen límite.
    /// </summary>
    private void ApproveOrReject(
        NetworkManager.ConnectionApprovalRequest req,
        NetworkManager.ConnectionApprovalResponse res)
    {
        if (_teamManager == null)
        {
            // Se non hai TeamManager, permitimos a conexión
            res.Approved = true;
            res.CreatePlayerObject = true;
            res.PlayerPrefabHash = null;
            res.Position = Vector3.zero;
            res.Rotation = Quaternion.identity;
            return;
        }

        int countTeam1 = _teamManager.GetCurrentCount(1);
        int countTeam2 = _teamManager.GetCurrentCount(2);
        if (countTeam1 >= _maxPlayersPerTeam && countTeam2 >= _maxPlayersPerTeam)
        {
            res.Approved = false;
            res.Reason = "Lobby cheo: equipos en capacidade.";
            return;
        }

        res.Approved = true;
        res.CreatePlayerObject = true;
        res.PlayerPrefabHash = null;
        res.Position = Vector3.zero;
        res.Rotation = Quaternion.identity;
    }

    /// <summary>
    /// Executado no servidor cando un cliente conéctase correctamente.
    /// Teleporta ao player á zona central e léo ao equipo 0 (sen equipo, cor branca).
    /// </summary>
    private void OnClientConnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer) return;
        if (_teamManager == null) return;

        var playerObj = _netManager.SpawnManager.GetPlayerNetworkObject(clientId);
        if (playerObj == null) return;

        // Spawn aleatorio na zona central
        Vector3 centerPos = _teamManager.GetRandomCenterPosition();
        playerObj.transform.position = centerPos;

        // Engadir ao equipo 0 (sen equipo)
        _teamManager.AddPlayerToTeam(clientId, 0);

        // Poñer cor branca no servidor e notificar a todos
        var pc = playerObj.GetComponent<PlayerController>();
        if (pc != null)
            pc.SetInitialTeamOnServer(0);
    }

    /// <summary>
    /// Executado no servidor cando un cliente se desconecta.
    /// Libera recursos do TeamManager (quitación de listas e cores).
    /// </summary>
    private void OnClientDisconnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer) return;
        if (_teamManager == null) return;

        int oldTeam = _teamManager.GetPlayerTeam(clientId);
        if (oldTeam == 1 || oldTeam == 2)
            _teamManager.ReleaseColorFromTeam(oldTeam, clientId);

        _teamManager.RemovePlayer(clientId);
    }
}
