using JetBrains.Annotations;
using UnityEngine;

namespace HarryTools.UI
{
    [CreateAssetMenu(menuName = "HarryTools/UI/View Collection", order = 500)]
    public class ViewCollection : ScriptableObject
    {
        [UsedImplicitly]
        public bool autoGenerate = true;

        [UsedImplicitly]
        public Object[] folders;

        public MonoView[] views;
    }
}
