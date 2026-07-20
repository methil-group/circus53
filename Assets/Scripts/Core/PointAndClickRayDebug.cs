using UnityEngine;

namespace Core
{
    /// <summary>Affiche le dernier raycast point & click dans les gizmos pendant le Play mode.</summary>
    public class PointAndClickRayDebug : MonoBehaviour
    {
        private Ray _lastRay;
        private RaycastHit[] _lastHits = System.Array.Empty<RaycastHit>();
        private bool _hasRay;

        public void Record(Ray ray, RaycastHit[] hits)
        {
            _lastRay = ray;
            _lastHits = hits;
            _hasRay = true;
        }

        private void OnDrawGizmos()
        {
            if (!_hasRay) return;

            Gizmos.color = Color.magenta;
            Gizmos.DrawRay(_lastRay.origin, _lastRay.direction * 100f);
            Gizmos.DrawSphere(_lastRay.origin, 0.06f);

            foreach (RaycastHit hit in _lastHits)
            {
                bool isCameraPoint = hit.collider.GetComponentInParent<CameraClickPoint>() != null;
                Gizmos.color = isCameraPoint ? Color.green : Color.red;
                Gizmos.DrawWireSphere(hit.point, 0.12f);
                Gizmos.DrawLine(hit.point, hit.point + hit.normal * 0.35f);
            }
        }
    }
}
