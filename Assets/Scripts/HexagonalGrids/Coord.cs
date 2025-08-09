using System.Collections.Generic;
using UnityEngine;

namespace HexagonalGrids
{
    /// <summary>
    /// 六边形网格的坐标类 采样Cube Coordinates
    ///</summary>
    /// <remarks>
    /// 六边形网格的坐标类 采样Cube Coordinates<br/>
    /// <br/>
    /// <a href="https://www.redblobgames.com/grids/hexagons/">六边形网格详解</a>
    /// </remarks>
    public class Coord
    {
        public readonly int q;
        public readonly int r;
        public readonly int s;


        /// <summary>
        /// 六边形网格的六个方向，角向上
        /// </summary>
        /// <remarks>
        /// 六边形网格的六个方向，pointy规则<br/>
        /// 方向顺序为<br/>
        /// 0:左上<br/>
        /// 1:右上<br/>
        /// 2:右<br/>
        /// 3:右下<br/>
        /// 4:左下<br/>
        /// 5:左<br/>    
        /// </remarks>
        public static readonly Coord[] directions = new[]
        {
            //左上
            new Coord(0, -1, +1),
            //右上
            new Coord(1, -1, 0),
            //右
            new Coord(1, 0, -1),
            //右下
            new Coord(0, +1, -1),
            //左下
            new Coord(-1, +1, 0),
            //左
            new Coord(-1, 0, 1)
        };


        public Coord(int q, int r, int s)
        {
            if (q + r + s != 0)
            {
                throw new System.ArgumentException("q + r + s must be 0");
            }

            this.q = q;
            this.r = r;
            this.s = s;
        }

        public Coord(int q, int r)
        {
            this.q = q;
            this.r = r;
            this.s = -q - r;
        }

        public Vector3 ToWorldPosition(float cellSize, Vector3 origin)
        {
            return new Vector3(q * Mathf.Sqrt(3) / 2, 0, -(float)r - ((float)q / 2)) * 2 * cellSize + origin;
        }

        /// <summary>
        /// 获取指定方向的邻居
        /// </summary>
        /// <param name="direction"> 方向索引 </param>
        /// <remarks>
        /// 获取指定方向的邻居<br/>
        /// 方向顺序为<br/>
        /// 0:左上<br/>
        /// 1:右上<br/>
        /// 2:右<br/>
        /// 3:右下<br/>
        /// 4:左下<br/>
        /// 5:左<br/>    
        /// </remarks>
        /// <returns></returns>
        public Coord Neighbor(int direction)
        {
            return this + directions[direction];
        }

        /// <summary>
        /// 获取指定半径的环状坐标
        /// </summary>
        /// <returns></returns>
        public static List<Coord> RingCoord(int radius)
        {
            List<Coord> result = new List<Coord>();

            if (radius == 0)
            {
                //返回原点
                result.Add(new Coord(0, 0, 0));
            }
            else
            {
                Coord coord = directions[4] * radius;
                for (int i = 0; i < 6; i++)
                {
                    for (int j = 1; j <= radius; j++)
                    {
                        result.Add(coord);
                        coord = coord.Neighbor(i);
                    }
                }
            }

            return result;
        }

        public static List<Coord> HexCoord(int radius)
        {
            List<Coord> result = new List<Coord>();

            for (int i = 0; i <= radius; i++)
            {
                result.AddRange(RingCoord(i));
            }

            return result;
        }

        #region 运算符重载

        public static Coord operator +(Coord a, Coord b)
        {
            return new Coord(a.q + b.q, a.r + b.r, a.s + b.s);
        }

        public static Coord operator -(Coord a, Coord b)
        {
            return new Coord(a.q - b.q, a.r - b.r, a.s - b.s);
        }

        public static bool operator ==(Coord a, Coord b)
        {
            return a.q == b.q && a.r == b.r && a.s == b.s;
        }

        public static bool operator !=(Coord a, Coord b)
        {
            return a.q != b.q || a.r != b.r || a.s != b.s;
        }

        public static Coord operator *(Coord coord, int scale)
        {
            return new Coord(coord.q * scale, coord.r * scale, coord.s * scale);
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            Coord other = (Coord)obj;
            return q == other.q && r == other.r && s == other.s;
        }

        public override int GetHashCode()
        {
            return q.GetHashCode() ^ r.GetHashCode() ^ s.GetHashCode();
        }

        #endregion


        public override string ToString()
        {
            return $"{q},{r},{s}";
        }
    }
}