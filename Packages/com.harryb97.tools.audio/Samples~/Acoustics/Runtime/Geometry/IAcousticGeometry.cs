using System;
using UnityEngine;

namespace HarryTools.Audio.Acoustics {
    /// <summary> A segment to trace through the acoustic geometry. </summary>
    internal struct AcousticRay {
        public Vector3 From;
        public Vector3 To;
    }

    /// <summary> One surface crossed by a ray. <see cref="ColliderId"/> is the collider instance ID. </summary>
    internal struct AcousticHit {
        public float Distance;
        public Vector3 Point;
        public int ColliderId;
    }

    /// <summary> Hides the physics engine from the acoustic simulation. </summary>
    internal interface IAcousticGeometry : IDisposable {
        /// <summary> Schedules a batch of rays and returns its id. Results become available on a later frame. </summary>
        int Schedule(ReadOnlySpan<AcousticRay> _rays, int _maxHits);

        /// <summary> Hits of ray i are at [i * maxHits, i * maxHits + hitCounts[i]), nearest first. The spans are valid until <see cref="Release"/>. </summary>
        bool TryGetResults(int _batch, out ReadOnlySpan<AcousticHit> _hits, out ReadOnlySpan<int> _hitCounts);

        void Release(int _batch);

        /// <summary> Acoustic material of a collider; the profile default when the collider no longer exists. </summary>
        AcousticMaterial MaterialOf(int _colliderId);

        /// <summary> True when the point is inside a solid collider. False when the collider no longer exists or its inside is undefined (open or non-convex meshes). </summary>
        bool ContainsPoint(int _colliderId, Vector3 _point);

        /// <summary> World bounds of a collider. False when the collider no longer exists, is disabled or its object is inactive. </summary>
        bool TryGetColliderBounds(int _colliderId, out Bounds _bounds);

        /// <summary> Applies the layer mask and materials of <paramref name="_profile"/> to batches scheduled from now on. </summary>
        void Configure(AcousticProfile _profile);
    }
}
