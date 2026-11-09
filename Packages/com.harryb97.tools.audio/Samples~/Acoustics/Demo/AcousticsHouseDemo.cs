using UnityEngine;
using UnityEngine.InputSystem;

namespace HarryTools.Audio.Acoustics.Demo {
    /// <summary> Controls of the house demo: E picks up or drops the radio, F opens or closes the door, G destroys the wall, R plays a short sound at the radio. </summary>
    [AddComponentMenu("HarryTools/Audio/Samples/Acoustics House Demo")]
    public sealed class AcousticsHouseDemo : MonoBehaviour {
        // ==================== VARIABLES ===================
        [Header("Scene")]
        public SoundAsset RadioSound;
        [Tooltip("Short sound that R plays at the radio, to hear the reverb tail after it ends.")]
        public SoundAsset ShortSound;
        public Transform Radio;
        public Transform Listener;
        [Tooltip("Pivot at the hinge of the door; it turns around Y.")]
        public Transform DoorPivot;
        [Tooltip("Wall that G destroys.")]
        public GameObject BreakableWall;

        [Header("Settings")]
        [Min(0), Tooltip("How close the listener must be to pick the radio up, in meters.")]
        public float PickUpDistance = 2.5f;
        [Tooltip("Yaw in degrees of the open door.")]
        public float DoorOpenAngle = -90f;
        [Tooltip("Where the held radio sits relative to the listener, in world axes.")]
        public Vector3 HeldOffset = new Vector3(0f, -0.8f, 0.5f);

        private AudioHandle radioHandle;
        private bool held;

        // ==================== PROPERTIES ===================
        public bool IsHeld => held;
        public bool IsDoorOpen { get; private set; }

        // ==================== UNITY ===================
        private void Start() {
            if (RadioSound == null || Radio == null) return;
            radioHandle = Audio.Play(RadioSound).Follow(Radio);
        }

        private void Update() {
            Keyboard _keyboard = Keyboard.current;
            if (_keyboard == null) return;
            if (_keyboard.eKey.wasPressedThisFrame) PickUpOrDrop();
            if (_keyboard.fKey.wasPressedThisFrame) ToggleDoor();
            if (_keyboard.gKey.wasPressedThisFrame) DestroyWall();
            if (_keyboard.rKey.wasPressedThisFrame) PlayShortSound();
        }

        // ==================== ACTIONS ===================
        /// <summary> Takes the radio when the listener is close; drops it on the floor at the listener otherwise. </summary>
        public void PickUpOrDrop() {
            if (Radio == null || Listener == null) return;

            if (held) {
                Radio.SetParent(null, true);
                Vector3 _at = Listener.position;
                Radio.position = new Vector3(_at.x, 0.2f, _at.z);
                held = false;
                Debug.Log("Radio dropped.");
                return;
            }

            Vector3 _gap = Radio.position - Listener.position;
            _gap.y = 0f;
            if (_gap.magnitude > PickUpDistance) {
                Debug.Log("The radio is too far to pick up (" + _gap.magnitude.ToString("0.0") + " m).");
                return;
            }
            Radio.SetParent(Listener, true);
            Radio.position = Listener.position + HeldOffset;
            held = true;
            Debug.Log("Radio picked up.");
        }

        public void ToggleDoor() {
            if (DoorPivot == null) return;
            IsDoorOpen = !IsDoorOpen;
            DoorPivot.localRotation = Quaternion.Euler(0f, IsDoorOpen ? DoorOpenAngle : 0f, 0f);
            // Colliders moved by transform are only seen by the next ray after a sync.
            Physics.SyncTransforms();
            Debug.Log(IsDoorOpen ? "Door opened." : "Door closed.");
        }

        /// <summary> Plays the short sound where the radio is. It ends fast, so the room's reverb tail is what keeps sounding. </summary>
        public void PlayShortSound() {
            if (ShortSound == null || Radio == null) return;
            Audio.Play(ShortSound).At(Radio.position);
            Debug.Log("Short sound played at the radio.");
        }

        public void DestroyWall() {
            if (BreakableWall == null) return;
            Destroy(BreakableWall);
            BreakableWall = null;
            Debug.Log("Wall destroyed.");
        }
    }
}
