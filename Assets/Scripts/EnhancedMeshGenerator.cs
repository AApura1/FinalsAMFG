using System.Collections.Generic;
using UnityEngine;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Quaternion = UnityEngine.Quaternion;
using Random = UnityEngine.Random;
using Vector3 = UnityEngine.Vector3;

public class EnhancedMeshGenerator : MonoBehaviour
{
    public Material material;
    public int instanceCount = 100;

    private Mesh cubeMesh;
    private List<Matrix4x4> matrices = new List<Matrix4x4>();
    private List<int> colliderIds = new List<int>();

    public float width = 1f;
    public float height = 1f;
    public float depth = 1f;

    public float movementSpeed = 6f;
    public float jumpForce = 12f;
    public float gravity = 20f;
    public float fallMultiplier = 2.5f;
    public float airControl = 0.5f;

    private int playerID = -1;
    private Vector3 playerVelocity = Vector3.zero;
    private bool isGrounded = false;

    public PlayerCameraFollow cameraFollow;

    public float constantZPosition = 0f;

    public float minX = -50f, maxX = 50f;
    public float minY = -10f, maxY = 20f;

    public float groundY = -10f;

    // SYSTEMS
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

        matrices.Add(Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one));
        colliderIds.Add(playerID);
    }

    void CreateGround()
    {
        Vector3 pos = new Vector3(0, groundY, constantZPosition);
        Vector3 scale = new Vector3(200, 1, 50);

        int id = CollisionManager.Instance.RegisterCollider(pos, scale, false);

        matrices.Add(Matrix4x4.TRS(pos, Quaternion.identity, scale));
        colliderIds.Add(id);
    }

    void GenerateWorld()
    {
        for (int i = 0; i < instanceCount; i++)
        {
            Vector3 pos = new Vector3(Random.Range(minX, maxX), Random.Range(minY, maxY), 0);
            Vector3 scale = Vector3.one * Random.Range(1f, 3f);

            int id = CollisionManager.Instance.RegisterCollider(pos, scale, false);

            matrices.Add(Matrix4x4.TRS(pos, Quaternion.identity, scale));
            colliderIds.Add(id);

            float r = Random.value;

            if (r > 0.85f) enemyIDs.Add(id);
            else if (r > 0.7f) killZones.Add(id);
            else if (r > 0.98f) goalID = id;
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
        int index = colliderIds.IndexOf(playerID);

        Matrix4x4 m = matrices[index];
        Vector3 pos = m.GetPosition();

        // Jump
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            playerVelocity.y = jumpForce;
            isGrounded = false;
        }

        // Gravity
        if (playerVelocity.y < 0)
            playerVelocity.y -= gravity * fallMultiplier * Time.deltaTime;
        else
            playerVelocity.y -= gravity * Time.deltaTime;

        float h = Input.GetAxis("Horizontal");
        float speed = isGrounded ? movementSpeed : movementSpeed * airControl;

        pos.x += h * speed * Time.deltaTime;
        pos.y += playerVelocity.y * Time.deltaTime;

        // Collision
        if (CollisionManager.Instance.CheckCollision(playerID, pos, out List<int> hits))
        {
            foreach (int hit in hits)
            {
                if (enemyIDs.Contains(hit) && !isInvincible)
                    TakeDamage(1);

                if (killZones.Contains(hit))
                    hp = 0;

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
        {
            isGrounded = false;
        }

        matrices[index] = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one);
        CollisionManager.Instance.UpdateCollider(playerID, pos, Vector3.one);

        cameraFollow.SetPlayerPosition(pos);

        if (Input.GetKeyDown(KeyCode.F))
            fireballs.Add(pos);
    }

    void UpdateEnemies()
    {
        foreach (int id in enemyIDs)
        {
            int i = colliderIds.IndexOf(id);
            Matrix4x4 m = matrices[i];

            Vector3 pos = m.GetPosition();
            pos.x += Mathf.Sin(Time.time + id) * Time.deltaTime * 3f;

            matrices[i] = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one);
            CollisionManager.Instance.UpdateCollider(id, pos, Vector3.one);
        }
    }

    void UpdateFireballs()
    {
        for (int i = 0; i < fireballs.Count; i++)
        {
            fireballs[i] += Vector3.right * 15f * Time.deltaTime;

            foreach (int enemy in enemyIDs)
            {
                if (CollisionManager.Instance.CheckCollision(enemy, fireballs[i], out _))
                {
                    enemyIDs.Remove(enemy);
                    break;
                }
            }
        }
    }

    void TakeDamage(int dmg)
    {
        if (isInvincible) return;

        hp -= dmg;
        isInvincible = true;

        Invoke(nameof(ResetInv), 2f);

        if (hp <= 0)
            Debug.Log("DEAD");
    }

    void ResetInv() => isInvincible = false;

    void Render()
    {
        Camera cam = Camera.main;

        List<Matrix4x4> visible = new List<Matrix4x4>();

        foreach (var m in matrices)
        {
            Vector3 pos = m.GetPosition();
            Vector3 dir = (pos - cam.transform.position).normalized;

            if (Vector3.Dot(cam.transform.forward, dir) > 0)
                visible.Add(m);
        }

        for (int i = 0; i < visible.Count; i += 1023)
        {
            int count = Mathf.Min(1023, visible.Count - i);
            Graphics.DrawMeshInstanced(cubeMesh, 0, material, visible.GetRange(i, count));
        }
    }
}