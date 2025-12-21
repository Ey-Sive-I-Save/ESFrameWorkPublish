using UnityEngine;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    public static class PlaneExtensions
    {
        /// <summary>
        /// Get a plane at a given point and it's relative vectors
        /// </summary>
        /// <param name="center">The center of the plane in world space.</param>
        /// <param name="up">The up vector of the plane in world space.</param>
        /// <param name="right">The right vector of the plane in world space.</param>
        /// <returns></returns>
        public static Plane GetPlaneFromPointAndRelativeVectors(Vector3 center, Vector3 up, Vector3 right)
        {
            Vector3 pointA = center + up;
            Vector3 pointB = center - ((up * 0.5f) + (right * 0.866f));
            Vector3 pointC = center - ((up * 0.5f) - (right * 0.866f));

            return new Plane(pointA, pointB, pointC);
        }

    } // class end
}
