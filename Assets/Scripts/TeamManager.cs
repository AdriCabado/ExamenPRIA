using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class TeamManager : MonoBehaviour
{
    public static TeamManager Instance { get; private set; }

    // Límite de players por equipo (definido polo servidor vía GameManager)
    private int _maxPerTeam = 2;

    // Dicionario: clave = ID de equipo (0=sen equipo, 1=Equipo 1, 2=Equipo 2), valor = lista de clientIds
    private readonly Dictionary<int, List<ulong>> _teamMembership = new Dictionary<int, List<ulong>>();

    // Paletas de cores por equipo
    private readonly List<Color> _team1Colors = new List<Color>
    {
        Color.red,
        new Color(1f, 0.5f, 0f), // laranxa
        new Color(1f, 0.4f, 0.7f) // rosa
    };
    private readonly List<Color> _team2Colors = new List<Color>
    {
        new Color(0f, 0f, 0.5f),  // azul escuro
        new Color(0.5f, 0f, 0.5f),// violeta
        new Color(0f, 0.5f, 1f)   // azul claro
    };

    // Copias dispoñibles de cores (pool) para cada equipo
    private List<Color> _team1Available;
    private List<Color> _team2Available;

    // Para gardar que cor foi asignada a cada clientId
    private readonly Dictionary<ulong, Color> _assignedColors = new Dictionary<ulong, Color>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _teamMembership[0] = new List<ulong>();
        _teamMembership[1] = new List<ulong>();
        _teamMembership[2] = new List<ulong>();
    }

    /// <summary>
    /// Inicializa o TeamManager co límite de players por equipo e clona as paletas.
    /// </summary>
    public void Initialize(int maxPerTeam)
    {
        _maxPerTeam = Mathf.Max(1, maxPerTeam);
        _team1Available = new List<Color>(_team1Colors);
        _team2Available = new List<Color>(_team2Colors);
    }

    /// <summary>
    /// Devolve true se aínda cabe outro player no equipo indicado.
    /// O equipo 0 (sen equipo) non ten límite.
    /// </summary>
    public bool CanJoinTeam(int teamId)
    {
        if (teamId == 0) return true;
        return _teamMembership[teamId].Count < _maxPerTeam;
    }

    /// <summary>
    /// Engade o clientId ao novo equipo (se cabe). 
    /// Elimina de calquera equipo anterior.
    /// </summary>
    public void AddPlayerToTeam(ulong clientId, int newTeam)
    {
        // Quitar de equipo anterior
        foreach (var kvp in _teamMembership)
        {
            if (kvp.Value.Contains(clientId))
            {
                kvp.Value.Remove(clientId);
                break;
            }
        }
        // Se é equipo 1 ou 2, comprobamos límite
        if (newTeam != 0 && _teamMembership[newTeam].Count >= _maxPerTeam)
            return;

        _teamMembership[newTeam].Add(clientId);
    }

    /// <summary>
    /// Elimina o player de calquera equipo en que estivese.
    /// </summary>
    public void RemovePlayer(ulong clientId)
    {
        foreach (var kvp in _teamMembership)
        {
            if (kvp.Value.Contains(clientId))
            {
                kvp.Value.Remove(clientId);
                break;
            }
        }
        if (_assignedColors.ContainsKey(clientId))
            _assignedColors.Remove(clientId);
    }

    /// <summary>
    /// Devolve cantos players hai agora no equipo indicado.
    /// </summary>
    public int GetCurrentCount(int teamId)
    {
        return _teamMembership[teamId].Count;
    }

    /// <summary>
    /// Devolve o equipo en que está ese clientId (ou 0 se non).
    /// </summary>
    public int GetPlayerTeam(ulong clientId)
    {
        foreach (var kvp in _teamMembership)
        {
            if (kvp.Value.Contains(clientId))
                return kvp.Key;
        }
        return 0;
    }

    /// <summary>
    /// Actualiza o límite por equipo en tempo de execución.
    /// </summary>
    public void SetMaxPerTeam(int newMax)
    {
        _maxPerTeam = Mathf.Max(1, newMax);
    }

    /// <summary>
    /// Devolve unha posición aleatoria no cadrado central (X e Z en [-2,2], Y=1).
    /// </summary>
    public Vector3 GetRandomCenterPosition()
    {
        float x = Random.Range(-2f, 2f);
        float z = Random.Range(-2f, 2f);
        return new Vector3(x, 1f, z);
    }

    /// <summary>
    /// Dada unha posición, devolve:
    /// 0 = sen equipo (central),
    /// 1 = Equipo 1 (X < -2),
    /// 2 = Equipo 2 (X > 2).
    /// </summary>
    public int DetermineTeamByPosition(Vector3 pos)
    {
        if (pos.x < -2f && pos.z >= -3f && pos.z <= 3f) return 1;
        if (pos.x > 2f && pos.z >= -3f && pos.z <= 3f) return 2;
        return 0;
    }

    /// <summary>
    /// Cando un player entra nun equipo, devolve unha cor aleatoria non usada dese equipo.
    /// Se o pool está baleiro, devolve branco.
    /// </summary>
    public Color GetRandomColorForTeam(int teamId)
    {
        List<Color> pool = teamId == 1 ? _team1Available : _team2Available;
        if (pool == null || pool.Count == 0)
            return Color.white;

        int idx = Random.Range(0, pool.Count);
        Color c = pool[idx];
        pool.RemoveAt(idx);
        return c;
    }

    /// <summary>
    /// Cando un player abandona un equipo, devolve a súa cor ao pool dese equipo.
    /// </summary>
    public void ReleaseColorFromTeam(int teamId, ulong clientId)
    {
        if (!_assignedColors.ContainsKey(clientId)) return;
        Color oldCol = _assignedColors[clientId];
        if (teamId == 1) _team1Available.Add(oldCol);
        else if (teamId == 2) _team2Available.Add(oldCol);
        _assignedColors.Remove(clientId);
    }

    /// <summary>
    /// Gardamos no dicionario que cor se asignou a ese clientId.
    /// </summary>
    public void RecordAssignedColor(ulong clientId, Color chosenColor)
    {
        if (_assignedColors.ContainsKey(clientId))
            _assignedColors[clientId] = chosenColor;
        else
            _assignedColors.Add(clientId, chosenColor);
    }

    /// <summary>
    /// Se un cliente tenta entrar nun equipo cheo, notifica vía ClientRpc.
    /// </summary>
    public void NotifyClientTeamFull(ulong targetClientId)
    {
        var playerObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(targetClientId);
        if (playerObj == null) return;
        var pc = playerObj.GetComponent<PlayerController>();
        if (pc != null)
            pc.TeamFullClientRpc();
    }
}
