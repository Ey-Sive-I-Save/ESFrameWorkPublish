using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    public static class GizmosExtensions
    {
        #region Line & Arrow

        /// <summary>
        /// Draws a line from a point to the normal intersection point between the line and the point.
        /// </summary>
        /// <param name="point"></param>
        /// <param name="lineStart"></param>
        /// <param name="lineEnd"></param>
        public static void DrawPointLineIntersection(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
        {
            Vector3 intersectionPoint = GgMaths.ProjectPointToLine(point, lineStart, lineEnd);
            Gizmos.DrawLine(intersectionPoint, point);
        }

        /// <summary>
        /// Draws a dashed line starting at from towards to, with a set number of dashes.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="lines"></param>
        public static void DrawDashedLine(Vector3 from, Vector3 to, int lines = 10)
        {
            DrawDottedLine(from, to, lines, false);
        }

        /// <summary>
        /// Draws a line of arrows starting at from towards to, with a set number of arrows.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="arrows"></param>
        public static void DrawArrowLine(Vector3 from, Vector3 to, int arrows = 10)
        {
            DrawDottedLine(from, to, arrows, true);
        }
        
        private static void DrawDottedLine(Vector3 from, Vector3 to, int lines = 10, bool arrows = false)
        {
            // calculate variables
            float sections = lines + (lines - 1);
            Vector3 direction = (to - from).normalized;
            float totalDistance = Vector3.Distance(from, to);
            float lineDistance = totalDistance / sections;
            
            // quick draw: single line / single arrow
            if (lines < 1)
            {
                if (arrows) { DrawWireArrow(from, direction, totalDistance); }
                else { Gizmos.DrawLine(from, to); }
                return;
            }
            
            // multi-line: get points to draw from and to
            List<Vector3> points = new List<Vector3>();
            for (int i = 0; i <= sections; i++)
            {
                points.Add(from + (direction * lineDistance * i));
            }
            
            // draw multiple lines
            for (int i = 0; i < points.Count - 1; i = i+2)
            {
                if (arrows) { DrawWireArrow(points[i], direction, lineDistance); }
                else { Gizmos.DrawLine(points[i], points[i + 1]); }
            }
        }
        
        /// <summary>
        /// Draws an arrow from the origin in a direction, with a set magnitude. The head's size is based on a multiplier of the arrows magnitude (length)
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="direction"></param>
        /// <param name="magnitude"></param>
        /// <param name="headMultiplier"></param>
        /// <param name="arrowHeadAngle"></param>
        public static void DrawWireArrow(Vector3 origin, Vector3 direction, float magnitude = 1f, float headMultiplier = 0.25f, float arrowHeadAngle = 30.0f)
        {
            // arrow body
            Vector3 normalisedDirection = direction.normalized;
            Vector3 arrowTip = origin + (normalisedDirection * magnitude);
            Gizmos.DrawLine(origin, arrowTip);

            // arrow head
            Quaternion lookRotation = normalisedDirection.Equals(Vector3.zero) ? new Quaternion() : Quaternion.LookRotation(normalisedDirection);
            Vector3 leftDirection = (lookRotation * Quaternion.Euler(0, 180 - arrowHeadAngle, 0) * new Vector3(0, 0, 1)).normalized;
            Vector3 rightDirection = (lookRotation * Quaternion.Euler(0, 180 + arrowHeadAngle, 0) * new Vector3(0, 0, 1)).normalized;
            Vector3 arrowTipLeft = arrowTip + (leftDirection * magnitude * headMultiplier);
            Vector3 arrowTipRight = arrowTip + (rightDirection * magnitude * headMultiplier);
            Gizmos.DrawLine(arrowTip, arrowTipLeft);
            Gizmos.DrawLine(arrowTip, arrowTipRight);
        }

        #endregion

        //----------------------------------------------------------------------------------------------------

        #region Rect & Plane

        /// <summary>
        /// Draws a plane at a given point and it's relative vectors
        /// </summary>
        /// <param name="center">The center of the plane in world space.</param>
        /// <param name="up">The up vector of the plane in world space.</param>
        /// <param name="right">The right vector of the plane in world space.</param>
        /// <param name="scale">The scale of the rect. [width, height]</param>
        public static void DrawWireRect(Vector3 center, Vector3 up, Vector3 right, Vector2 scale)
        {
            Vector3 rectTopLeft = center + (up * scale.x * 0.5f) - (right * scale.y * 0.5f);
            Vector3 rectTopRight = center + (up * scale.x * 0.5f) + (right * scale.y * 0.5f);
            Vector3 rectBottomRight = center - (up * scale.x * 0.5f) + (right * scale.y * 0.5f);
            Vector3 rectBottomLeft = center - (up * scale.x * 0.5f) - (right * scale.y * 0.5f);
            
            Gizmos.DrawLine(rectTopLeft, rectTopRight);
            Gizmos.DrawLine(rectTopRight, rectBottomRight);
            Gizmos.DrawLine(rectBottomRight, rectBottomLeft);
            Gizmos.DrawLine(rectBottomLeft, rectTopLeft);
        }
        
        /// <summary>
        /// Draws a plane at a given point and it's relative vectors
        /// </summary>
        /// <param name="center">The center of the plane in world space.</param>
        /// <param name="up">The up vector of the plane in world space.</param>
        /// <param name="right">The right vector of the plane in world space.</param>
        /// <param name="scale">The scale of the rect. [width, height]</param>
        public static void DrawSolidRect(Vector3 center, Vector3 up, Vector3 right, Vector2 scale)
        {
            Vector3 rectTopLeft = center + (up * scale.x * 0.5f) - (right * scale.y * 0.5f);
            Vector3 rectTopRight = center + (up * scale.x * 0.5f) + (right * scale.y * 0.5f);
            Vector3 rectBottomRight = center - (up * scale.x * 0.5f) + (right * scale.y * 0.5f);
            Vector3 rectBottomLeft = center - (up * scale.x * 0.5f) - (right * scale.y * 0.5f);
            Vector3[] verts = new[] { rectTopLeft, rectTopRight, rectBottomRight, rectBottomLeft};
            
#if UNITY_EDITOR
            // cache defaults
            Color32 defaultColor = UnityEditor.Handles.color;
        
            UnityEditor.Handles.color = Gizmos.color;
            UnityEditor.Handles.DrawSolidRectangleWithOutline(verts, Gizmos.color, Gizmos.color);
        
            // reset to default values
            UnityEditor.Handles.color = defaultColor;
#endif
        }
        
        /// <summary>
        /// Draws a plane at a given point and it's relative vectors
        /// </summary>
        /// <param name="center">The center of the plane in world space.</param>
        /// <param name="up">The up vector of the plane in world space.</param>
        /// <param name="right">The right vector of the plane in world space.</param>
        /// <param name="scale">The scale of the rect. [width, height]</param>
        /// <param name="lines">The number of lines to draw across the plane</param>
        public static void DrawWirePlane(Vector3 center, Vector3 up, Vector3 right, Vector2 scale, int lines = 3)
        {
            // calculate scale
            Vector3 scaleX = Vector3.one * scale.x / (lines * 2.0f);
            Vector3 scaleY = Vector3.one * scale.y / (lines * 2.0f);

            // calculate gaps between lines
            Vector3 lineGapX = Vector3.Scale(right, scaleX);
            Vector3 lineGapY = Vector3.Scale(up, scaleY);

            // calculate base start and end point for lines
            Vector3 startX = center - (lineGapY * lines);
            Vector3 endX = center + (lineGapY * lines);
            Vector3 startY = center - (lineGapX * lines);
            Vector3 endY = center + (lineGapX * lines);

            // draw lines in ...
            for (int i = -lines; i <= lines; i++)
            {
                // line gaps
                Vector3 xGap = lineGapX * i;
                Vector3 yGap = lineGapY * i;

                // ... X axis
                Gizmos.DrawLine(startX + xGap, endX + xGap);

                // ... Y axis
                Gizmos.DrawLine(startY + yGap, endY + yGap);
            }
        }
        
        /// <summary>
        /// Draws a line from a point to the normal intersection point between the rect and the point.
        /// </summary>
        /// <param name="point">The position in 3D space to be projected.</param>
        /// <param name="center">The center of the rect in world space.</param>
        /// <param name="up">The up vector of the rect in world space.</param>
        /// <param name="right">The right vector of the rect in world space.</param>
        /// <param name="scale">The scale of the rect. [width, height]</param>
        /// <param name="onOutline">If true then the projected point will always be on the outline of the rect</param>
        public static void DrawPointRectIntersection(Vector3 point, Vector3 center, Vector3 up, Vector3 right, Vector2 scale, bool onOutline)
        {
            Vector3 closestPoint = GgMaths.ProjectPointToRect(point, center, up, right, scale, onOutline);
            Gizmos.DrawLine(point, closestPoint);
        }
        
        /// <summary>
        /// Draws a line from a point to the normal intersection point between the plane and the point.
        /// </summary>
        /// <param name="point">The position in 3D space to be projected.</param>
        /// <param name="center">The center of the plane in world space.</param>
        /// <param name="up">The up vector of the plane in world space.</param>
        /// <param name="right">The right vector of the plane in world space.</param>
        public static void DrawPointPlaneIntersection(Vector3 point, Vector3 center, Vector3 up, Vector3 right)
        {
            Plane plane = PlaneExtensions.GetPlaneFromPointAndRelativeVectors(center, up, right);
            Vector3 closestPoint = plane.ClosestPointOnPlane(point);
            Gizmos.DrawLine(point, closestPoint);
        }

        #endregion

        //----------------------------------------------------------------------------------------------------

        #region Circle & Arc

        /// <summary>
        /// Draws a complete circle in 3D space.
        /// </summary>
        /// <param name="center">The center of the circle in world space.</param>
        /// <param name="normal">The normal of the circle in world space.</param>
        /// <param name="radius">The radius of the circle in world space units.</param>
        public static void DrawCircle(Vector3 center, Vector3 normal, float radius)
        {
#if UNITY_EDITOR
            Color32 defaultColor = UnityEditor.Handles.color;
        
            UnityEditor.Handles.color = Gizmos.color;
            UnityEditor.Handles.DrawWireDisc(center, normal, radius);
        
            // reset to default values
            UnityEditor.Handles.color = defaultColor;
#endif
        }

        /// <summary>
        /// Draws a circular arc in 3D space.
        /// </summary>
        /// <param name="center">The center of the circle in world space.</param>
        /// <param name="normal">The normal of the circle in world space.</param>
        /// <param name="from">The direction of the point on the circle circumference, relative to the center, where the arc begins.</param>
        /// <param name="angle">The angle of the arc, in degrees.</param>
        /// <param name="radius">The radius of the circle in world space units.</param>
        public static void DrawWireArc(Vector3 center, Vector3 normal, Vector3 from, float angle, float radius)
        {
#if UNITY_EDITOR
            // cache defaults
            Color32 defaultColor = UnityEditor.Handles.color;
        
            UnityEditor.Handles.color = Gizmos.color;
            UnityEditor.Handles.DrawWireArc(center, normal, from, angle, radius);
        
            // reset to default values
            UnityEditor.Handles.color = defaultColor;
#endif
        }
        
        /// <summary>
        /// Draws a circular arc in 3D space.
        /// </summary>
        /// <param name="center">The center of the circle in world space.</param>
        /// <param name="normal">The normal of the circle in world space.</param>
        /// <param name="from">The direction of the point on the circle circumference, relative to the center, where the arc begins.</param>
        /// <param name="angle">The angle of the arc, in degrees.</param>
        /// <param name="radius">The radius of the circle in world space units.</param>
        public static void DrawSolidArc(Vector3 center, Vector3 normal, Vector3 from, float angle, float radius)
        {
#if UNITY_EDITOR
            // cache defaults
            Color32 defaultColor = UnityEditor.Handles.color;
        
            UnityEditor.Handles.color = Gizmos.color;
            UnityEditor.Handles.DrawSolidArc(center, normal, from, angle, radius);
        
            // reset to default values
            UnityEditor.Handles.color = defaultColor;
#endif
        }
        
        #endregion

        //----------------------------------------------------------------------------------------------------

        #region Sphere
    
        /// <summary>
        /// Draws a wire sphere with a set radius
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="radius"></param>
        public static void DrawWireSphere(Transform transform, Vector3 offset, float radius)
        {
            DrawRadius_Handles(transform, offset, radius, false);
        }
    
        /// <summary>
        /// Draws a solid sphere with a set radius
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="radius"></param>
        /// <param name="alpha"></param>
        public static void DrawSolidSphere(Transform transform, Vector3 offset, float radius, float alpha = 0.2f)
        {
            DrawRadius_Handles(transform, offset, radius, true, alpha);
        }
        
        private static void DrawRadius_Handles(Transform transform, Vector3 offset, float radius, bool solid, float alpha = 0.2f, float thickness = 1)
        {
#if UNITY_EDITOR
            // cache defaults
            Matrix4x4 defaultMatrix = UnityEditor.Handles.matrix;
            CompareFunction defaultZTest = UnityEditor.Handles.zTest;
            Color32 defaultColor = UnityEditor.Handles.color;

            // set variable values
            UnityEditor.Handles.matrix = transform.localToWorldMatrix;
            Vector3 center;
            Vector3 normal;
            float diskRadius;
            if (Camera.current.orthographic)
            {
                normal = offset - UnityEditor.Handles.inverseMatrix.MultiplyVector(Camera.current.transform.forward);
                diskRadius = radius;
                center = offset;
            }
            else
            {
                normal = offset - UnityEditor.Handles.inverseMatrix.MultiplyPoint(Camera.current.transform.position);
                float sqrMagnitude = normal.sqrMagnitude;
                float a = radius * radius;
                float b = a * a / sqrMagnitude;

                diskRadius = Mathf.Sqrt(a - b);
                center = offset - a * normal / sqrMagnitude;
            }

            // right (x)
            DrawHandles_DepthTestedWireDisk(offset, Vector3.right, radius, thickness);

            // up (y)
            DrawHandles_DepthTestedWireDisk(offset, Vector3.up, radius, thickness);

            // forward (z)
            DrawHandles_DepthTestedWireDisk(offset, Vector3.forward, radius, thickness);
            
            // camera
            DrawHandles_DepthTestedWireDisk(center, normal, diskRadius, thickness);
            if (solid) { DrawHandles_DepthTestedDisk(center, normal, diskRadius, alpha); }

            // reset to default values
            UnityEditor.Handles.matrix = defaultMatrix;
            UnityEditor.Handles.zTest = defaultZTest;
            UnityEditor.Handles.color = defaultColor;
#endif
        }

        private static void DrawHandles_DepthTestedWireDisk(Vector3 center, Vector3 normal, float radius, float thickness = 1)
        {
#if UNITY_EDITOR
            // in front of object
            UnityEditor.Handles.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.5f);
            UnityEditor.Handles.zTest = CompareFunction.LessEqual;
            UnityEditor.Handles.DrawWireDisc(center, normal, radius, thickness);

            // behind object
            UnityEditor.Handles.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.2f);
            UnityEditor.Handles.zTest = CompareFunction.Greater;
            UnityEditor.Handles.DrawWireDisc(center, normal, radius, thickness);
#endif
        }

        private static void DrawHandles_DepthTestedDisk(Vector3 center, Vector3 normal, float radius, float alpha = 0.2f)
        {
#if UNITY_EDITOR
            // in front of object
            UnityEditor.Handles.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, alpha);
            UnityEditor.Handles.zTest = CompareFunction.LessEqual;
            UnityEditor.Handles.DrawSolidDisc(center, normal, radius);

            // behind object
            UnityEditor.Handles.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, alpha * 0.5f);
            UnityEditor.Handles.zTest = CompareFunction.Greater;
            UnityEditor.Handles.DrawSolidDisc(center, normal, radius);
#endif
        }

        #endregion

        //----------------------------------------------------------------------------------------------------

        #region Audio
    
        /// <summary>
        /// Draws the min and max distance visualisers from an AudioSource
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="minDistance"></param>
        /// <param name="maxDistance"></param>
        /// <param name="alpha"></param>
        public static void DrawAudioSource(Transform transform, float minDistance, float maxDistance, float alpha = 0.2f)
        {
            DrawSolidSphere(transform, Vector3.zero, minDistance, alpha);
            DrawSolidSphere(transform, Vector3.zero, maxDistance, alpha);
        }

        #endregion

    } // class end
}