using System;
using UnityEngine;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    public static class GgMaths
    {
        #region Logic Gates
        
        /// <summary>
        /// Returns a bool value based on two input values, and a logic type [BUFFER, NOT, AND, OR, XOR, NAND, NOR, XNOR]
        /// </summary>
        /// <param name="inputs"></param>
        /// <param name="logicGate"></param>
        /// <returns></returns>
		public static bool LogicGateOutputValue(bool[] inputs, LogicGate logicGate)
		{
            switch (logicGate)
            {
                // single-input based logic
                case LogicGate.BUFFER:
                    return inputs[0];
                case LogicGate.NOT:
                    return !inputs[0];
                
                // multi-input based logic
                case LogicGate.AND:
                    return Logic_AND(inputs);
                case LogicGate.NAND:
                    return Logic_NAND(inputs);
                case LogicGate.OR:
                    return Logic_OR(inputs);
                case LogicGate.NOR:
                    return Logic_NOR(inputs);
                case LogicGate.XOR:
                    return Logic_XOR(inputs);
                case LogicGate.XNOR:
                    return Logic_XNOR(inputs);
                
                default:
                    return false;
            }
		}

        /// <summary>
        /// AND: The output is TRUE when all inputs are true. Otherwise, the output is FALSE.
        /// </summary>
        /// <param name="inputs"></param>
        /// <returns></returns>
        private static bool Logic_AND(bool[] inputs)
        {
            foreach (bool input in inputs)
            {
                if (!input) { return false; }
            }
            return true;
        }

        /// <summary>
        /// AND: The output is TRUE when any inputs are false. Otherwise, the output is FALSE.
        /// </summary>
        /// <param name="inputs"></param>
        /// <returns></returns>
        private static bool Logic_NAND(bool[] inputs)
        {
            return !Logic_AND(inputs);
        }

        /// <summary>
        /// OR: The output is TRUE if any of the inputs are true. Otherwise, FALSE.
        /// </summary>
        /// <param name="inputs"></param>
        /// <returns></returns>
        private static bool Logic_OR(bool[] inputs)
        {
            foreach (bool input in inputs)
            {
                if (input) { return true;}
            }
            return false;
        }

        /// <summary>
        /// NOR: The output is TRUE if none of the inputs are true. Otherwise, FALSE.
        /// </summary>
        /// <param name="inputs"></param>
        /// <returns></returns>
        private static bool Logic_NOR(bool[] inputs)
        {
            return !Logic_OR(inputs);
        }

        /// <summary>
        /// XOR: The output is TRUE if only one of the inputs are true. Otherwise, FALSE.
        /// </summary>
        /// <param name="inputs"></param>
        /// <returns></returns>
        private static bool Logic_XOR(bool[] inputs)
        {
            int count = 0;
            foreach (bool input in inputs)
            {
                if (input) { count++;}
            }
            return count == 1;
        }

        /// <summary>
        /// The output is TRUE if none, or more than one, of the inputs are true. Otherwise, FALSE.
        /// </summary>
        /// <param name="inputs"></param>
        /// <returns></returns>
        private static bool Logic_XNOR(bool[] inputs)
        {
            return !Logic_XOR(inputs);
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region Bitfield

        /// <summary>
        /// Set the value [True, False] for a specific position in a bitfield (int32)
        /// </summary>
        /// <param name="bitfield"></param>
        /// <param name="bitPosition"></param>
        /// <param name="bitValue"></param>
        public static void SetBitfieldValue(ref int bitfield, int bitPosition, bool bitValue)
        {
            bitPosition = Mathf.Clamp(bitPosition, 0, 31);
            bitfield = bitValue ? bitfield | (1 << bitPosition) : bitfield & ~(1 << bitPosition);
        }
    
        /// <summary>
        /// Get the value [True, False] for a specific position in a bitfield (int32)
        /// </summary>
        /// <param name="bitfield"></param>
        /// <param name="bitPosition"></param>
        /// <returns></returns>
        public static bool GetBitfieldValue(int bitfield, int bitPosition)
        {
            return 0 < (bitfield & (1 << bitPosition));
        }
    
        /// <summary>
        /// Get a bitfield (int32) as a string of 0's and 1's
        /// </summary>
        /// <param name="bitfield"></param>
        /// <param name="length"></param>
        public static string BitfieldAsString(int bitfield, int length = 32)
        {
            int totalWidth = Mathf.Clamp(length, 0, 32);
            return Convert.ToString(bitfield, 2).PadLeft(totalWidth, '0');
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Rounding

        public static float RoundFloat(float value, int decimalPlaces)
        {
            float multiplier = Mathf.Pow(10f, decimalPlaces);
            return Mathf.Round(value * multiplier) / multiplier;
        }
        
        public static Vector2 RoundVector2(Vector2 value, int decimalPlaces)
        {
            return new Vector2(RoundFloat(value.x, decimalPlaces), RoundFloat(value.y, decimalPlaces));
        }
        
        public static Vector3 RoundVector3(Vector3 value, int decimalPlaces)
        {
            return new Vector3(RoundFloat(value.x, decimalPlaces), RoundFloat(value.y, decimalPlaces), RoundFloat(value.z, decimalPlaces));
        }
        
        public static Vector4 RoundVector4(Vector4 value, int decimalPlaces)
        {
            return new Vector4(RoundFloat(value.x, decimalPlaces), RoundFloat(value.y, decimalPlaces), RoundFloat(value.z, decimalPlaces), RoundFloat(value.w, decimalPlaces));
        }

        #endregion

        //----------------------------------------------------------------------------------------------------

        #region Lerp

        public static Vector2 LerpVector2(Vector2 min, Vector2 max, Vector2 value)
        {
            float x = Mathf.Lerp(min.x, max.x, value.x);
            float y = Mathf.Lerp(min.y, max.y, value.y);
			
            return new Vector2(x, y);
        }

        public static Vector2 InverseLerpVector2(Vector2 min, Vector2 max, Vector2 value)
        {
            float x = Mathf.InverseLerp(min.x, max.x, value.x);
            float y = Mathf.InverseLerp(min.y, max.y, value.y);
			
            return new Vector2(x, y);
        }

        #endregion

        //----------------------------------------------------------------------------------------------------
        
        #region Remap
        
        /// <summary>
        /// Remap a value from a range to a second range
        /// </summary>
        /// <param name="input"></param>
        /// <param name="inputLow"></param>
        /// <param name="inputHigh"></param>
        /// <param name="newLow"></param>
        /// <param name="newHigh"></param>
        /// <returns></returns>
        public static float RemapFloat(float input, float inputLow, float inputHigh, float newLow, float newHigh)
        {
            float t = Mathf.InverseLerp(inputLow, inputHigh, input);
            return Mathf.Lerp(newLow, newHigh, t);
        }

        #endregion

        //----------------------------------------------------------------------------------------------------

        #region Distance / Angle Conversion

        /// <summary>
        /// Convert a distance of a circular arc on a circle with given radius to angle of that circular arc.
        /// </summary>
        /// <param name="distance">Distance of the arc.</param>
        /// <param name="radius">Radius of the circle on which the arc is created.</param>
        /// <returns>Angle of the circular arc in degrees.</returns>
        public static float DistanceToAngle(float distance, float radius)
        {
            float angle = (distance * 180f) / (radius * Mathf.PI);
            return angle;
        }

        /// <summary>
        /// Convert an angle of a circular arc on a circle with given radius to distance of that circular arc.
        /// </summary>
        /// <param name="angle">Angle of the circular arc in degrees.</param>
        /// <param name="radius">Radius of the circle on which the arc is created.</param>
        /// <returns>Distance of the circular arc.</returns>
        public static float AngleToDistance(float angle, float radius)
        {
            float distance = (angle * (radius * Mathf.PI)) / 180f;
            return distance;
        }

        #endregion

        //----------------------------------------------------------------------------------------------------
        
        #region Direction Conversions

        /// <summary>
        /// Get the right vector given the up and forward vectors
        /// </summary>
        /// <param name="up"></param>
        /// <param name="forward"></param>
        /// <returns></returns>
        public static Vector3 GetRight(Vector3 up, Vector3 forward)
        {
            return Vector3.Cross(up, forward).normalized;
        }

        /// <summary>
        /// Get the up vector given the forward and right vectors
        /// </summary>
        /// <param name="forward"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static Vector3 GetUp(Vector3 forward, Vector3 right)
        {
            return Vector3.Cross(forward, right).normalized;
        }

        /// <summary>
        /// Get the forward vector given the right and up vectors
        /// </summary>
        /// <param name="right"></param>
        /// <param name="up"></param>
        /// <returns></returns>
        public static Vector3 GetForward(Vector3 right, Vector3 up)
        {
            return Vector3.Cross(right, up).normalized;
        }
        
        /// <summary>
        /// Converts a direction vector to Quaternion rotation
        /// </summary>
        /// <param name="direction"></param>
        /// <returns></returns>
        public static Quaternion DirectionToRotation(Vector3 direction)
        {
            Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
            return rotation;
        }
        
        /// <summary>
        /// Converts a direction vector to EulerAngle rotation
        /// </summary>
        /// <param name="direction"></param>
        /// <returns></returns>
        public static Vector3 DirectionToEulerAngles(Vector3 direction)
        {
            Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
            return rotation.eulerAngles;
        }

        /// <summary>
        /// Converts from EulerAngles to give the direction vector
        /// </summary>
        /// <param name="eulerAngles"></param>
        /// <returns></returns>
        public static Vector3 EulerAnglesToDirection(Vector3 eulerAngles)
        {
            float sinYaw = Mathf.Sin(eulerAngles.y);
            float cosYaw = Mathf.Cos(eulerAngles.y);
            float sinPitch = Mathf.Sin(eulerAngles.x);
            float cosPitch = Mathf.Cos(eulerAngles.x);
            cosPitch *= -1.0f;

            Vector3 rotatedDirection = new Vector3(
                sinYaw * cosPitch,
                sinPitch,
                cosYaw * cosPitch
            );

            return rotatedDirection;
        }

        /// <summary>
        /// Converts from EulerAngles to give the forward vector
        /// </summary>
        /// <param name="eulerAngles"></param>
        /// <returns></returns>
        public static Vector3 EulerAnglesToDirectionForward(Vector3 eulerAngles)
        {
            // calculate vectors from rotation
            float pitch = Mathf.Deg2Rad * eulerAngles.x;
            float yaw = Mathf.Deg2Rad * eulerAngles.y;
            Vector3 forward = new Vector3
            {
                x = Mathf.Cos(pitch) * Mathf.Sin(yaw),
                y = -Mathf.Sin(pitch),
                z = Mathf.Cos(pitch) * Mathf.Cos(yaw)
            };

            return forward;
        }

        /// <summary>
        /// Converts from EulerAngles to give the up vector
        /// </summary>
        /// <param name="eulerAngles"></param>
        /// <returns></returns>
        public static Vector3 EulerAnglesToDirectionUp(Vector3 eulerAngles)
        {
            // calculate vectors from rotation
            float pitch = Mathf.Deg2Rad * eulerAngles.x;
            float yaw = Mathf.Deg2Rad * eulerAngles.y;
            Vector3 up = new Vector3
            {
                x = Mathf.Sin(pitch) * Mathf.Sin(yaw),
                y = Mathf.Cos(pitch),
                z = Mathf.Sin(pitch) * Mathf.Cos(yaw)
            };

            return up;
        }

        /// <summary>
        /// Converts from EulerAngles to give the right vector
        /// </summary>
        /// <param name="eulerAngles"></param>
        /// <returns></returns>
        public static Vector3 EulerAnglesToDirectionRight(Vector3 eulerAngles)
        {
            // calculate vectors from rotation
            float pitch = Mathf.Deg2Rad * eulerAngles.x;
            float yaw = Mathf.Deg2Rad * eulerAngles.y;
            Vector3 right = new Vector3
            {
                x = Mathf.Cos(yaw),
                y = 0,
                z = -Mathf.Sin(yaw)
            };

            return right;
        }

        #endregion

        //----------------------------------------------------------------------------------------------------

        #region Transforms

        /// <summary>
        /// Transforms a rotation from parent's local space to world space
        /// </summary>
        /// <param name="eulerAngles"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        public static Vector3 TransformRotation(Vector3 eulerAngles, Transform parent)
        {
            return eulerAngles + parent.eulerAngles;
        }

        /// <summary>
        /// Transforms a rotation from world space to parent's local space
        /// </summary>
        /// <param name="eulerAngles"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        public static Vector3 InverseTransformRotation(Vector3 eulerAngles, Transform parent)
        {
            return eulerAngles - parent.eulerAngles;
        }

        /// <summary>
        /// Transforms point from parent's local space to world space
        /// </summary>
        /// <param name="point"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        public static Vector3 TransformPoint(Vector3 point, Transform parent)
        {
            return parent.rotation * Vector3.Scale(point,parent.localScale) + parent.position;
        }

        /// <summary>
        /// Transforms point from world space to parent's local space
        /// </summary>
        /// <param name="point"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        public static Vector3 InverseTransformPoint(Vector3 point, Transform parent)
        {
            Vector3 offset = (point - parent.position);
            Vector3 pointRelativeToScale = new Vector3(offset.x / parent.lossyScale.x, offset.y / parent.lossyScale.y, offset.z / parent.lossyScale.z);
            Vector3 pointRelativeToRotation =  Quaternion.Inverse(parent.rotation) * pointRelativeToScale;

            return pointRelativeToRotation;
        }
        
        /// <summary>
        /// Transforms point from parent's local space (pivot, forward vector and right vector) to world space
        /// </summary>
        /// <param name="point"></param>
        /// <param name="pivot"></param>
        /// <param name="forward"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static Vector3 TransformPoint(Vector3 point, Vector3 pivot, Vector3 forward, Vector3 right)
        {
            Vector3 offset = (point - pivot);
            Vector3 up = GetUp(forward, right);
            Quaternion rotation = Quaternion.LookRotation(forward, up);
            Vector3 pointRelativeToRotation = (rotation * offset) + pivot;

            return pointRelativeToRotation;
        }

        #endregion

        //----------------------------------------------------------------------------------------------------

        #region Line Projection

        /// <summary>
        /// Projects a point onto a line in 3D space
        /// </summary>
        /// <param name="point">The position in 3D space to be projected.</param>
        /// <param name="lineStart">The starting point of the line.</param>
        /// <param name="lineEnd">The ending point of the line.</param>
        /// <returns></returns>
        public static Vector3 ProjectPointToLine(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
        {
            Vector3 hypotenuse = point - lineStart;
            Vector3 lineDirection = (lineEnd - lineStart).normalized;
            float lineDistance = Vector3.Distance(lineStart, lineEnd);
            float angle = Vector3.Dot(lineDirection, hypotenuse);

            // handle end points
            if (angle <= 0) { return lineStart; }
            if (angle >= lineDistance) { return lineEnd; }

            // handle other points
            Vector3 distanceAlongLine = lineDirection * angle;
            Vector3 closestPoint = lineStart + distanceAlongLine;

            return closestPoint;
        }

        /// <summary>
        /// Get whether a point is on a line defined by start and end points
        /// </summary>
        /// <param name="point"></param>
        /// <param name="lineStart"></param>
        /// <param name="lineEnd"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public static bool IsPointOnLine(Vector3 point, Vector3 lineStart, Vector3 lineEnd, float precision = 0.001f)
        {
            return Vector3.Distance(point, ProjectPointToLine(point, lineStart, lineEnd)) < precision;
        }
        
        /// <summary>
        /// Get the intersection point between two lines
        /// </summary>
        /// <param name="intersectionPoint"></param>
        /// <param name="lineAStart"></param>
        /// <param name="lineADirection"></param>
        /// <param name="lineBStart"></param>
        /// <param name="lineBDirection"></param>
        /// <param name="precision"></param>
        /// <returns>True if intersection exists, false if not</returns>
        public static bool LineLineIntersection(out Vector3 intersectionPoint, Vector3 lineAStart, Vector3 lineADirection, Vector3 lineBStart, Vector3 lineBDirection, float precision = 0.001f)
        {
            Vector3 lineC = lineBStart - lineAStart;
            Vector3 crossAB = Vector3.Cross(lineADirection, lineBDirection);
            Vector3 crossBC = Vector3.Cross(lineC, lineBDirection);

            float planarFactor = Vector3.Dot(lineC, crossAB);

            // check if is coplanar, and not parallel
            if ( Mathf.Abs(planarFactor) <= precision && precision <= crossAB.sqrMagnitude)
            {
                float distance = Vector3.Dot(crossBC, crossAB) / crossAB.sqrMagnitude;
                intersectionPoint = lineAStart + (lineADirection * distance);
                return true;
            }
            else
            {
                intersectionPoint = Vector3.zero;
                return false;
            }
        }

        #endregion

        //----------------------------------------------------------------------------------------------------

        #region Circle Projection
        
        /// <summary>
        /// Projects a point onto a circular arc in 3D space.
        /// </summary>
        /// <param name="point">The position in 3D space to be projected.</param>
        /// <param name="center">The center of the circle in world space.</param>
        /// <param name="normal">The normal direction of the circle in world space.</param>
        /// <param name="up">The up direction of the circle in world space.</param>
        /// <param name="radius">The radius of the circle in world space units.</param>
        /// <param name="onCircumference">If true then the projected point will always be on the outline of the circle</param>
        /// <returns></returns>
        public static Vector3 ProjectPointToCircle(Vector3 point, Vector3 center, Vector3 normal, Vector3 up, float radius, bool onCircumference)
        {
            // project onto 2D plane perpendicular to the normal
            Vector3 direction = point - center;
            Vector3 projectionPlane = Vector3.Project(direction, up) + Vector3.Project(direction, GetRight(up, normal));
            
            // clamp angleDelta
            float angleDelta = Vector3.SignedAngle(up, projectionPlane, normal);
            
            // check if "inside" circle
            bool inSide = projectionPlane.magnitude < radius;
            
            // return projected point on circle
            if (inSide && !onCircumference)
            {
                return center + projectionPlane;
            }
            else
            {
                // calculate and return projected point on circumference
                return center + Quaternion.AngleAxis(angleDelta, normal) * up * radius;
            }
        }

        /// <summary>
        /// Projects a point onto a circular arc in 3D space.
        /// </summary>
        /// <param name="point">The position in 3D space to be projected.</param>
        /// <param name="center">The center of the circle in world space.</param>
        /// <param name="normal">The normal direction of the circle in world space.</param>
        /// <param name="from">The direction of the point on the circle circumference, relative to the center, where the arc begins.</param>
        /// <param name="angle">The angle of the arc, in degrees [0-360].</param>
        /// <param name="radius">The radius of the circle in world space units.</param>
        /// <returns></returns>
        public static Vector3 ProjectPointToArc(Vector3 point, Vector3 center, Vector3 normal, Vector3 from, float angle, float radius)
        {
            // use circle projection of angle is greater than a full circle
            if (360 <= angle) { return ProjectPointToCircle(point, center, normal, from, radius, true); }
            
            // project onto 2D plane perpendicular to the normal
            Vector3 direction = point - center;
            Vector3 planeProjection = Vector3.Project(direction, from) + Vector3.Project(direction, GetRight(from, normal));
            
            // calculate angleDelta [-180, 180] and convert angles to circle range [0, 360]
            float angleDelta = Vector3.SignedAngle(from, planeProjection, normal);
            angleDelta = angleDelta < 0 ? angleDelta + 360 : angleDelta;

            // instead of using Mathf.Clamp here we do a trick to clamp to correct end of the arc
            // (otherwise any angle outside of the arc would be clamped to the max value)
            float offsetDelta = angle < 0 ? 360 - angleDelta : angleDelta;
            angle = Mathf.Abs(angle);
            if (offsetDelta < angle)
            {
                // return projected point on arc
                return center + (Quaternion.AngleAxis(angleDelta, normal) * from * radius);
            }
            else if ((offsetDelta - angle) < ((360 - angle) * 0.5))
            {
                // return projected point on arc
                return center + (Quaternion.AngleAxis(angle, normal) * from * radius);
            }
            else
            {
                // return projected point on arc
                return center + (from * radius);
            }
        }
        
        /// <summary>
        /// Is a point on the positive side of a circle in 3D space?
        /// </summary>
        /// <param name="point">The position in 3D space to be projected.</param>
        /// <param name="center">The center of the circle in world space.</param>
        /// <param name="normal">The normal direction of the circle in world space.</param>
        /// <param name="up">The up direction of the circle in world space.</param>
        /// <returns></returns>
        public static bool GetSideOfCircle(Vector3 point, Vector3 center, Vector3 normal, Vector3 up)
        {
            Plane plane = PlaneExtensions.GetPlaneFromPointAndRelativeVectors(center, up, GetRight(up, normal));
            return plane.GetSide(point);
        }
        
        /// <summary>
        /// Get a point on a circle in the x-axis and y-axis (flat z-axis)
        /// </summary>
        /// <param name="center"></param>
        /// <param name="radius"></param>
        /// <param name="angle"></param>
        /// <returns></returns>
        public static Vector3 GetPointOnCircle(Vector3 center, float radius, float angle)
        {
            Vector3 point = new Vector3
            {
                x = center.x + (radius * Mathf.Cos(angle / (180f / Mathf.PI))),
                y = center.y + (radius * Mathf.Sin(angle / (180f / Mathf.PI))),
                z = center.z
            };

            return point;
        }

        #endregion

        //----------------------------------------------------------------------------------------------------

        #region Rect Projection

        /// <summary>
        /// Projects a point onto a rect in 3D space.
        /// </summary>
        /// <param name="point">The position in 3D space to be projected.</param>
        /// <param name="center">The center of the rect in world space.</param>
        /// <param name="up">The up vector of the rect in world space.</param>
        /// <param name="right">The right vector of the rect in world space.</param>
        /// <param name="scale">The scale of the rect. [width, height]</param>
        /// <param name="onOutline">If true then the projected point will always be on the outline of the rect</param>
        /// <returns></returns>
        public static Vector3 ProjectPointToRect(Vector3 point, Vector3 center, Vector3 up, Vector3 right, Vector2 scale, bool onOutline)
        {
            // cache rect and plane
            Vector3 rectTopLeft = center + (up * scale.x * 0.5f) - (right * scale.y * 0.5f);
            Vector3 rectTopRight = center + (up * scale.x * 0.5f) + (right * scale.y * 0.5f);
            Vector3 rectBottomRight = center - (up * scale.x * 0.5f) + (right * scale.y * 0.5f);
            Vector3 rectBottomLeft = center - (up * scale.x * 0.5f) - (right * scale.y * 0.5f);
            
            // calculate closest points
            Vector3 projectionPlane = ProjectPointToPlane(point, center, up, right);
            Vector3 projectionLeft = ProjectPointToLine(point, rectTopLeft, rectBottomLeft);
            Vector3 projectionRight = ProjectPointToLine(point, rectTopRight, rectBottomRight);
            Vector3 projectionTop = ProjectPointToLine(point, rectTopLeft, rectTopRight);
            Vector3 projectionBottom = ProjectPointToLine(point, rectBottomLeft, rectBottomRight);
            
            // calculate shortest distance
            float distanceLeft = Vector3.Distance(point, projectionLeft);
            float distanceRight = Vector3.Distance(point, projectionRight);
            float distanceTop = Vector3.Distance(point, projectionTop);
            float distanceBottom = Vector3.Distance(point, projectionBottom);
            
            // check if "inside" rect
            bool inHeight = projectionBottom.y < projectionPlane.y && projectionPlane.y < projectionTop.y;
            bool inWidth = projectionLeft.x < projectionPlane.x && projectionPlane.x < projectionRight.x;
            
            // return projected point on rect
            if (inHeight && inWidth && !onOutline)
            {
                return projectionPlane;
            }
            else if (distanceLeft <= Mathf.Min(new []{ distanceRight, distanceTop, distanceBottom }))
            {
                return projectionLeft;
            }
            else if (distanceRight <= Mathf.Min(new []{ distanceLeft, distanceTop, distanceBottom }))
            {
                return projectionRight;
            }
            else if (distanceTop <= Mathf.Min(new []{ distanceLeft, distanceRight, distanceBottom }))
            {
                return projectionTop;
            }
            else // if (distanceBottom <= Mathf.Min(new []{ distanceLeft, distanceRight, distanceTop }))
            {
                return projectionBottom;
            }
        }
        
        /// <summary>
        /// Is a point on the positive side of a rect in 3D space?
        /// </summary>
        /// <param name="point">The position in 3D space to be projected.</param>
        /// <param name="center">The center of the plane in world space.</param>
        /// <param name="up">The up vector of the plane in world space.</param>
        /// <param name="right">The right vector of the plane in world space.</param>
        /// <returns></returns>
        public static bool GetSideOfRect(Vector3 point, Vector3 center, Vector3 up, Vector3 right)
        {
            Plane plane = PlaneExtensions.GetPlaneFromPointAndRelativeVectors(center, up, right);
            return plane.GetSide(point);
        }

        /// <summary>
        /// Projects a point onto a plane in 3D space.
        /// </summary>
        /// <param name="point">The position in 3D space to be projected.</param>
        /// <param name="center">The center of the plane in world space.</param>
        /// <param name="up">The up vector of the plane in world space.</param>
        /// <param name="right">The right vector of the plane in world space.</param>
        /// <returns></returns>
        public static Vector3 ProjectPointToPlane(Vector3 point, Vector3 center, Vector3 up, Vector3 right)
        {
            Plane plane = PlaneExtensions.GetPlaneFromPointAndRelativeVectors(center, up, right);
            return plane.ClosestPointOnPlane(point);
        }

        #endregion
        
    } // class end
}
