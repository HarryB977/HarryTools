using UnityEngine;

namespace HarryTools.Platformer3DController.Sample {
    /// <summary> Moves a kinematic rigidbody back and forth along an offset and spins it around its up axis. </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlatformMover : MonoBehaviour {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private Vector3 offset = new(0f, 0f, 6f);
        [SerializeField] private float period = 4f;
        [SerializeField] private float spinSpeed = 0f;

        private Rigidbody rB;
        private Vector3 startPosition;
        private Quaternion startRotation;
        #endregion

        // ==================== START ====================
        private void Awake() {
            TryGetComponent(out rB);
            rB.isKinematic = true;
            rB.interpolation = RigidbodyInterpolation.Interpolate;

            startPosition = rB.position;
            startRotation = rB.rotation;
        }

        private void FixedUpdate() {
            float _time = Time.fixedTime;
            float _wave = 0.5f - 0.5f * Mathf.Cos(_time * 2f * Mathf.PI / Mathf.Max(0.01f, period));

            rB.MovePosition(startPosition + offset * _wave);
            rB.MoveRotation(startRotation * Quaternion.Euler(0f, spinSpeed * _time, 0f));
        }

        private void OnDrawGizmosSelected() {
            Vector3 _start = Application.isPlaying ? startPosition : transform.position;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(_start, _start + offset);
        }
    }
}
