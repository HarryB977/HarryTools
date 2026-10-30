using UnityEngine;

namespace HarryTools.Runner.Example {
    public class ExampleEntity : MonoBehaviour {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private float delay = 3f;
        private ExampleClass exampleClass;
        #endregion
        
        // ==================== START ====================
        private void Start() {
            exampleClass = new ExampleClass(delay);
        }
    }
}