using System.Collections.Generic;
using UnityEngine;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

public class EnhancedMeshGenerator : MonoBehaviour
{
    [Header("Materials")]
    public Material playerMaterial;
    public Material enemyMaterial;
    public Material obstacleMaterial;
    public Material fireballMaterial;

    [Header("Settings")]
    public int instanceCount = 100;
    public float width = 1f;
    public float height = 1f;
    public float depth = 1f;
    public float movementSpeed = 6f;
    public float jumpForce = 12f;
    public float gravity = 20f;
    public float fallMultiplier = 2.5f;
    public float airControl = 0.5f;

    private Mesh cubeMesh;

    private List<Matrix4x4> playerMatrices = new List<Matrix4x4>();
    private List<Matrix4x4> enemyMatrices = new List<Matrix4x4>();
    private List<Matrix4x4> obstacleMatrices = new List<Matrix4x4>();
    private List<Matrix4x4> fireballMatrices = new List<Matrix4x4>();

    private List<int> playerColliderIds = new List<int>();
    private List<int> enemyColliderIds = new List<int>();
    private List<int> obstacleColliderIds = new List<int>();

    private int playerID = -1;
    private Vector3 playerVelocity = Vector3.zero;
    private bool isGrounded = false;

    public PlayerCameraFollow cameraFollow;
    public float constantZPosition = 0f;

    public float minX = -50f, maxX = 50f;
    public float minY = -10f, maxY = 20f;
    public float groundY = -10f;

    private HashSet<int> enemyIDs = new HashSet<int>();
    private HashSet<int> killZones = new HashSet<int>();
    private int goalID;

    private List<Vector3> fireballs = new List<Vector3>();

    public int hp = 3;
    public bool isInvincible = false;

    void Start()
    {
        SetupCamera();
        CreateCubeMesh();
        CreatePlayer();
        CreateGround();
        GenerateWorld();
    }

    void SetupCamera()
    {
        Camera cam = Camera.main;
        cameraFollow = cam.GetComponent<PlayerCameraFollow>();
        if (cameraFollow == null)
            cameraFollow = cam.gameObject.AddComponent<PlayerCameraFollow>();

        cameraFollow.offset = new Vector3(0, 2, -15);
    }

    void CreateCubeMesh()
    {
        cubeMesh = GameObject.CreatePrimitive(PrimitiveType.Cube).GetComponent<MeshFilter>().sharedMesh;
    }

    void CreatePlayer()
    {
        Vector3 pos = new Vector3(0, 5, constantZPosition);
        playerID = CollisionManager.Instance.RegisterCollider(pos, Vector3.one, true);

        playerMatrices.Add(Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one));
        playerColliderIds.Add(playerID);
    }

    void CreateGround()
    {
        Vector3 pos = new Vector3(0, groundY, constantZPosition);
        Vector3 scale = new Vector3(200, 1, 50);

        int id = CollisionManager.Instance.RegisterCollider(pos, scale, false);

        obstacleMatrices.Add(Matrix4x4.TRS(pos, Quaternion.identity, scale));
        obstacleColliderIds.Add(id);
    }

    void GenerateWorld()
    {
        for (int i = 0; i < instanceCount; i++)
        {
            Vector3 pos = new Vector3(Random.Range(minX, maxX), Random.Range(minY, maxY), 0);
            Vector3 scale = Vector3.one * Random.Range(1f, 3f);

            int id = CollisionManager.Instance.RegisterCollider(pos, scale, false);

            float r = Random.value;

            if (r > 0.85f)
            {
                enemyIDs.Add(id);
                enemyMatrices.Add(Matrix4x4.TRS(pos, Quaternion.identity, scale));
                enemyColliderIds.Add(id);
            }
            else if (r > 0.7f)
            {
                killZones.Add(id);
                obstacleMatrices.Add(Matrix4x4.TRS(pos, Quaternion.identity, scale));
                obstacleColliderIds.Add(id);
            }
            else if (r > 0.98f)
            {
                goalID = id;
                obstacleMatrices.Add(Matrix4x4.TRS(pos, Quaternion.identity, scale));
                obstacleColliderIds.Add(id);
            }
            else
            {
                obstacleMatrices.Add(Matrix4x4.TRS(pos, Quaternion.identity, scale));
                obstacleColliderIds.Add(id);
            }
        }
    }

    void Update()
    {
        UpdatePlayer();
        UpdateEnemies();
        UpdateFireballs();
        Render();
    }

    void UpdatePlayer()
    {
        int index = playerColliderIds.IndexOf(playerID);
        Matrix4x4 m = playerMatrices[index];
        Vector3 pos = m.GetPosition();

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            playerVelocity.y = jumpForce;
            isGrounded = false;
        }

        if (playerVelocity.y < 0)
            playerVelocity.y -= gravity * fallMultiplier * Time.deltaTime;
        else
            playerVelocity.y -= gravity * Time.deltaTime;

        float h = Input.GetAxis("Horizontal");
        float speed = isGrounded ? movementSpeed : movementSpeed * airControl;

        pos.x += h * speed * Time.deltaTime;
        pos.y += playerVelocity.y * Time.deltaTime;

        if (CollisionManager.Instance.CheckCollision(playerID, pos, out List<int> hits))
        {
            foreach (int hit in hits)
            {
                if (enemyIDs.Contains(hit) && !isInvincible)
                {
                    Debug.Log($"Player touched by enemy! HP before damage: {hp}");
                    TakeDamage(1);
                }

                if (killZones.Contains(hit))
                {
                    hp = 0;
                    Debug.Log("Player touched kill zone! DEAD");
                }

                if (hit == goalID)
                {
                    Debug.Log("YOU WIN");
                    Time.timeScale = 0;
                }
            }

            if (playerVelocity.y < 0)
                isGrounded = true;

            playerVelocity.y = 0;
        }
        else
            isGrounded = false;

        playerMatrices[index] = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one);
        CollisionManager.Instance.UpdateCollider(playerID, pos, Vector3.one);
        cameraFollow.SetPlayerPosition(pos);

        if (Input.GetKeyDown(KeyCode.F))
            fireballs.Add(pos);
    }

    void UpdateEnemies()
    {
        for (int i = 0; i < enemyIDs.Count; i++)
        {
            int id = enemyColliderIds[i];
            Matrix4x4 m = enemyMatrices[i];
            Vector3 pos = m.GetPosition();

            pos.x += Mathf.Sin(Time.time + id) * Time.deltaTime * 3f;

            enemyMatrices[i] = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one);
            CollisionManager.Instance.UpdateCollider(id, pos, Vector3.one);
        }
    }

    void UpdateFireballs()
    {
        for (int i = 0; i < fireballs.Count; i++)
        {
            fireballs[i] += Vector3.right * 15f * Time.deltaTime;

            foreach (int enemy in new List<int>(enemyIDs))
            {
                if (CollisionManager.Instance.CheckCollision(enemy, fireballs[i], out _))
                {
                    Debug.Log($"Enemy {enemy} killed by fireball at position {fireballs[i]}");

                    enemyIDs.Remove(enemy);
                    int idx = enemyColliderIds.IndexOf(enemy);
                    if (idx >= 0)
                    {
                        enemyColliderIds.RemoveAt(idx);
                        enemyMatrices.RemoveAt(idx);
                    }

                    break;
                }
            }
        }

        fireballMatrices.Clear();
        foreach (var f in fireballs)
            fireballMatrices.Add(Matrix4x4.TRS(f, Quaternion.identity, Vector3.one));
    }

    void TakeDamage(int dmg)
    {
        if (isInvincible) return;

        hp -= dmg;
        isInvincible = true;
        Invoke(nameof(ResetInv), 2f);

        Debug.Log($"Player took {dmg} damage. Remaining HP: {hp}");

        if (hp <= 0)
            Debug.Log("Player DEAD");
    }

    void ResetInv() => isInvincible = false;

    void Render()
    {
        Graphics.DrawMeshInstanced(cubeMesh, 0, playerMaterial, playerMatrices);
        Graphics.DrawMeshInstanced(cubeMesh, 0, enemyMaterial, enemyMatrices);
        Graphics.DrawMeshInstanced(cubeMesh, 0, obstacleMaterial, obstacleMatrices);
        Graphics.DrawMeshInstanced(cubeMesh, 0, fireballMaterial, fireballMatrices);
    }
}