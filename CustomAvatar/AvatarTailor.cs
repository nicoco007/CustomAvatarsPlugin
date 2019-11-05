using System;
using System.Collections;
using CustomAvatar.Utilities;
using UnityEngine;
using static IPA.Logging.Logger;

namespace CustomAvatar
{
	public class AvatarTailor
	{
		private float? _currentAvatarArmLength = null;
		private Vector3? _initialPlatformPosition = null;
		private float? _initialAvatarPositionY = null;
		private Vector3 _initialAvatarLocalScale = Vector3.one;

		private Animator FindAvatarAnimator(GameObject gameObject)
		{
			var vrik = gameObject.GetComponentInChildren<AvatarScriptPack.VRIK>();
			if (vrik == null) return null;
			var animator = vrik.gameObject.GetComponentInChildren<Animator>();
			if (animator.avatar == null || !animator.isHuman) return null;
			return animator;
		}

		public void OnAvatarLoaded(SpawnedAvatar avatar)
		{
			_initialAvatarLocalScale = avatar.gameObject.transform.localScale;
			_initialAvatarPositionY = null;
			_currentAvatarArmLength = null;
		}

		public void ResizeAvatar(SpawnedAvatar avatar)
		{
			var animator = FindAvatarAnimator(avatar.gameObject);
			if (animator == null)
			{
				Plugin.Logger.Log(Level.Error, "Tailor: Animator not found");
				return;
			}

			// compute scale
			float scale = 1.0f;
			AvatarResizeMode resizeMode = Settings.resizeMode;
			if (resizeMode == AvatarResizeMode.ArmSpan)
			{
				float playerArmLength = Settings.playerArmSpan;
				_currentAvatarArmLength = _currentAvatarArmLength ?? MeasureAvatarArmSpan(animator);
				var avatarArmLength = _currentAvatarArmLength ?? playerArmLength;
				Plugin.Logger.Log(Level.Debug, "Avatar arm length: " + avatarArmLength);

				scale = playerArmLength / avatarArmLength;
			}
			else if (resizeMode == AvatarResizeMode.Height)
			{
				scale = BeatSaberUtil.GetPlayerEyeHeight() / avatar.customAvatar.eyeHeight;
			}

			// apply scale
			avatar.gameObject.transform.localScale = _initialAvatarLocalScale * scale;

			Plugin.Logger.Log(Level.Info, "Avatar resized with scale: " + scale);

			SharedCoroutineStarter.instance.StartCoroutine(FloorMendingWithDelay(avatar, animator, scale));
		}

		private IEnumerator FloorMendingWithDelay(SpawnedAvatar avatar, Animator animator, float scale)
		{
			if (!Settings.enableFloorAdjust) yield break;

			yield return new WaitForEndOfFrame(); // wait for CustomFloorPlugin:PlatformManager:Start hides original platform

			float playerViewPointHeight = BeatSaberUtil.GetPlayerEyeHeight();
			float avatarViewPointHeight = avatar.customAvatar.viewPoint?.position.y ?? playerViewPointHeight;
			_initialAvatarPositionY = _initialAvatarPositionY ?? animator.transform.position.y;
			float floorOffset = playerViewPointHeight - avatarViewPointHeight * scale;

			// apply offset
			animator.transform.position = new Vector3(animator.transform.position.x, floorOffset + _initialAvatarPositionY ?? 0, animator.transform.position.z);
			
			var originalFloor = GameObject.Find("MenuPlayersPlace") ?? GameObject.Find("Static/PlayersPlace");
			var customFloor = GameObject.Find("Platform Loader");

			Plugin.Logger.Info("originalFloor " + originalFloor);

			if (originalFloor != null)
			{
				Plugin.Logger.Info($"Moving original floor {Math.Abs(floorOffset)} m {(floorOffset >= 0 ? "up" : "down")}");
				originalFloor.transform.position = new Vector3(0, floorOffset, 0);
			}

			if (customFloor != null)
			{
				Plugin.Logger.Info($"Moving Custom Platforms floor {Math.Abs(floorOffset)} m {(floorOffset >= 0 ? "up" : "down")}");

				_initialPlatformPosition = _initialPlatformPosition ?? customFloor.transform.position;
				customFloor.transform.position = (Vector3.up * floorOffset) + _initialPlatformPosition ?? Vector3.zero;
			}
		}

		public static float MeasureAvatarArmSpan(Animator animator)
		{
			var indexFinger1 = animator.GetBoneTransform(HumanBodyBones.LeftIndexProximal).position;
			var leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).position;
			var leftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftShoulder).position;
			var rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightShoulder).position;
			var leftElbow = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm).position;
			var leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand).position;

			var shoulderLength = Vector3.Distance(leftUpperArm, leftShoulder) * 2.0f + Vector3.Distance(leftShoulder, rightShoulder);
			var armLength = (Vector3.Distance(indexFinger1, leftHand) * 0.5f + Vector3.Distance(leftHand, leftElbow) + Vector3.Distance(leftElbow, leftUpperArm)) * 2.0f;

			return shoulderLength + armLength;
		}
	}
}
