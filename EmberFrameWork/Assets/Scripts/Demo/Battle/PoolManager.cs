// Adapted from TEngine (Copyright (c) 2022 ALEXTANG, MIT). See ../../FrameWork/Runtime/Core/LICENSE.txt.
using System;
using System.Collections.Generic;
using EmberFrameWork;
using UnityEngine;

namespace GameLogic
{
    public class PoolManager : MonoBehaviour
    {
        private static PoolManager _instance;

        public static PoolManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<PoolManager>();
                }

                if (_instance == null)
                {
                    GameObject gameObject = new GameObject();
                    gameObject.name = nameof(PoolManager);
                    _instance = gameObject.AddComponent<PoolManager>();
                    _instance.poolRootObj = gameObject;
                    DontDestroyOnLoad(_instance);
                }

                return _instance;
            }
        }

        [SerializeField] private GameObject poolRootObj;
        private readonly Dictionary<string, GameObjectPoolData> _gameObjectPools = new Dictionary<string, GameObjectPoolData>();

        public GameObject GetGameObject(string assetName, Transform parent = null)
        {
            GameObject obj = null;
            if (_gameObjectPools.TryGetValue(assetName, out var gameObjectPoolData) && gameObjectPoolData.poolQueue.Count > 0)
            {
                obj = gameObjectPoolData.GetObj(parent);
            }

            if (obj == null)
            {
                var folder = assetName.StartsWith("explosion_", StringComparison.Ordinal) ? "Effects/Explosions" : "Actor";
                var location = $"{BattleSystem.ResourceRoot}/{folder}/{assetName}.prefab";
                obj = GameModule.Resource.LoadGameObject(location, parent: parent);
                obj.name = assetName;
            }
            return obj;
        }

        public void PushGameObject(GameObject obj)
        {
            if (!obj.activeSelf)
                return;

            string objName = obj.name;
            if (_gameObjectPools.TryGetValue(objName, out var gameObjectPoolData))
            {
                gameObjectPoolData.PushObj(obj);
            }
            else
            {
                _gameObjectPools.Add(objName, new GameObjectPoolData(obj, poolRootObj));
            }
        }

        internal static void ClearRoomObjects()
        {
            if (_instance == null)
                return;

            foreach (var pool in _instance._gameObjectPools.Values)
                Destroy(pool.fatherObj);
            _instance._gameObjectPools.Clear();
        }
    }

    internal sealed class GameObjectPoolData
    {
        public readonly GameObject fatherObj;
        public readonly Queue<GameObject> poolQueue;

        public GameObjectPoolData(GameObject obj, GameObject poolRootObj)
        {
            fatherObj = new GameObject(obj.name);
            fatherObj.transform.SetParent(poolRootObj.transform);
            poolQueue = new Queue<GameObject>();
            PushObj(obj);
        }

        public void PushObj(GameObject obj)
        {
            poolQueue.Enqueue(obj);
            obj.transform.SetParent(fatherObj.transform);
            obj.SetActive(false);
        }

        public GameObject GetObj(Transform parent = null)
        {
            GameObject go = poolQueue.Dequeue();
            go.transform.SetParent(parent);
            if (parent == null)
            {
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            }

            go.SetActive(true);
            return go;
        }
    }
}
