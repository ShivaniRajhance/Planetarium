using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using VRSYS.Core.Avatar;
using VRSYS.Core.Navigation;
using VRSYS.Core.Utility;

namespace VRSYS.ProjectTemplate.Navigation
{
    /// <summary>
    /// Provides thumbstick-driven teleport and rotation controls with optional group teleport execution.
    /// </summary>
    /// <remarks>
    /// This component mirrors the existing thumbstick teleport interaction model while redirecting locked
    /// teleports through a <see cref="GroupTeleportPlatformCoordinator"/> when the local user is linked to
    /// a group-capable platform.
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AvatarHMDAnatomy))]
    [RequireComponent(typeof(NetworkObject))]
    public class GroupThumbstickTeleportNavigation : NetworkBehaviour
    {
        /// <summary>
        /// Defines how thumbstick rotation is applied while teleport navigation is enabled.
        /// </summary>
        private enum TeleportState
        {
            Idle,
            Aiming,
            Locked
        }

        /// <summary>
        /// Defines how the non-navigation thumbstick rotates the user.
        /// </summary>
        public enum RotationMode
        {
            Continuous,
            Snap
        }

        [Header("Input Actions")]
        [Tooltip("Expects input as Vector2 from left thumbstick.")]
        public InputActionProperty leftThumbstick;

        [Tooltip("Expects input as Vector2 from right thumbstick.")]
        public InputActionProperty rightThumbstick;

        [Header("General Configuration")]
        [Tooltip("Thumbstick used for teleport navigation. The other thumbstick is used for rotation.")]
        public HandType navigationHand = HandType.Left;

        [Tooltip("Transform modified during teleportation. Defaults to this transform.")]
        public Transform teleportationTarget;

        [Header("Teleport Configuration")]
        public LineRenderer ray;
        public TeleportPreviewAvatar previewAvatar;
        public float maxRayLength = 30f;
        public LayerMask teleportLayerMask;

        [Header("Rotation Configuration")]
        public Transform rotationTarget;
        public Transform rotationReference;
        public RotationMode rotationMode = RotationMode.Continuous;
        [Range(0, 360)] public float continuousRotationSpeed = 180f;
        [Range(0, 180)] public float snapRotationAmount = 30f;
        public bool enableDirectionFlip;

        [Header("Group Teleport")]
        [Tooltip("Optional explicit participant. Defaults to the participant on this object.")]
        public GroupTeleportParticipant participant;

        [Tooltip("Optional explicit coordinator. If empty, the current linked platform is checked.")]
        public GroupTeleportPlatformCoordinator coordinatorOverride;

        private AvatarHMDAnatomy anatomy;
        private TeleportState teleportState;
        private float lastFlipInput;
        private float lastRotInput;
        private bool initialized;

        private const float ActivationThreshold = 0.1f;
        private const float LockThreshold = 0.9f;
        private const float SnapThreshold = 0.9f;

        private Transform Head => anatomy.head;
        private Transform LeftHand => anatomy.leftHand;
        private Transform RightHand => anatomy.rightHand;

        private void Start()
        {
            if (!IsOwner)
            {
                Destroy(this);
                return;
            }

            Initialize();
        }

        private void Update()
        {
            if (!initialized)
            {
                Initialize();
                return;
            }

            ApplyTeleport();
            ApplyRotation();
        }

        private void Initialize()
        {
            if (anatomy == null)
            {
                TryGetComponent(out anatomy);
            }

            if (participant == null)
            {
                TryGetComponent(out participant);
            }

            if (teleportationTarget == null)
            {
                teleportationTarget = transform;
            }

            if (rotationTarget == null)
            {
                rotationTarget = transform;
            }

            if (rotationReference == null)
            {
                rotationReference = transform;
            }

            initialized = anatomy != null && ray != null && previewAvatar != null;
        }

        private void ApplyTeleport()
        {
            InputAction action = navigationHand == HandType.Left ? leftThumbstick.action : rightThumbstick.action;
            float input = action.ReadValue<Vector2>().y;

            if (input < ActivationThreshold)
            {
                if (teleportState == TeleportState.Locked)
                {
                    PerformTeleport();
                    return;
                }

                if (teleportState != TeleportState.Idle)
                {
                    ray.enabled = false;
                    previewAvatar.Deactivate();
                    teleportState = TeleportState.Idle;
                }

                return;
            }

            if (input < LockThreshold)
            {
                if (teleportState == TeleportState.Locked)
                {
                    PerformTeleport();
                    return;
                }

                if (teleportState != TeleportState.Aiming)
                {
                    ray.enabled = true;
                    previewAvatar.ActivateIndicator();
                    teleportState = TeleportState.Aiming;
                }

                UpdateTeleportRay(input);
                return;
            }

            if (teleportState != TeleportState.Locked)
            {
                ray.enabled = true;
                previewAvatar.ActivateAvatar();
                teleportState = TeleportState.Locked;
            }

            UpdateTeleportRay(input);
        }

        private void UpdateTeleportRay(float input)
        {
            Transform hand = navigationHand == HandType.Left ? LeftHand : RightHand;
            ray.SetPosition(0, hand.position);

            if (Physics.Raycast(hand.position, hand.forward, out RaycastHit hit, maxRayLength, teleportLayerMask))
            {
                ray.SetPosition(1, hit.point);

                if (teleportState == TeleportState.Aiming)
                {
                    previewAvatar.UpdateIndicator(hit.point, input);
                }
                else if (teleportState == TeleportState.Locked)
                {
                    previewAvatar.UpdateAvatar(hit.point, Head.localPosition.y);
                }
            }
            else
            {
                ray.SetPosition(1, hand.position + hand.forward);
            }
        }

        private void PerformTeleport()
        {
            Transform target = previewAvatar.transform;
            Vector3 worldPosition = CalculateSoloTargetPosition(target);
            Quaternion worldRotation = CalculateSoloTargetRotation(target);

            if (!TryRequestGroupTeleport(worldPosition, worldRotation))
            {
                ApplySoloTeleport(worldPosition, worldRotation);
            }

            previewAvatar.Deactivate();
            ray.enabled = false;
            teleportState = TeleportState.Idle;
        }

        private Vector3 CalculateSoloTargetPosition(Transform target)
        {
            Vector3 headPos = Head.position;
            headPos.y = transform.position.y;
            Vector3 movement = target.position - headPos;
            return teleportationTarget.position + movement;
        }

        private Quaternion CalculateSoloTargetRotation(Transform target)
        {
            float angle = Vector3.SignedAngle(Head.forward, target.forward, Vector3.up);
            return Quaternion.AngleAxis(angle, Vector3.up) * teleportationTarget.rotation;
        }

        private bool TryRequestGroupTeleport(Vector3 worldPosition, Quaternion worldRotation)
        {
            if (participant == null)
            {
                return false;
            }

            GroupTeleportPlatformCoordinator coordinator = ResolveCoordinator();
            if (coordinator == null)
            {
                return false;
            }

            RequestGroupTeleportServerRpc(new NetworkObjectReference(coordinator.NetworkObject), worldPosition, worldRotation);
            return true;
        }

        private GroupTeleportPlatformCoordinator ResolveCoordinator()
        {
            if (coordinatorOverride != null)
            {
                return coordinatorOverride;
            }

            NavigationPlatformLink platformLink = participant != null ? participant.PlatformLink : null;
            if (platformLink == null || platformLink.platformTransform == null)
            {
                return null;
            }

            return platformLink.platformTransform.TryGetComponent(out GroupTeleportPlatformCoordinator coordinator)
                ? coordinator
                : null;
        }

        [ServerRpc]
        private void RequestGroupTeleportServerRpc(NetworkObjectReference coordinatorRef, Vector3 worldPosition, Quaternion worldRotation)
        {
            if (!coordinatorRef.TryGet(out NetworkObject coordinatorObject))
            {
                return;
            }

            if (!coordinatorObject.TryGetComponent(out GroupTeleportPlatformCoordinator coordinator) || participant == null)
            {
                return;
            }

            coordinator.TryExecuteGroupTeleport(participant, worldPosition, worldRotation);
        }

        private void ApplySoloTeleport(Vector3 worldPosition, Quaternion worldRotation)
        {
            GroupTeleportParticipant localParticipant = participant;
            if (localParticipant != null)
            {
                localParticipant.TeleportToWorldPose(worldPosition, worldRotation);
                return;
            }

            teleportationTarget.SetPositionAndRotation(worldPosition, worldRotation);
        }

        private void ApplyRotation()
        {
            Vector2 input = navigationHand == HandType.Left
                ? rightThumbstick.action.ReadValue<Vector2>()
                : leftThumbstick.action.ReadValue<Vector2>();

            if (rotationMode == RotationMode.Continuous)
            {
                float angle = input.x * continuousRotationSpeed * Time.deltaTime;
                rotationTarget.RotateAround(rotationReference.position, Vector3.up, angle);
            }
            else
            {
                if (enableDirectionFlip && lastFlipInput > -SnapThreshold && input.y <= -SnapThreshold)
                {
                    rotationTarget.RotateAround(rotationReference.position, Vector3.up, 180f);
                }

                if (Mathf.Abs(lastRotInput) < SnapThreshold && Mathf.Abs(input.x) >= SnapThreshold)
                {
                    float angle = input.x < 0f ? -snapRotationAmount : snapRotationAmount;
                    rotationTarget.RotateAround(rotationReference.position, Vector3.up, angle);
                }
            }

            lastFlipInput = input.y;
            lastRotInput = input.x;
        }
    }
}
