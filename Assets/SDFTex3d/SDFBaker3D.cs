using System;
using System.Collections.Generic;
using UnityEngine;

public static class SDFBaker3D
{
    // 输出 SDF（单位：世界空间的距离，负值为内部；索引顺序：(z,y,x)）
    public static float[] Bake(
        Mesh mesh,
        Matrix4x4 localToWorld,
        Vector3Int resolution,
        float padding01,
        out Bounds usedBounds,
        float surfaceThickness = 1.0f,
        int dilateIterations = 0,
        bool invertSign = false)
    {
        if (mesh == null) throw new ArgumentNullException(nameof(mesh));
        if (resolution.x <= 1 || resolution.y <= 1 || resolution.z <= 1)
            throw new ArgumentException("Resolution must be greater than 1 in all axes.");

        // 顶点转世界空间与包围盒
        var vertices = mesh.vertices;
        var tris = mesh.triangles;

        var vWorld = new Vector3[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
            vWorld[i] = localToWorld.MultiplyPoint3x4(vertices[i]);

        var bounds = ComputeBounds(vWorld);
        // padding
        var size = bounds.size;
        bounds.Expand(new Vector3(size.x * padding01, size.y * padding01, size.z * padding01));
        usedBounds = bounds;

        int nx = resolution.x, ny = resolution.y, nz = resolution.z;
        int voxelCount = nx * ny * nz;

        var surface = new byte[voxelCount]; // 0=空，1=表面
        var outside = new byte[voxelCount]; // 0=未知，1=外部

        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        Vector3 sizeB = bounds.size;

        Vector3 voxelSize = new Vector3(sizeB.x / nx, sizeB.y / ny, sizeB.z / nz);
        float voxelDiag = voxelSize.magnitude;
        float surfThresh = 0.5f * voxelDiag * Mathf.Max(0.1f, surfaceThickness);

        // 标记表面体素（按三角形 AABB 投射，检测体素中心到三角形距离）
        for (int ti = 0; ti < tris.Length; ti += 3)
        {
            Vector3 a = vWorld[tris[ti]];
            Vector3 b = vWorld[tris[ti + 1]];
            Vector3 c = vWorld[tris[ti + 2]];

            Vector3 triMin = Vector3.Min(a, Vector3.Min(b, c));
            Vector3 triMax = Vector3.Max(a, Vector3.Max(b, c));

            // 扩展一点，避免漏采到边界上的体素
            triMin -= Vector3.one * surfThresh;
            triMax += Vector3.one * surfThresh;

            // 转为体素索引范围
            Vector3Int minIdx = WorldToIndexClamp(triMin, min, voxelSize, nx, ny, nz);
            Vector3Int maxIdx = WorldToIndexClamp(triMax, min, voxelSize, nx, ny, nz);

            for (int z = minIdx.z; z <= maxIdx.z; z++)
            {
                for (int y = minIdx.y; y <= maxIdx.y; y++)
                {
                    for (int x = minIdx.x; x <= maxIdx.x; x++)
                    {
                        Vector3 center = IndexToWorldCenter(x, y, z, min, voxelSize);
                        float d2 = PointTriangleDistanceSqr(center, a, b, c);
                        if (d2 <= surfThresh * surfThresh)
                        {
                            int idx = Index3D(x, y, z, nx, ny);
                            surface[idx] = 1;
                        }
                    }
                }
            }
        }

        // 膨胀表面体素（可选，修补小孔）
        for (int it = 0; it < dilateIterations; it++)
        {
            var copy = (byte[])surface.Clone();
            for (int z = 0; z < nz; z++)
            {
                for (int y = 0; y < ny; y++)
                {
                    for (int x = 0; x < nx; x++)
                    {
                        int idx = Index3D(x, y, z, nx, ny);
                        if (copy[idx] == 1) continue;
                        // 6 邻域
                        if ((x > 0 && copy[Index3D(x - 1, y, z, nx, ny)] == 1) ||
                            (x < nx - 1 && copy[Index3D(x + 1, y, z, nx, ny)] == 1) ||
                            (y > 0 && copy[Index3D(x, y - 1, z, nx, ny)] == 1) ||
                            (y < ny - 1 && copy[Index3D(x, y + 1, z, nx, ny)] == 1) ||
                            (z > 0 && copy[Index3D(x, y, z - 1, nx, ny)] == 1) ||
                            (z < nz - 1 && copy[Index3D(x, y, z + 1, nx, ny)] == 1))
                        {
                            surface[idx] = 1;
                        }
                    }
                }
            }
        }

        // 从边界泛洪标记外部（不可穿越表面体素）
        FloodFillOutside(surface, outside, nx, ny, nz);

        // 距离变换输入：表面体素为 0，其余为 +inf
        var f = new float[voxelCount];
        float INF = 1e20f;
        for (int i = 0; i < voxelCount; i++)
            f[i] = (surface[i] == 1) ? 0f : INF;

        // 三维欧氏距离变换（各轴引入尺度因子 dx^2,dy^2,dz^2）
        var dist2 = EDT3D(f, nx, ny, nz, voxelSize.x * voxelSize.x, voxelSize.y * voxelSize.y, voxelSize.z * voxelSize.z);

        // 组合符号并开方得世界距离
        var sdf = new float[voxelCount];
        for (int i = 0; i < voxelCount; i++)
        {
            float d = Mathf.Sqrt(dist2[i]); // 世界单位
            bool isOutside = outside[i] == 1;
            float s = isOutside ? +1f : -1f; // 外部正、内部负（常见约定）
            if (invertSign) s = -s;
            // 表面体素距离为 0
            sdf[i] = (f[i] == 0f) ? 0f : d * s;
        }

        return sdf;
    }

    private static Bounds ComputeBounds(Vector3[] v)
    {
        if (v.Length == 0) return new Bounds(Vector3.zero, Vector3.one);
        var b = new Bounds(v[0], Vector3.zero);
        for (int i = 1; i < v.Length; i++)
            b.Encapsulate(v[i]);
        // 避免退化
        var size = b.size;
        if (size.x < 1e-6f) size.x = 1e-3f;
        if (size.y < 1e-6f) size.y = 1e-3f;
        if (size.z < 1e-6f) size.z = 1e-3f;
        b.size = size;
        return b;
    }

    private static Vector3Int WorldToIndexClamp(Vector3 p, Vector3 min, Vector3 voxel, int nx, int ny, int nz)
    {
        int x = Mathf.Clamp(Mathf.FloorToInt((p.x - min.x) / voxel.x), 0, nx - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt((p.y - min.y) / voxel.y), 0, ny - 1);
        int z = Mathf.Clamp(Mathf.FloorToInt((p.z - min.z) / voxel.z), 0, nz - 1);
        return new Vector3Int(x, y, z);
    }

    private static Vector3 IndexToWorldCenter(int x, int y, int z, Vector3 min, Vector3 voxel)
    {
        return new Vector3(
            min.x + (x + 0.5f) * voxel.x,
            min.y + (y + 0.5f) * voxel.y,
            min.z + (z + 0.5f) * voxel.z
        );
    }

    private static int Index3D(int x, int y, int z, int nx, int ny)
    {
        return (z * ny + y) * nx + x;
    }

    // 点到三角形的平方距离（实时碰撞检测标准实现）
    private static float PointTriangleDistanceSqr(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        // 边向量
        Vector3 ab = b - a;
        Vector3 ac = c - a;
        Vector3 ap = p - a;

        float d1 = Vector3.Dot(ab, ap);
        float d2 = Vector3.Dot(ac, ap);
        if (d1 <= 0f && d2 <= 0f) return (p - a).sqrMagnitude; // barycentric (1,0,0)

        Vector3 bp = p - b;
        float d3 = Vector3.Dot(ab, bp);
        float d4 = Vector3.Dot(ac, bp);
        if (d3 >= 0f && d4 <= d3) return (p - b).sqrMagnitude; // barycentric (0,1,0)

        float vc = d1 * d4 - d3 * d2;
        if (vc <= 0f && d1 >= 0f && d3 <= 0f)
        {
            float v = d1 / (d1 - d3);
            return (p - (a + v * ab)).sqrMagnitude; // 边 AB
        }

        Vector3 cp = p - c;
        float d5 = Vector3.Dot(ab, cp);
        float d6 = Vector3.Dot(ac, cp);
        if (d6 >= 0f && d5 <= d6) return (p - c).sqrMagnitude; // barycentric (0,0,1)

        float vb = d5 * d2 - d1 * d6;
        if (vb <= 0f && d2 >= 0f && d6 <= 0f)
        {
            float w = d2 / (d2 - d6);
            return (p - (a + w * ac)).sqrMagnitude; // 边 AC
        }

        float va = d3 * d6 - d5 * d4;
        if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f)
        {
            float w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
            return (p - (b + w * (c - b))).sqrMagnitude; // 边 BC
        }

        // 内部区域，投影到三角形平面
        Vector3 n = Vector3.Cross(ab, ac);
        float dist = Vector3.Dot(p - a, n) / n.magnitude;
        return dist * dist;
    }

    private static void FloodFillOutside(byte[] surface, byte[] outside, int nx, int ny, int nz)
    {
        var q = new Queue<int>(nx * ny + ny * nz + nx * nz);

        // 将边界上非表面体素作为起点
        void TryEnqueue(int x, int y, int z)
        {
            int idx = Index3D(x, y, z, nx, ny);
            if (surface[idx] == 0 && outside[idx] == 0)
            {
                outside[idx] = 1;
                q.Enqueue(idx);
            }
        }

        for (int z = 0; z < nz; z++)
        for (int y = 0; y < ny; y++)
        {
            TryEnqueue(0, y, z);
            TryEnqueue(nx - 1, y, z);
        }

        for (int z = 0; z < nz; z++)
        for (int x = 0; x < nx; x++)
        {
            TryEnqueue(x, 0, z);
            TryEnqueue(x, ny - 1, z);
        }

        for (int y = 0; y < ny; y++)
        for (int x = 0; x < nx; x++)
        {
            TryEnqueue(x, y, 0);
            TryEnqueue(x, y, nz - 1);
        }

        // 6-邻域泛洪
        while (q.Count > 0)
        {
            int idx = q.Dequeue();
            int z = idx / (nx * ny);
            int r = idx - z * nx * ny;
            int y = r / nx;
            int x = r - y * nx;

            void Nbr(int xx, int yy, int zz)
            {
                if (xx < 0 || yy < 0 || zz < 0 || xx >= nx || yy >= ny || zz >= nz) return;
                int ii = Index3D(xx, yy, zz, nx, ny);
                if (surface[ii] == 0 && outside[ii] == 0)
                {
                    outside[ii] = 1;
                    q.Enqueue(ii);
                }
            }

            Nbr(x - 1, y, z);
            Nbr(x + 1, y, z);
            Nbr(x, y - 1, z);
            Nbr(x, y + 1, z);
            Nbr(x, y, z - 1);
            Nbr(x, y, z + 1);
        }
    }

    // 三维欧氏距离变换：三次 1D 变换（Felzenszwalb & Huttenlocher）
    // f: 0 为特征点（表面），INF 为非特征；dist2 输出为"平方距离"（已考虑轴尺度）
    private static float[] EDT3D(float[] f, int nx, int ny, int nz, float sx2, float sy2, float sz2)
    {
        int N = nx * ny * nz;
        var tmp = new float[N];
        var outD = new float[N];

        // X 方向
        var line = new float[nx];
        var outLine = new float[nx];
        for (int z = 0; z < nz; z++)
        {
            for (int y = 0; y < ny; y++)
            {
                int baseIdx = (z * ny + y) * nx;
                for (int x = 0; x < nx; x++) line[x] = f[baseIdx + x];
                DT1D(line, nx, sx2, outLine);
                for (int x = 0; x < nx; x++) tmp[baseIdx + x] = outLine[x];
            }
        }

        // Y 方向
        line = new float[ny];
        outLine = new float[ny];
        for (int z = 0; z < nz; z++)
        {
            for (int x = 0; x < nx; x++)
            {
                int baseIdx = z * ny * nx + x;
                for (int y = 0; y < ny; y++) line[y] = tmp[baseIdx + y * nx];
                DT1D(line, ny, sy2, outLine);
                for (int y = 0; y < ny; y++) tmp[baseIdx + y * nx] = outLine[y];
            }
        }

        // Z 方向
        line = new float[nz];
        outLine = new float[nz];
        for (int y = 0; y < ny; y++)
        {
            for (int x = 0; x < nx; x++)
            {
                int baseIdx = y * nx + x;
                for (int z = 0; z < nz; z++) line[z] = tmp[z * ny * nx + baseIdx];
                DT1D(line, nz, sz2, outLine);
                for (int z = 0; z < nz; z++) outD[z * ny * nx + baseIdx] = outLine[z];
            }
        }

        return outD;
    }

    // 1D 欧氏距离变换（平方距离），支持轴尺度（系数）scale2 = (spacing)^2
    // 参考: "Distance Transforms of Sampled Functions" (Felzenszwalb, Huttenlocher)
    // f: 输入，0 为特征点，INF 为非特征；n: 长度；scale2: 轴向 spacing^2
    private static void DT1D(float[] f, int n, float scale2, float[] d)
    {
        int[] v = new int[n];
        float[] z = new float[n + 1];

        int k = 0;
        v[0] = 0;
        z[0] = float.NegativeInfinity;
        z[1] = float.PositiveInfinity;

        float INF = 1e20f;

        // 将 f[i] 视为抛物线 y = (x - i)^2 * scale2 + f[i] 的下包络
        for (int q = 1; q < n; q++)
        {
            float s;
            while (true)
            {
                int vk = v[k];
                // 交点 s = ( (f[q] + scale2*q^2) - (f[vk] + scale2*vk^2) ) / (2*scale2*(q - vk))
                float num = (f[q] + scale2 * q * q) - (f[vk] + scale2 * vk * vk);
                float den = 2f * scale2 * (q - vk);
                s = (Mathf.Abs(den) < 1e-12f) ? (float)q : (num / den);
                if (s > z[k]) break;
                k--;
                if (k < 0)
                {
                    k = 0;
                    break;
                }
            }

            k++;
            v[k] = q;
            z[k] = s;
            z[k + 1] = float.PositiveInfinity;
        }

        k = 0;
        for (int q = 0; q < n; q++)
        {
            while (z[k + 1] < q) k++;
            float val = (q - v[k]);
            d[q] = val * val * scale2 + f[v[k]];
            if (d[q] > INF) d[q] = INF; // 数值稳定
        }
    }
}