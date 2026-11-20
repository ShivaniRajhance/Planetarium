using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using VRSYS.Core.Navigation;

namespace VRSYS.ProjectTemplate.Navigation
{
    /// <summary>
    /// Coordinates synchronized teleports for all users currently linked to a navigation platform.
    /// </summary>
    /// <remarks>
    /// The coordinator tracks participants via <see cref="INavigationPlatformCallbacks"/> and executes
    /// a server-side teleport batch by snapping the platform and all linked user roots in one flow.
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public class GroupTeleportPlatformCoordinator : NetworkBehaviour, INavigationPlatformCallbacks
    {
        private struct RelativePose
        {
            public GroupTeleportParticipant Participant;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
        }

        [Tooltip("Transform that represents the platform root. Defaults to this transform.")]
        [SerializeField] private Transform platformRoot;

        [Tooltip("NetworkTransform used to snap the platform. Defaults to the first NetworkTransform on this object.")]
        [SerializeField] private NetworkTransform platformNetworkTransform;

        [Tooltip("When enabled, the requesting user is teleported solo if group teleport execution fails.")]
        [SerializeField] private bool fallbackToRequesterSoloTeleport = true;

        private readonly Dictionary<ulong, GroupTeleportParticipant> participants = new();
        private ulong teleportSequence;

        /// <summary>
        /// Gets the transform that represents the replicated platform root.
        /// </summary>
        public Transform PlatformRoot => platformRoot != null ? platformRoot : transform;

        private void Awake()
        {
            if (platformRoot == null)
            {
                platformRoot = transform;
            }

            if (platformNetworkTransform == null)
            {
                TryGetComponent(out platformNetworkTransform);
            }
        }

        /// <summary>
        /// Registers a linked user as a participant when it enters this platform.
        /// </summary>
        /// <param name="link">The link of the user entering the platform.</param>
        public void OnEnterPlatform(NavigationPlatformLink link)
        {
            if (link == null)
            {
                return;
            }

            if (!link.TryGetComponent(out GroupTeleportParticipant participant))
            {
                return;
            }

            participants[participant.NetworkObjectId] = participant;
        }

        /// <summary>
        /// Unregisters a linked user when it leaves this platform.
        /// </summary>
        /// <param name="link">The link of the user leaving the platform.</param>
        public void OnLeavePlatform(NavigationPlatformLink link)
        {
            if (link == null)
            {
                return;
            }

            if (!link.TryGetComponent(out GroupTeleportParticipant participant))
            {
                return;
            }

            participants.Remove(participant.NetworkObjectId);
        }

        /// <summary>
        /// Attempts to execute a synchronized group teleport for the current platform membership.
        /// </summary>
        /// <param name="requester">The participant that initiated the teleport request.</param>
        /// <param name="targetPosition">The target world position for the platform root.</param>
        /// <param name="targetRotation">The target world rotation for the platform root.</param>
        /// <returns>
        /// <see langword="true"/> when the teleport batch was accepted and executed; otherwise <see langword="false"/>.
        /// </returns>
        public bool TryExecuteGroupTeleport(GroupTeleportParticipant requester, Vector3 targetPosition, Quaternion targetRotation)
        {
            if (!IsServer)
            {
                return false;
            }

            if (requester == null || !requester.IsSpawned)
            {
                return false;
            }

            if (!participants.ContainsKey(requester.NetworkObjectId))
            {
                return false;
            }

            if (platformNetworkTransform == null)
            {
                return TryFallbackSoloTeleport(requester, targetPosition, targetRotation);
            }

            List<RelativePose> capturedParticipants = new();
            foreach (GroupTeleportParticipant participant in participants.Values)
            {
                if (participant == null || !participant.IsSpawned)
                {
                    continue;
                }

                if (!participant.TryCaptureRelativePose(PlatformRoot, out Vector3 localPosition, out Quaternion localRotation))
                {
                    continue;
                }

                capturedParticipants.Add(new RelativePose
                {
                    Participant = participant,
                    LocalPosition = localPosition,
                    LocalRotation = localRotation
                });
            }

            teleportSequence++;
            platformNetworkTransform.Teleport(targetPosition, targetRotation, PlatformRoot.localScale);

            foreach (RelativePose relativePose in capturedParticipants)
            {
                Vector3 worldPosition = targetPosition + targetRotation * relativePose.LocalPosition;
                Quaternion worldRotation = targetRotation * relativePose.LocalRotation;
                relativePose.Participant.TeleportToWorldPose(worldPosition, worldRotation);
            }

            NotifyTeleportAppliedClientRpc(teleportSequence, targetPosition, targetRotation);
            return true;
        }

        [ClientRpc]
        private void NotifyTeleportAppliedClientRpc(ulong sequenceId, Vector3 targetPosition, Quaternion targetRotation)
        {
        }

        private bool TryFallbackSoloTeleport(GroupTeleportParticipant requester, Vector3 targetPosition, Quaternion targetRotation)
        {
            if (!fallbackToRequesterSoloTeleport)
            {
                return false;
            }

            requester.TeleportToWorldPose(targetPosition, targetRotation);
            return true;
        }
    }
}
