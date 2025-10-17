using UnityEngine;

[RequireComponent(typeof(Renderer)), ExecuteInEditMode]
public class MetaballController : MonoBehaviour
{
    private static readonly int SmoothFactor = Shader.PropertyToID("_SmoothFactor");
    private static readonly int BallCount = Shader.PropertyToID("_BallCount");
    private static readonly int BallPositions = Shader.PropertyToID("_BallPositions");
    private static readonly int BallRadii = Shader.PropertyToID("_BallRadii");
    private static readonly int SphereRadius = Shader.PropertyToID("_SphereRadius");

    [Header("Metaball Settings")]
    public float radius = 0.5f;

    public float smoothFactor = 0.5f;

    [Header("Secondary Ball")]
    public Transform secondaryBall;

    public float secondaryRadius = 0.3f;
    // public float minDistance = 0.5f;
    // public float maxDistance = 2.0f;
    // public float moveSpeed = 1.0f;

    private Material material;
    private Vector3 secondaryPosition;
    private float currentDistance;
    private bool movingCloser = true;

    void Start()
    {
        material = GetComponent<Renderer>().material;
        secondaryPosition = secondaryBall.position;
        currentDistance = Vector3.Distance(transform.position, secondaryPosition);
    }

    void Update()
    {
        // 更新次要球体位置（模拟移动）
        UpdateSecondaryPosition();

        // 更新着色器参数
        UpdateShaderParameters();
    }

    void UpdateSecondaryPosition()
    {
        // // 在最小和最大距离之间来回移动
        // if (movingCloser)
        // {
        //     currentDistance -= moveSpeed * Time.deltaTime;
        //     if (currentDistance <= minDistance)
        //     {
        //         currentDistance = minDistance;
        //         movingCloser = false;
        //     }
        // }
        // else
        // {
        //     currentDistance += moveSpeed * Time.deltaTime;
        //     if (currentDistance >= maxDistance)
        //     {
        //         currentDistance = maxDistance;
        //         movingCloser = true;
        //     }
        // }

        // 计算新位置（围绕主球体旋转）
        // float angle = Time.time * moveSpeed;
        // Vector3 offset = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * currentDistance;
        // secondaryPosition = transform.position + offset;
        // secondaryBall.position = secondaryPosition;
    }

    void UpdateShaderParameters()
    {
        // 设置球体参数
        material.SetFloat(SphereRadius, radius);


        // 设置球体位置和半径数组
        Vector4[] positions = new Vector4[2];
        positions[0] = new Vector4(transform.position.x, transform.position.y, transform.position.z, 0);
        positions[1] = new Vector4(secondaryBall.position.x, secondaryBall.position.y, secondaryBall.position.z, 0);

        float[] radii = new float[2];
        radii[0] = radius;
        radii[1] = secondaryRadius;

        Shader.SetGlobalFloat(SmoothFactor, smoothFactor);
        Shader.SetGlobalInt(BallCount, 2);
        Shader.SetGlobalVectorArray(BallPositions, positions);
        Shader.SetGlobalFloatArray(BallRadii, radii);
    }

    void OnDrawGizmos()
    {
        // 可视化球体位置
        if (Application.isPlaying)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, radius);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(secondaryPosition, secondaryRadius);
        }
    }
}