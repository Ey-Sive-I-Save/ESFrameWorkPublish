using UnityEngine;

namespace Gaskellgames
{
    /// <summary>
    /// Code created by Gaskellgames: https://gaskellgames.com: https://github.com/Gaskellgames
    /// </summary>
    
    public static class GLExtensions
    {
        /// <summary>
        /// Draws a line starting at from towards to, in a specified colour
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="color"></param>
        public static void GL_DrawLine(Vector2 from, Vector2 to, Color color)
        {
            GL.Color(color);
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                Vector2 tangent = (to - from).normalized;
                Vector2 mult = new Vector2(tangent.y > tangent.x ? -1 : 1, tangent.y > tangent.x ? 1 : -1);
                tangent = new Vector2(mult.x * tangent.y, mult.y * tangent.x) * 0.5f;
                GL.Vertex(new Vector3(from.x + tangent.x, from.y + tangent.y, 0f));
                GL.Vertex(new Vector3(from.x - tangent.x, from.y - tangent.y, 0f));
                GL.Vertex(new Vector3(to.x + tangent.x, to.y + tangent.y, 0f));
                GL.Vertex(new Vector3(to.x - tangent.x, to.y - tangent.y, 0f));
            }
            else
            {
                GL.Vertex(new Vector3(from.x, from.y, 0f));
                GL.Vertex(new Vector3(to.x, to.y, 0f));
            }
        }

        /// <summary>
        /// Draw a circle at a given position, with a set radius and thickness, in a specified colour
        /// </summary>
        /// <param name="position"></param>
        /// <param name="radius"></param>
        /// <param name="thickness"></param>
        /// <param name="color"></param>
        public static void GL_DrawCircle(Vector2 position, float radius, float thickness, Color color)
        {
            int circleDivisions = 18;
            GL.Color(color);

            for (int i = 1; i <= circleDivisions; i++)
            {
                float vC = Mathf.PI * 2 * (i / (float)circleDivisions - 1 / (float)circleDivisions);
                float vP = Mathf.PI * 2 * (i / (float)circleDivisions);
                Vector2 from = position + new Vector2(Mathf.Sin(vP) * radius, Mathf.Cos(vP) * radius);
                Vector2 to = position + new Vector2(Mathf.Sin(vC) * radius, Mathf.Cos(vC) * radius);
                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    Vector2 tangent = (to - from).normalized;
                    Vector2 mult = new Vector2(tangent.y > tangent.x ? -1 : 1, tangent.y > tangent.x ? 1 : -1);
                    tangent = new Vector2(mult.x * tangent.y, mult.y * tangent.x) * (0.5f * thickness);
                    GL.Vertex(new Vector3(from.x + tangent.x, from.y + tangent.y, 0f));
                    GL.Vertex(new Vector3(to.x + tangent.x, to.y + tangent.y, 0f));
                    GL.Vertex(new Vector3(to.x - tangent.x, to.y - tangent.y, 0f));
                    GL.Vertex(new Vector3(from.x - tangent.x, from.y - tangent.y, 0f));
                }
                else
                {
                    GL.Vertex(new Vector3(from.x, from.y, 0f));
                    GL.Vertex(new Vector3(to.x, to.y, 0f));
                }
            }
        }

    } // class end
}
