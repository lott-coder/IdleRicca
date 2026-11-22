using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace IdleRicca.EditorTools.Gameplay
{
    public static class CityEnemyNavigationBaker
    {
        private const string ScenePath = "Assets/Scenes/Demo.unity";
        private const string DataFolder = "Assets/Scenes/Demo";
        private const string DataPath = DataFolder + "/CityEnemyNavigation.asset";

        [MenuItem("IdleRicca/Gameplay/Rebake City enemy navigation")]
        private static void BakeOpenDemo() => Bake(SceneManager.GetActiveScene());

        public static NavMeshData Bake(Scene scene)
        {
            if (scene.path != ScenePath)
                throw new InvalidOperationException("Open the City Demo scene before baking enemy navigation.");

            GameObject city = scene.GetRootGameObjects().Single(root => root.name == "City Environment");
            var sources = new List<NavMeshBuildSource>();
            var markup = new List<NavMeshBuildMarkup>();
            NavMeshBuilder.CollectSources(city.transform, 1, NavMeshCollectGeometry.PhysicsColliders, 0, markup, sources);
            sources.RemoveAll(source => source.component is Collider collider &&
                (collider.isTrigger || collider.attachedRigidbody));

            var colliders = city.GetComponentsInChildren<Collider>();
            var bounds = new Bounds();
            bool initialized = false;
            foreach (Collider collider in colliders)
            {
                if (!collider.enabled || collider.isTrigger || collider is TerrainCollider || collider.attachedRigidbody)
                    continue;

                if (!initialized)
                {
                    bounds = collider.bounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            if (!initialized || sources.Count == 0)
                throw new InvalidOperationException("City has no static collision geometry for navigation.");

            bounds.Expand(new Vector3(14f, 8f, 14f));
            var settings = NavMesh.GetSettingsByID(0);
            settings.agentClimb = .4f;

            NavMeshData data = AssetDatabase.LoadAssetAtPath<NavMeshData>(DataPath);
            if (data)
            {
                if (!NavMeshBuilder.UpdateNavMeshData(data, settings, sources, bounds))
                    throw new InvalidOperationException("Updating City enemy navigation failed.");

                EditorUtility.SetDirty(data);
            }
            else
            {
                data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
                if (!data)
                    throw new InvalidOperationException("Baking City enemy navigation failed.");

                if (!AssetDatabase.IsValidFolder(DataFolder))
                    AssetDatabase.CreateFolder("Assets/Scenes", "Demo");

                AssetDatabase.CreateAsset(data, DataPath);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Baked City enemy navigation from {sources.Count} static sources in {bounds}.");
            return data;
        }
    }
}
