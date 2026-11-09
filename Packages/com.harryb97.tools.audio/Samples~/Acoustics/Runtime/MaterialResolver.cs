using System.Collections.Generic;
using UnityEngine;

namespace HarryTools.Audio.Acoustics {
    /// <summary> Finds the acoustic material of a collider and caches it per collider. </summary>
    internal sealed class MaterialResolver {
        // ==================== VARIABLES ===================
        private readonly AcousticProfile profile;
        private readonly Dictionary<int, AcousticMaterial> cache = new Dictionary<int, AcousticMaterial>();

        // ==================== INITIALIZATION ===================
        public MaterialResolver(AcousticProfile _profile) {
            profile = _profile;
        }

        // ==================== PUBLIC API ===================
        /// <summary> AcousticSurface on the collider or a parent, then the mapped physics material, then the profile default. </summary>
        public AcousticMaterial Resolve(Collider _collider) {
            int _id = _collider.GetInstanceID();
            if (cache.TryGetValue(_id, out AcousticMaterial _cached)) return _cached;

            AcousticMaterial _material = null;
            AcousticSurface _surface = _collider.GetComponentInParent<AcousticSurface>();
            if (_surface != null) _material = _surface.Material;

            if (_material == null) _material = FromPhysicsMaterial(_collider.sharedMaterial);
            if (_material == null) _material = profile.DefaultMaterial;

            cache[_id] = _material;
            return _material;
        }

        public void Forget(int _colliderInstanceId) {
            cache.Remove(_colliderInstanceId);
        }

        public void Clear() {
            cache.Clear();
        }

        // ==================== HELPERS ===================
        private AcousticMaterial FromPhysicsMaterial(PhysicsMaterial _physics) {
            if (_physics == null || profile.PhysicsMaterials == null) return null;
            for (int i = 0; i < profile.PhysicsMaterials.Count; i++) {
                PhysicsMaterialMapping _mapping = profile.PhysicsMaterials[i];
                if (_mapping.Physics == _physics && _mapping.Acoustic != null) return _mapping.Acoustic;
            }
            return null;
        }
    }
}
