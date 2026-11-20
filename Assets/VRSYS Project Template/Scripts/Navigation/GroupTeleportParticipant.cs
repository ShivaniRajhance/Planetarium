using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using VRSYS.Core.Navigation;

namespace VRSYS.ProjectTemplate.Navigation
{
    /// <summary>
    /// Identifies a networked user root that can participate in synchronized group teleports.
    /// </summary>
    /// <remarks>
    /// This component captures the user's pose relative to a linked platform and applies teleport
    /// snaps through the assigned <see cref="NetworkTransform"/> when the server executes a group teleport.
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public class GroupTeleportParticipant : NetworkBehaviour
    {
        [Tooltip("Root transform that should be teleported. Defaults to this transform.")]
        [SerializeField] private Transform teleportRoot;

        [Tooltip("NetworkTransform used for teleport snaps. Defaults to the first NetworkTransform on this object.")]
        [SerializeField] private NetworkTransform networkTransform;

        [Tooltip("Optional explicit NavigationPlatformLink reference. Defaults to the one on this object.")]
        [SerializeField] private NavigationPlatformLink navigationPlatformLink;

        /// <summary>
        /// Gets the transform that should be repositioned during a group teleport.
        /// </summary>
        public Transform TeleportRoot => teleportRoot != null ? teleportRoot : transform;

        /// <summary>
        /// Gets the <see cref="NetworkTransform"/> used to perform teleport snaps for this participant.
        /// </summary>
        public NetworkTransform MovementNetworkTransform => networkTransform;

        /// <summary>
        /// Gets the current navigation platform link for this participant.
        /// </summary>
        public NavigationPlatformLink PlatformLink => navigationPlatformLink;

        private void Awake()
        {
            if (teleportRoot == null)
            {
                teleportRoot = transform;
            }

            if (networkTransform == null)
            {
                TryGetComponent(out networkTransform);
            }

            if (navigationPlatformLink == null)
            {
                TryGetComponent(out navigationPlatformLink);
            }
        }

        /// <summary>
        /// Captures the participant pose relative to the provided platform transform.
        /// </summary>
        /// <param name="platform">The platform transform used as the relative frame of reference.</param>
        /// <param name="localPosition">The captured local position relative to <paramref name="platform"/>.</param>
        /// <param name="localRotation">The captured local rotation relative to <paramref name="platform"/>.</param>
        /// <returns>
        /// <see langword="true"/> when the platform is valid and a relative pose could be captured;
        /// otherwise <see langword="false"/>.
        /// </returns>
        public bool TryCaptureRelativePose(Transform platform, out Vector3 localPosition, out Quaternion localRotation)
        {
            localPosition = default;
            localRotation = default;

            if (platform == null)
            {
                return false;
            }

            Transform root = TeleportRoot;
            localPosition = platform.InverseTransformPoint(root.position);
            localRotation = Quaternion.Inverse(platform.rotation) * root.rotation;
            return true;
        }

        /// <summary>
        /// Snaps this participant to the provided world pose.
        /// </summary>
        /// <param name="worldPosition">The target world position.</param>
        /// <param name="worldRotation">The target world rotation.</param>
        public void TeleportToWorldPose(Vector3 worldPosition, Quaternion worldRotation)
        {
            Vector3 scale = TeleportRoot.localScale;

            if (networkTransform != null)
            {
                networkTransform.Teleport(worldPosition, worldRotation, scale);
                return;
            }

            TeleportRoot.SetPositionAndRotation(worldPosition, worldRotation);
        }
    }
}
