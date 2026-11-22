using System;
using System.Collections.Generic;
using System.Linq;
using IdleRicca.Presentation.Gameplay;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace IdleRicca.EditorTools.Gameplay
{
    [CustomEditor(typeof(NormalAttackSettings))]
    internal sealed class NormalAttackSettingsEditor : Editor
    {
        private const float MaxBladeTravelPerSample = 0.075f;
        private GameObject _sourcePrefab;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var settings = (NormalAttackSettings)target;
            if (!settings.HasValidTrajectory())
                EditorGUILayout.HelpBox("Bake the blade trajectory after changing its source clip or hit window.",
                    MessageType.Warning);

            _sourcePrefab = (GameObject)EditorGUILayout.ObjectField("Source prefab", _sourcePrefab,
                typeof(GameObject), false);
            using (new EditorGUI.DisabledScope(!_sourcePrefab || !settings.Clip))
            {
                if (GUILayout.Button("Bake Blade Trajectory"))
                {
                    try
                    {
                        Bake(settings, AssetDatabase.GetAssetPath(_sourcePrefab));
                    }
                    catch (InvalidOperationException ex)
                    {
                        EditorUtility.DisplayDialog("Blade Trajectory", ex.Message, "OK");
                    }
                }
            }
        }

        internal static void Bake(NormalAttackSettings settings, string prefabPath)
        {
            if (string.IsNullOrEmpty(prefabPath) || !settings.Clip || !(settings.Clip.length > 0f) ||
                !(settings.ActiveStart >= 0f) || !(settings.ActiveEnd <= 1f) ||
                !(settings.ActiveEnd > settings.ActiveStart))
                throw new InvalidOperationException("A source prefab, attack clip and valid hit window are required.");

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                SwordHitbox hitbox = root.GetComponent<SwordHitbox>();
                if (!hitbox || !hitbox.Geometry || hitbox.Geometry.enabled)
                    throw new InvalidOperationException("The prefab must have a disabled blade BoxCollider.");

                AnimationClip clip = settings.Clip;
                var animator = root.GetComponent<Animator>();
                var controller = animator ? animator.runtimeAnimatorController as AnimatorController : null;
                AnimatorState attack = controller?.layers[0].stateMachine.states
                    .SingleOrDefault(entry => entry.state.name == "Attack").state;
                if (!attack || attack.motion != clip)
                    throw new InvalidOperationException("The source clip must be the prefab's Base Layer Attack motion.");

                float sampleCount = clip.length * clip.frameRate * 2f;
                int spans = Mathf.Max(1, Mathf.CeilToInt((settings.ActiveEnd - settings.ActiveStart) * sampleCount));
                if (spans + 1 > NormalAttackSettings.MaxBakedPoses)
                    throw new InvalidOperationException("The hit window exceeds the blade trajectory sample budget.");

                var samples = new List<Sample>(NormalAttackSettings.MaxBakedPoses);
                Sample first = ReadPose(settings.ActiveStart, root, hitbox.Geometry, clip);
                samples.Add(first);
                for (int i = 1; i <= spans; i++)
                {
                    float time = i == spans ? settings.ActiveEnd :
                        Mathf.Lerp(settings.ActiveStart, settings.ActiveEnd, (float)i / spans);
                    Sample next = ReadPose(time, root, hitbox.Geometry, clip);
                    AppendSamples(samples, first, next, root, hitbox.Geometry, clip);
                    first = next;
                }

                WriteTrajectory(settings, controller, samples);
                if (!settings.HasValidTrajectory())
                    throw new InvalidOperationException("The sampled blade boxes contain invalid pose data.");

                AssetDatabase.SaveAssetIfDirty(settings);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AppendSamples(List<Sample> samples, Sample start, Sample end, GameObject root,
            BoxCollider box, AnimationClip clip)
        {
            if (samples.Count >= NormalAttackSettings.MaxBakedPoses)
                throw new InvalidOperationException("The blade moves too far for the current hit window budget.");

            float distance = Vector3.Distance(start.Center, end.Center);
            float arcDistance = Quaternion.Angle(start.Rotation, end.Rotation) * Mathf.Deg2Rad *
                Mathf.Max(start.HalfExtents.magnitude, end.HalfExtents.magnitude);
            if (distance + arcDistance <= MaxBladeTravelPerSample)
            {
                samples.Add(end);
                return;
            }

            float midpoint = (start.Progress + end.Progress) * 0.5f;
            if (!(midpoint > start.Progress && midpoint < end.Progress))
                throw new InvalidOperationException("Cannot refine a discontinuous blade trajectory.");

            Sample middle = ReadPose(midpoint, root, box, clip);
            AppendSamples(samples, start, middle, root, box, clip);
            AppendSamples(samples, middle, end, root, box, clip);
        }

        private static Sample ReadPose(float progress, GameObject root, BoxCollider box, AnimationClip clip)
        {
            clip.SampleAnimation(root, progress * clip.length);
            Transform blade = box.transform;
            Vector3 scale = blade.lossyScale;
            Vector3 rootScale = root.transform.lossyScale;
            Vector3 size = box.size * 0.5f;
            var half = new Vector3(
                Mathf.Abs(size.x * scale.x / rootScale.x),
                Mathf.Abs(size.y * scale.y / rootScale.y),
                Mathf.Abs(size.z * scale.z / rootScale.z));
            return new Sample(progress, root.transform.InverseTransformPoint(blade.TransformPoint(box.center)),
                Quaternion.Inverse(root.transform.rotation) * blade.rotation, half);
        }

        private static void WriteTrajectory(NormalAttackSettings settings, AnimatorController controller, List<Sample> samples)
        {
            using var data = new SerializedObject(settings);
            data.FindProperty("_bakedClip").objectReferenceValue = settings.Clip;
            data.FindProperty("_bakedController").objectReferenceValue = controller;
            SerializedProperty poses = data.FindProperty("_poses");
            poses.arraySize = samples.Count;
            for (int i = 0; i < samples.Count; i++)
            {
                SerializedProperty pose = poses.GetArrayElementAtIndex(i);
                pose.FindPropertyRelative("_progress").floatValue = samples[i].Progress;
                pose.FindPropertyRelative("_center").vector3Value = samples[i].Center;
                pose.FindPropertyRelative("_rotation").quaternionValue = samples[i].Rotation;
                pose.FindPropertyRelative("_halfExtents").vector3Value = samples[i].HalfExtents;
            }

            data.ApplyModifiedProperties();
        }

        private readonly struct Sample
        {
            internal Sample(float progress, Vector3 center, Quaternion rotation, Vector3 halfExtents)
            {
                Progress = progress;
                Center = center;
                Rotation = rotation;
                HalfExtents = halfExtents;
            }

            internal float Progress { get; }
            internal Vector3 Center { get; }
            internal Quaternion Rotation { get; }
            internal Vector3 HalfExtents { get; }
        }
    }
}
