using System;

namespace HarryTools.Audio {
    /// <summary> Saved bus state. The index of both arrays is (int)AudioBus. </summary>
    [Serializable]
    public struct AudioVolumeData {
        public float[] Volumes;
        public bool[] Muted;
    }
}
