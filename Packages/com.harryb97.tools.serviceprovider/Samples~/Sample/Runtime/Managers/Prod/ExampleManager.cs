using UnityEngine;

namespace HarryTools.ServiceLocator.Example.Prod {
    public class ExampleManager : IExampleService {
        // ==================== START ====================
        public ExampleManager() {
            Debug.Log("Created Example Manager in Production Scope");
        }
        
        // ==================== METHODS ====================
        #region IExampleService
        void IExampleService.ExampleMethod() {
            Debug.Log("Example in Production Scope!");
        }
        #endregion
    }
}