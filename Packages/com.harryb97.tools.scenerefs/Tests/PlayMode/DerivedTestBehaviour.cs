using UnityEngine;

namespace HarryTools.SceneRefs.Tests
{
    /// <summary>
    /// Subclass of <see cref="BaseTestBehaviour"/>; must not count when resolving a BaseTestBehaviour by index.
    /// </summary>
    [AddComponentMenu("")]
    public class DerivedTestBehaviour : BaseTestBehaviour { }
}
